using System.Net;
using System.Net.Mail;

namespace LogiApp.Servicios;

public class ServicioEmail
{
    #region CONFIGURACIÓN Y ENVÍO
    private const string CorreoEmisor = "eltonyaelalbaroldan@gmail.com";
    private const string PasswordApp = "ecyk przx ymdz otzu";

    public async Task EnviarCredencialesAsync(string correoDestino, string nombre, string idOperador, string password)
    {
        try
        {
            var smtpClient = new SmtpClient("smtp.gmail.com")
            {
                Port = 587,
                Credentials = new NetworkCredential(CorreoEmisor, PasswordApp),
                EnableSsl = true,
            };

            string cuerpo = $@"
                <h2>¡Bienvenido a LogiApp, {nombre}!</h2>
                <p>Se ha creado tu expediente digital exitosamente.</p>
                <p>Tus credenciales de acceso son:</p>
                <ul style='list-style:none; padding:0;'>
                    <li><b>Usuario:</b> <span style='color:#2196F3;'>{idOperador}</span></li>
                    <li><b>Contraseña:</b> <span style='color:#2196F3;'>{password}</span></li>
                </ul>
                <p>Por favor, cambia tu contraseña desde tu perfil al iniciar sesión por primera vez.</p>
                <p><i>Este es un mensaje automático, no responder.</i></p>";

            var mensaje = new MailMessage
            {
                From = new MailAddress(CorreoEmisor, "LogiApp System"),
                Subject = "Tus Credenciales de Acceso - LogiApp",
                Body = cuerpo,
                IsBodyHtml = true
            };

            mensaje.To.Add(correoDestino);

            await smtpClient.SendMailAsync(mensaje);
        }
        catch (Exception ex)
        {
            throw new Exception("Error al enviar el correo: " + ex.Message);
        }
    }
    #endregion
}