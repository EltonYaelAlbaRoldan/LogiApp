using LogiApp.Servicios;
using LogiApp.Modelos;

namespace LogiApp.Vistas;

// Pantalla principal de navegación que gestiona el acceso a módulos y valida vigencias de documentos.
public partial class PaginaMenu : ContentPage
{
    #region Atributos y Servicios

    private ConsultasGoogleSheets _sheetsService;
    private readonly ConexionGoogleAuth _authService = new();

    #endregion

    #region Constructor

    public PaginaMenu()
    {
        InitializeComponent();
    }
    // Se ejecuta CADA VEZ que la página aparece en pantalla
    protected override async void OnAppearing()
    {
        base.OnAppearing();

        try
        {
            // 1. PRIMERO inicializamos el servicio si es nulo
            if (_sheetsService == null)
            {
                var (sService, _) = await _authService.ObtenerServiciosAsync();
                _sheetsService = new ConsultasGoogleSheets(sService);
            }

            // 2. Ahora sí, obtenemos el ID y validamos
            string usuarioId = Preferences.Get("UsuarioLogeado", string.Empty);

            if (!string.IsNullOrEmpty(usuarioId))
            {
                // 3. Consultar el estado actual en Sheets
                // Usamos el nombre del método correcto: ObtenerDatosOperadorAsync
                var operador = await _sheetsService.ObtenerDatosOperadorAsync(usuarioId);

                if (operador == null || operador.Estado != "ACTIVO")
                {
                    Preferences.Remove("UsuarioLogeado");

                    string mensaje = operador?.Estado switch
                    {
                        "INACTIVO" => "Tu cuenta ha sido desactivada por el administrador.",
                        "VALIDACION" => "Tu cuenta todavía está pendiente de validación.",
                        _ => "Sesión expirada o cuenta no válida."
                    };

                    await DisplayAlert("Sesión Cerrada", mensaje, "OK");
                    Application.Current.MainPage = new NavigationPage(new InicioSesion());
                    return; // Salimos para no ejecutar lo que sigue
                }

                // Si el operador es válido, actualizamos el semáforo de vigencias
                ValidarBloqueoDeViajes(operador.VigenciaLicencia, operador.VigenciaMedico);
            }
        }
        catch (Exception ex)
        {
            BtnModuloViajes.IsEnabled = false;
            BtnModuloViajes.BackgroundColor = Colors.Gray;
            BtnModuloViajes.Text = "SIN CONEXIÓN - BLOQUEADO";
            await GestorErrores.GuardarErrorAsync(ex, "ValidarOperadorMenu", Preferences.Get("UsuarioLogeado", "Desconocido"));
        }

        ActualizarEstadoBotones();
    }

    // Evento para el gesto de deslizar hacia abajo
    private async void OnRefreshMenuRequested(object sender, EventArgs e)
    {
        ActualizarEstadoBotones();

        // Simulamos un pequeño tiempo de carga para que la animación se vea bien
        await Task.Delay(500);
        RefreshMenu.IsRefreshing = false;
    }

    private void ActualizarEstadoBotones()
    {
        // Revisamos si hay un viaje activo en las preferencias
        bool hayViajeActivo = Preferences.Default.Get("ViajeEnCurso", false);

    }

    #endregion

    #region Lógica de Sincronización y Validación

    // Inicializa servicios y consulta datos del operador para validar vigencias.
    private async Task ActualizarEstadoDesdeNube()
    {
        try
        {
            IsBusy = true;

            var auth = new ConexionGoogleAuth();
            var (sService, _) = await auth.ObtenerServiciosAsync();
            _sheetsService = new ConsultasGoogleSheets(sService);

            string id = Preferences.Default.Get("UsuarioLogeado", "");
            var operador = await _sheetsService.ObtenerDatosOperadorAsync(id);

            if (operador != null)
            {
                ValidarBloqueoDeViajes(operador.VigenciaLicencia, operador.VigenciaMedico);
            }
        }
        catch (Exception ex)
        {
            // Bloqueo de seguridad por falta de conexión o falla de servicio
            BtnModuloViajes.IsEnabled = false;
            BtnModuloViajes.BackgroundColor = Colors.Gray;
            BtnModuloViajes.Text = "SIN CONEXIÓN - BLOQUEADO";
            await DisplayAlert("Error de Sincronización", "Verifica tu conexión para validar vigencias.", "OK");
        }
        finally
        {
            IsBusy = false;
            VerificarViajePendienteLocal();
        }
    }

    // Gestiona el estado del botón de viajes según la vigencia de documentos (Semáforo).
    private void ValidarBloqueoDeViajes(DateTime vLicencia, DateTime vMedico)
    {
        int diasLicencia = (vLicencia - DateTime.Now).Days;
        int diasMedico = (vMedico - DateTime.Now).Days;
        int diasCriticos = Math.Min(diasLicencia, diasMedico);

        if (diasCriticos <= 0)
        {
            // ESTADO ROJO: Vencido y Bloqueado
            BtnModuloViajes.IsEnabled = false;
            BtnModuloViajes.BackgroundColor = Colors.Red;
            BtnModuloViajes.Text = "BLOQUEADO: VENCIDO";

            MainThread.BeginInvokeOnMainThread(async () => {
                await DisplayAlert("🚫 BLOQUEADO", "Tus documentos han vencido. No puedes iniciar viajes.", "Entendido");
            });
        }
        else if (diasCriticos <= 90)
        {
            // ESTADO AMARILLO: Próximo a vencer (Permite operar con aviso)
            BtnModuloViajes.IsEnabled = true;
            BtnModuloViajes.BackgroundColor = Colors.Orange;
            BtnModuloViajes.Text = "GESTIÓN DE VIAJES";

            MainThread.BeginInvokeOnMainThread(async () => {
                await DisplayAlert("⚠️ ADVERTENCIA", $"Tus documentos vencen en {diasCriticos} días.", "Entendido");
            });
        }
        else
        {
            // ESTADO VERDE: Documentación en regla
            BtnModuloViajes.IsEnabled = true;
            BtnModuloViajes.BackgroundColor = Color.FromArgb("#2196F3");
            BtnModuloViajes.Text = "GESTIÓN DE VIAJES";
        }
    }

    // Verifica si hay un viaje guardado localmente que no se ha finalizado.
    private async void VerificarViajePendienteLocal()
    {
        bool tieneViajeActivo = Preferences.Default.Get("ViajeEnCurso", false);
        if (tieneViajeActivo)
        {
            string folio = Preferences.Default.Get("FolioPendiente", "Sin Folio");
            bool irAViaje = await DisplayAlert("Viaje en Curso",
                $"Debes terminar el viaje {folio} antes de iniciar uno nuevo.",
                "Ir a completar viaje", "Entendido");

            if (irAViaje)
            {
                await Navigation.PushAsync(new Vistas.PaginaTerminarViaje(folio));
            }
        }
    }

    #endregion

    #region Eventos de UI y Navegación

    private async void OnPerfilClicked(object sender, EventArgs e)
    {
        await Navigation.PushAsync(new Vistas.PaginaPerfil());
    }

    private async void OnViajesClicked(object sender, EventArgs e)
    {
        bool tieneViajeActivo = Preferences.Default.Get("ViajeEnCurso", false);
        string folio = Preferences.Default.Get("FolioPendiente", string.Empty);

        if (tieneViajeActivo && !string.IsNullOrEmpty(folio))
        {
            await Navigation.PushAsync(new Vistas.PaginaTerminarViaje(folio));
        }
        else
        {
            Application.Current.MainPage = new NavigationPage(new Vistas.PaginaViajes());
        }
    }

    private async void OnMantenimientoClicked(object sender, EventArgs e)
    {
        Application.Current.MainPage = new NavigationPage(new Vistas.PaginaMantenimiento());
    }

    private async void OnHistorialClicked(object sender, EventArgs e)
    {
        // Navega a la página donde implementaste el historial
        await Navigation.PushAsync(new Vistas.PaginaHistorial());
    }

    private async void OnLogoutClicked(object sender, EventArgs e)
    {
        if (Preferences.Default.Get("ViajeEnCurso", false))
        {
            string folio = Preferences.Default.Get("FolioPendiente", "");
            await DisplayAlert("Viaje en curso", $"Debes finalizar el viaje {folio} antes de cerrar sesión.", "OK");
            return;
        }

        bool deseaSalir = await DisplayAlert("Cerrar Sesión", "¿Está seguro?", "Sí", "No");
        if (deseaSalir)
        {
            Preferences.Default.Remove("UsuarioLogeado");
            Application.Current.MainPage = new NavigationPage(new Vistas.PaginaLogeo());
        }
    }

    #endregion
}
