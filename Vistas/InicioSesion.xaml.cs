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
        string usuario = UsuarioEntry.Text?.Trim();
        string pass = ContrasenaEntry.Text?.Trim();

        if (string.IsNullOrEmpty(usuario) || string.IsNullOrEmpty(pass))
        {
            await DisplayAlert("Campos Vacíos", "Ingresa tu ID y contraseña.", "OK");
            return;
        }

        try
        {
            IsBusy = true; // Aquí sí existe IsBusy porque es una ContentPage

            // Usamos el método que ya tienes en ConsultasGoogleSheets
            string resultado = await _sheets.ValidarUsuarioAsync(usuario, pass);

            switch (resultado)
            {
                case "EXITO":
                    Preferences.Set("UsuarioLogeado", usuario);
                    Application.Current.MainPage = new NavigationPage(new Vistas.PaginaMenu());
                    break;

                case "CONTRASEÑA_INCORRECTA":
                    await DisplayAlert("Contraseña Incorrecta", "La contraseña no es correcta.", "Reintentar");
                    ContrasenaEntry.Text = string.Empty;
                    break;

                case "USUARIO_NO_EXISTE":
                    await DisplayAlert("Usuario Inexistente", "El ID de operador ingresado no está registrado.", "OK");
                    break;
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", "Problema de conexión: " + ex.Message, "OK");
        }
        finally
        {
            IsBusy = false;
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