using LogiApp.Servicios;

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

        string usuario = UsuarioEntry.Text?.Trim();
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

            // Lógica de códigos y reenvíos... (Abreviada para claridad, mantén tu lógica de ciclos)
            // Si hay un error aquí, saltará al catch y usará el idOpFinal
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