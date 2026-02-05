namespace LogiApp.Vistas;

public partial class PaginaViajes : ContentPage
{
	public PaginaViajes()
	{
		InitializeComponent();
	}

    private async void OnVolverClicked(object sender, EventArgs e)
    {
        // Regresa a la página anterior de forma segura
        Application.Current.MainPage = new NavigationPage(new Vistas.PaginaMenu());
    }

    private async void OnGuardarViajeClicked(object sender, EventArgs e)
    {
        // 1. Validación de campos vacíos
        if (string.IsNullOrWhiteSpace(OrigenEntry.Text) ||
            string.IsNullOrWhiteSpace(DestinoEntry.Text) ||
            string.IsNullOrWhiteSpace(ClienteEntry.Text) ||
            string.IsNullOrWhiteSpace(UnidadEntry.Text) ||
            string.IsNullOrWhiteSpace(KmSalidaEntry.Text))
        {
            await DisplayAlert("Datos Incompletos", "Por favor, completa todos los campos para continuar.", "Aceptar");
            return;
        }

        // 2. Mostrar indicador de carga (Opcional pero recomendado)
        BtnGuardarViaje.IsEnabled = false;
        BtnGuardarViaje.Text = "GUARDANDO...";

        try
        {
            // Aquí llamarás a tu servicio de Google Sheets próximamente
            // Ejemplo: await _googleSheetsService.RegistrarViajeAsync(...);

            await DisplayAlert("Éxito", "Viaje registrado e iniciado correctamente.", "OK");

            // Regresar al menú
            await Navigation.PopAsync();
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"No se pudo guardar: {ex.Message}", "OK");
        }
        finally
        {
            BtnGuardarViaje.IsEnabled = true;
            BtnGuardarViaje.Text = "INICIAR VIAJE";
        }
    }
}