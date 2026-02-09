using Google.Apis.Drive.v3;
using DriveData = Google.Apis.Drive.v3.Data;

namespace LogiApp.Servicios;

public class ConsultasGoogleDrive
{
    #region CONFIGURACIÓN
    private readonly DriveService _service;
    private readonly string _rootFolderId = "18UK5_tCQmtOLyEOoIWCqaeKqNKJsaFGM";

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
            var metadata = new DriveData.File()
            {
                Name = nombreDeseado,
                Parents = new List<string> { folderId }
            };

            using (var stream = new FileStream(rutaLocal, FileMode.Open))
            {
                var request = _service.Files.Create(metadata, stream, "application/pdf");
                request.Fields = "id";
                var progreso = await request.UploadAsync();

                if (progreso.Status == Google.Apis.Upload.UploadStatus.Failed)
                    throw new Exception(progreso.Exception?.Message ?? "Error en la carga.");

                var archivoSubido = request.ResponseBody;
                if (archivoSubido == null)
                {
                    throw new Exception("La subida fue exitosa pero no se recibió el link de confirmación.");
                }

                return archivoSubido.WebViewLink;
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

            if (progreso.Status == Google.Apis.Upload.UploadStatus.Failed)
                throw new Exception(progreso.Exception?.Message);

            return request.ResponseBody?.Id ?? string.Empty;
        }
    }

    public async Task<(string fileId, string folderLink)> SubirEvidenciaMantenimientoAsync(string rutaLocal, string idOperador, string folio)
    {
        try
        {
            string idCarpetaOperador = await ObtenerOCrearCarpetaAsync(idOperador, _rootFolderId, true);
            string idCarpetaMantenimiento = await ObtenerOCrearCarpetaAsync("mantenimiento", idCarpetaOperador, false);
            string nombreArchivo = $"Folio_{folio}.jpg";

            string fileId = await SubirArchivoGenericoAsync(rutaLocal, nombreArchivo, idCarpetaMantenimiento);
            string folderLink = $"https://drive.google.com/drive/folders/{idCarpetaMantenimiento}";

            return (fileId, folderLink);
        }
        catch (Exception ex)
        {
            throw new Exception($"Error al organizar Drive: {ex.Message}");
        }
    }
    #endregion
}