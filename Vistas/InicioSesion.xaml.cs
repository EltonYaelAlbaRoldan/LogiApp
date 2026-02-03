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
        try
        {
            var (sService, _) = await _authService.ObtenerServiciosAsync();
            _sheets = new ConsultasGoogleSheets(sService);
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", ex.Message, "OK");
        }
    }

    private async void OnLoginClicked(object sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(UsuarioEntry.Text) || string.IsNullOrWhiteSpace(ContrasenaEntry.Text))
        {
            await DisplayAlert("Campos Vacíos", "Ingresa tus credenciales.", "OK");
            return;
        }

        // REGLA DE NEGOCIO: Si hay un registro pendiente en este teléfono, no dejar entrar
        if (Preferences.ContainsKey("IdPendiente"))
        {
            await DisplayAlert("Registro Incompleto", "Tienes un proceso de registro pendiente. Termínalo antes de iniciar sesión.", "Ir a Registro");
            Application.Current.MainPage = new NavigationPage(new Vistas.PaginaRegistro());
            return;
        }

        SetLoadingState(true);

        try
        {
            bool esValido = await _sheets.ValidarUsuarioAsync(UsuarioEntry.Text, ContrasenaEntry.Text);

            if (esValido)
            {
                // GUARDAR SESIÓN: Guardamos el ID del usuario para el auto-login
                Preferences.Set("UsuarioLogeado", UsuarioEntry.Text);
                // Aquí podrías agregar otra verificación contra Sheets para ver si ya subió archivos
                Application.Current.MainPage = new NavigationPage(new PaginaMenu());
            }
            else
            {
                await DisplayAlert("Acceso Denegado", "Usuario o contraseña incorrectos.", "Reintentar");
            }
        }
        catch (Exception ex) { await DisplayAlert("Error", ex.Message, "OK"); }
        finally { SetLoadingState(false); }
    }

    private void OnTogglePasswordClicked(object sender, EventArgs e)
    {
        ContrasenaEntry.IsPassword = !ContrasenaEntry.IsPassword;
        TogglePasswordButton.Source = ContrasenaEntry.IsPassword ? "ver.png" : "nover.png";
    }

    private void SetLoadingState(bool isLoading)
    {
        CargandoIndicator.IsVisible = isLoading;
        CargandoIndicator.IsRunning = isLoading;
    }

    private async void OnVolverClicked(object sender, EventArgs e)
    {
        Application.Current.MainPage = new NavigationPage(new Vistas.PaginaLogeo());
    }
}