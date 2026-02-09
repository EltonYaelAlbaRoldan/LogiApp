using LogiApp.Servicios;

namespace LogiApp.Vistas;

/// <summary>
/// Vista encargada de gestionar el inicio de sesión de los usuarios.
/// </summary>
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

    /// <summary>
    /// Conecta con el servicio de autenticación de Google y prepara el cliente de Sheets.
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
            await DisplayAlert("Error", ex.Message, "OK");
        }
    }

    #endregion

    #region Eventos de UI

    /// <summary>
    /// Procesa la lógica de autenticación cuando el usuario presiona el botón de entrar.
    /// </summary>
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

                // Navegación al menú principal
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

    /// <summary>
    /// Alterna la visibilidad del campo de contraseña.
    /// </summary>
    private void OnTogglePasswordClicked(object sender, EventArgs e)
    {
        ContrasenaEntry.IsPassword = !ContrasenaEntry.IsPassword;
        TogglePasswordButton.Source = ContrasenaEntry.IsPassword ? "ver.png" : "nover.png";
    }

    /// <summary>
    /// Regresa a la pantalla de selección de tipo de logeo.
    /// </summary>
    private async void OnVolverClicked(object sender, EventArgs e)
    {
        Application.Current.MainPage = new NavigationPage(new Vistas.PaginaLogeo());
    }

    #endregion

    #region Métodos Auxiliares

    /// <summary>
    /// Controla la visibilidad y animación del indicador de carga.
    /// </summary>
    /// <param name="isLoading">Estado de carga.</param>
    private void SetLoadingState(bool isLoading)
    {
        CargandoIndicator.IsVisible = isLoading;
        CargandoIndicator.IsRunning = isLoading;
    }

    #endregion
}