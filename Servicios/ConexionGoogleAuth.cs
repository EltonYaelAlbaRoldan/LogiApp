using Google.Apis.Auth.OAuth2;
using Google.Apis.Drive.v3;
using Google.Apis.Sheets.v4;
using Google.Apis.Services;
using Google.Apis.Util.Store; // Necesario para guardar el token

namespace LogiApp.Servicios;

public class ConexionGoogleAuth
{
    public async Task<(SheetsService, DriveService)> ObtenerServiciosAsync()
    {
        // 1. AGREGA AQUÍ TUS NUEVAS CREDENCIALES DE ESCRITORIO
        string clientId = "1021863353895-n3j9fh9a8gsv018a95h57mhfeeq38kna.apps.googleusercontent.com";
        string clientSecret = "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";

        // 2. El flujo de escritorio SÍ requiere el ClientSecret para validar
        UserCredential credential = await GoogleWebAuthorizationBroker.AuthorizeAsync(
            new ClientSecrets
            {
                ClientId = clientId,
                ClientSecret = clientSecret
            },
            new[] { DriveService.Scope.Drive, SheetsService.Scope.Spreadsheets },
            "user",
            CancellationToken.None
        );

        var initializer = new BaseClientService.Initializer()
        {
            HttpClientInitializer = credential,
            ApplicationName = "LogiApp"
        };

        return (new SheetsService(initializer), new DriveService(initializer));
    }
}