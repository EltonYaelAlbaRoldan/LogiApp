using LogiApp.Servicios;

namespace LogiApp.Vistas;

// Vista encargada de gestionar el inicio de sesión de los usuarios.
public partial class InicioSesion : ContentPage
{
    #region Atributos y Servicios

    private ConsultasGoogleSheets _sheets;
    private readonly ConexionGoogleAuth _authService = new();
    private readonly ServicioEmail _emailService = new ServicioEmail();

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

    #region Recuperación de Contraseña

    private async void OnOlvideContrasenaClicked(object sender, EventArgs e)
    {
        // 1. Pedir el ID del operador
        string idOp = await DisplayPromptAsync("Recuperación", "Ingresa tu ID de Operador (Ej: OP-001):", "Continuar", "Cancelar");
        if (string.IsNullOrEmpty(idOp)) return;

        // 2. Buscar al operador y su correo en Sheets
        var operador = await _sheets.ObtenerDatosOperadorAsync(idOp); // Ya tienes este método o similar
        if (operador == null || string.IsNullOrEmpty(operador.Correo))
        {
            await DisplayAlert("Error", "ID no encontrado o no tiene correo registrado.", "OK");
            return;
        }

        // 3. Generar código de 6 dígitos
        string codigoGenerado = new Random().Next(100000, 999999).ToString();

        // 4. Enviar correo (Usando tu ServicioEmail existente)
        bool enviado = await _emailService.EnviarCodigoRecuperacionAsync(operador.Correo, codigoGenerado);

        if (enviado)
        {
            // 5. Pedir el código al usuario
            string codigoIngresado = await DisplayPromptAsync("Verificación", $"Enviamos un código a {operador.Correo}", "Validar", "Cancelar");

            if (codigoIngresado == codigoGenerado)
            {
                // 6. Pedir nueva contraseña
                string nuevaPass = await DisplayPromptAsync("Nueva Contraseña", "Ingresa tu nueva clave de acceso:", "Actualizar", "Cancelar", keyboard: Keyboard.Text);

                if (!string.IsNullOrEmpty(nuevaPass))
                {
                    bool exito = await _sheets.ActualizarPasswordAsync(idOp, nuevaPass);
                    if (exito) await DisplayAlert("Éxito", "Contraseña actualizada correctamente.", "OK");
                }
            }
            else
            {
                await DisplayAlert("Error", "Código incorrecto.", "OK");
            }
        }
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