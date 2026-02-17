using LogiApp.Servicios;
using Microsoft.Maui.Devices.Sensors;

namespace LogiApp.Vistas;

public partial class PaginaTerminarViaje : ContentPage
{
    private ConsultasGoogleSheets _sheets;
    private readonly ConexionGoogleAuth _authService = new();
    private string _folioActual;

    public PaginaTerminarViaje(string folio)
    {
        InitializeComponent();
        _folioActual = folio;
        LblFolio.Text = $"Folio: {folio}";
        InicializarServicio();
    }

    private async void InicializarServicio()
    {
        var (sService, _) = await _authService.ObtenerServiciosAsync();
        _sheets = new ConsultasGoogleSheets(sService);
    }

    private async void OnReportarUbicacionClicked(object sender, EventArgs e)
    {
        try
        {
            BtnReportar.IsEnabled = false;
            var ubicacion = await Geolocation.Default.GetLocationAsync(new GeolocationRequest(GeolocationAccuracy.Medium, TimeSpan.FromSeconds(10)));
            if (ubicacion != null)
            {
                string coordenadas = $"{ubicacion.Latitude:F4}, {ubicacion.Longitude:F4}";
                bool exito = await _sheets.ActualizarUbicacionViajeAsync(_folioActual, coordenadas);
                if (exito)
                {
                    LblUltimaActualizacion.Text = $"Último reporte: {coordenadas} ({DateTime.Now:HH:mm})";
                    await DisplayAlert("GPS", "Ubicación reportada.", "OK");
                }
            }
        }
        catch { await DisplayAlert("Error GPS", "No se pudo obtener la ubicación.", "OK"); }
        finally { BtnReportar.IsEnabled = true; }
    }
    private async void OnMarcarLlegadaClicked(object sender, EventArgs e)
    {
        var confirmar = await DisplayAlert("Confirmar", "¿Has llegado a tu destino?", "Sí", "No");
        if (!confirmar) return;

        IndicadorCarga.IsVisible = true;
        IndicadorCarga.IsRunning = true;

        try
        {
            string hora = DateTime.Now.ToString("dd/MM/yyyy HH:mm");
            bool exito = await _sheets.ActualizarEstatusYLlegadaAsync(_folioActual, "EN DESTINO", hora);

            if (exito)
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    // DESHABILITAR botón de ubicación al llegar
                    BtnReportar.IsEnabled = false;
                    BtnReportar.Opacity = 0.5;
                    BtnReportar.Text = "UBICACIÓN FINALIZADA";

                    // Habilitar sección de cierre
                    BtnArribo.IsEnabled = false;
                    BtnArribo.Text = "LLEGADA REGISTRADA ✅";
                    SeccionCierre.IsEnabled = true;
                    SeccionCierre.Opacity = 1.0;
                });
                await DisplayAlert("Éxito", "Llegada marcada. El monitoreo GPS se ha detenido.", "OK");
            }
        }
        catch (Exception ex) { await DisplayAlert("Error", ex.Message, "OK"); }
        finally { IndicadorCarga.IsRunning = false; }
    }

    private async void OnTerminarViajeClicked(object sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(KmLlegadaEntry.Text) || string.IsNullOrWhiteSpace(CombustibleLlegadaEntry.Text))
        {
            await DisplayAlert("Atención", "Captura kilometraje y combustible final.", "OK");
            return;
        }

        IndicadorCarga.IsVisible = true;
        IndicadorCarga.IsRunning = true;

        try
        {
            // Llamamos al servicio con los datos capturados
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

                // Navegación inmediata al menú principal [cite: 2026-02-17]
                MainThread.BeginInvokeOnMainThread(() => {
                    Application.Current.MainPage = new NavigationPage(new PaginaMenu());
                });
            }
            else
            {
                // Intentar una segunda validación: si en Sheets ya dice FINALIZADO, ignorar el error
                await DisplayAlert("Servidor Ocupado", "Los datos se enviaron, pero la confirmación tardó demasiado. Verifica tu menú principal.", "OK");
                Application.Current.MainPage = new NavigationPage(new PaginaMenu());
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error de Red", "Verifica tu conexión a internet.", "OK");
        }
        finally { IndicadorCarga.IsRunning = false; }
    }


    private async void OnTomarFotoClicked(object sender, EventArgs e)
    {
        if (MediaPicker.Default.IsCaptureSupported)
        {
            FileResult photo = await MediaPicker.Default.CapturePhotoAsync();
            if (photo != null)
            {
                var stream = await photo.OpenReadAsync();
                ImgPreview.Source = ImageSource.FromStream(() => stream);
                ImgPreview.IsVisible = true;
                await DisplayAlert("Evidencia", "Foto capturada.", "OK");
            }
        }
    }

}