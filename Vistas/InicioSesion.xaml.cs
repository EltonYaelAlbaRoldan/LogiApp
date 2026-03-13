using LogiApp.Servicios;

namespace LogiApp.Vistas;

// Vista encargada de gestionar el inicio de sesión de los usuarios.
public partial class InicioSesion : ContentPage
{
    #region Atributos y Servicios

    private ConsultasGoogleSheets _sheets;
    private readonly ConexionGoogleAuth _authService = new();

    #endregion

    #region Constructor

    public InicioSesion()
    {
        InitializeComponent();
        InicializarServicio();
    }

    #endregion

    #region Configuración Inicial

    // Conecta con el servicio de autenticación de Google y prepara el cliente de Sheets.
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

    #endregion

    #region Eventos de UI

    // Procesa la lógica de autenticación cuando el usuario presiona el botón de entrar.
    private async void OnLoginClicked(object sender, EventArgs e)
    {
        // Validación de entradas
        if (string.IsNullOrWhiteSpace(UsuarioEntry.Text) || string.IsNullOrWhiteSpace(ContrasenaEntry.Text))
        {
            await DisplayAlert("Campos Vacíos", "Ingresa tus credenciales.", "OK");
            return;
        }

        // Si hay un registro pendiente en este teléfono, no dejar entrar
        if (Preferences.ContainsKey("IdPendiente"))
        {
            await DisplayAlert("Registro Incompleto", "Tienes un proceso de registro pendiente.", "Ir a Registro");
            Application.Current.MainPage = new NavigationPage(new Vistas.PaginaRegistro());
            return;
        }

        SetLoadingState(true);

        try
        {
            bool esValido = await _sheets.ValidarUsuarioAsync(UsuarioEntry.Text, ContrasenaEntry.Text);

            if (esValido)
            {
                // Guardamos el ID del usuario para el auto-login
                Preferences.Set("UsuarioLogeado", UsuarioEntry.Text);
                Application.Current.MainPage = new NavigationPage(new PaginaMenu());
            }
            else
            {
                await DisplayAlert("Acceso Denegado", "Usuario o contraseña incorrectos.", "Reintentar");
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", ex.Message, "OK");
        }
        finally
        {
            SetLoadingState(false);
        }
    }

    // Alterna la visibilidad del campo de contraseña.
    private void OnTogglePasswordClicked(object sender, EventArgs e)
    {
        ContrasenaEntry.IsPassword = !ContrasenaEntry.IsPassword;
        TogglePasswordButton.Source = ContrasenaEntry.IsPassword ? "ver.png" : "nover.png";
    }

    // Regresa a la pantalla de selección de tipo de logeo.
    private async void OnVolverClicked(object sender, EventArgs e)
    {
        Application.Current.MainPage = new NavigationPage(new Vistas.PaginaLogeo());
    }

    #endregion

    #region Métodos Auxiliares

    // Controla la visibilidad y animación del indicador de carga.
    private void SetLoadingState(bool isLoading)
    {
        CargandoIndicator.IsVisible = isLoading;
        CargandoIndicator.IsRunning = isLoading;
    }

    #endregion
}