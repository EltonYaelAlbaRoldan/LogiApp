using LogiApp.Servicios;
using Microsoft.Maui.Devices.Sensors;
using Microsoft.Maui.Networking;

namespace LogiApp.Vistas;

public partial class PaginaTerminarViaje : ContentPage
{
    #region Atributos y Servicios

    private ConsultasGoogleSheets _sheets;
    private ConsultasGoogleDrive _drive;
    private readonly ConexionGoogleAuth _authService = new();
    private string _folioActual;
    private string _rutaFotoTemporal = string.Empty;

    #endregion

    #region Constructor e Inicialización

    public PaginaTerminarViaje(string folio)
    {
        InitializeComponent();
        _folioActual = folio;
        LblFolio.Text = $"Folio: {folio}";
    }

    private async Task InicializarServicio()
    {
        if (_sheets != null) return; // Si ya se inicializó, no hace nada

        try
        {
            var (sService, dService) = await _authService.ObtenerServiciosAsync();
            _sheets = new ConsultasGoogleSheets(sService);
            _drive = new ConsultasGoogleDrive(dService);
        }
        catch { }
    }

    #endregion


    #region Ciclo de Vida y Sincronización en Vivo (El Sensor)

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await InicializarServicio();

        if (Preferences.Default.Get("ArriboConfirmado_" + _folioActual, false))
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                BtnReportar.IsEnabled = false;
                BtnArribo.IsEnabled = false;
                BtnArribo.Text = "LLEGADA REGISTRADA ✅";
                SeccionCierre.IsEnabled = true;
                SeccionCierre.Opacity = 1.0;
            });
        }

        Connectivity.Current.ConnectivityChanged += OnConnectivityChanged;

        // Ejecutamos el sensor general
        _ = IntentarSincronizarDatosEnVivoAsync();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        Connectivity.Current.ConnectivityChanged -= OnConnectivityChanged;
    }

    private async void OnConnectivityChanged(object sender, ConnectivityChangedEventArgs e)
    {
        if (e.NetworkAccess == NetworkAccess.Internet)
        {
            await IntentarSincronizarDatosEnVivoAsync();
        }
    }

    // RENOMBRADO Y AMPLIADO: Ahora sincroniza GPS y Arribos
    private async Task IntentarSincronizarDatosEnVivoAsync()
    {
        if (_sheets == null || Connectivity.Current.NetworkAccess != NetworkAccess.Internet) return;

        // 1. REVISAR SI HAY GPS PENDIENTE
        bool ubiPendiente = Preferences.Default.Get($"Offline_UbiPendiente_{_folioActual}", false);
        if (ubiPendiente)
        {
            double lat = Preferences.Default.Get($"Offline_UbiLat_{_folioActual}", 0.0);
            double lon = Preferences.Default.Get($"Offline_UbiLon_{_folioActual}", 0.0);
            string horaCapturada = Preferences.Default.Get($"Offline_UbiHora_{_folioActual}", "");

            try
            {
                bool exito = await _sheets.ActualizarUbicacionViajeAsync(_folioActual, lat, lon, horaCapturada);
                if (exito)
                {
                    Preferences.Default.Remove($"Offline_UbiPendiente_{_folioActual}");
                    Preferences.Default.Remove($"Offline_UbiLat_{_folioActual}");
                    Preferences.Default.Remove($"Offline_UbiLon_{_folioActual}");
                    Preferences.Default.Remove($"Offline_UbiHora_{_folioActual}");
                }
            }
            catch { }
        }

        // 2. REVISAR SI HAY ARRIBO PENDIENTE
        bool arriboPendiente = Preferences.Default.Get($"Offline_ArriboPendiente_{_folioActual}", false);
        if (arriboPendiente)
        {
            string horaLlegada = Preferences.Default.Get("HoraLlegadaReal_" + _folioActual, "");
            try
            {
                bool exito = await _sheets.ActualizarEstatusYLlegadaAsync(_folioActual, "EN DESTINO", horaLlegada);
                if (exito) Preferences.Default.Remove($"Offline_ArriboPendiente_{_folioActual}");
            }
            catch { }
        }
    }

    #endregion

    #region Lógica de Monitoreo (GPS)

    private async void OnReportarUbicacionClicked(object sender, EventArgs e)
    {
        try
        {
            BtnReportar.IsEnabled = false;

            // Obtenemos el GPS sin importar si hay internet (El chip GPS es independiente)
            var ubicacion = await Geolocation.Default.GetLocationAsync(new GeolocationRequest(GeolocationAccuracy.Medium, TimeSpan.FromSeconds(5)));

            if (ubicacion != null)
            {
                string horaExacta = DateTime.Now.ToString("HH:mm");
                string coordsBreves = $"{ubicacion.Latitude:F4}, {ubicacion.Longitude:F4}";

                // Guardamos en pantalla inmediatamente
                LblUltimaActualizacion.Text = $"Último reporte: {coordsBreves} ({horaExacta})";

                if (Connectivity.Current.NetworkAccess == NetworkAccess.Internet)
                {
                    // Si hay red, lo subimos de una vez
                    await _sheets.ActualizarUbicacionViajeAsync(_folioActual, ubicacion.Latitude, ubicacion.Longitude, horaExacta);
                    await DisplayAlert("GPS", "Ubicación enviada exitosamente.", "OK");
                }
                else
                {
                    // MODO OFFLINE: Guardamos en la bóveda
                    Preferences.Default.Set($"Offline_UbiPendiente_{_folioActual}", true);
                    Preferences.Default.Set($"Offline_UbiLat_{_folioActual}", ubicacion.Latitude);
                    Preferences.Default.Set($"Offline_UbiLon_{_folioActual}", ubicacion.Longitude);
                    Preferences.Default.Set($"Offline_UbiHora_{_folioActual}", horaExacta);

                    await DisplayAlert("Modo Offline", "Coordenadas guardadas. Se enviarán al recuperar la señal.", "OK");
                }
            }
        }
        catch
        {
            await DisplayAlert("Error GPS", "No se pudo obtener la ubicación. Verifica que tu GPS esté encendido.", "OK");
        }
        finally { BtnReportar.IsEnabled = true; }
    }

    #endregion

    #region Gestión de Arribo y Cierre

    private async void OnMarcarLlegadaClicked(object sender, EventArgs e)
    {
        var confirmar = await DisplayAlert("Confirmar", "¿Has llegado a tu destino?", "Sí", "No");
        if (!confirmar) return;

        IndicadorCarga.IsVisible = true;

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

        // EVALUACIÓN DE RED AL INSTANTE
        if (Connectivity.Current.NetworkAccess == NetworkAccess.Internet)
        {
            try
            {
                await _sheets.ActualizarEstatusYLlegadaAsync(_folioActual, "EN DESTINO", horaLlegadaReal);
            }
            catch
            {
                // Si la red es intermitente y falla, activamos la banderita
                Preferences.Default.Set($"Offline_ArriboPendiente_{_folioActual}", true);
            }
        }
        else
        {
            // Si no hay internet de entrada, activamos la banderita directamente
            Preferences.Default.Set($"Offline_ArriboPendiente_{_folioActual}", true);
        }

        MainThread.BeginInvokeOnMainThread(() =>
        {
            BtnReportar.IsEnabled = false;
            BtnArribo.IsEnabled = false;
            BtnArribo.Text = $"LLEGADA ✅ ({duracionStr})";
            SeccionCierre.IsEnabled = true;
            SeccionCierre.Opacity = 1.0;
        });

        IndicadorCarga.IsVisible = false;
    }

    private async void OnTerminarViajeClicked(object sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(KmLlegadaEntry.Text) || string.IsNullOrWhiteSpace(CombustibleLlegadaEntry.Text) || string.IsNullOrEmpty(_rutaFotoTemporal))
        {
            await DisplayAlert("Atención", "Captura kilometraje, combustible y foto obligatoria.", "OK");
            return;
        }

        double kmSalida = 0, fuelSalida = 0, kmLlegada = 0, fuelLlegada = 0;
        double.TryParse(Preferences.Default.Get("KmSalida_" + _folioActual, "0"), out kmSalida);
        double.TryParse(Preferences.Default.Get("FuelSalida_" + _folioActual, "0"), out fuelSalida);
        double.TryParse(KmLlegadaEntry.Text, out kmLlegada);
        double.TryParse(CombustibleLlegadaEntry.Text, out fuelLlegada);

        if (kmLlegada < kmSalida || fuelLlegada > fuelSalida)
        {
            await DisplayAlert("Error", "Los datos de KM o Combustible no son lógicos respecto a la salida.", "OK");
            return;
        }

        BtnFinalizar.IsEnabled = false;
        IndicadorCarga.IsVisible = true;

        string recorridoKmStr = (kmLlegada - kmSalida).ToString("0.##");
        string consumoLitrosStr = (fuelSalida - fuelLlegada).ToString("0.##");
        string duracionViaje = Preferences.Default.Get("Duracion_" + _folioActual, "N/A");
        string fechaFinReal = DateTime.Now.ToString("dd/MM/yyyy HH:mm");
        string horaLlegadaReal = Preferences.Default.Get("HoraLlegadaReal_" + _folioActual, fechaFinReal);

        string rutaSeguraFoto = Path.Combine(FileSystem.AppDataDirectory, $"evidencia_{_folioActual}.jpg");
        File.Copy(_rutaFotoTemporal, rutaSeguraFoto, true);

        Preferences.Default.Set($"Offline_Pendiente_{_folioActual}", true);
        Preferences.Default.Set($"Offline_HoraLlegada_{_folioActual}", horaLlegadaReal);
        Preferences.Default.Set($"Offline_FechaFin_{_folioActual}", fechaFinReal);
        Preferences.Default.Set($"Offline_KmLlegada_{_folioActual}", KmLlegadaEntry.Text);
        Preferences.Default.Set($"Offline_FuelLlegada_{_folioActual}", CombustibleLlegadaEntry.Text);
        Preferences.Default.Set($"Offline_Obs_{_folioActual}", ObservacionesEditor.Text ?? "N/A");
        Preferences.Default.Set($"Offline_RutaFoto_{_folioActual}", rutaSeguraFoto);
        Preferences.Default.Set($"Offline_Duracion_{_folioActual}", duracionViaje);
        Preferences.Default.Set($"Offline_Recorrido_{_folioActual}", recorridoKmStr);
        Preferences.Default.Set($"Offline_Consumo_{_folioActual}", consumoLitrosStr);

        string foliosAtorados = Preferences.Default.Get("ListaFoliosOffline", "");
        if (!foliosAtorados.Contains(_folioActual))
        {
            Preferences.Default.Set("ListaFoliosOffline", foliosAtorados + _folioActual + ",");
        }

        Preferences.Default.Remove("ViajeEnCurso");
        Preferences.Default.Remove("FolioPendiente");

        if (Connectivity.Current.NetworkAccess == NetworkAccess.Internet)
        {
            try
            {
                string idOp = Preferences.Default.Get("UsuarioLogeado", "SinID");
                string linkCarpeta = await _drive.SubirEvidenciaViajeRapidoAsync(rutaSeguraFoto, idOp, _folioActual);

                await _sheets.ActualizarEstatusYLlegadaAsync(_folioActual, "EN DESTINO", horaLlegadaReal);
                bool exito = await _sheets.FinalizarViajeAsync(_folioActual, fechaFinReal, KmLlegadaEntry.Text, CombustibleLlegadaEntry.Text, ObservacionesEditor.Text, linkCarpeta, duracionViaje, recorridoKmStr, consumoLitrosStr);

                if (exito)
                {
                    Preferences.Default.Remove($"Offline_Pendiente_{_folioActual}");

                    foliosAtorados = Preferences.Default.Get("ListaFoliosOffline", "");
                    foliosAtorados = foliosAtorados.Replace(_folioActual + ",", "");
                    Preferences.Default.Set("ListaFoliosOffline", foliosAtorados);
                }
            }
            catch { }
        }

        IndicadorCarga.IsVisible = false;
        await DisplayAlert("Viaje Terminado", "Datos capturados con éxito. Puedes continuar con tu siguiente viaje.", "OK");
        Application.Current.MainPage = new NavigationPage(new PaginaMenu());
    }

    #endregion

    #region Manejo de Evidencias

    private async void OnTomarFotoClicked(object sender, EventArgs e)
    {
        if (MediaPicker.Default.IsCaptureSupported)
        {
            FileResult photo = await MediaPicker.Default.CapturePhotoAsync();
            if (photo != null)
            {
                _rutaFotoTemporal = photo.FullPath;
                var stream = await photo.OpenReadAsync();
                ImgPreview.Source = ImageSource.FromStream(() => stream);
                ImgPreview.IsVisible = true;
            }
        }
    }

    #endregion
}