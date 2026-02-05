using Google.Apis.Auth.OAuth2;
using Google.Apis.Auth.OAuth2.Flows;
using Google.Apis.Auth.OAuth2.Responses;
using Google.Apis.Drive.v3;
using Google.Apis.Services;
using Google.Apis.Sheets.v4;

namespace LogiApp.Servicios;

/// <summary>
/// Gestiona la autenticación con Google APIs utilizando OAuth 2.0 y Refresh Tokens.
/// Proporciona acceso a los servicios de Google Sheets y Google Drive.
/// </summary>
public class ConexionGoogleAuth
{
    // --- CONSTANTES DE AUTENTICACIÓN ---
    private const string ClientId = "1021863353895-55eb5gi10kt8mmd0u4kb08i0dq5ahf45.apps.googleusercontent.com";
    private const string ClientSecret = "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";
    private const string RefreshToken = "1//04EHNDjplHNepCgYIARAAGAQSNwF-L9IrNlnTSVCnFUOmZ6KsiDStc9lpanE-ct1a5yYWCri_WAHq9y5NmwmrlthXEoUfRC0lScc";
    private const string ApplicationName = "LogiApp";

    /// <summary>
    /// Genera y retorna los servicios autorizados de Sheets y Drive
    /// Realiza la renovación automática del token si este ha expirado.
    /// </summary>
    /// <returns>Tupla conteniendo (SheetsService, DriveService)</returns>
    public async Task<(SheetsService, DriveService)> ObtenerServiciosAsync()
    {
        // 1. Configuración del flujo de autorización
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

        // 2. Creación de la credencial a partir del Refresh Token guardado
        var tokenResponse = new TokenResponse { RefreshToken = RefreshToken };
        var credential = new UserCredential(flow, "user", tokenResponse);

        // 3. Verificación y renovación automática del Access Token
        // Esto evita errores de "401 Unauthorized" durante la ejecución
        if (tokenResponse.IsExpired(Google.Apis.Util.SystemClock.Default))
        {
            await credential.RefreshTokenAsync(CancellationToken.None);
        }

        // 4. Inicialización de los servicios con la credencial autorizada
        var initializer = new BaseClientService.Initializer()
        {
            HttpClientInitializer = credential,
            ApplicationName = ApplicationName
        };

        return (new SheetsService(initializer), new DriveService(initializer));
    }
}