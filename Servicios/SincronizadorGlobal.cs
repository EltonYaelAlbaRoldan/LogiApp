using Microsoft.Maui.Networking;
using LogiApp.Servicios;
using LogiApp.Modelos;
using System.IO;
using System.Text.Json;

namespace LogiApp.Servicios;

public static class SincronizadorGlobal
{
    private static readonly SemaphoreSlim _semaforo = new(1, 1);

    public static async Task IntentarSincronizacionSilenciosa()
    {
        // Si no hay internet, se vuelve a dormir
        if (Connectivity.Current.NetworkAccess != NetworkAccess.Internet) return;
        if (!await _semaforo.WaitAsync(0)) return;

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
                    string fuelRecarga = Preferences.Default.Get($"Offline_FuelRecarga_{folio}", "0"); // <-- NUEVA LÍNEA
                    string obs = Preferences.Default.Get($"Offline_Obs_{folio}", "N/A");
                    string rutasFotosJson = Preferences.Default.Get($"Offline_RutasFotos_{folio}", "");
                    var rutasFotos = string.IsNullOrWhiteSpace(rutasFotosJson)
                        ? new List<string>()
                        : JsonSerializer.Deserialize<List<string>>(rutasFotosJson) ?? new List<string>();

                    // Compatibilidad con pendientes creados por versiones anteriores.
                    string rutaFotoAnterior = Preferences.Default.Get($"Offline_RutaFoto_{folio}", "");
                    if (rutasFotos.Count == 0 && File.Exists(rutaFotoAnterior)) rutasFotos.Add(rutaFotoAnterior);
                    string duracion = Preferences.Default.Get($"Offline_Duracion_{folio}", "N/A");
                    string recorrido = Preferences.Default.Get($"Offline_Recorrido_{folio}", "0");
                    string consumo = Preferences.Default.Get($"Offline_Consumo_{folio}", "0");
                    string horaLlegada = Preferences.Default.Get($"Offline_HoraLlegada_{folio}", fechaFin);

                    string idOp = Preferences.Default.Get($"Offline_IdOperador_{folio}", Preferences.Default.Get("UsuarioLogeado", "SinID"));
                    string linkCarpeta = "Sin Evidencia";

                    if (rutasFotos.Any(File.Exists))
                    {
                        linkCarpeta = await drive.SubirEvidenciasViajeAsync(rutasFotos, idOp, folio);
                    }

                    await sheets.ActualizarEstatusYLlegadaAsync(folio, "EN DESTINO", horaLlegada);
                    bool exito = await sheets.FinalizarViajeAsync(folio, fechaFin, kmLlegada, fuelLlegada, fuelRecarga, obs, linkCarpeta, duracion, recorrido, consumo);
                    if (exito)
                    {
                        Preferences.Default.Remove($"Offline_Pendiente_{folio}");
                        Preferences.Default.Remove($"Offline_IdOperador_{folio}");
                        Preferences.Default.Remove($"Offline_FechaFin_{folio}");
                        Preferences.Default.Remove($"Offline_HoraLlegada_{folio}");
                        Preferences.Default.Remove($"Offline_KmLlegada_{folio}");
                        Preferences.Default.Remove($"Offline_FuelLlegada_{folio}");
                        Preferences.Default.Remove($"Offline_Obs_{folio}");
                        Preferences.Default.Remove($"Offline_RutasFotos_{folio}");
                        Preferences.Default.Remove($"Offline_RutaFoto_{folio}");
                        Preferences.Default.Remove($"Offline_Duracion_{folio}");
                        Preferences.Default.Remove($"Offline_Recorrido_{folio}");
                        Preferences.Default.Remove($"Offline_Consumo_{folio}");
                        Preferences.Default.Remove($"Offline_FuelRecarga_{folio}");
                        listaFolios.Remove(folio);
                        string nuevaLista = string.Join(",", listaFolios);
                        Preferences.Default.Set("ListaFoliosOffline", nuevaLista);
                    }
                }
            }

            // 2. REVISAR LLEGADAS EN VIVO (El de la pantalla de Viaje)
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

            }

            // 3. Cada punto conserva su folio, hoja y fila. Así nunca se mezcla
            // con el viaje que esté abierto cuando regrese la conexión.
            var dbLocal = new BaseDeDatosLocal();
            var ubiPendientes = await dbLocal.ObtenerUbicacionesPendientesAsync();
            foreach (var ubi in ubiPendientes.OrderBy(x => x.FechaHora))
            {
                if (string.IsNullOrWhiteSpace(ubi.HojaTracking) || ubi.FilaTracking <= 0)
                    continue;

                bool esFormatoAnterior = !string.IsNullOrWhiteSpace(ubi.ColumnaTrackingA) ||
                    !string.IsNullOrWhiteSpace(ubi.ColumnaTrackingB);
                bool ok = esFormatoAnterior
                    ? await sheets.SubirPuntoGPSTiempoRealAsync(
                        ubi.HojaTracking, ubi.ColumnaTrackingA ?? "A", ubi.ColumnaTrackingB ?? "B", ubi.FilaTracking, ubi)
                    : await sheets.SubirPuntoGPSTiempoRealAsync(
                        ubi.HojaTracking, ubi.FolioViaje ?? "SIN FOLIO", ubi.FilaTracking, ubi);
                if (ok)
                {
                    await dbLocal.BorrarUbicacionAsync(ubi.Id);
                    int filaMapa = esFormatoAnterior
                        ? (await sheets.ObtenerOCrearFilaTrackingAsync(ubi.HojaTracking, ubi.FolioViaje ?? "SIN FOLIO")).fila
                        : ubi.FilaTracking;
                    await sheets.ActualizarVinculoMapaRutaAsync(
                        ubi.FolioViaje ?? "SIN FOLIO", ubi.HojaTracking, filaMapa);
                }
                else break;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error de sincronización global: {ex}");
            await GestorErrores.GuardarErrorAsync(ex, "SincronizacionGlobal", Preferences.Get("UsuarioLogeado", "Desconocido"));
        }
        finally
        {
            _semaforo.Release();
        }
    }
}
