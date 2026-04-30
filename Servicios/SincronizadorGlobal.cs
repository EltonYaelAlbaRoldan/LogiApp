using Microsoft.Maui.Networking;
using LogiApp.Servicios;
using LogiApp.Modelos;
using System.IO;

namespace LogiApp.Servicios;

public static class SincronizadorGlobal
{
    public static async Task IntentarSincronizacionSilenciosa()
    {
        // Si no hay internet, se vuelve a dormir
        if (Connectivity.Current.NetworkAccess != NetworkAccess.Internet) return;

        try
        {
            // Instanciamos los servicios
            var authService = new ConexionGoogleAuth();
            var (sService, dService) = await authService.ObtenerServiciosAsync();
            var sheets = new ConsultasGoogleSheets(sService);
            var drive = new ConsultasGoogleDrive(dService);

            // 1. REVISAR VIAJES COMPLETOS ATORADOS (El del Menú)
            string foliosPendientes = Preferences.Default.Get("ListaFoliosOffline", "");
            if (!string.IsNullOrEmpty(foliosPendientes))
            {
                var listaFolios = foliosPendientes.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).ToList();
                foreach (string folio in listaFolios.ToList())
                {
                    string fechaFin = Preferences.Default.Get($"Offline_FechaFin_{folio}", "");
                    string kmLlegada = Preferences.Default.Get($"Offline_KmLlegada_{folio}", "");
                    string fuelLlegada = Preferences.Default.Get($"Offline_FuelLlegada_{folio}", "");
                    string obs = Preferences.Default.Get($"Offline_Obs_{folio}", "N/A");
                    string rutaFoto = Preferences.Default.Get($"Offline_RutaFoto_{folio}", "");
                    string duracion = Preferences.Default.Get($"Offline_Duracion_{folio}", "N/A");
                    string recorrido = Preferences.Default.Get($"Offline_Recorrido_{folio}", "0");
                    string consumo = Preferences.Default.Get($"Offline_Consumo_{folio}", "0");
                    string horaLlegada = Preferences.Default.Get($"Offline_HoraLlegada_{folio}", fechaFin);

                    string idOp = Preferences.Default.Get("UsuarioLogeado", "SinID");
                    string linkCarpeta = "Sin Evidencia";

                    if (File.Exists(rutaFoto))
                    {
                        linkCarpeta = await drive.SubirEvidenciaViajeRapidoAsync(rutaFoto, idOp, folio);
                    }

                    await sheets.ActualizarEstatusYLlegadaAsync(folio, "EN DESTINO", horaLlegada);
                    bool exito = await sheets.FinalizarViajeAsync(folio, fechaFin, kmLlegada, fuelLlegada, obs, linkCarpeta, duracion, recorrido, consumo);

                    if (exito)
                    {
                        Preferences.Default.Remove($"Offline_Pendiente_{folio}");
                        listaFolios.Remove(folio);
                        string nuevaLista = string.Join(",", listaFolios);
                        if (!string.IsNullOrEmpty(nuevaLista)) nuevaLista += ",";
                        Preferences.Default.Set("ListaFoliosOffline", nuevaLista);
                    }
                }
            }

            // 2. REVISAR LLEGADAS O GPS EN VIVO (El de la pantalla de Viaje)
            string folioActual = Preferences.Default.Get("FolioPendiente", "");
            if (!string.IsNullOrEmpty(folioActual))
            {
                // --- Lógica Original de GPS ---
                if (Preferences.Default.Get($"Offline_UbiPendiente_{folioActual}", false))
                {
                    double lat = Preferences.Default.Get($"Offline_UbiLat_{folioActual}", 0.0);
                    double lon = Preferences.Default.Get($"Offline_UbiLon_{folioActual}", 0.0);
                    string horaCapturada = Preferences.Default.Get($"Offline_UbiHora_{folioActual}", "");

                    if (await sheets.ActualizarUbicacionViajeAsync(folioActual, lat, lon, horaCapturada))
                    {
                        Preferences.Default.Remove($"Offline_UbiPendiente_{folioActual}");
                    }
                }

                // --- Lógica Original de Arribo ---
                if (Preferences.Default.Get($"Offline_ArriboPendiente_{folioActual}", false))
                {
                    string horaLlegada = Preferences.Default.Get("HoraLlegadaReal_" + folioActual, "");
                    if (await sheets.ActualizarEstatusYLlegadaAsync(folioActual, "EN DESTINO", horaLlegada))
                    {
                        Preferences.Default.Remove($"Offline_ArriboPendiente_{folioActual}");
                    }
                }

                // --- 3. NUEVA LÓGICA: SINCRONIZAR TRACKING DE SQLite (Celdas Gemelas) ---
                var dbLocal = new BaseDeDatosLocal();
                var ubiPendientes = await dbLocal.ObtenerUbicacionesPendientesAsync();

                if (ubiPendientes != null && ubiPendientes.Count > 0)
                {
                    // Recuperamos columnas y la fila donde nos quedamos
                    string colA = Preferences.Default.Get("ColTracking_A", "A");
                    string colB = Preferences.Default.Get("ColTracking_B", "B");
                    int filaActual = Preferences.Default.Get("FilaActual_Tracking", 2);
                    string hojaMes = $"Historial_{DateTime.Now.ToString("MMMM")}_{DateTime.Now.Year}";

                    // Ordenamos por fecha para que se peguen en orden cronológico en las filas
                    foreach (var ubi in ubiPendientes.OrderBy(x => x.FechaHora))
                    {
                        // Intentamos subir a la celda exacta
                        bool ok = await sheets.SubirPuntoGPSTiempoRealAsync(hojaMes, colA, colB, filaActual, ubi);

                        if (ok)
                        {
                            // Si se subió, borramos de SQLite y avanzamos la fila para el siguiente
                            await dbLocal.BorrarUbicacionAsync(ubi.Id);
                            filaActual++;
                            Preferences.Default.Set("FilaActual_Tracking", filaActual);
                        }
                        else
                        {
                            // Si falla uno, detenemos el ciclo para no saltarnos filas
                            break;
                        }
                    }
                }
            }
        }
        catch
        {
            /* Falla silenciosa, se intenta en la próxima conexión */
        }
    }
}