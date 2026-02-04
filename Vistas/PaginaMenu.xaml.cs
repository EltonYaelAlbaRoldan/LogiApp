using Microsoft.Maui.Controls;

namespace LogiApp.Vistas;

/// <summary>
/// Panel principal de control después del acceso. 
/// Permite navegar a los módulos de Viajes, Mantenimiento y cerrar la sesión.
/// </summary>
public partial class PaginaMenu : ContentPage
{
    // --- CONSTRUCTOR ---
    public PaginaMenu()
    {
        InitializeComponent();
    }

    // --- GESTIÓN DE MÓDULOS (NAVEGACIÓN) ---

    private async void OnPerfilClicked(object sender, EventArgs e)
    {
        // Navegación a la página de Perfil (Asegúrate de que el nombre coincida)
        await Navigation.PushAsync(new Vistas.PaginaPerfil());
    }

    // Dirige al usuario al módulo de Gestión de Viajes.
    private async void OnViajesClicked(object sender, EventArgs e)
    {
        // Cambia a la nueva página de Viajes
        Application.Current.MainPage = new NavigationPage(new Vistas.PaginaViajes());
    }

    /// <summary>
    /// Dirige al usuario al módulo de Mantenimiento de unidades.
    /// </summary>
    private async void OnMantenimientoClicked(object sender, EventArgs e)
    {
        Application.Current.MainPage = new NavigationPage(new Vistas.PaginaMantenimiento());
    }

    // ---  CERRAR SESIÓN ---

    /// <summary>
    /// Solicita confirmación y cierra la sesión actual regresando al portal de acceso.
    /// </summary>
    private async void OnLogoutClicked(object sender, EventArgs e)
    {
        // "Cerrar sesión"
        bool deseaSalir = await DisplayAlert("Cerrar Sesión", "¿Está seguro que desea cerrar la sesión?", "Sí", "No");

        if (deseaSalir)
        {
            // Solo borramos los datos de la sesión y registros pendientes
            Preferences.Default.Remove("UsuarioLogeado");
            Preferences.Default.Remove("IdPendiente");
            Preferences.Default.Remove("CarpetaPendiente");

            // Regresamos a la página de logeo
            Application.Current.MainPage = new NavigationPage(new Vistas.PaginaLogeo());
        }
    }

}