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
    public async Task EnviarCredencialesAsync(string correoDestino, string nombre, string idOperador)
    {
        string cuerpo = $@"
        <div style='font-family: sans-serif; max-width: 600px; border: 1px solid #eee; padding: 20px;'>
            <h2 style='color: #0A0A33;'>¡Bienvenido a LogiApp, {nombre}!</h2>
            <p>Hemos recibido tu registro y se ha creado tu expediente digital exitosamente.</p>
            
            <div style='background-color: #FFF3CD; border-left: 5px solid #FFC107; padding: 15px; margin: 20px 0;'>
                <p style='margin: 0; color: #856404;'>
                    <strong>Estado actual: En proceso de validación.</strong>
                </p>
                <p style='margin: 10px 0 0 0;'>
                    Tus documentos están siendo revisados por el administrador. 
                    Por seguridad, tu acceso se encuentra temporalmente restringido.
                </p>
            </div>

            <p>Tu ID de usuario asignado es: <b style='color:#2196F3;'>{idOperador}</b></p>
            
            <p>Una vez que tu información sea validada, recibirás un <b>segundo correo electrónico</b> con tu contraseña y la confirmación de activación para que puedas iniciar sesión.</p>
            
            <hr style='border: none; border-top: 1px solid #eee; margin: 20px 0;'>
            <p style='font-size: 12px; color: #777;'>
                Si tienes alguna duda sobre tu proceso, contacta al departamento de recursos humanos.
            </p>
        </div>";

        // Nota: Eliminamos el parámetro 'password' de la llamada porque ya no se enviará aquí
        await EnviarCorreoGenericoAsync(correoDestino, "Expediente Recibido - LogiApp", cuerpo);
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