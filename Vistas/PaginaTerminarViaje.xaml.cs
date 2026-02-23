using LogiApp.Servicios;
using Microsoft.Maui.Devices.Sensors;

namespace LogiApp.Vistas;

/// <summary>
/// Pantalla para gestionar el monitoreo en ruta, marcar arribo a destino 
/// y realizar el cierre formal del viaje con captura de evidencias.
/// </summary>
public partial class PaginaTerminarViaje : ContentPage
{
    #region Atributos y Servicios

    private ConsultasGoogleSheets _sheets;
    private ConsultasGoogleDrive _drive; // Nuevo servicio
    private readonly ConexionGoogleAuth _authService = new();
    private string _folioActual;
    private string _rutaFotoTemporal = string.Empty; // Ruta para la evidencia

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
            // Inicializamos ambos servicios: Sheets y Drive
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
            string hora = DateTime.Now.ToString("dd/MM/yyyy HH:mm");
            bool exito = await _sheets.ActualizarEstatusYLlegadaAsync(_folioActual, "EN DESTINO", hora);

            if (exito)
            {
                Preferences.Default.Set("ArriboConfirmado_" + _folioActual, true);

                MainThread.BeginInvokeOnMainThread(() =>
                {
                    BtnReportar.IsEnabled = false;
                    BtnArribo.IsEnabled = false;
                    BtnArribo.Text = "LLEGADA REGISTRADA ✅";
                    SeccionCierre.IsEnabled = true;
                    SeccionCierre.Opacity = 1.0;
                });
                await DisplayAlert("Éxito", "Llegada marcada.", "OK");
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

        BtnFinalizar.IsEnabled = false;
        IndicadorCarga.IsVisible = true;
        IndicadorCarga.IsRunning = true;

        try
        {
            string linkCarpetaEvidencias = "Sin Evidencia";

            // 1. SUBIR FOTO A DRIVE (Si existe captura)
            if (!string.IsNullOrEmpty(_rutaFotoTemporal) && File.Exists(_rutaFotoTemporal))
            {
                string idOp = Preferences.Default.Get("UsuarioLogeado", "SinID");
                // Usamos FileStream seguro para evitar crashes en Windows/Android
                using (var stream = new FileStream(_rutaFotoTemporal, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    // Este método sube la foto y nos devuelve el link de la carpeta 'evidencias'
                    linkCarpetaEvidencias = await _drive.SubirEvidenciaViajeRapidoAsync(_rutaFotoTemporal, idOp, _folioActual);
                }
            }

            // 2. FINALIZAR EN GOOGLE SHEETS (Se envía el link a la columna O)
            bool exito = await _sheets.FinalizarViajeAsync(
                _folioActual,
                DateTime.Now.ToString("dd/MM/yyyy HH:mm"),
                KmLlegadaEntry.Text,
                CombustibleLlegadaEntry.Text,
                ObservacionesEditor.Text,
                linkCarpetaEvidencias
            );

            if (exito)
            {
                // Limpiamos persistencia local
                Preferences.Default.Remove("ViajeEnCurso");
                Preferences.Default.Remove("FolioPendiente");
                Preferences.Default.Remove("ArriboConfirmado_" + _folioActual);

                await DisplayAlert("Viaje Finalizado", "Registro y evidencias guardadas correctamente.", "OK");

                // Navegación forzada para limpiar el stack
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
                    // Guardamos la ruta del archivo para subirlo al finalizar
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