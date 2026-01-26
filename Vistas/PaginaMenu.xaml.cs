namespace LogiApp.Vistas;

public partial class PaginaMenu : ContentPage
{
    public PaginaMenu()
    {
        InitializeComponent();
    }

    private async void OnViajesClicked(object sender, EventArgs e)
    {
        // Navega a la página de registro que ya tienes lista
        Application.Current.MainPage = new NavigationPage(new Vistas.PaginaLogeo());
    }

    private async void OnMantenimientoClicked(object sender, EventArgs e)
    {
        // Aquí podrías crear una nueva página llamada PaginaMantenimiento
        await DisplayAlert("Mantenimiento", "Módulo en desarrollo", "OK");
    }
}