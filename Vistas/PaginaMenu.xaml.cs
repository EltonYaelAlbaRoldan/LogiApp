using Microsoft.Maui.Controls;

namespace LogiApp.Vistas;

/// <summary>
/// Panel principal de control después del acceso. 
/// Permite navegar a los módulos de Viajes, Mantenimiento y gestionar la sesión del usuario.
/// </summary>
public partial class PaginaMenu : ContentPage
{
    #region Constructor

    public PaginaMenu()
    {
        InitializeComponent();
    }

    #endregion

    #region Gestión de Módulos (Navegación)

    /// <summary>
    /// Navega a la página de visualización del perfil del usuario.
    /// </summary>
    private async void OnPerfilClicked(object sender, EventArgs e)
    {
        // Navegación a la página de Perfil usando el stack de navegación actual
        await Navigation.PushAsync(new Vistas.PaginaPerfil());
    }

    /// <summary>
    /// Dirige al usuario al módulo de Viajes, redirigiendo si hay uno pendiente.
    /// </summary>
    private async void OnViajesClicked(object sender, EventArgs e)
    {
        // Revisamos si existe un folio guardado en la memoria local [cite: 2026-02-17]
        bool tieneViajeActivo = Preferences.Default.Get("ViajeEnCurso", false);
        string folio = Preferences.Default.Get("FolioPendiente", string.Empty);

        if (tieneViajeActivo && !string.IsNullOrEmpty(folio))
        {
            // REDIRECCIÓN AUTOMÁTICA: Si hay viaje, lo mandamos a terminarlo [cite: 2026-02-17]
            await DisplayAlert("Viaje en Curso", $"Redirigiendo al viaje pendiente: {folio}", "Aceptar");
            await Navigation.PushAsync(new Vistas.PaginaTerminarViaje(folio));
        }
        else
        {
            // FLUJO NORMAL: Si no hay nada pendiente, puede crear uno nuevo [cite: 2026-02-17]
            Application.Current.MainPage = new NavigationPage(new Vistas.PaginaViajes());
        }
    }

    /// <summary>
    /// Dirige al usuario al módulo de Mantenimiento de unidades.
    /// </summary>
    private async void OnMantenimientoClicked(object sender, EventArgs e)
    {
        // Establece la página de Mantenimiento como nueva raíz de navegación
        Application.Current.MainPage = new NavigationPage(new Vistas.PaginaMantenimiento());
    }

    #endregion

    #region Gestión de Sesión

    /// <summary>
    /// Solicita confirmación al usuario y limpia las credenciales locales para cerrar la sesión.
    /// </summary>
    private async void OnLogoutClicked(object sender, EventArgs e)
    {
        // Confirmación de salida
        bool deseaSalir = await DisplayAlert("Cerrar Sesión", "¿Está seguro que desea cerrar la sesión?", "Sí", "No");

        if (deseaSalir)
        {
            // Limpieza de datos de sesión y registros pendientes en preferencias locales
            Preferences.Default.Remove("UsuarioLogeado");
            Preferences.Default.Remove("IdPendiente");
            Preferences.Default.Remove("CarpetaPendiente");

            // Regreso a la página de acceso principal (Login)
            Application.Current.MainPage = new NavigationPage(new Vistas.PaginaLogeo());
        }
    }

    #endregion

    #region Gestión de Ciclo de Vida (Persistencia)

    /// <summary>
    /// Se ejecuta al entrar al menú. Verifica si existe un viaje pendiente 
    /// para bloquear el acceso a nuevos viajes y ofrecer la reanudación.
    /// </summary>
    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await VerificarVigenciasYBloquear();

        // REVISAR SI HAY UN VIAJE QUE NO SE CERRÓ [cite: 2026-02-17]
        bool tieneViajeActivo = Preferences.Default.Get("ViajeEnCurso", false);

        if (tieneViajeActivo)
        {
            // BLOQUEO VISUAL: Como no usamos x:Name aún, buscamos el botón por su texto 
            // o simplemente deshabilitamos la navegación lógica.
            // Nota: Es altamente recomendable poner x:Name="BtnModuloViajes" en el XAML.

            string folio = Preferences.Default.Get("FolioPendiente", "Sin Folio");

            // Pequeña pausa para que la UI termine de renderizar antes del alert
            await Task.Delay(500);

            bool irAViaje = await DisplayAlert("Viaje en Curso",
                $"Debes terminar el viaje {folio} antes de iniciar uno nuevo.",
                "Ir a completar viaje", "Entendido");

            if (irAViaje)
            {
                // Navegación directa al monitoreo del viaje guardado [cite: 2026-02-17]
                await Navigation.PushAsync(new Vistas.PaginaTerminarViaje(folio));
            }
        }
    }

    private async Task VerificarVigenciasYBloquear()
    {
        try
        {
            // 1. Recuperar fechas (usamos fecha vieja por defecto para obligar a cargar datos)
            DateTime fechaDefault = new DateTime(2000, 1, 1);
            DateTime vLicencia = Preferences.Default.Get("VigenciaLicencia", fechaDefault);
            DateTime vMedico = Preferences.Default.Get("VigenciaMedico", fechaDefault);

            // 2. Calcular días restantes (Lógica de Semáforo)
            int diasLicencia = (vLicencia - DateTime.Now).Days;
            int diasMedico = (vMedico - DateTime.Now).Days;

            // 3. Determinar el estado más crítico
            int diasCriticos = Math.Min(diasLicencia, diasMedico);

            // 4. Lógica de Colores y Bloqueo
            if (diasCriticos <= 0)
            {
                // ROJO - BLOQUEADO
                BtnModuloViajes.IsEnabled = false;
                BtnModuloViajes.BackgroundColor = Colors.Red;
                BtnModuloViajes.Text = "BLOQUEADO: VIGENCIA VENCIDA";

                await DisplayAlert("⚠️ ALERTA", "Documentación vencida. Actualiza en tu Perfil.", "OK");
            }
            else if (diasCriticos <= 90)
            {
                // AMARILLO - ADVERTENCIA (Pero permite viajar)
                BtnModuloViajes.IsEnabled = true;
                BtnModuloViajes.BackgroundColor = Colors.Orange;
                BtnModuloViajes.Text = "GESTIÓN DE VIAJES";

                await DisplayAlert("⚠️ ALERTA", "DOCUMENTOS PROXIMOS A VENCER, REVISAR", "OK");
            }
            else
            {
                // VERDE - TODO OK
                BtnModuloViajes.IsEnabled = true;
                BtnModuloViajes.BackgroundColor = Color.FromArgb("#2196F3"); // Tu azul original
                BtnModuloViajes.Text = "GESTIÓN DE VIAJES";
            }
        }
        catch
        {
            BtnModuloViajes.IsEnabled = false;
        }
    }
    #endregion

}