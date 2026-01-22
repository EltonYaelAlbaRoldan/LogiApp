using Google.Apis.Auth.OAuth2;
using Google.Apis.Sheets.v4;
using Google.Apis.Drive.v3;
using Google.Apis.Services;
using System.Diagnostics;

public class GoogleServiceTest
{
    // Tu ID de Sheets identificado anteriormente
    private string spreadsheetId = "1wx2DSjhjzUeTxtxby0akYFTFul84DwSI1OMe5IYezb8";
    // El nombre de tu archivo JSON en Resources/Raw
    private string jsonFileName = "logiapp-dual-efa4b11f5ef2.json";

    public async Task ProbarConexion()
    {
        try
        {
            // 1. Cargar las credenciales
            using var stream = await FileSystem.OpenAppPackageFileAsync(jsonFileName);
            var credential = GoogleCredential.FromStream(stream)
                .CreateScoped(SheetsService.Scope.Spreadsheets, DriveService.Scope.DriveReadonly);

            // 2. Probar Google Sheets
            var sheetsService = new SheetsService(new BaseClientService.Initializer()
            {
                HttpClientInitializer = credential,
                ApplicationName = "LogiApp",
            });

            var sheet = await sheetsService.Spreadsheets.Get(spreadsheetId).ExecuteAsync();
            Debug.WriteLine($"✅ Éxito Sheets: Conectado al archivo '{sheet.Properties.Title}'");

            // 3. Probar Google Drive
            var driveService = new DriveService(new BaseClientService.Initializer()
            {
                HttpClientInitializer = credential,
                ApplicationName = "LogiApp",
            });

            // Buscamos específicamente tu carpeta llamada "Test"
            var listRequest = driveService.Files.List();
            listRequest.Q = "name = 'Test' and mimeType = 'application/vnd.google-apps.folder'";
            var response = await listRequest.ExecuteAsync();

            if (response.Files.Count > 0)
                Debug.WriteLine($"✅ Éxito Drive: Carpeta '{response.Files[0].Name}' encontrada.");
            else
                Debug.WriteLine("⚠️ Drive: Conectado, pero no encontré la carpeta llamada 'Test'. Verifica el nombre.");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"❌ Error: {ex.Message}");
        }
    }
}