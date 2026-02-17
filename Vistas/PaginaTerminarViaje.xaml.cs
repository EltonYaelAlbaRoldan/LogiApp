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
    /// Captura las coordenadas actuales y actualiza el estatus del viaje en la nube.
    /// </summary>
    private async void OnReportarUbicacionClicked(object sender, EventArgs e)
    {
        try
        {
            BtnReportar.IsEnabled = false;

            // Solicitamos ubicación con precisión media para rapidez y ahorro de batería
            var ubicacion = await Geolocation.Default.GetLocationAsync(new GeolocationRequest(
                GeolocationAccuracy.Medium, TimeSpan.FromSeconds(10)));

            if (ubicacion != null)
            {
                string coordenadas = $"{ubicacion.Latitude:F4}, {ubicacion.Longitude:F4}";

                // Actualiza el estatus en Sheets incluyendo la hora del reporte
                bool exito = await _sheets.ActualizarUbicacionViajeAsync(_folioActual, coordenadas);

                if (exito)
                {
                    LblUltimaActualizacion.Text = $"Último reporte: {coordenadas} ({DateTime.Now:HH:mm})";
                    await DisplayAlert("GPS", "Ubicación reportada correctamente.", "OK");
                }
            }
        }
        catch (Exception)
        {
            await DisplayAlert("Error GPS", "Asegúrate de tener el GPS encendido y dar permisos a la App.", "OK");
        }
        finally
        {
            BtnReportar.IsEnabled = true;
        }
    }

    #endregion

    #region Gestión de Arribo y Cierre

    /// <summary>
    /// Registra la llegada al destino, detiene el monitoreo y habilita la sección de evidencias.
    /// </summary>
    private async void OnMarcarLlegadaClicked(object sender, EventArgs e)
    {
        var confirmar = await DisplayAlert("Confirmar", "¿Has llegado a tu destino?", "Sí", "No");
        if (!confirmar) return;

        IndicadorCarga.IsVisible = true;
        IndicadorCarga.IsRunning = true;

        try
        {
            string hora = DateTime.Now.ToString("dd/MM/yyyy HH:mm");

            // Actualiza el estatus a "EN DESTINO" y registra la hora de llegada
            bool exito = await _sheets.ActualizarEstatusYLlegadaAsync(_folioActual, "EN DESTINO", hora);

            if (exito)
            {
                // Actualización de la UI en el hilo principal
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    // Deshabilitar monitoreo GPS
                    BtnReportar.IsEnabled = false;
                    BtnReportar.Opacity = 0.5;
                    BtnReportar.Text = "UBICACIÓN FINALIZADA";

                    // Habilitar sección de evidencias y datos técnicos
                    BtnArribo.IsEnabled = false;
                    BtnArribo.Text = "LLEGADA REGISTRADA ✅";
                    SeccionCierre.IsEnabled = true;
                    SeccionCierre.Opacity = 1.0;
                });

                await DisplayAlert("Éxito", "Llegada marcada. El monitoreo GPS se ha detenido.", "OK");
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", "No se pudo marcar la llegada: " + ex.Message, "OK");
        }
        finally
        {
            IndicadorCarga.IsRunning = false;
            IndicadorCarga.IsVisible = false;
        }
    }

    /// <summary>
    /// Valida y envía los datos finales (KM, Combustible, Obs) a Sheets y regresa al menú.
    /// </summary>
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
                await DisplayAlert("Viaje Finalizado", "Registro guardado y estatus actualizado.", "OK");

                // Reinicia la navegación hacia el Menú Principal
                MainThread.BeginInvokeOnMainThread(() => {
                    Application.Current.MainPage = new NavigationPage(new PaginaMenu());
                });
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