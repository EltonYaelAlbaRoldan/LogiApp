using Google.Apis.Drive.v3;
using Google.Apis.Drive.v3.Data;

namespace LogiApp.Servicios;

public class ConsultasGoogleDrive
{
    private readonly DriveService _service;
    private readonly string _rootFolderId = "18UK5_tCQmtOLyEOoIWCqaeKqNKJsaFGM";

    public ConsultasGoogleDrive(DriveService service) => _service = service;

    // CREATE: Crear carpeta para operador
    public async Task<string> CrearEstructuraOperadorAsync(string nombreOperador)
    {
        var metaPrincipal = new Google.Apis.Drive.v3.Data.File
        {
            Name = nombreOperador,
            MimeType = "application/vnd.google-apps.folder",
            Parents = new[] { _rootFolderId }
        };
        var carpetaPrincipal = await _service.Files.Create(metaPrincipal).ExecuteAsync();
        string idPrincipal = carpetaPrincipal.Id;


        //Crear subcarpeta "documentos"
        string idDocumentos = await CrearSubcarpetaAsync("documentos", idPrincipal);

        //Crear subcarpeta "evidencias"
        await CrearSubcarpetaAsync("evidencias", idPrincipal);

        return idDocumentos; 
    }
    private async Task<string> CrearSubcarpetaAsync(string nombreSubcarpeta, string idPadre)
    {
        var metaSub = new Google.Apis.Drive.v3.Data.File
        {
            Name = nombreSubcarpeta,
            MimeType = "application/vnd.google-apps.folder",
            Parents = new[] { idPadre }
        };
        var sub = await _service.Files.Create(metaSub).ExecuteAsync();
        return sub.Id;
    }

    // READ: Listar carpetas
    public async Task<List<Google.Apis.Drive.v3.Data.File>> ListarCarpetasAsync()
    {
        var request = _service.Files.List();
        request.Q = $"'{_rootFolderId}' in parents and trashed = false";
        var result = await request.ExecuteAsync();
        return result.Files.ToList();
    }

    // UPDATE: Cambiar nombre de la carpeta
    public async Task ActualizarNombreCarpetaAsync(string fileId, string nuevoNombre)
    {
        var meta = new Google.Apis.Drive.v3.Data.File
        {
            Name = nuevoNombre
        };

        // En Google Drive API v3, para renombrar se usa Update
        var request = _service.Files.Update(meta, fileId);
        await request.ExecuteAsync();
    }

    // DELETE: Enviar a la papelera
    public async Task BorrarCarpetaAsync(string fileId)
    {
        var meta = new Google.Apis.Drive.v3.Data.File { Trashed = true };
        await _service.Files.Update(meta, fileId).ExecuteAsync();
    }

    // --- MÉTODO PARA SUBIR ARCHIVOS PDF ---

    public async Task<string> SubirArchivoAsync(string rutaLocal, string nombreDeseado, string folderId)
    {
        try
        {
            // Ahora usamos directamente el folderId del operador (ej. la subcarpeta 'documentos')
            // Porque con OAuth 2.0 tú eres el dueño de todo lo que subas.
            var metadata = new Google.Apis.Drive.v3.Data.File()
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
                    throw new Exception(progreso.Exception.Message);

                // Retornamos el ID del archivo recién subido
                return request.ResponseBody?.Id ?? "";
            }
        }
        catch (Exception ex)
        {
            throw new Exception($"Error al cargar archivo en Drive: {ex.Message}");
        }
    }

}