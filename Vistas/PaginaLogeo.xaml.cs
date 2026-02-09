using Microsoft.Maui.Controls;

namespace LogiApp.Vistas;

/// <summary>
/// Pantalla de bienvenida principal que permite al usuario elegir entre iniciar sesión o registrarse.
/// </summary>
public partial class PaginaLogeo : ContentPage
{
    #region Constructor

    public PaginaLogeo()
    {
        InitializeComponent();
    }

    #endregion

    #region Eventos de Navegación

    /// <summary>
    /// Redirige al usuario a la pantalla de Inicio de Sesión.
    /// </summary>
    /// <remarks>Reinicia la raíz de navegación para asegurar una pila limpia.</remarks>
    private async void OnLoginClicked(object sender, EventArgs e)
    {
        // Reiniciamos la raíz de navegación
        Application.Current.MainPage = new NavigationPage(new Vistas.InicioSesion());
    }

    /// <summary>
    /// Redirige al usuario a la pantalla de Registro de Operadores.
    /// </summary>
    /// <remarks>Establece la PaginaRegistro como nueva raíz de navegación.</remarks>
    private async void OnRegisterClicked(object sender, EventArgs e)
    {
        // Se establece la PaginaRegistro como nueva raíz
        Application.Current.MainPage = new NavigationPage(new Vistas.PaginaRegistro());
    }

    #endregion
}