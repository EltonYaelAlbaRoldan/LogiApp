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

    /// <summary>
    /// Dirige al usuario al módulo de Gestión de Viajes.
    /// </summary>
    private async void OnViajesClicked(object sender, EventArgs e)
    {
        // Nota: Actualmente redirige al inicio, ajustar si existe una página de Viajes específica
        Application.Current.MainPage = new NavigationPage(new Vistas.PaginaLogeo());
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
            // Reiniciamos la raíz de la aplicación al portal inicial
            Application.Current.MainPage = new NavigationPage(new PaginaLogeo());
        }
    }
}