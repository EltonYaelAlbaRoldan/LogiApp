using Google.Apis.Auth.OAuth2;
using Google.Apis.Drive.v3;
using Google.Apis.Services;
using Google.Apis.Sheets.v4;
using Google.Apis.Util.Store;
using Google.Apis.Auth.OAuth2.Flows;
using Google.Apis.Auth.OAuth2.Responses;

namespace LogiApp.Servicios;

public class ConexionGoogleAuth
{
    public async Task<(SheetsService, DriveService)> ObtenerServiciosAsync()
    {
        string clientId = "1021863353895-55eb5gi10kt8mmd0u4kb08i0dq5ahf45.apps.googleusercontent.com";
        string clientSecret = "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";
        string refreshToken = "1//04xIdmJ6T-aWWCgYIARAAGAQSNwF-L9IrQWF7I-adOHrW-IthKUJLzbS5WRXHmuk34HcNYU-2TfSV_yLcFv4VV21UNnPxfXOsW-s";

        var flow = new GoogleAuthorizationCodeFlow(new GoogleAuthorizationCodeFlow.Initializer
        {
            ClientSecrets = new ClientSecrets { ClientId = clientId, ClientSecret = clientSecret },
            Scopes = new[] { DriveService.Scope.Drive, SheetsService.Scope.Spreadsheets }
        });

        // Creamos la credencial usando el Refresh Token directamente
        var tokenResponse = new TokenResponse { RefreshToken = refreshToken };
        var credential = new UserCredential(flow, "user", tokenResponse);

        // Si el token expiró, esto lo renueva automáticamente sin avisar al usuario
        if (tokenResponse.IsExpired(Google.Apis.Util.SystemClock.Default))
        {
            await credential.RefreshTokenAsync(CancellationToken.None);
        }

        var initializer = new BaseClientService.Initializer()
        {
            HttpClientInitializer = credential,
            ApplicationName = "LogiApp"
        };

        return (new SheetsService(initializer), new DriveService(initializer));
    }
}