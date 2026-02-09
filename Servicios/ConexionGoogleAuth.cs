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
    private const string ClientId = "1021863353895-55eb5gi10kt8mmd0u4kb08i0dq5ahf45.apps.googleusercontent.com";
    private const string ClientSecret = "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"; 
    private const string RefreshToken = "1//04EHNDjplHNepCgYIARAAGAQSNwF-L9IrNlnTSVCnFUOmZ6KsiDStc9lpanE-ct1a5yYWCri_WAHq9y5NmwmrlthXEoUfRC0lScc";
    private const string ApplicationName = "LogiApp";
    #endregion

    #region MÉTODOS DE SERVICIO
    public async Task<(SheetsService, DriveService)> ObtenerServiciosAsync()
    {
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