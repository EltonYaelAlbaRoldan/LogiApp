using System;
using System.Threading.Tasks;
using LogiApp.Modelos;

namespace LogiApp.Servicios
{
    public static class GestorErrores
    {
        public static async Task GuardarErrorAsync(Exception ex, string nombreProceso, string idOperador = "Desconocido")
        {
            try
            {
                var dbLocal = new BaseDeDatosLocal();

                // --- LIMPIEZA Y TRADUCCIÓN DE MENSAJES PARA EXCEL ---
                string mensajeLimpio = ex.Message;

                if (ex.Message.Contains("Host desconocido") || ex.Message.Contains("network") || ex.Message.Contains("access denied"))
                {
                    mensajeLimpio = "⚠️ FALLO DE RED: Sin internet o Google bloqueado";
                }
                else if (ex.Message.Contains("Object reference") || ex.Message.Contains("null"))
                {
                    mensajeLimpio = "🧩 ERROR LÓGICO: Objeto no cargado (Null)";
                }
                else if (ex.Message.Contains("quota") || ex.Message.Contains("429"))
                {
                    mensajeLimpio = "⏳ LÍMITE: Demasiadas peticiones a Google";
                }
                else if (ex.Message.Contains("era null al intentar login"))
                {
                    mensajeLimpio = "🚫 SERVICIO: Sheets no estaba listo al presionar botón";
                }
                else if (ex.Message.Contains("Format") || ex.Message.Contains("convert"))
                {
                    mensajeLimpio = "🔢 ERROR DE FORMATO: Datos mal escritos (ej. letras en campos numéricos)";
                }
                else if (ex.Message.Contains("Drive") || ex.Message.Contains("Upload"))
                {
                    mensajeLimpio = "☁️ ERROR DE DRIVE: Problema con la foto";
                }

                var nuevoLog = new LogRecord
                {
                    FechaHora = DateTime.Now,
                    TipoError = nombreProceso,
                    Mensaje = mensajeLimpio, // Mensaje corto para la columna C
                    StackTrace = ex.StackTrace, // Detalles técnicos para la columna D (puedes ocultarla en Sheets)
                    Sincronizado = false,
                    IdOperador = idOperador // Columna E
                };

                await dbLocal.InsertarLogAsync(nuevoLog);
            }
            catch { }
        }

        public static async Task SincronizarLogsPendientesAsync(ConsultasGoogleSheets servicioSheets)
        {
            try
            {
                var dbLocal = new BaseDeDatosLocal();
                var pendientes = await dbLocal.ObtenerLogsPendientesAsync();

                if (pendientes == null || pendientes.Count == 0) return;

                foreach (var log in pendientes)
                {
                    // 1. Intentamos mandar a Google
                    bool enviado = await servicioSheets.GuardarLogEnSheetsAsync(log);

                    if (enviado)
                    {
                        // 2. Si se envió, lo borramos INMEDIATAMENTE de la base de datos local
                        // Usamos un borrado directo por ID para que no haya duda
                        await dbLocal.BorrarLogPorIdAsync(log.Id);
                    }
                }
            }
            catch
            {
                // Si falla algo, se quedan para el próximo intento
            }
        }
    }
}