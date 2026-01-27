using LogiApp.Servicios;

namespace LogiApp.Vistas;

public partial class InicioSesion : ContentPage
{

    private ConsultasGoogleSheets _sheets;
    private readonly ConexionGoogleAuth _authService = new();

    public InicioSesion()
    {
        InitializeComponent();
        InicializarServicio();

    }

    private async void InicializarServicio()
    {
        // Obtenemos los servicios de Google
        var (sService, _) = await _authService.ObtenerServiciosAsync();
        _sheets = new ConsultasGoogleSheets(sService);
    }

    private async void OnLoginClicked(object sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(UsuarioEntry.Text) || string.IsNullOrWhiteSpace(ContrasenaEntry.Text))
        {
            await DisplayAlert("Campos Vacíos", "Ingresa tus datos.", "OK");
            return;
        }

        CargandoIndicator.IsVisible = true;
        CargandoIndicator.IsRunning = true;

        // VALIDACIÓN REAL DESDE GOOGLE SHEETS
        bool esValido = await _sheets.ValidarUsuarioAsync(UsuarioEntry.Text, ContrasenaEntry.Text);

        if (esValido)
        {
            Application.Current.MainPage = new NavigationPage(new PaginaMenu());
        }
        else
        {
            await DisplayAlert("Error", "Usuario o contraseña no encontrados en la base de datos.", "Reintentar");
        }

        CargandoIndicator.IsRunning = false;
        CargandoIndicator.IsVisible = false;
    }

    private async void OnVolverClicked(object sender, EventArgs e)
    {
        // Esto te regresa a la pantalla anterior en la pila de navegación
        Application.Current.MainPage = new NavigationPage(new Vistas.PaginaLogeo());
    }

}