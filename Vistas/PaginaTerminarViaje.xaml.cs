using LogiApp.Servicios;
using LogiApp.Modelos;
using Microsoft.Maui.Devices.Sensors;
using Microsoft.Maui.Networking;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System;
using System.IO;
using System.Collections.ObjectModel;
using System.Text.Json;

namespace LogiApp.Vistas;

public partial class PaginaTerminarViaje : ContentPage
{
    #region Atributos y Servicios
    private ConsultasGoogleSheets _sheets;
    private ConsultasGoogleDrive _drive;
    private readonly ConexionGoogleAuth _authService = new();
    private string _folioActual;
    private readonly ObservableCollection<string> _rutasFotosTemporales = new();
    private CancellationTokenSource _ctsTracking;
    private bool _isTrackingRunning = false;
    private readonly BaseDeDatosLocal _dbLocal = new();
    private readonly SemaphoreSlim _semaforoTracking = new(1, 1);
    #endregion

    public PaginaTerminarViaje(string folio)
    {
        InitializeComponent();
        _folioActual = folio;
        LblFolio.Text = $"Folio: {folio}";
        string destinoGuardado = Preferences.Default.Get("Destino_" + folio, "Destino no especificado");
        LblDestino.Text = $"Destino: {destinoGuardado}";
        ListaFotos.ItemsSource = _rutasFotosTemporales;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await InicializarServicio();
        await AsegurarTrackingVerticalAsync();
        bool permisoNotificaciones = await ControlTrackingSegundoPlano.SolicitarPermisoNotificacionesAsync();
        string llaveAvisoNotificacion = $"AvisoNotificacionTracking_{_folioActual}";
        if (!permisoNotificaciones && !Preferences.Default.Get(llaveAvisoNotificacion, false))
        {
            Preferences.Default.Set(llaveAvisoNotificacion, true);
            await DisplayAlert(
                "Permiso recomendado",
                "Activa las notificaciones de LogiApp para ver el aviso permanente mientras el viaje está en curso.",
                "Entendido");
        }
        ControlTrackingSegundoPlano.Iniciar();
#if !ANDROID
        if (!_isTrackingRunning) _ = IniciarTimerTracking();
#endif

        if (Preferences.Default.Get("ArriboConfirmado_" + _folioActual, false))
        {
            string duracionStr = Preferences.Default.Get("Duracion_" + _folioActual, "N/A");
            BtnReportar.IsEnabled = false;
            BtnArribo.IsEnabled = false;
            BtnArribo.Text = $"LLEGADA ✅ ({duracionStr})";
            SeccionCierre.IsEnabled = true;
            SeccionCierre.Opacity = 1.0;
        }

        Connectivity.Current.ConnectivityChanged += OnConnectivityChanged;
        _ = IntentarSincronizarDatosEnVivoAsync();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        Connectivity.Current.ConnectivityChanged -= OnConnectivityChanged;
    }

    private async void OnConnectivityChanged(object sender, ConnectivityChangedEventArgs e)
    {
        if (e.NetworkAccess == NetworkAccess.Internet) await IntentarSincronizarDatosEnVivoAsync();
    }

    private async Task InicializarServicio()
    {
        if (_sheets != null) return;
        try
        {
            var (sService, dService) = await _authService.ObtenerServiciosAsync();
            _sheets = new ConsultasGoogleSheets(sService);
            _drive = new ConsultasGoogleDrive(dService);
        }
        catch { }
    }

    private async Task AsegurarTrackingVerticalAsync()
    {
        if (_sheets == null) return;
        string hoja = Preferences.Default.Get("HojaActual_Tracking", $"Historial_{DateTime.Now:MMMM}_{DateTime.Now.Year}");
        bool vieneDeFormatoAnterior = !string.IsNullOrWhiteSpace(Preferences.Default.Get("ColTracking_A", "")) ||
            !string.IsNullOrWhiteSpace(Preferences.Default.Get("ColTracking_B", ""));
        int fila = Preferences.Default.Get("FilaActual_Tracking", 0);
        if (!vieneDeFormatoAnterior && fila >= 2) return;

        try
        {
            (fila, _) = await _sheets.ObtenerOCrearFilaTrackingAsync(hoja, _folioActual);
            Preferences.Default.Set("FilaActual_Tracking", fila);
            Preferences.Default.Remove("ColTracking_A");
            Preferences.Default.Remove("ColTracking_B");
        }
        catch { }
    }

    private async Task IntentarSincronizarDatosEnVivoAsync()
    {
        if (_sheets == null || Connectivity.Current.NetworkAccess != NetworkAccess.Internet) return;

        try
        {
            var pendientes = (await _dbLocal.ObtenerUbicacionesPendientesAsync())
                .Where(x => string.Equals(x.FolioViaje, _folioActual, StringComparison.OrdinalIgnoreCase))
                .OrderBy(x => x.FechaHora)
                .ToList();
            if (pendientes != null && pendientes.Count > 0)
            {
                foreach (var ubi in pendientes)
                {
                    if (string.IsNullOrWhiteSpace(ubi.HojaTracking) || ubi.FilaTracking <= 0)
                        continue;

                    bool ok = !string.IsNullOrWhiteSpace(ubi.ColumnaTrackingA) || !string.IsNullOrWhiteSpace(ubi.ColumnaTrackingB)
                        ? await _sheets.SubirPuntoGPSTiempoRealAsync(
                            ubi.HojaTracking, ubi.ColumnaTrackingA ?? "A", ubi.ColumnaTrackingB ?? "B", ubi.FilaTracking, ubi)
                        : await _sheets.SubirPuntoGPSTiempoRealAsync(
                            ubi.HojaTracking, ubi.FolioViaje ?? _folioActual, ubi.FilaTracking, ubi);
                    if (ok) await _dbLocal.BorrarUbicacionAsync(ubi.Id);
                }
            }
        }
        catch { }
    }

    private async void OnReportarUbicacionClicked(object sender, EventArgs e)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        try
        {
            BtnReportar.IsEnabled = false;
            BtnReportar.Text = "Enviando...";

            var request = new GeolocationRequest(GeolocationAccuracy.High, TimeSpan.FromSeconds(10));
            var location = await Geolocation.Default.GetLocationAsync(request, cts.Token);

            if (location == null) throw new Exception("GPS no disponible");

            var punto = new UbicacionViaje
            {
                FolioViaje = _folioActual,
                Latitud = location.Latitude,
                Longitud = location.Longitude,
                FechaHora = DateTime.Now,
                Sincronizado = false
            };

            bool subioExitosamente = false;
            int filaActual = Preferences.Default.Get("FilaActual_Tracking", 2);
            string hojaTracking = Preferences.Default.Get("HojaActual_Tracking", $"Historial_{DateTime.Now:MMMM}_{DateTime.Now.Year}");
            punto.HojaTracking = hojaTracking;
            punto.FilaTracking = filaActual;
            await _dbLocal.GuardarUbicacionLocalAsync(punto);

            if (Connectivity.Current.NetworkAccess == NetworkAccess.Internet)
            {
                try
                {
                    subioExitosamente = await _sheets.SubirPuntoGPSTiempoRealAsync(hojaTracking, _folioActual, filaActual, punto);
                }
                catch { subioExitosamente = false; }
            }

            if (subioExitosamente)
            {
                if (punto.Id > 0) await _dbLocal.BorrarUbicacionAsync(punto.Id);
                BtnReportar.BackgroundColor = Colors.Green;
                BtnReportar.Text = "UBICACIÓN ENVIADA ✅";
            }
            else
            {
                BtnReportar.BackgroundColor = Colors.Orange;
                BtnReportar.Text = "GUARDADO LOCAL ⚠️";
            }

            Preferences.Default.Set("FilaActual_Tracking", filaActual);
        }
        catch (Exception)
        {
            BtnReportar.Text = "ERROR - GUARDADO LOCAL";
        }
        finally
        {
            await Task.Delay(3000);
            BtnReportar.IsEnabled = true;
            BtnReportar.BackgroundColor = Color.FromArgb("#4CAF50");
            BtnReportar.Text = "REPORTAR UBICACIÓN ACTUAL";
        }
    }

    private async Task IniciarTimerTracking()
    {
        _isTrackingRunning = true;
        _ctsTracking = new CancellationTokenSource();
        while (_isTrackingRunning && !_ctsTracking.Token.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(TimeSpan.FromMinutes(10), _ctsTracking.Token);
                await RealizarReporteAutomatico();
            }
            catch { break; }
        }
    }

    private async Task RealizarReporteAutomatico()
    {
        try
        {
            var location = await Geolocation.Default.GetLocationAsync(new GeolocationRequest(GeolocationAccuracy.Medium));
            if (location != null)
            {
                var punto = new UbicacionViaje { FolioViaje = _folioActual, Latitud = location.Latitude, Longitud = location.Longitude, FechaHora = DateTime.Now };

                int fila = Preferences.Default.Get("FilaActual_Tracking", 2);
                string hojaTracking = Preferences.Default.Get("HojaActual_Tracking", $"Historial_{DateTime.Now:MMMM}_{DateTime.Now.Year}");
                punto.HojaTracking = hojaTracking;
                punto.FilaTracking = fila;
                await _dbLocal.GuardarUbicacionLocalAsync(punto);
                bool subio = false;

                if (Connectivity.Current.NetworkAccess == NetworkAccess.Internet)
                {
                    try { subio = await _sheets.SubirPuntoGPSTiempoRealAsync(hojaTracking, _folioActual, fila, punto); }
                    catch { subio = false; }
                }

                if (subio && punto.Id > 0) await _dbLocal.BorrarUbicacionAsync(punto.Id);
                Preferences.Default.Set("FilaActual_Tracking", fila);
            }
        }
        catch { }
    }

    private async void OnMarcarLlegadaClicked(object sender, EventArgs e)
    {
        var confirmar = await DisplayAlert("Confirmar", "¿Has llegado a tu destino?", "Sí", "No");
        if (!confirmar) return;

        IndicadorCarga.IsVisible = true;

        BtnArribo.IsEnabled = false;
        BtnReportar.IsEnabled = false;
        _isTrackingRunning = false;
        _ctsTracking?.Cancel();

        bool coordenadaFinalGuardada = await CapturarYGuardarPuntoFinalAsync();
        if (!coordenadaFinalGuardada)
        {
            IndicadorCarga.IsVisible = false;
            BtnArribo.IsEnabled = true;
            BtnReportar.IsEnabled = true;
            _isTrackingRunning = true;
            _ = IniciarTimerTracking();
            await DisplayAlert("Ubicación requerida",
                "No se pudo obtener la ubicación final. Activa el GPS y concede el permiso de ubicación antes de marcar la llegada.",
                "OK");
            return;
        }
        Preferences.Default.Set($"TrackingUbicacionActivo_{_folioActual}", false);
        ControlTrackingSegundoPlano.Detener();

        // RESTAURACIÓN: Cálculo de duración de viaje
        string horaLlegadaReal = DateTime.Now.ToString("dd/MM/yyyy HH:mm");
        string horaInicio = Preferences.Default.Get("Inicio_" + _folioActual, horaLlegadaReal);
        string duracionStr = "N/A";

        if (DateTime.TryParseExact(horaInicio, "dd/MM/yyyy HH:mm", null, System.Globalization.DateTimeStyles.None, out DateTime dtInicio) &&
            DateTime.TryParseExact(horaLlegadaReal, "dd/MM/yyyy HH:mm", null, System.Globalization.DateTimeStyles.None, out DateTime dtFin))
        {
            duracionStr = $"{(int)(dtFin - dtInicio).TotalHours}h {(dtFin - dtInicio).Minutes}m";
        }

        Preferences.Default.Set("Duracion_" + _folioActual, duracionStr);
        Preferences.Default.Set("ArriboConfirmado_" + _folioActual, true);
        Preferences.Default.Set("HoraLlegadaReal_" + _folioActual, horaLlegadaReal);

        if (Connectivity.Current.NetworkAccess == NetworkAccess.Internet)
        {
            try { await _sheets.ActualizarEstatusYLlegadaAsync(_folioActual, "EN DESTINO", horaLlegadaReal); }
            catch { Preferences.Default.Set($"Offline_ArriboPendiente_{_folioActual}", true); }
        }
        else Preferences.Default.Set($"Offline_ArriboPendiente_{_folioActual}", true);

        BtnArribo.IsEnabled = false;
        BtnArribo.Text = $"LLEGADA ✅ ({duracionStr})";
        SeccionCierre.IsEnabled = true;
        SeccionCierre.Opacity = 1.0;
        IndicadorCarga.IsVisible = false;
    }

    private async Task<bool> CapturarYGuardarPuntoFinalAsync()
    {
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            var request = new GeolocationRequest(GeolocationAccuracy.High, TimeSpan.FromSeconds(15));
            var location = await Geolocation.Default.GetLocationAsync(request, cts.Token);
            if (location == null) return false;

            await _semaforoTracking.WaitAsync();
            try
            {
                int fila = Preferences.Default.Get("FilaActual_Tracking", 2);
                string hoja = Preferences.Default.Get("HojaActual_Tracking", $"Historial_{DateTime.Now:MMMM}_{DateTime.Now.Year}");
                var punto = new UbicacionViaje
                {
                    FolioViaje = _folioActual,
                    Latitud = location.Latitude,
                    Longitud = location.Longitude,
                    FechaHora = DateTime.Now,
                    Sincronizado = false,
                    HojaTracking = hoja,
                    FilaTracking = fila
                };
                // La llegada conserva su hora real aunque el vehículo lleve varios
                // minutos detenido en exactamente las mismas coordenadas.
                await _dbLocal.GuardarUbicacionLocalAsync(punto, permitirDuplicado: true);

                bool subio = false;
                if (_sheets != null && Connectivity.Current.NetworkAccess == NetworkAccess.Internet)
                {
                    subio = await _sheets.SubirPuntoGPSTiempoRealAsync(
                        hoja, _folioActual, fila, punto, permitirDuplicado: true);
                    if (subio)
                        await _sheets.ActualizarVinculoMapaRutaAsync(_folioActual, hoja, fila);
                }

                if (subio && punto.Id > 0) await _dbLocal.BorrarUbicacionAsync(punto.Id);
                Preferences.Default.Set("FilaActual_Tracking", fila);
                return true;
            }
            finally
            {
                _semaforoTracking.Release();
            }
        }
        catch (Exception ex)
        {
            await GestorErrores.GuardarErrorAsync(ex, "UbicacionFinalViaje", Preferences.Get("UsuarioLogeado", "Desconocido"));
            return false;
        }
    }

    private async void OnTerminarViajeClicked(object sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(KmLlegadaEntry.Text) || string.IsNullOrWhiteSpace(CombustibleLlegadaEntry.Text) || _rutasFotosTemporales.Count == 0)
        {
            await DisplayAlert("Atención", "Captura kilometraje, combustible y al menos una foto.", "OK");
            return;
        }

        double.TryParse(Preferences.Default.Get("KmSalida_" + _folioActual, "0"), out double kmSalida);
        double.TryParse(Preferences.Default.Get("FuelSalida_" + _folioActual, "0"), out double fuelSalida);
        double.TryParse(KmLlegadaEntry.Text, out double kmLlegada);
        double.TryParse(CombustibleLlegadaEntry.Text, out double fuelLlegada);
        double.TryParse(CombustibleRecargaEntry?.Text ?? "0", out double fuelRecargado);

        if (kmLlegada < kmSalida)
        {
            await DisplayAlert("Error", "El kilometraje final no puede ser menor al inicial.", "OK");
            return;
        }

        if (fuelLlegada > (fuelSalida + fuelRecargado))
        {
            await DisplayAlert("Error", "El combustible final no coincide. Si cargaste gasolina, activa la opción de recarga.", "OK");
            return;
        }

        BtnFinalizar.IsEnabled = false;
        IndicadorCarga.IsVisible = true;

        string recorridoKmStr = (kmLlegada - kmSalida).ToString("0.##");
        string consumoLitrosStr = (fuelSalida + fuelRecargado - fuelLlegada).ToString("0.##");
        string fuelRecargadoStr = fuelRecargado.ToString("0.##");
        string duracionViaje = Preferences.Default.Get("Duracion_" + _folioActual, "N/A");
        string fechaFinReal = DateTime.Now.ToString("dd/MM/yyyy HH:mm");
        string horaLlegadaReal = Preferences.Default.Get("HoraLlegadaReal_" + _folioActual, fechaFinReal);

        string carpetaLocalViaje = Path.Combine(FileSystem.AppDataDirectory, "EvidenciasViajes", _folioActual);
        Directory.CreateDirectory(carpetaLocalViaje);
        var rutasSeguras = new List<string>();

        for (int i = 0; i < _rutasFotosTemporales.Count; i++)
        {
            string extension = Path.GetExtension(_rutasFotosTemporales[i]);
            if (string.IsNullOrWhiteSpace(extension)) extension = ".jpg";
            string rutaSegura = Path.Combine(carpetaLocalViaje, $"evidencia_{i + 1:D2}{extension}");
            File.Copy(_rutasFotosTemporales[i], rutaSegura, true);
            rutasSeguras.Add(rutaSegura);
        }

        // Registro offline preventivo (ahora con los datos calculados)
        Preferences.Default.Set($"Offline_Pendiente_{_folioActual}", true);
        Preferences.Default.Set($"Offline_IdOperador_{_folioActual}", Preferences.Get("UsuarioLogeado", "SinID"));
        Preferences.Default.Set($"Offline_KmLlegada_{_folioActual}", KmLlegadaEntry.Text);
        Preferences.Default.Set($"Offline_FuelLlegada_{_folioActual}", CombustibleLlegadaEntry.Text);
        Preferences.Default.Set($"Offline_FuelRecarga_{_folioActual}", fuelRecargadoStr);
        Preferences.Default.Set($"Offline_RutasFotos_{_folioActual}", JsonSerializer.Serialize(rutasSeguras));
        Preferences.Default.Set($"Offline_FechaFin_{_folioActual}", fechaFinReal);
        Preferences.Default.Set($"Offline_HoraLlegada_{_folioActual}", horaLlegadaReal);
        Preferences.Default.Set($"Offline_Obs_{_folioActual}", ObservacionesEditor.Text ?? "N/A");
        Preferences.Default.Set($"Offline_Duracion_{_folioActual}", duracionViaje);
        Preferences.Default.Set($"Offline_Recorrido_{_folioActual}", recorridoKmStr);
        Preferences.Default.Set($"Offline_Consumo_{_folioActual}", consumoLitrosStr);

        var foliosPendientes = Preferences.Default.Get("ListaFoliosOffline", "")
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        foliosPendientes.Add(_folioActual);
        Preferences.Default.Set("ListaFoliosOffline", string.Join(",", foliosPendientes));

        bool guardadoEnNube = false;

        if (Connectivity.Current.NetworkAccess == NetworkAccess.Internet)
        {
            try
            {
                string idOp = Preferences.Default.Get("UsuarioLogeado", "SinID");
                string link = await _drive.SubirEvidenciasViajeAsync(rutasSeguras, idOp, _folioActual);

                await _sheets.ActualizarEstatusYLlegadaAsync(_folioActual, "EN DESTINO", horaLlegadaReal);

                bool ok = await _sheets.FinalizarViajeAsync(_folioActual, fechaFinReal, KmLlegadaEntry.Text, CombustibleLlegadaEntry.Text, fuelRecargadoStr, ObservacionesEditor.Text, link, duracionViaje, recorridoKmStr, consumoLitrosStr);

                if (ok)
                {
                    guardadoEnNube = true;
                    Preferences.Default.Remove($"Offline_Pendiente_{_folioActual}");
                    foliosPendientes.Remove(_folioActual);
                    Preferences.Default.Set("ListaFoliosOffline", string.Join(",", foliosPendientes));
                }
            }
            catch (Exception ex)
            {
                await GestorErrores.GuardarErrorAsync(ex, "SubirEvidenciasViaje", Preferences.Get("UsuarioLogeado", "Desconocido"));
            }
        }

        // --- LIMPIEZA PROFUNDA DE CACHÉ ---
        // Borramos todo rastro de este folio para que no se "auto-termine" si se vuelve a usar
        Preferences.Default.Remove("ArriboConfirmado_" + _folioActual);
        Preferences.Default.Remove("HoraLlegadaReal_" + _folioActual);
        Preferences.Default.Remove("Duracion_" + _folioActual);
        Preferences.Default.Remove("Inicio_" + _folioActual);
        Preferences.Default.Remove("Destino_" + _folioActual);
        Preferences.Default.Remove("KmSalida_" + _folioActual);
        Preferences.Default.Remove("FuelSalida_" + _folioActual);
        Preferences.Default.Remove("FilaActual_Tracking"); // Para que el GPS reinicie desde arriba
        Preferences.Default.Remove("HojaActual_Tracking");
        Preferences.Default.Remove($"UltimaCapturaTrackingTicks_{_folioActual}");
        Preferences.Default.Remove($"TrackingUbicacionActivo_{_folioActual}");
        Preferences.Default.Remove($"AvisoNotificacionTracking_{_folioActual}");

        Preferences.Default.Set("ViajeEnCurso", false);
        Preferences.Default.Remove("FolioPendiente");
        _isTrackingRunning = false;
        _ctsTracking?.Cancel();
        ControlTrackingSegundoPlano.Detener();

        string mensajeCierre = guardadoEnNube
            ? $"Viaje cerrado. Se guardaron {_rutasFotosTemporales.Count} foto(s) en su carpeta de Drive."
            : "Viaje cerrado y guardado localmente. Las fotos se subirán cuando haya conexión.";
        await DisplayAlert("Éxito", mensajeCierre, "OK");
        Application.Current.MainPage = new NavigationPage(new PaginaMenu());
    }

    private async void OnTomarFotoClicked(object sender, EventArgs e)
    {
        if (MediaPicker.Default.IsCaptureSupported)
        {
            FileResult photo = await MediaPicker.Default.CapturePhotoAsync();
            if (photo != null)
            {
                _rutasFotosTemporales.Add(photo.FullPath);
                LblCantidadFotos.Text = $"{_rutasFotosTemporales.Count} foto(s) agregada(s)";
            }
        }
    }

    private void OnEliminarFotoClicked(object sender, EventArgs e)
    {
        if (sender is Button { CommandParameter: string ruta })
        {
            _rutasFotosTemporales.Remove(ruta);
            LblCantidadFotos.Text = $"{_rutasFotosTemporales.Count} foto(s) agregada(s)";
        }
    }
    private void OnSwitchRecargaToggled(object sender, ToggledEventArgs e)
    {
        SeccionRecarga.IsVisible = e.Value;
        if (!e.Value && CombustibleRecargaEntry != null)
        {
            CombustibleRecargaEntry.Text = "0";
        }
    }
}
