using Microsoft.Maui.Controls;

namespace LogiApp.Vistas;

/// <summary>
/// Pantalla de bienvenida principal. 
/// Actúa como el enrutador inicial hacia el inicio de sesión o registro.
/// </summary>
public partial class PaginaLogeo : ContentPage
{
    // --- CONSTRUCTOR ---
    public PaginaLogeo()
    {
        InitializeComponent();
    }

    // --- EVENTOS DE NAVEGACIÓN ---

    /// <summary>
    /// Redirige al usuario a la pantalla de Inicio de Sesión.
    /// </summary>
    private async void OnLoginClicked(object sender, EventArgs e)
    {
        // Reiniciamos la raíz de navegación para asegurar una pila limpia
        Application.Current.MainPage = new NavigationPage(new Vistas.InicioSesion());
        //await DisplayAlert("Funcionalidad en Desarrollo", "La pantalla de inicio de sesión estará disponible próximamente.", "OK");
    }

    /// <summary>
    /// Redirige al usuario a la pantalla de Registro de Operadores.
    /// </summary>
    private async void OnRegisterClicked(object sender, EventArgs e)
    {
        // Se establece la PaginaRegistro como nueva raíz de navegación
        Application.Current.MainPage = new NavigationPage(new Vistas.PaginaRegistro());
    }
}