using LogiApp.Servicios;

using System.Security.Cryptography;

namespace LogiApp.Vistas;

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

        Task.Run(async () =>
        {
            try
            {
                var (sService, _) = await _authService.ObtenerServiciosAsync();
                var tempSheets = new ConsultasGoogleSheets(sService);
                await GestorErrores.SincronizarLogsPendientesAsync(tempSheets);
            }
            catch { }
        });
    }
    #endregion

    private async void InicializarServicio()
    {
        try
        {
            var (sService, _) = await _authService.ObtenerServiciosAsync();
            _sheets = new ConsultasGoogleSheets(sService);
        }
        catch (Exception ex)
        {
            await GestorErrores.GuardarErrorAsync(ex, "InicializarServicio");
            await DisplayAlert("Error", "No se pudo conectar: " + ex.Message, "OK");
        }
    }

    #region Eventos de UI
    private async void OnLoginClicked(object sender, EventArgs e)
    {
        // Captura preventiva para el log si falla algo
        string idParaLog = string.IsNullOrWhiteSpace(UsuarioEntry.Text) ? "Desconocido" : UsuarioEntry.Text.Trim();

        string usuario = UsuarioEntry.Text?.Trim().ToUpperInvariant();
        string pass = ContrasenaEntry.Text?.Trim();

        if (string.IsNullOrEmpty(usuario) || string.IsNullOrEmpty(pass))
        {
            await DisplayAlert("Campos Vacíos", "Ingresa tu ID y contraseña.", "OK");
            return;
        }

        if (_sheets == null)
        {
            IsBusy = true;
            try
            {
                var (sService, _) = await _authService.ObtenerServiciosAsync();
                _sheets = new ConsultasGoogleSheets(sService);
            }
            catch (Exception ex)
            {
                await GestorErrores.GuardarErrorAsync(ex, "Reintento Conexión Login", idParaLog);
                await DisplayAlert("Sin Conexión", "Revisa tu internet.", "OK");
                IsBusy = false;
                return;
            }
        }

        try
        {
            IsBusy = true;
            string resultado = await _sheets.ValidarUsuarioAsync(usuario, pass);

            switch (resultado)
            {
                case "EXITO":
                    Preferences.Set("UsuarioLogeado", usuario);
                    _ = GestorErrores.SincronizarLogsPendientesAsync(_sheets);
                    Application.Current.MainPage = new NavigationPage(new Vistas.PaginaMenu());
                    break;
                case "CONTRASEÑA_INCORRECTA":
                    await DisplayAlert("Error", "La contraseña es incorrecta.", "Reintentar");
                    break;
                case "USUARIO_NO_EXISTE":
                    await DisplayAlert("No Encontrado", "El ID de operador no existe.", "OK");
                    break;
                default:
                    await DisplayAlert("Aviso", "Estado de cuenta: " + resultado, "OK");
                    break;
            }
        }
        catch (Exception ex)
        {
            await GestorErrores.GuardarErrorAsync(ex, "OnLoginClicked - Catch", idParaLog);
            await DisplayAlert("Error", "Problema de conexión: " + ex.Message, "OK");
        }
        finally { IsBusy = false; }
    }

    private void OnTogglePasswordClicked(object sender, EventArgs e)
    {
        ContrasenaEntry.IsPassword = !ContrasenaEntry.IsPassword;
        TogglePasswordButton.Source = ContrasenaEntry.IsPassword ? "ver.png" : "nover.png";
    }

    private async void OnVolverClicked(object sender, EventArgs e)
    {
        Application.Current.MainPage = new NavigationPage(new Vistas.PaginaLogeo());
    }
    #endregion

    #region Recuperación de Contraseña
    private async void OnOlvideContrasenaClicked(object sender, EventArgs e)
    {
        // SOLUCIÓN AL ERROR idOp: Se declara al inicio del método
        string idOpFinal = "Desconocido";

        try
        {
            if (_sheets == null)
            {
                var (sService, _) = await _authService.ObtenerServiciosAsync();
                _sheets = new ConsultasGoogleSheets(sService);
            }

            DateTime tiempoBloqueo = Preferences.Default.Get("BloqueoRecuperacion", DateTime.MinValue);
            if (DateTime.Now < tiempoBloqueo)
            {
                var restante = tiempoBloqueo - DateTime.Now;
                await DisplayAlert("Acceso Restringido", $"Intenta en {restante.Minutes}m {restante.Seconds}s.", "OK");
                return;
            }

            string inputId = await DisplayPromptAsync("Recuperación", "Ingresa tu ID de Operador:", "Continuar", "Cancelar");
            if (string.IsNullOrWhiteSpace(inputId)) return;

            idOpFinal = inputId.Trim(); // Se asigna el valor ingresado

            var operador = await _sheets.ObtenerDatosOperadorAsync(idOpFinal);
            if (operador == null || string.IsNullOrEmpty(operador.Correo))
            {
                await DisplayAlert("Error", "ID no encontrado o sin correo registrado.", "OK");
                return;
            }

            string codigo = RandomNumberGenerator.GetInt32(100000, 1000000).ToString();
            bool enviado = await _emailService.EnviarCodigoRecuperacionAsync(operador.Correo, codigo);
            if (!enviado)
            {
                await DisplayAlert("Error", "No fue posible enviar el código de recuperación.", "OK");
                return;
            }

            bool codigoValido = false;
            for (int intento = 1; intento <= 3; intento++)
            {
                string codigoIngresado = await DisplayPromptAsync(
                    "Verificación", "Ingresa el código de 6 dígitos enviado a tu correo:",
                    "Validar", "Cancelar", keyboard: Keyboard.Numeric, maxLength: 6);
                if (string.IsNullOrWhiteSpace(codigoIngresado)) return;
                if (codigoIngresado.Trim() == codigo)
                {
                    codigoValido = true;
                    break;
                }
                await DisplayAlert("Código incorrecto", $"Quedan {3 - intento} intento(s).", "OK");
            }

            if (!codigoValido)
            {
                Preferences.Default.Set("BloqueoRecuperacion", DateTime.Now.AddMinutes(15));
                await DisplayAlert("Acceso restringido", "Demasiados intentos. Intenta nuevamente en 15 minutos.", "OK");
                return;
            }

            string nuevaPassword = await DisplayPromptAsync(
                "Nueva contraseña", "Ingresa una contraseña de al menos 8 caracteres:",
                "Continuar", "Cancelar", maxLength: 64, keyboard: Keyboard.Text);
            if (string.IsNullOrWhiteSpace(nuevaPassword)) return;
            if (nuevaPassword.Length < 8)
            {
                await DisplayAlert("Contraseña inválida", "Debe tener al menos 8 caracteres.", "OK");
                return;
            }

            string confirmacion = await DisplayPromptAsync(
                "Confirmar contraseña", "Escribe nuevamente la contraseña:",
                "Guardar", "Cancelar", maxLength: 64, keyboard: Keyboard.Text);
            if (confirmacion != nuevaPassword)
            {
                await DisplayAlert("No coincide", "Las contraseñas no coinciden.", "OK");
                return;
            }

            bool actualizada = await _sheets.ActualizarPasswordAsync(idOpFinal, nuevaPassword);
            if (!actualizada) throw new Exception("No se pudo actualizar la contraseña en Sheets.");

            Preferences.Default.Remove("BloqueoRecuperacion");
            await DisplayAlert("Éxito", "Tu contraseña fue actualizada.", "Iniciar sesión");
        }
        catch (Exception ex)
        {
            await GestorErrores.GuardarErrorAsync(ex, "OnOlvideContrasenaClicked", idOpFinal);
            await DisplayAlert("Error", "Ocurrió un problema: " + ex.Message, "OK");
        }
    }
    #endregion

    private void SetLoadingState(bool isLoading)
    {
        CargandoIndicator.IsVisible = isLoading;
        CargandoIndicator.IsRunning = isLoading;
    }
}
