using System.Net;
using System.Net.Mail;

namespace LogiApp.Servicios;

public class ServicioEmail
{
    private const string CorreoEmisor = "eltonyaelalbaroldan@gmail.com"; // Tu correo
    private const string PasswordApp = "ecyk przx ymdz otzu"; // El que generaste

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
                <ul>
                    <li><b>Usuario:</b> {idOperador}</li>
                    <li><b>Contraseña:</b> {password}</li>
                </ul>
                <p>Por favor, no compartas estos datos con nadie.</p>";

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
            // Solo lanzamos el error para que la página lo atrape
            throw new Exception("Error al enviar el correo: " + ex.Message);
        }
    }
}