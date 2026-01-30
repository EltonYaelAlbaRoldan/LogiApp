namespace LogiApp.Vistas;

public partial class PaginaViajes : ContentPage
{
	public PaginaViajes()
	{
		InitializeComponent();
	}
    private async void OnVolverClicked(object sender, EventArgs e)
    {
        // Regresa al menú principal
        Application.Current.MainPage = new NavigationPage(new PaginaMenu());
    }

    private async void OnGuardarViajeClicked(object sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(OrigenEntry.Text) || string.IsNullOrWhiteSpace(DestinoEntry.Text))
        {
            await DisplayAlert("Atención", "Todos los campos de la ruta son obligatorios.", "OK");
            return;
        }

        // Aquí implementarás el RegistrarViajeAsync en tu servicio de Sheets después
        await DisplayAlert("Éxito", "Viaje registrado e iniciado correctamente.", "OK");
        await Navigation.PopAsync();
    }
}