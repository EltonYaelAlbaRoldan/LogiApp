using Google.Apis.Drive.v3;
using DriveData = Google.Apis.Drive.v3.Data; // Alias para limpiar el código

namespace LogiApp.Servicios;

/// <summary>
/// Gestiona la jerarquía de carpetas y carga de archivos en Google Drive.
/// </summary>
public class ConsultasGoogleDrive
{
    private readonly DriveService _service;
    private readonly string _rootFolderId = "18UK5_tCQmtOLyEOoIWCqaeKqNKJsaFGM";

    public ConsultasGoogleDrive(DriveService service) => _service = service;

    // --- SECCIÓN: GESTIÓN DE ESTRUCTURA (CARPETAS) ---

    /// <summary>
    /// Crea la carpeta raíz del operador y sus subcarpetas necesarias.
    /// Retorna el ID de la subcarpeta 'documentos' para vincularla al registro.
    /// </summary>
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

    /// <summary>
    /// Método auxiliar para crear carpetas dentro de otras.
    /// </summary>
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

    // --- SECCIÓN: OPERACIONES CRUD ---

    /// <summary>
    /// Lista todas las carpetas activas dentro del directorio raíz.
    /// </summary>
    public async Task<List<DriveData.File>> ListarCarpetasAsync()
    {
        var request = _service.Files.List();
        request.Q = $"'{_rootFolderId}' in parents and trashed = false";
        var result = await request.ExecuteAsync();
        return result.Files.ToList();
    }

    /// <summary>
    /// Actualiza el nombre de una carpeta o archivo existente.
    /// </summary>
    public async Task ActualizarNombreCarpetaAsync(string fileId, string nuevoNombre)
    {
        var meta = new DriveData.File { Name = nuevoNombre };
        await _service.Files.Update(meta, fileId).ExecuteAsync();
    }

    /// <summary>
    /// Envía un elemento a la papelera de reciclaje de Drive.
    /// </summary>
    public async Task BorrarCarpetaAsync(string fileId)
    {
        var meta = new DriveData.File { Trashed = true };
        await _service.Files.Update(meta, fileId).ExecuteAsync();
    }

    // --- SECCIÓN: CARGA DE ARCHIVOS (UPLOAD) ---

    /// <summary>
    /// Sube un archivo PDF desde el almacenamiento local a una carpeta específica en Drive.
    /// </summary>
    /// <param name="rutaLocal">Ruta física del archivo en el dispositivo.</param>
    /// <param name="nombreDeseado">Nombre que tendrá el archivo en la nube.</param>
    /// <param name="folderId">ID de la carpeta de destino.</param>
    /// <returns>ID del archivo creado en Drive.</returns>
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
                    throw new Exception(progreso.Exception?.Message ?? "Error desconocido en la carga.");

                return request.ResponseBody?.Id ?? string.Empty;
            }
        }
        catch (Exception ex)
        {
            throw new Exception($"Error en DriveService: {ex.Message}");
        }
    }
}