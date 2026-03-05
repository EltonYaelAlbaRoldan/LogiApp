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

            // Cargamos las 3 listas simultáneamente
            await Task.WhenAll(CargarUnidades(), CargarRemolques(), CargarDestinos(), CargarClientes());
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

    private void OnRemolqueCheckedChanged(object sender, CheckedChangedEventArgs e)
    {
        // Muestra u oculta la sección dependiendo de si el CheckBox está marcado
        SeccionRemolque.IsVisible = e.Value;

        // Si se desmarca, limpiamos la selección por seguridad
        if (!e.Value)
        {
            RemolquePicker.SelectedItem = null;
        }
    }
    private async Task CargarRemolques()
    {
        CargandoRemolques.IsVisible = true;
        CargandoRemolques.IsRunning = true;

        try
        {
            var lista = await _sheets.ObtenerListaRemolquesAsync();
            RemolquePicker.ItemsSource = lista;
        }
        finally
        {
            CargandoRemolques.IsVisible = false;
            CargandoRemolques.IsRunning = false;
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

    // Nuevo método para poblar el Picker de Clientes
    private async Task CargarClientes()
    {
        try
        {
            var clientes = await _sheets.ObtenerListaClientesAsync();
            ClientePicker.ItemsSource = clientes;
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", "No se pudieron cargar los clientes: " + ex.Message, "OK");
        }
    }

    private async void OnGuardarViajeClicked(object sender, EventArgs e)
    {

        // Agregamos la validación para que el ClientePicker no esté vacío (-1)
        if (OrigenPicker.SelectedIndex == -1 || DestinoPicker.SelectedIndex == -1 ||
            UnidadPicker.SelectedIndex == -1 || ClientePicker.SelectedIndex == -1 ||
            string.IsNullOrWhiteSpace(KmSalidaEntry.Text) || string.IsNullOrWhiteSpace(CombustibleEntry.Text))
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
            string remolqueSeleccionado = CheckLlevaRemolque.IsChecked
                ? (RemolquePicker.SelectedItem?.ToString() ?? "N/A")
                : "SIN REMOLQUE";

            bool exito = await _sheets.RegistrarInicioViajeAsync(
                folio,
                fechaInicio,
                idOp,
                UnidadPicker.SelectedItem.ToString(),
                remolqueSeleccionado,
                OrigenPicker.SelectedItem.ToString(),
                DestinoPicker.SelectedItem.ToString(),
                ClientePicker.SelectedItem.ToString(), 
                KmSalidaEntry.Text,
                CombustibleEntry.Text,
                "EN TRANSITO"
            );

            Preferences.Default.Set("Destino_" + folio, DestinoPicker.SelectedItem.ToString());

            if (exito)
            {
                Preferences.Default.Set("ViajeEnCurso", true);
                Preferences.Default.Set("FolioPendiente", folio);
                Preferences.Default.Set("Inicio_" + folio, fechaInicio);
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

    private void OnVolverClicked(object sender, EventArgs e)
    {
        Application.Current.MainPage = new NavigationPage(new Vistas.PaginaMenu());
    }
}