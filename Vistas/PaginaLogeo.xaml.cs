using Microsoft.Maui.Controls;

namespace LogiApp.Vistas;

// Pantalla de bienvenida principal que permite al usuario elegir entre iniciar sesión o registrarse.
public partial class PaginaLogeo : ContentPage
{
    #region Constructor

    public PaginaLogeo()
    {
        InitializeComponent();
    }

    #endregion

    #region Eventos de Navegación

    // Redirige al usuario a la pantalla de Inicio de Sesión reiniciando la pila de navegación.
    private async void OnLoginClicked(object sender, EventArgs e)
    {
        Application.Current.MainPage = new NavigationPage(new Vistas.InicioSesion());
    }

    // Redirige al usuario a la pantalla de Registro de Operadores estableciéndola como nueva raíz.
    private async void OnRegisterClicked(object sender, EventArgs e)
    {
        Application.Current.MainPage = new NavigationPage(new Vistas.PaginaRegistro());
    }

    
    #endregion
}