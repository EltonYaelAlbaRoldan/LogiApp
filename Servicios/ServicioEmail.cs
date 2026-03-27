using System.Net;
using System.Net.Mail;

namespace LogiApp.Servicios;

public class ServicioEmail
{
    #region CONFIGURACIÓN
    private const string CorreoEmisor = "eltonyaelalbaroldan@gmail.com";
    private const string PasswordApp = "ecyk przx ymdz otzu";
    #endregion

    #region MÉTODOS PÚBLICOS

    // Método para enviar las credenciales al registrarse
    public async Task EnviarCredencialesAsync(string correoDestino, string nombre, string idOperador, string password)
    {
        string cuerpo = $@"
            <div style='font-family: sans-serif;'>
                <h2 style='color: #0A0A33;'>¡Bienvenido a LogiApp, {nombre}!</h2>
                <p>Se ha creado tu expediente digital exitosamente.</p>
                <p>Tus credenciales de acceso son:</p>
                <div style='background-color: #F5F5F7; padding: 15px; border-radius: 10px;'>
                    <p><b>Usuario:</b> <span style='color:#2196F3;'>{idOperador}</span></p>
                    <p><b>Contraseña:</b> <span style='color:#2196F3;'>{password}</span></p>
                </div>
                <p>Por favor, inicia sesión y verifica tu información.</p>
            </div>";

        await EnviarCorreoGenericoAsync(correoDestino, "Tus Credenciales de Acceso - LogiApp", cuerpo);
    }

    // Método para enviar el código de recuperación
    public async Task<bool> EnviarCodigoRecuperacionAsync(string correoDestino, string codigo)
    {
        string cuerpo = $@"
            <div style='font-family: sans-serif;'>
                <h2 style='color: #0A0A33;'>Recuperación de Contraseña</h2>
                <p>Has solicitado restablecer tu contraseña en <strong>LogiApp</strong>.</p>
                <p>Tu código de verificación es:</p>
                <h1 style='background-color: #F5F5F7; padding: 20px; text-align: center; letter-spacing: 10px; color: #2196F3;'>{codigo}</h1>
                <p>Si no solicitaste este cambio, ignora este correo por seguridad.</p>
            </div>";

        return await EnviarCorreoGenericoAsync(correoDestino, "LogiApp - Código de Recuperación", cuerpo);
    }

    #endregion

    #region MOTOR DE ENVÍO (MÉTODO GENÉRICO)

    // Este es el método que te faltaba. Centraliza la configuración de SMTP.
    private async Task<bool> EnviarCorreoGenericoAsync(string correoDestino, string asunto, string cuerpoHtml)
    {
        try
        {
            using (var smtpClient = new SmtpClient("smtp.gmail.com"))
            {
                smtpClient.Port = 587;
                smtpClient.Credentials = new NetworkCredential(CorreoEmisor, PasswordApp);
                smtpClient.EnableSsl = true;

                var mensaje = new MailMessage
                {
                    From = new MailAddress(CorreoEmisor, "Sistema LogiApp"),
                    Subject = asunto,
                    Body = cuerpoHtml,
                    IsBodyHtml = true
                };

                mensaje.To.Add(correoDestino);

                await smtpClient.SendMailAsync(mensaje);
                return true;
            }
        }
        catch (Exception ex)
        {
            // Log del error para depuración
            System.Diagnostics.Debug.WriteLine("Error SMTP: " + ex.Message);
            return false;
        }
    }

    #endregion
}