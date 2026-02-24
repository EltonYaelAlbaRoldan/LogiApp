using LogiApp.Servicios;

namespace LogiApp.Vistas;

public partial class PaginaViajes : ContentPage
{
    private ConsultasGoogleSheets _sheets;
    private readonly ConexionGoogleAuth _authService = new();

    public PaginaViajes()
    {
        InitializeComponent();
        InicializarDatos();
    }

    private async void InicializarDatos()
    {
        try
        {
            var (sService, _) = await _authService.ObtenerServiciosAsync();
            _sheets = new ConsultasGoogleSheets(sService);
            await Task.WhenAll(CargarUnidades(), CargarDestinos());
        }
        catch (Exception ex) { await DisplayAlert("Error", ex.Message, "OK"); }
    }

    private async Task CargarUnidades()
    {
        CargandoUnidades.IsVisible = true;
        CargandoUnidades.IsRunning = true;
        try
        {
            var lista = await _sheets.ObtenerListaUnidadesAsync();
            UnidadPicker.ItemsSource = lista;
        }
        finally
        {
            CargandoUnidades.IsVisible = false;
            CargandoUnidades.IsRunning = false;
        }
    }

    private async Task CargarDestinos()
    {
        try
        {
            var destinos = await _sheets.ObtenerListaDestinosAsync();
            OrigenPicker.ItemsSource = destinos;
            DestinoPicker.ItemsSource = destinos;
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", "No se pudieron cargar los destinos: " + ex.Message, "OK");
        }
    }

    private async void OnGuardarViajeClicked(object sender, EventArgs e)
    {
        if (OrigenPicker.SelectedIndex == -1 || DestinoPicker.SelectedIndex == -1 ||
            UnidadPicker.SelectedIndex == -1 || string.IsNullOrWhiteSpace(KmSalidaEntry.Text) ||
            string.IsNullOrWhiteSpace(CombustibleEntry.Text))
        {
            await DisplayAlert("Datos Incompletos", "Por favor, completa todos los campos para continuar.", "Aceptar");
            return;
        }

        BtnGuardarViaje.IsEnabled = false;
        IsBusy = true;

        try
        {
            string folio = await _sheets.GenerarFolioViajeAsync();
            string idOp = Preferences.Default.Get("UsuarioLogeado", "Sin ID");
            string fechaInicio = DateTime.Now.ToString("dd/MM/yyyy HH:mm");

            bool exito = await _sheets.RegistrarInicioViajeAsync(
                folio,
                fechaInicio,
                idOp,
                UnidadPicker.SelectedItem.ToString(),
                OrigenPicker.SelectedItem.ToString(),
                DestinoPicker.SelectedItem.ToString(),
                ClienteEntry.Text,
                KmSalidaEntry.Text,
                CombustibleEntry.Text,
                "EN TRANSITO"
            );

            if (exito)
            {
                Preferences.Default.Set("ViajeEnCurso", true);
                Preferences.Default.Set("FolioPendiente", folio);
                Preferences.Default.Set("Inicio_" + folio, fechaInicio);

                // Guardamos los datos de salida para el cálculo matemático final
                Preferences.Default.Set("KmSalida_" + folio, KmSalidaEntry.Text);
                Preferences.Default.Set("FuelSalida_" + folio, CombustibleEntry.Text);

                await DisplayAlert("Éxito", $"Viaje {folio} registrado. ¡Buen camino!", "OK");
                Application.Current.MainPage = new NavigationPage(new Vistas.PaginaTerminarViaje(folio));
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", "Error al guardar en Sheets: " + ex.Message, "OK");
        }
        finally
        {
            BtnGuardarViaje.IsEnabled = true;
            IsBusy = false;
        }
    }

    private async void OnVolverClicked(object sender, EventArgs e)
    {
        Application.Current.MainPage = new NavigationPage(new Vistas.PaginaMenu());
    }
}