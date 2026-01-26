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
        // 1. Validaciones de Interfaz (Falla rápido)
        if (string.IsNullOrWhiteSpace(NombreEntry.Text))
        {
            await DisplayAlert("Validación", "El nombre es obligatorio.", "Aceptar");
            return;
        }

        if (!int.TryParse(EdadEntry.Text, out int edad) || edad <= 0 || edad >= 100)
        {
            await DisplayAlert("Dato Inválido", "La edad debe ser entre 1 y 99 años.", "Corregir");
            return;
        }

        try
        {
            // Bloqueamos la UI para evitar doble clic
            // IsBusy = true; 

            // 2. Generar el ID con el formato OP-XXX (Evita repetidos)
            string idFormateado = await _sheets.GenerarIdUnicoFormateadoAsync();

            // 3. Crear la estructura de carpetas en Drive con ese ID
            // Es mejor usar el ID en el nombre de la carpeta para organización
            string nombreCarpeta = $"{idFormateado} - {NombreEntry.Text}";
            string folderId = await _drive.CrearEstructuraOperadorAsync(nombreCarpeta);

            // 4. Registrar todo en Google Sheets (Base de Datos)
            await _sheets.RegistrarOperadorAsync(NombreEntry.Text, EdadEntry.Text, folderId, idFormateado);

            await DisplayAlert("Éxito", $"Operador registrado con Folio: {idFormateado}", "OK");

            // Limpieza
            NombreEntry.Text = string.Empty;
            EdadEntry.Text = string.Empty;
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error de Registro", ex.Message, "Cerrar");
        }
        finally
        {
            // IsBusy = false;
        }
    }
}