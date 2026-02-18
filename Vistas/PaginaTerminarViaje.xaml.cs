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
    private readonly ConexionGoogleAuth _authService = new();
    private string _folioActual;

    #endregion

    #region Constructor

    /// <summary>
    /// Inicializa la página recibiendo el folio del viaje activo.
    /// </summary>
    /// <param name="folio">Folio único del viaje generado en Sheets.</param>
    public PaginaTerminarViaje(string folio)
    {
        InitializeComponent();
        _folioActual = folio;
        LblFolio.Text = $"Folio: {folio}";
        InicializarServicio();
    }

    #endregion

    #region Inicialización

    /// <summary>
    /// Obtiene y configura los servicios de Google para la sesión actual.
    /// </summary>
    private async void InicializarServicio()
    {
        try
        {
            var (sService, _) = await _authService.ObtenerServiciosAsync();
            _sheets = new ConsultasGoogleSheets(sService);
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error de Conexión", "No se pudieron cargar los servicios: " + ex.Message, "OK");
        }
    }

    #endregion

    #region Lógica de Monitoreo (GPS)

    /// <summary>
    /// Captura las coordenadas actuales y solicita al servicio que las convierta a nombre de lugar.
    /// </summary>
    private async void OnReportarUbicacionClicked(object sender, EventArgs e)
    {
        try
        {
            BtnReportar.IsEnabled = false;

            // Solicitamos ubicación con precisión media
            var ubicacion = await Geolocation.Default.GetLocationAsync(new GeolocationRequest(
                GeolocationAccuracy.Medium, TimeSpan.FromSeconds(10)));

            if (ubicacion != null)
            {
                // Ahora enviamos la latitud y longitud por separado al servicio
                // El servicio se encargará de convertir esto a un nombre (Ej: "Pachuca, Centro")
                bool exito = await _sheets.ActualizarUbicacionViajeAsync(_folioActual, ubicacion.Latitude, ubicacion.Longitude);

                if (exito)
                {
                    // En la etiqueta local seguimos mostrando coordenadas por rapidez
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

    // --- DENTRO DE PaginaTerminarViaje.xaml.cs ---

    protected override void OnAppearing()
    {
        base.OnAppearing();

        // REVISAR SI YA SE HABÍA MARCADO LA LLEGADA ANTES [cite: 2026-02-17]
        bool yaLlego = Preferences.Default.Get("ArriboConfirmado_" + _folioActual, false);

        if (yaLlego)
        {
            // Restauramos la interfaz sin disparar el evento de la base de datos
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
                // --- PERSISTENCIA DEL ARRIBO ---
                // Guardamos que este folio específico ya marcó llegada [cite: 2026-02-17]
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
        // Validación de campos obligatorios
        if (string.IsNullOrWhiteSpace(KmLlegadaEntry.Text) || string.IsNullOrWhiteSpace(CombustibleLlegadaEntry.Text))
        {
            await DisplayAlert("Atención", "Captura kilometraje y combustible final.", "OK");
            return;
        }

        IndicadorCarga.IsVisible = true;
        IndicadorCarga.IsRunning = true;

        try
        {
            // Ejecuta el cierre formal en la base de datos
            bool exito = await _sheets.FinalizarViajeAsync(
                _folioActual,
                DateTime.Now.ToString("dd/MM/yyyy HH:mm"),
                KmLlegadaEntry.Text,
                CombustibleLlegadaEntry.Text,
                ObservacionesEditor.Text
            );

            if (exito)
            {
                // Limpiamos todo el rastro del viaje actual [cite: 2026-02-17]
                Preferences.Default.Remove("ViajeEnCurso");
                Preferences.Default.Remove("FolioPendiente");
                Preferences.Default.Remove("ArriboConfirmado_" + _folioActual);

                await DisplayAlert("Viaje Finalizado", "Registro completado.", "OK");
                Application.Current.MainPage = new NavigationPage(new PaginaMenu());
            }
            else
            {
                // Plan de contingencia si la API no confirma pero los datos se enviaron
                await DisplayAlert("Servidor Ocupado", "Los datos se enviaron, pero la confirmación tardó demasiado. Verifica tu menú principal.", "OK");
                Application.Current.MainPage = new NavigationPage(new PaginaMenu());
            }
        }
        catch (Exception)
        {
            await DisplayAlert("Error de Red", "Verifica tu conexión a internet para finalizar el viaje.", "OK");
        }
        finally
        {
            IndicadorCarga.IsRunning = false;
            IndicadorCarga.IsVisible = false;
        }
    }

    #endregion

    #region Manejo de Evidencias

    /// <summary>
    /// Abre la cámara del dispositivo para capturar evidencia del viaje.
    /// </summary>
    private async void OnTomarFotoClicked(object sender, EventArgs e)
    {
        try
        {
            if (MediaPicker.Default.IsCaptureSupported)
            {
                FileResult photo = await MediaPicker.Default.CapturePhotoAsync();

                if (photo != null)
                {
                    // Muestra la vista previa de la foto capturada
                    var stream = await photo.OpenReadAsync();
                    ImgPreview.Source = ImageSource.FromStream(() => stream);
                    ImgPreview.IsVisible = true;

                    await DisplayAlert("Evidencia", "Foto capturada correctamente.", "OK");
                }
            }
            else
            {
                await DisplayAlert("Error", "La cámara no está disponible en este dispositivo.", "OK");
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error de Cámara", "No se pudo capturar la foto: " + ex.Message, "OK");
        }
    }

    #endregion
}