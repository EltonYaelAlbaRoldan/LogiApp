using LogiApp.Servicios;

namespace LogiApp.Vistas;

public partial class PaginaRegistro : ContentPage
{
    // Usamos los nombres de clase correctos de tus archivos de servicio
    private ConsultasGoogleSheets _sheets;
    private ConsultasGoogleDrive _drive;
    private readonly ConexionGoogleAuth _authService = new();

    public PaginaRegistro()
    {
        InitializeComponent(); // Esto ya no debería dar error si el XAML está bien
        InicializarConexion();

    }

    private async void InicializarConexion()
    {
        try
        {
            // Obtenemos los servicios reales desde el JSON
            var (sService, dService) = await _authService.ObtenerServiciosAsync();

            // Ahora sí, inicializamos tus consultas con servicios reales
            _sheets = new ConsultasGoogleSheets(sService);
            _drive = new ConsultasGoogleDrive(dService);
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error de Auth", ex.Message, "OK");
        }
    }

    private async void OnProbarRegistroClicked(object sender, EventArgs e)
    {
        // El nombre debe coincidir con el x:Name del XAML
        if (string.IsNullOrWhiteSpace(NombreEntry.Text))
        {
            await DisplayAlert("Campo vacío", "Escribe un nombre", "OK");
            return;
        }

        try
        {
            // Lógica de prueba dinámica
            string folderId = await _drive.CrearCarpetaOperadorAsync(NombreEntry.Text);
            await _sheets.RegistrarOperadorAsync(NombreEntry.Text, folderId);

            await DisplayAlert("Éxito", "Operador y carpeta creados", "OK");
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", ex.Message, "Cerrar");
        }
    }
}