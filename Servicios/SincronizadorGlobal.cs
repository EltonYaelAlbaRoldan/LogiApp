using Microsoft.Maui.Networking;
using LogiApp.Servicios;

namespace LogiApp.Servicios;

public static class SincronizadorGlobal
{
    public static async Task IntentarSincronizacionSilenciosa()
    {
        // Si no hay internet, se vuelve a dormir
        if (Connectivity.Current.NetworkAccess != NetworkAccess.Internet) return;

        try
        {
            // Instanciamos los servicios (esto requiere que modifiques ligeramente ConexionGoogleAuth si es necesario,
            // pero podemos crear instancias nuevas rápido)
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
                // Checar GPS pendiente
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

                // Checar Arribo pendiente
                if (Preferences.Default.Get($"Offline_ArriboPendiente_{folioActual}", false))
                {
                    string horaLlegada = Preferences.Default.Get("HoraLlegadaReal_" + folioActual, "");
                    if (await sheets.ActualizarEstatusYLlegadaAsync(folioActual, "EN DESTINO", horaLlegada))
                    {
                        Preferences.Default.Remove($"Offline_ArriboPendiente_{folioActual}");
                    }
                }
            }
        }
        catch { /* Falla silenciosa, nadie se da cuenta, se intenta luego */ }
    }
}