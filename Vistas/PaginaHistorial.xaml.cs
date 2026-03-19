using LogiApp.Servicios;
using LogiApp.Modelos;

namespace LogiApp.Vistas;


public partial class PaginaHistorial : ContentPage
{
    private ConsultasGoogleSheets _sheets;

    public PaginaHistorial()
    {
        InitializeComponent();
        CargarHistorial();
    }

    private async void CargarHistorial()
    {
        try
        {
            RefreshHistorial.IsRefreshing = true;

            var auth = new ConexionGoogleAuth();
            var (sService, _) = await auth.ObtenerServiciosAsync();
            _sheets = new ConsultasGoogleSheets(sService);

            string idOperador = Preferences.Default.Get("UsuarioLogeado", "");

            // Este método lo agregaremos a tu clase ConsultasGoogleSheets
            var viajes = await _sheets.ObtenerHistorialPorOperadorAsync(idOperador);

            ListaViajes.ItemsSource = viajes;
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", "No se pudo cargar el historial: " + ex.Message, "OK");
        }
        finally
        {
            RefreshHistorial.IsRefreshing = false;
        }
    }
    private async void OnVolverClicked(object sender, EventArgs e)
    {
        // Esto regresa a la PaginaMenu
        await Navigation.PopAsync();
    }
    private void OnRefreshRequested(object sender, EventArgs e) => CargarHistorial();
}