using LogiApp.Servicios;
using Microsoft.Maui.Devices.Sensors;

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

    #region Constructor

    public PaginaTerminarViaje(string folio)
    {
        InitializeComponent();
        _folioActual = folio;
        LblFolio.Text = $"Folio: {folio}";
        InicializarServicio();
    }

    #endregion

    #region Inicialización

    private async void InicializarServicio()
    {
        try
        {
            var (sService, dService) = await _authService.ObtenerServiciosAsync();
            _sheets = new ConsultasGoogleSheets(sService);
            _drive = new ConsultasGoogleDrive(dService);
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error de Conexión", "No se pudieron cargar los servicios: " + ex.Message, "OK");
        }
    }

    #endregion

    #region Lógica de Monitoreo (GPS)

    private async void OnReportarUbicacionClicked(object sender, EventArgs e)
    {
        try
        {
            BtnReportar.IsEnabled = false;

            var ubicacion = await Geolocation.Default.GetLocationAsync(new GeolocationRequest(
                GeolocationAccuracy.Medium, TimeSpan.FromSeconds(10)));

            if (ubicacion != null)
            {
                bool exito = await _sheets.ActualizarUbicacionViajeAsync(_folioActual, ubicacion.Latitude, ubicacion.Longitude);

                if (exito)
                {
                    string coordsBreves = $"{ubicacion.Latitude:F4}, {ubicacion.Longitude:F4}";
                    LblUltimaActualizacion.Text = $"Último reporte: {coordsBreves} ({DateTime.Now:HH:mm})";
                    await DisplayAlert("GPS", "Ubicación enviada.", "OK");
                }
            }
        }
        catch (Exception)
        {
            await DisplayAlert("Error GPS", "Verifica los permisos de ubicación e internet.", "OK");
        }
        finally
        {
            BtnReportar.IsEnabled = true;
        }
    }

    #endregion

    #region Gestión de Arribo y Cierre

    protected override void OnAppearing()
    {
        base.OnAppearing();

        bool yaLlego = Preferences.Default.Get("ArriboConfirmado_" + _folioActual, false);

        if (yaLlego)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                BtnReportar.IsEnabled = false;
                BtnReportar.Opacity = 0.5;
                BtnReportar.Text = "UBICACIÓN FINALIZADA";

                BtnArribo.IsEnabled = false;
                BtnArribo.Text = "LLEGADA REGISTRADA ✅";

                SeccionCierre.IsEnabled = true;
                SeccionCierre.Opacity = 1.0;
            });
        }
    }

    private async void OnMarcarLlegadaClicked(object sender, EventArgs e)
    {
        var confirmar = await DisplayAlert("Confirmar", "¿Has llegado a tu destino?", "Sí", "No");
        if (!confirmar) return;

        IndicadorCarga.IsVisible = true;
        try
        {
            string horaLlegada = DateTime.Now.ToString("dd/MM/yyyy HH:mm");

            // 1. Cálculo de duración
            string horaInicio = Preferences.Default.Get("Inicio_" + _folioActual, horaLlegada);
            string duracionStr = "N/A";

            if (DateTime.TryParseExact(horaInicio, "dd/MM/yyyy HH:mm", null, System.Globalization.DateTimeStyles.None, out DateTime dtInicio) &&
                DateTime.TryParseExact(horaLlegada, "dd/MM/yyyy HH:mm", null, System.Globalization.DateTimeStyles.None, out DateTime dtFin))
            {
                TimeSpan tiempo = dtFin - dtInicio;
                duracionStr = $"{(int)tiempo.TotalHours}h {tiempo.Minutes}m";
            }
            Preferences.Default.Set("Duracion_" + _folioActual, duracionStr);

            // 2. Captura de GPS final en segundo plano (Silenciosa)
            try
            {
                var ubicacion = await Geolocation.Default.GetLocationAsync(new GeolocationRequest(GeolocationAccuracy.Medium, TimeSpan.FromSeconds(5)));
                if (ubicacion != null)
                {
                    await _sheets.ActualizarUbicacionViajeAsync(_folioActual, ubicacion.Latitude, ubicacion.Longitude);
                }
            }
            catch { /* Se ignora si falla para no trabar el arribo */ }

            // 3. Registrar llegada en Sheets
            bool exito = await _sheets.ActualizarEstatusYLlegadaAsync(_folioActual, "EN DESTINO", horaLlegada);

            if (exito)
            {
                Preferences.Default.Set("ArriboConfirmado_" + _folioActual, true);

                MainThread.BeginInvokeOnMainThread(() =>
                {
                    BtnReportar.IsEnabled = false;
                    BtnArribo.IsEnabled = false;
                    BtnArribo.Text = $"LLEGADA ✅ ({duracionStr})"; // Feedback visual discreto
                    SeccionCierre.IsEnabled = true;
                    SeccionCierre.Opacity = 1.0;
                });
                await DisplayAlert("Éxito", $"Llegada marcada. Duración del viaje: {duracionStr}", "OK");
            }
        }
        catch (Exception ex) { await DisplayAlert("Error", ex.Message, "OK"); }
        finally { IndicadorCarga.IsVisible = false; }
    }
    private async void OnTerminarViajeClicked(object sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(KmLlegadaEntry.Text) || string.IsNullOrWhiteSpace(CombustibleLlegadaEntry.Text))
        {
            await DisplayAlert("Atención", "Captura kilometraje y combustible final.", "OK");
            return;
        }

        if (string.IsNullOrEmpty(_rutaFotoTemporal) || !File.Exists(_rutaFotoTemporal))
        {
            await DisplayAlert("Evidencia Requerida", "Es obligatorio capturar una fotografía como evidencia para finalizar el viaje.", "OK");
            return;
        }

        // --- CÁLCULOS MATEMÁTICOS ---
        double kmSalida = 0, fuelSalida = 0, kmLlegada = 0, fuelLlegada = 0;

        double.TryParse(Preferences.Default.Get("KmSalida_" + _folioActual, "0"), out kmSalida);
        double.TryParse(Preferences.Default.Get("FuelSalida_" + _folioActual, "0"), out fuelSalida);
        double.TryParse(KmLlegadaEntry.Text, out kmLlegada);
        double.TryParse(CombustibleLlegadaEntry.Text, out fuelLlegada);

        // Validación 1: Kilometraje
        if (kmLlegada < kmSalida)
        {
            await DisplayAlert("Error de Captura", $"El KM final no puede ser menor al inicial ({kmSalida}).", "OK");
            return;
        }

        // Validación 2: Combustible (Evita consumo negativo)
        if (fuelLlegada > fuelSalida)
        {
            await DisplayAlert("Error de Captura", $"El combustible final ({fuelLlegada} L) no puede ser mayor al inicial ({fuelSalida} L). Si recargaste en ruta, anótalo en Observaciones.", "OK");
            return;
        }

        double totalKm = kmLlegada - kmSalida;
        double totalLitros = fuelSalida - fuelLlegada;

        string recorridoKmStr = totalKm.ToString("0.##");
        string consumoLitrosStr = totalLitros.ToString("0.##");

        BtnFinalizar.IsEnabled = false;
        IndicadorCarga.IsVisible = true;
        IndicadorCarga.IsRunning = true;

        try
        {
            string duracionViaje = Preferences.Default.Get("Duracion_" + _folioActual, "N/A");
            string idOp = Preferences.Default.Get("UsuarioLogeado", "SinID");
            string linkCarpetaEvidencias = string.Empty;

            using (var stream = new FileStream(_rutaFotoTemporal, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                linkCarpetaEvidencias = await _drive.SubirEvidenciaViajeRapidoAsync(_rutaFotoTemporal, idOp, _folioActual);
            }

            bool exito = await _sheets.FinalizarViajeAsync(
                _folioActual,
                DateTime.Now.ToString("dd/MM/yyyy HH:mm"),
                KmLlegadaEntry.Text,
                CombustibleLlegadaEntry.Text,
                ObservacionesEditor.Text,
                linkCarpetaEvidencias,
                duracionViaje,
                recorridoKmStr,
                consumoLitrosStr
            );

            if (exito)
            {
                Preferences.Default.Remove("ViajeEnCurso");
                Preferences.Default.Remove("FolioPendiente");
                Preferences.Default.Remove("ArriboConfirmado_" + _folioActual);
                Preferences.Default.Remove("Inicio_" + _folioActual);
                Preferences.Default.Remove("Duracion_" + _folioActual);
                Preferences.Default.Remove("KmSalida_" + _folioActual);
                Preferences.Default.Remove("FuelSalida_" + _folioActual);

                await DisplayAlert("Viaje Finalizado", $"Cierre exitoso.\nRecorriste: {recorridoKmStr} KM\nConsumiste: {consumoLitrosStr} L", "OK");

                MainThread.BeginInvokeOnMainThread(() =>
                {
                    Application.Current.MainPage = new NavigationPage(new PaginaMenu());
                });
            }
            else
            {
                await DisplayAlert("Error", "No se pudo actualizar la base de datos.", "OK");
                BtnFinalizar.IsEnabled = true;
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error Crítico", "Ocurrió un problema: " + ex.Message, "OK");
            BtnFinalizar.IsEnabled = true;
        }
        finally
        {
            IndicadorCarga.IsRunning = false;
            IndicadorCarga.IsVisible = false;
        }
    }

    #endregion

    #region Manejo de Evidencias

    private async void OnTomarFotoClicked(object sender, EventArgs e)
    {
        try
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

                    await DisplayAlert("Evidencia", "Foto capturada correctamente.", "OK");
                }
            }
            else
            {
                await DisplayAlert("Error", "La cámara no está disponible.", "OK");
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error de Cámara", "No se pudo capturar la foto: " + ex.Message, "OK");
        }
    }

    #endregion
}