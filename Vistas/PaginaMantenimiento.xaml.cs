namespace LogiApp.Vistas;

public partial class PaginaMantenimiento : ContentPage
{
	public PaginaMantenimiento()
	{
		InitializeComponent();
	}
    private async void OnVolverClicked(object sender, EventArgs e)
    {
        // Regresa a la pagina de menú principal
        Application.Current.MainPage = new NavigationPage(new Vistas.PaginaMenu());
    }
}