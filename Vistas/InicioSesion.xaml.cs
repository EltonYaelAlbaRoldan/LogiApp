using LogiApp.Servicios;

namespace LogiApp.Vistas;

/// <summary>
/// Pantalla de acceso principal. Gestiona la autenticación contra Google Sheets.
/// </summary>
public partial class InicioSesion : ContentPage
{
    // --- ATRIBUTOS Y SERVICIOS ---
    private ConsultasGoogleSheets _sheets;
    private readonly ConexionGoogleAuth _authService = new();

    // --- CONSTRUCTOR ---
    public InicioSesion()
    {
        InitializeComponent();
        InicializarServicio();
    }

    // --- CONFIGURACIÓN INICIAL ---

    /// <summary>
    /// Conecta la vista con los servicios de Google API al cargar la página.
    /// </summary>
    private async void InicializarServicio()
    {
        try
        {
            // Obtenemos solo el servicio de Sheets (el de Drive se ignora con '_')
            var (sService, _) = await _authService.ObtenerServiciosAsync();
            _sheets = new ConsultasGoogleSheets(sService);
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error de Conexión", "No se pudo conectar con la base de datos: " + ex.Message, "OK");
        }
    }

    // --- EVENTOS DE USUARIO (LÓGICA DE NEGOCIO) ---

    /// <summary>
    /// Proceso de autenticación: Valida campos, activa el loader y consulta Sheets.
    /// </summary>
    private async void OnLoginClicked(object sender, EventArgs e)
    {
        // 1. Validación de interfaz (Campos vacíos)
        if (string.IsNullOrWhiteSpace(UsuarioEntry.Text) || string.IsNullOrWhiteSpace(ContrasenaEntry.Text))
        {
            await DisplayAlert("Campos Vacíos", "Por favor, ingresa tus credenciales.", "OK");
            return;
        }

        // 2. Estado visual de carga
        SetLoadingState(true);

        try
        {
            // 3. Consulta asíncrona a la base de datos en la nube
            bool esValido = await _sheets.ValidarUsuarioAsync(UsuarioEntry.Text, ContrasenaEntry.Text);

            if (esValido)
            {
                // Navegación exitosa: Resetea la pila de navegación al menú principal
                Application.Current.MainPage = new NavigationPage(new PaginaMenu());
            }
            else
            {
                await DisplayAlert("Acceso Denegado", "Usuario o contraseña incorrectos.", "Reintentar");
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error Crítico", "Fallo en el servidor: " + ex.Message, "OK");
        }
        finally
        {
            // 4. Restaurar interfaz
            SetLoadingState(false);
        }
    }

    // --- MÉTODOS DE APOYO E INTERFAZ ---

    /// <summary>
    /// Alterna la visibilidad de la contraseña y cambia el icono del botón.
    /// </summary>
    private void OnTogglePasswordClicked(object sender, EventArgs e)
    {
        ContrasenaEntry.IsPassword = !ContrasenaEntry.IsPassword;
        TogglePasswordButton.Source = ContrasenaEntry.IsPassword ? "ver.png" : "nover.png";
    }

    /// <summary>
    /// Controla la visibilidad del ActivityIndicator para evitar código duplicado.
    /// </summary>
    private void SetLoadingState(bool isLoading)
    {
        CargandoIndicator.IsVisible = isLoading;
        CargandoIndicator.IsRunning = isLoading;
        // Opcional: Bloquear el botón de login mientras carga
        //BotonLogin.IsEnabled = !isLoading; 
    }

    /// <summary>
    /// Regresa a la página de selección de perfil (Logeo).
    /// </summary>
    private async void OnVolverClicked(object sender, EventArgs e)
    {
        Application.Current.MainPage = new NavigationPage(new Vistas.PaginaLogeo());
    }
}