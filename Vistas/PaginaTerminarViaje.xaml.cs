using LogiApp.Servicios;
using LogiApp.Modelos;
using Microsoft.Maui.Devices.Sensors;
using Microsoft.Maui.Networking;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System;
using System.IO;

namespace LogiApp.Vistas;

public partial class PaginaTerminarViaje : ContentPage
{
    #region Atributos y Servicios
    private ConsultasGoogleSheets _sheets;
    private ConsultasGoogleDrive _drive;
    private readonly ConexionGoogleAuth _authService = new();
    private string _folioActual;
    private string _rutaFotoTemporal = string.Empty;
    private CancellationTokenSource _ctsTracking;
    private bool _isTrackingRunning = false;
    private readonly BaseDeDatosLocal _dbLocal = new();
    #endregion

    public PaginaTerminarViaje(string folio)
    {
        InitializeComponent();
        _folioActual = folio;
        LblFolio.Text = $"Folio: {folio}";
        string destinoGuardado = Preferences.Default.Get("Destino_" + folio, "Destino no especificado");
        LblDestino.Text = $"Destino: {destinoGuardado}";
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await InicializarServicio();
        if (!_isTrackingRunning) _ = IniciarTimerTracking();

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

    private async Task IntentarSincronizarDatosEnVivoAsync()
    {
        if (_sheets == null || Connectivity.Current.NetworkAccess != NetworkAccess.Internet) return;

        try
        {
            var pendientes = await _dbLocal.ObtenerUbicacionesPendientesAsync();
            if (pendientes != null && pendientes.Count > 0)
            {
                string hoja = $"Historial_{DateTime.Now:MMMM}_{DateTime.Now.Year}";
                string colA = Preferences.Default.Get("ColTracking_A", "A");
                string colB = Preferences.Default.Get("ColTracking_B", "B");

                foreach (var ubi in pendientes)
                {
                    bool ok = await _sheets.SubirPuntoGPSTiempoRealAsync(hoja, colA, colB, ubi.Id, ubi);
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
            string colA = Preferences.Default.Get("ColTracking_A", "A");
            string colB = Preferences.Default.Get("ColTracking_B", "B");
            int filaActual = Preferences.Default.Get("FilaActual_Tracking", 2);

            if (Connectivity.Current.NetworkAccess == NetworkAccess.Internet)
            {
                try
                {
                    string hoja = $"Historial_{DateTime.Now:MMMM}_{DateTime.Now.Year}";
                    subioExitosamente = await _sheets.SubirPuntoGPSTiempoRealAsync(hoja, colA, colB, filaActual, punto);
                }
                catch { subioExitosamente = false; }
            }

            if (subioExitosamente)
            {
                BtnReportar.BackgroundColor = Colors.Green;
                BtnReportar.Text = "UBICACIÓN ENVIADA ✅";
            }
            else
            {
                punto.Id = filaActual;
                await _dbLocal.GuardarUbicacionLocalAsync(punto);
                BtnReportar.BackgroundColor = Colors.Orange;
                BtnReportar.Text = "GUARDADO LOCAL ⚠️";
            }

            Preferences.Default.Set("FilaActual_Tracking", filaActual + 1);
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

                string colA = Preferences.Default.Get("ColTracking_A", "A");
                string colB = Preferences.Default.Get("ColTracking_B", "B");
                int fila = Preferences.Default.Get("FilaActual_Tracking", 2);
                bool subio = false;

                if (Connectivity.Current.NetworkAccess == NetworkAccess.Internet)
                {
                    try { subio = await _sheets.SubirPuntoGPSTiempoRealAsync($"Historial_{DateTime.Now:MMMM}_{DateTime.Now.Year}", colA, colB, fila, punto); }
                    catch { subio = false; }
                }

                if (subio) Preferences.Default.Set("FilaActual_Tracking", fila + 1);
                else { punto.Id = fila; await _dbLocal.GuardarUbicacionLocalAsync(punto); Preferences.Default.Set("FilaActual_Tracking", fila + 1); }
            }
        }
        catch { }
    }

    private async void OnMarcarLlegadaClicked(object sender, EventArgs e)
    {
        var confirmar = await DisplayAlert("Confirmar", "¿Has llegado a tu destino?", "Sí", "No");
        if (!confirmar) return;

        IndicadorCarga.IsVisible = true;

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

    private async void OnTerminarViajeClicked(object sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(KmLlegadaEntry.Text) || string.IsNullOrWhiteSpace(CombustibleLlegadaEntry.Text) || string.IsNullOrEmpty(_rutaFotoTemporal))
        {
            await DisplayAlert("Atención", "Captura kilometraje, combustible y foto obligatoria.", "OK");
            return;
        }

        // RESTAURACIÓN: Lógica de validación de KM y Combustible
        double kmSalida = 0, fuelSalida = 0, kmLlegada = 0, fuelLlegada = 0;
        double.TryParse(Preferences.Default.Get("KmSalida_" + _folioActual, "0"), out kmSalida);
        double.TryParse(Preferences.Default.Get("FuelSalida_" + _folioActual, "0"), out fuelSalida);
        double.TryParse(KmLlegadaEntry.Text, out kmLlegada);
        double.TryParse(CombustibleLlegadaEntry.Text, out fuelLlegada);

        if (kmLlegada < kmSalida || fuelLlegada > fuelSalida)
        {
            await DisplayAlert("Error", "Los datos de KM o Combustible no son lógicos.", "OK");
            return;
        }

        BtnFinalizar.IsEnabled = false;
        IndicadorCarga.IsVisible = true;

        // RESTAURACIÓN: Cálculos finales
        string recorridoKmStr = (kmLlegada - kmSalida).ToString("0.##");
        string consumoLitrosStr = (fuelSalida - fuelLlegada).ToString("0.##");
        string duracionViaje = Preferences.Default.Get("Duracion_" + _folioActual, "N/A");
        string fechaFinReal = DateTime.Now.ToString("dd/MM/yyyy HH:mm");
        string horaLlegadaReal = Preferences.Default.Get("HoraLlegadaReal_" + _folioActual, fechaFinReal);

        string rutaSeguraFoto = Path.Combine(FileSystem.AppDataDirectory, $"evidencia_{_folioActual}.jpg");
        File.Copy(_rutaFotoTemporal, rutaSeguraFoto, true);

        // Registro offline preventivo (ahora con los datos calculados)
        Preferences.Default.Set($"Offline_Pendiente_{_folioActual}", true);
        Preferences.Default.Set($"Offline_KmLlegada_{_folioActual}", KmLlegadaEntry.Text);
        Preferences.Default.Set($"Offline_FuelLlegada_{_folioActual}", CombustibleLlegadaEntry.Text);
        Preferences.Default.Set($"Offline_RutaFoto_{_folioActual}", rutaSeguraFoto);
        Preferences.Default.Set($"Offline_Duracion_{_folioActual}", duracionViaje);
        Preferences.Default.Set($"Offline_Recorrido_{_folioActual}", recorridoKmStr);
        Preferences.Default.Set($"Offline_Consumo_{_folioActual}", consumoLitrosStr);

        if (Connectivity.Current.NetworkAccess == NetworkAccess.Internet)
        {
            try
            {
                string idOp = Preferences.Default.Get("UsuarioLogeado", "SinID");
                string link = await _drive.SubirEvidenciaViajeRapidoAsync(rutaSeguraFoto, idOp, _folioActual);

                await _sheets.ActualizarEstatusYLlegadaAsync(_folioActual, "EN DESTINO", horaLlegadaReal);

                // RESTAURACIÓN: Pasar los datos calculados a la función final en lugar de "N/A"
                bool ok = await _sheets.FinalizarViajeAsync(_folioActual, fechaFinReal, KmLlegadaEntry.Text, CombustibleLlegadaEntry.Text, ObservacionesEditor.Text, link, duracionViaje, recorridoKmStr, consumoLitrosStr);

                if (ok) Preferences.Default.Remove($"Offline_Pendiente_{_folioActual}");
            }
            catch { }
        }

        Preferences.Default.Set("ViajeEnCurso", false);
        Preferences.Default.Remove("FolioPendiente");
        _isTrackingRunning = false;
        _ctsTracking?.Cancel();

        await DisplayAlert("Éxito", "Viaje cerrado y guardado.", "OK");
        Application.Current.MainPage = new NavigationPage(new PaginaMenu());
    }

    private async void OnTomarFotoClicked(object sender, EventArgs e)
    {
        if (MediaPicker.Default.IsCaptureSupported)
        {
            FileResult photo = await MediaPicker.Default.CapturePhotoAsync();
            if (photo != null)
            {
                _rutaFotoTemporal = photo.FullPath;
                ImgPreview.Source = ImageSource.FromStream(async (c) => await photo.OpenReadAsync());
                ImgPreview.IsVisible = true;
            }
        }
    }
}