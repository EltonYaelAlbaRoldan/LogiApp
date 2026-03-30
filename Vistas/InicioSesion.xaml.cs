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
        // 1. Verificar si el usuario está bloqueado por exceso de reenvíos
        DateTime tiempoBloqueo = Preferences.Default.Get("BloqueoRecuperacion", DateTime.MinValue);
        if (DateTime.Now < tiempoBloqueo)
        {
            var restante = tiempoBloqueo - DateTime.Now;
            await DisplayAlert("Acceso Restringido", $"Demasiados reenvíos. Intenta de nuevo en {restante.Minutes}m {restante.Seconds}s.", "OK");
            return;
        }

        // 2. Pedir ID y buscar operador
        string idOp = await DisplayPromptAsync("Recuperación", "Ingresa tu ID de Operador:", "Continuar", "Cancelar");
        if (string.IsNullOrWhiteSpace(idOp)) return;

        var operador = await _sheets.ObtenerDatosOperadorAsync(idOp);
        if (operador == null || string.IsNullOrEmpty(operador.Correo))
        {
            await DisplayAlert("Error", "ID no encontrado o sin correo registrado.", "OK");
            return;
        }

        int intentosCodigo = 0;
        int conteoReenvios = 0;
        const int maxIntentosCodigo = 3;
        const int maxReenvios = 3;
        bool verificado = false;

        // Ciclo principal de reenvíos
        while (conteoReenvios < maxReenvios && !verificado)
        {
            string codigoGenerado = new Random().Next(100000, 999999).ToString();
            bool enviado = await _emailService.EnviarCodigoRecuperacionAsync(operador.Correo, codigoGenerado);

            if (!enviado)
            {
                await DisplayAlert("Error", "No se pudo enviar el correo.", "OK");
                return;
            }

            conteoReenvios++;
            bool solicitarNuevoReenvio = false;

            // Ciclo de validación del código actual
            while (intentosCodigo < maxIntentosCodigo)
            {
                string msg = $"Intento {intentosCodigo + 1} de {maxIntentosCodigo}\nCódigo enviado a {operador.Correo}";
                string btnCancel = (conteoReenvios < maxReenvios) ? "Reenviar Código" : "Cancelar";

                string input = await DisplayPromptAsync("Verificación", msg, "Validar", btnCancel);

                // Si presiona "Reenviar Código" o "Cancelar"
                if (string.IsNullOrEmpty(input))
                {
                    if (conteoReenvios < maxReenvios)
                    {
                        solicitarNuevoReenvio = true;
                        break;
                    }
                    return; // Cancelado definitivamente
                }

                if (input == codigoGenerado)
                {
                    verificado = true;
                    break;
                }
                else
                {
                    intentosCodigo++;
                    if (intentosCodigo < maxIntentosCodigo)
                        await DisplayAlert("Incorrecto", $"Te quedan {maxIntentosCodigo - intentosCodigo} intentos.", "OK");
                }
            }

            if (verificado) break;
            if (solicitarNuevoReenvio) continue;

            // Si sale de los ciclos sin verificar y sin pedir reenvío, o agotó intentos
            if (intentosCodigo >= maxIntentosCodigo) break;
        }

        // 3. Manejo de bloqueos y éxito
        if (verificado)
        {
            // Resetear bloqueos al tener éxito
            Preferences.Default.Remove("BloqueoRecuperacion");

            string nuevaPass = await DisplayPromptAsync("Nueva Contraseña", "Ingresa tu nueva clave:", "Actualizar", "Cancelar");
            if (!string.IsNullOrWhiteSpace(nuevaPass))
            {
                bool exito = await _sheets.ActualizarPasswordAsync(idOp, nuevaPass);
                if (exito) await DisplayAlert("Éxito", "Contraseña actualizada.", "OK");
            }
        }
        else
        {
            if (conteoReenvios >= maxReenvios)
            {
                // BLOQUEO DE 10 MINUTOS
                Preferences.Default.Set("BloqueoRecuperacion", DateTime.Now.AddMinutes(2));
                await DisplayAlert("Bloqueo Temporal", "Has solicitado el código demasiadas veces. Espera 10 minutos.", "OK");
            }
            else if (intentosCodigo >= maxIntentosCodigo)
            {
                await DisplayAlert("Agotado", "Has fallado el código 3 veces. El proceso se ha cancelado.", "OK");
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