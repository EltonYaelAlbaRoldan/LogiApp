using Google.Apis.Auth.OAuth2;
using Google.Apis.Auth.OAuth2.Flows;
using Google.Apis.Auth.OAuth2.Responses;
using Google.Apis.Drive.v3;
using Google.Apis.Services;
using Google.Apis.Sheets.v4;

namespace LogiApp.Servicios;

public class ConexionGoogleAuth
{
    #region CONFIGURACIÓN Y CREDENCIALES
    private const string ClientId = "547051792054-1u9vftvu5fltgcimbo3mteu4qk53253o.apps.googleusercontent.com";
    private const string ClientSecret = "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";
    private const string RefreshToken = "1//04rdjOe3v9nXACgYIARAAGAQSNwF-L9Ir9YjhXRgY_C0lLEX3LD03q-7QkPcv3c4-gd6kYW70tr5Zuwj7R-j3fHPlr1ROOHvMw-g";
    private const string ApplicationName = "LogiApp";
    #endregion

    #region MÉTODOS DE SERVICIO
    public async Task<(SheetsService, DriveService)> ObtenerServiciosAsync()
    {
        SeguridadApp.VerificarIntegridad();
        // 1. Configuración del flujo de autorización (Scopes para Drive y Sheets)
        var flow = new GoogleAuthorizationCodeFlow(new GoogleAuthorizationCodeFlow.Initializer
        {
            ClientSecrets = new ClientSecrets
            {
                ClientId = ClientId,
                ClientSecret = ClientSecret
            },
            Scopes = new[]
            {
                DriveService.Scope.Drive,
                SheetsService.Scope.Spreadsheets
            }
        });

        // 2. Uso del Refresh Token para mantener la sesión activa
        var tokenResponse = new TokenResponse { RefreshToken = RefreshToken };
        var credential = new UserCredential(flow, "user", tokenResponse);

        // 3. Renovación automática si el token expiró
        if (tokenResponse.IsExpired(Google.Apis.Util.SystemClock.Default))
        {
            await credential.RefreshTokenAsync(CancellationToken.None);
        }

        // 4. Inicializador base para los servicios
        var initializer = new BaseClientService.Initializer()
        {
            HttpClientInitializer = credential,
            ApplicationName = ApplicationName
        };

        return (new SheetsService(initializer), new DriveService(initializer));
    }
    #endregion
}
