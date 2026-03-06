using LogiApp.Servicios;
using LogiApp.Modelos;

namespace LogiApp.Vistas;

public partial class PaginaMenu : ContentPage
{
    private ConsultasGoogleSheets _sheetsService;

    public PaginaMenu()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Cada vez que el usuario regresa al menú, se consultan los datos reales en la nube.
    /// </summary>
    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await ActualizarEstadoDesdeNube();
    }

    private async Task ActualizarEstadoDesdeNube()
    {
        try
        {
            IsBusy = true;

            // 1. Inicializar servicios de autenticación y Sheets
            var auth = new ConexionGoogleAuth();
            var (sService, _) = await auth.ObtenerServiciosAsync();
            _sheetsService = new ConsultasGoogleSheets(sService);

            // 2. Obtener el ID del operador logueado
            string id = Preferences.Default.Get("UsuarioLogeado", "");

            // 3. Consultar datos DIRECTO desde Google Sheets
            var operador = await _sheetsService.ObtenerDatosOperadorAsync(id);

            if (operador != null)
            {
                // 4. Validar bloqueos basados en la respuesta de la nube
                ValidarBloqueoDeViajes(operador.VigenciaLicencia, operador.VigenciaMedico);
            }
        }
        catch (Exception ex)
        {
            // Si no hay internet o falla el servicio, bloqueamos por seguridad
            BtnModuloViajes.IsEnabled = false;
            BtnModuloViajes.BackgroundColor = Colors.Gray;
            BtnModuloViajes.Text = "SIN CONEXIÓN - BLOQUEADO";
            await DisplayAlert("Error de Sincronización", "No se pudieron validar tus vigencias. Verifica tu conexión.", "OK");
        }
        finally
        {
            IsBusy = false;
            VerificarViajePendienteLocal();
        }
    }

    private void ValidarBloqueoDeViajes(DateTime vLicencia, DateTime vMedico)
    {
        int diasLicencia = (vLicencia - DateTime.Now).Days;
        int diasMedico = (vMedico - DateTime.Now).Days;
        int diasCriticos = Math.Min(diasLicencia, diasMedico);

        if (diasCriticos <= 0)
        {
            // ROJO - VENCIDO
            BtnModuloViajes.IsEnabled = false;
            BtnModuloViajes.BackgroundColor = Colors.Red;
            BtnModuloViajes.Text = "BLOQUEADO: VIGENCIA VENCIDA";

               MainThread.BeginInvokeOnMainThread(async () => {
                    await DisplayAlert("🚫 BLOQUEADO", $"Tus documentos han vencido. No puedes iniciar viajes hasta renovarlos.", "Entendido");
                });
        }
        else if (diasCriticos <= 90)
        {
            // AMARILLO - ADVERTENCIA (Permite viajar, pero avisa)
            BtnModuloViajes.IsEnabled = true;
            BtnModuloViajes.BackgroundColor = Colors.Orange;
            BtnModuloViajes.Text = "GESTIÓN DE VIAJES";

            // Alertamos al usuario solo si está en zona de riesgo (90 días)
            MainThread.BeginInvokeOnMainThread(async () => {
                await DisplayAlert("⚠️ ADVERTENCIA", $"Tus documentos vencen en {diasCriticos} días. Favor de renovar.", "Entendido");
            });
        }
        else
        {
            // VERDE - TODO EN REGLA
            BtnModuloViajes.IsEnabled = true;
            BtnModuloViajes.BackgroundColor = Color.FromArgb("#2196F3");
            BtnModuloViajes.Text = "GESTIÓN DE VIAJES";
        }
    }

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

    #region Navegación y Sesión

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

    private async void OnLogoutClicked(object sender, EventArgs e)
    {
        bool deseaSalir = await DisplayAlert("Cerrar Sesión", "¿Está seguro?", "Sí", "No");
        if (deseaSalir)
        {
            Preferences.Default.Remove("UsuarioLogeado");
            Application.Current.MainPage = new NavigationPage(new Vistas.PaginaLogeo());
        }
    }

    #endregion
}