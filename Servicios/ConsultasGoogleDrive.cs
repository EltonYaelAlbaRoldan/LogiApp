using Google.Apis.Drive.v3;
using DriveData = Google.Apis.Drive.v3.Data;

namespace LogiApp.Servicios;

public class ConsultasGoogleDrive
{
    #region CONFIGURACIÓN
    private readonly DriveService _service;
    private readonly string _rootFolderId = "1hNEuyBKMIaMHhWrYWK0uuspVzc6zGgq3";

    public ConsultasGoogleDrive(DriveService service) => _service = service;
    #endregion

    #region GESTIÓN DE ESTRUCTURA (CARPETAS)
    public async Task<string> CrearEstructuraOperadorAsync(string nombreOperador)
    {
        // 1. Crear carpeta principal del Operador
        var metaPrincipal = new DriveData.File
        {
            Name = nombreOperador,
            MimeType = "application/vnd.google-apps.folder",
            Parents = new[] { _rootFolderId }
        };
        var carpetaPrincipal = await _service.Files.Create(metaPrincipal).ExecuteAsync();
        string idPrincipal = carpetaPrincipal.Id;

        // 2. Crear subcarpetas organizativas
        string idDocumentos = await CrearSubcarpetaAsync("documentos", idPrincipal);
        await CrearSubcarpetaAsync("evidencias", idPrincipal);

        return idDocumentos;
    }

    private async Task<string> CrearSubcarpetaAsync(string nombreSubcarpeta, string idPadre)
    {
        var metaSub = new DriveData.File
        {
            Name = nombreSubcarpeta,
            MimeType = "application/vnd.google-apps.folder",
            Parents = new[] { idPadre }
        };
        var sub = await _service.Files.Create(metaSub).ExecuteAsync();
        return sub.Id;
    }

    private async Task<string> ObtenerOCrearCarpetaAsync(string nombreCarpeta, string idPadre, bool esBusquedaDeOperador = false)
    {
        var request = _service.Files.List();

        if (esBusquedaDeOperador)
            request.Q = $"name contains '{nombreCarpeta}' and '{idPadre}' in parents and mimeType = 'application/vnd.google-apps.folder' and trashed = false";
        else
            request.Q = $"name = '{nombreCarpeta}' and '{idPadre}' in parents and mimeType = 'application/vnd.google-apps.folder' and trashed = false";

        request.Fields = "files(id, name)";
        var resultado = await request.ExecuteAsync();
        var carpetaExistente = resultado.Files.FirstOrDefault(f => f.Name.StartsWith(nombreCarpeta));

        return carpetaExistente?.Id ?? await CrearSubcarpetaAsync(nombreCarpeta, idPadre);
    }

    private async Task<bool> ExisteArchivoAsync(string nombreArchivo, string idCarpeta)
    {
        var request = _service.Files.List();
        request.Q = $"name = '{nombreArchivo}' and '{idCarpeta}' in parents and trashed = false";
        request.Fields = "files(id)";
        var resultado = await request.ExecuteAsync();
        return resultado.Files.Count > 0;
    }
    #endregion

    #region OPERACIONES CRUD
    public async Task<List<DriveData.File>> ListarCarpetasAsync()
    {
        var request = _service.Files.List();
        request.Q = $"'{_rootFolderId}' in parents and trashed = false";
        var result = await request.ExecuteAsync();
        return result.Files.ToList();
    }

    public async Task ActualizarNombreCarpetaAsync(string fileId, string nuevoNombre)
    {
        var meta = new DriveData.File { Name = nuevoNombre };
        await _service.Files.Update(meta, fileId).ExecuteAsync();
    }

    public async Task BorrarCarpetaAsync(string fileId)
    {
        var meta = new DriveData.File { Trashed = true };
        await _service.Files.Update(meta, fileId).ExecuteAsync();
    }
    #endregion

    #region CARGA DE ARCHIVOS (UPLOAD)
    public async Task<string> SubirArchivoAsync(string rutaLocal, string nombreDeseado, string folderId)
    {
        try
        {
            var listRequest = _service.Files.List();
            listRequest.Q = $"name = '{nombreDeseado}' and '{folderId}' in parents and trashed = false";
            listRequest.Fields = "files(id, webViewLink)";
            var existente = (await listRequest.ExecuteAsync()).Files.FirstOrDefault();

            var metadata = new DriveData.File { Name = nombreDeseado };
            if (existente == null) metadata.Parents = new List<string> { folderId };

            using (var stream = new FileStream(rutaLocal, FileMode.Open))
            {
                Google.Apis.Upload.IUploadProgress progreso;
                string? link;

                if (existente == null)
                {
                    var request = _service.Files.Create(metadata, stream, "application/pdf");
                    request.Fields = "id, webViewLink";
                    progreso = await request.UploadAsync();
                    link = request.ResponseBody?.WebViewLink;
                }
                else
                {
                    var request = _service.Files.Update(metadata, existente.Id, stream, "application/pdf");
                    request.Fields = "id, webViewLink";
                    progreso = await request.UploadAsync();
                    link = request.ResponseBody?.WebViewLink ?? existente.WebViewLink;
                }

                if (progreso.Status != Google.Apis.Upload.UploadStatus.Completed)
                    throw new Exception(progreso.Exception?.Message ?? "Error en la carga.");

                return link ?? string.Empty;
            }
        }
        catch (Exception ex)
        {
            throw new Exception($"Error en DriveService: {ex.Message}");
        }
    }

    private async Task<string> SubirArchivoGenericoAsync(string rutaLocal, string nombreDeseado, string folderId)
    {
        var metadata = new DriveData.File()
        {
            Name = nombreDeseado,
            Parents = new List<string> { folderId }
        };

        using (var stream = new FileStream(rutaLocal, FileMode.Open))
        {
            var request = _service.Files.Create(metadata, stream, "image/jpeg");
            request.Fields = "id";
            var progreso = await request.UploadAsync();

            if (progreso.Status != Google.Apis.Upload.UploadStatus.Completed)
                throw new Exception(progreso.Exception?.Message ?? "No se completó la carga de la imagen.");

            return request.ResponseBody?.Id ?? string.Empty;
        }
    }

    public async Task<string> SubirEvidenciasViajeAsync(IEnumerable<string> rutasLocales, string idOperador, string folio)
    {
        try
        {
            string idCarpetaOperador = await ObtenerOCrearCarpetaAsync(idOperador, _rootFolderId, true);
            string idCarpetaEvidencias = await ObtenerOCrearCarpetaAsync("evidencias", idCarpetaOperador, false);
            string idCarpetaViaje = await ObtenerOCrearCarpetaAsync(folio, idCarpetaEvidencias, false);
            int numeroFoto = 1;

            foreach (string rutaLocal in rutasLocales.Where(File.Exists))
            {
                string extension = Path.GetExtension(rutaLocal);
                if (string.IsNullOrWhiteSpace(extension)) extension = ".jpg";

                string nombreArchivo = $"Evidencia_{folio}_{numeroFoto:D2}{extension}";

                // Si una sincronización anterior quedó a medias, no duplicamos las fotos ya cargadas.
                if (await ExisteArchivoAsync(nombreArchivo, idCarpetaViaje))
                {
                    numeroFoto++;
                    continue;
                }

                var metadata = new DriveData.File
                {
                    Name = nombreArchivo,
                    Parents = new List<string> { idCarpetaViaje }
                };

                using var stream = new FileStream(rutaLocal, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                var request = _service.Files.Create(metadata, stream, "image/jpeg");
                request.Fields = "id";
                var progreso = await request.UploadAsync();

                if (progreso.Status != Google.Apis.Upload.UploadStatus.Completed)
                    throw new Exception(progreso.Exception?.Message ?? $"No se pudo subir la fotografía {numeroFoto}.");

                numeroFoto++;
            }

            if (numeroFoto == 1)
                throw new Exception("No se encontraron fotografías válidas para subir.");

            return $"https://drive.google.com/drive/folders/{idCarpetaViaje}";
        }
        catch (Exception ex)
        {
            throw new Exception("Error al subir evidencia a Drive: " + ex.Message);
        }
    }

    public Task<string> SubirEvidenciaViajeRapidoAsync(string rutaLocal, string idOperador, string folio)
    {
        return SubirEvidenciasViajeAsync(new[] { rutaLocal }, idOperador, folio);
    }

    public async Task<string> SubirEvidenciasMantenimientoAsync(IEnumerable<string> rutasLocales, string idOperador, string folio)
    {
        try
        {
            string idCarpetaOperador = await ObtenerOCrearCarpetaAsync(idOperador, _rootFolderId, true);
            string idCarpetaMantenimiento = await ObtenerOCrearCarpetaAsync("mantenimiento", idCarpetaOperador, false);
            string idCarpetaFolio = await ObtenerOCrearCarpetaAsync(folio, idCarpetaMantenimiento, false);
            int numeroFoto = 1;

            foreach (string rutaLocal in rutasLocales.Where(File.Exists))
            {
                string extension = Path.GetExtension(rutaLocal);
                if (string.IsNullOrWhiteSpace(extension)) extension = ".jpg";
                string nombreArchivo = $"Mantenimiento_{folio}_{numeroFoto:D2}{extension}";

                if (await ExisteArchivoAsync(nombreArchivo, idCarpetaFolio))
                {
                    numeroFoto++;
                    continue;
                }

                string fileId = await SubirArchivoGenericoAsync(rutaLocal, nombreArchivo, idCarpetaFolio);
                if (string.IsNullOrWhiteSpace(fileId))
                    throw new Exception($"No se pudo confirmar la fotografía {numeroFoto}.");

                numeroFoto++;
            }

            if (numeroFoto == 1)
                throw new Exception("No se encontraron fotografías válidas para subir.");

            return $"https://drive.google.com/drive/folders/{idCarpetaFolio}";
        }
        catch (Exception ex)
        {
            throw new Exception($"Error al organizar Drive: {ex.Message}");
        }
    }

    public async Task<(string fileId, string folderLink)> SubirEvidenciaMantenimientoAsync(string rutaLocal, string idOperador, string folio)
    {
        string folderLink = await SubirEvidenciasMantenimientoAsync(new[] { rutaLocal }, idOperador, folio);
        return (string.Empty, folderLink);
    }
    #endregion
}
