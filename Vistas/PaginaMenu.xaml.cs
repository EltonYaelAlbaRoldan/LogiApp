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
    /// Dirige al usuario al módulo de Gestión de Viajes.
    /// </summary>
    private async void OnViajesClicked(object sender, EventArgs e)
    {
        // Establece la página de Viajes como nueva raíz de navegación
        Application.Current.MainPage = new NavigationPage(new Vistas.PaginaViajes());
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
}