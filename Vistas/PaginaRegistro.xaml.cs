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
        if (string.IsNullOrWhiteSpace(NombreEntry.Text)) return;

        try
        {
            // Ahora crea la carpeta principal y las dos internas automáticamente
            string folderId = await _drive.CrearEstructuraOperadorAsync(NombreEntry.Text);

            // El ID principal sigue siendo el que guardamos en Sheets para referencia
            await _sheets.RegistrarOperadorAsync(NombreEntry.Text, EdadEntry.Text, folderId);

            await DisplayAlert("Éxito", $"Operador {NombreEntry.Text} registrado con carpetas de documentos y evidencias", "OK");

            NombreEntry.Text = string.Empty;
            EdadEntry.Text = string.Empty;
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", ex.Message, "Cerrar");
        }
    }
}