using Google.Apis.Drive.v3;
using Google.Apis.Drive.v3.Data;

namespace LogiApp.Servicios;

public class ConsultasGoogleDrive
{
    private readonly DriveService _service;
    private readonly string _rootFolderId = "1J4KToWdv-uOfe8KwH-4F9a7RZc7zEdEf";

    public ConsultasGoogleDrive(DriveService service) => _service = service;

    // CREATE: Crear carpeta para operador
    public async Task<string> CrearCarpetaOperadorAsync(string nombre)
    {
        var meta = new Google.Apis.Drive.v3.Data.File
        {
            Name = nombre,
            MimeType = "application/vnd.google-apps.folder",
            Parents = new[] { _rootFolderId }
        };

        var request = _service.Files.Create(meta);
        request.Fields = "id";
        var file = await request.ExecuteAsync();
        return file.Id;
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
}