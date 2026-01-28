namespace LogiApp.Vistas;

public partial class PaginaMenu : ContentPage
{
    public PaginaMenu()
    {
        InitializeComponent();
    }

    private async void OnViajesClicked(object sender, EventArgs e)
    {
        // Navega a la página de registro
        Application.Current.MainPage = new NavigationPage(new Vistas.PaginaLogeo());
    }

    private async void OnMantenimientoClicked(object sender, EventArgs e)
    {
        // Navega a la página de Mantenimiento
        Application.Current.MainPage = new NavigationPage(new Vistas.PaginaMantenimiento());
    }

    private async void OnLogoutClicked(object sender, EventArgs e)
    {
        // Regresa al Login reiniciando la página principal
        bool answer = await DisplayAlert("Serrar sesion", "¿Desea Serrar la sesion?", "Sí", "No");
        if (answer)
        {
            Application.Current.MainPage = new NavigationPage(new PaginaLogeo());
        }
    }
}