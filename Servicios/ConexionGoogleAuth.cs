using Google.Apis.Auth.OAuth2;
using Google.Apis.Drive.v3;
using Google.Apis.Sheets.v4;
using Google.Apis.Services;

namespace LogiApp.Servicios;

public class ConexionGoogleAuth
{
    private const string JsonFileName = "logiapp-dual-efa4b11f5ef2.json"; // El que está en Resources/Raw

    public async Task<(SheetsService, DriveService)> ObtenerServiciosAsync()
    {
        using var stream = await FileSystem.OpenAppPackageFileAsync(JsonFileName);
        var credential = GoogleCredential.FromStream(stream)
            .CreateScoped(SheetsService.Scope.Spreadsheets, DriveService.Scope.Drive);

        var sheetsService = new SheetsService(new BaseClientService.Initializer()
        {
            HttpClientInitializer = credential,
            ApplicationName = "LogiApp"
        });

        var driveService = new DriveService(new BaseClientService.Initializer()
        {
            HttpClientInitializer = credential,
            ApplicationName = "LogiApp"
        });

        return (sheetsService, driveService);
    }
}