using LogiApp.Servicios;

namespace LogiApp.Vistas;

public partial class PaginaRegistro : ContentPage
{
    // Usamos los nombres de clase correctos de tus archivos de servicio
    private ConsultasGoogleSheets _sheets;
    private ConsultasGoogleDrive _drive;
    private readonly ConexionGoogleAuth _authService = new();
    public bool IsNotBusy => !IsBusy;
    private bool _estaProcesando = false; // Bloqueo manual de seguridad
    public PaginaRegistro()
    {
        InitializeComponent(); 
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
    private async void OnVolverClicked(object sender, EventArgs e)
    {
        // Esto te regresa a la pantalla anterior en la pila de navegación
        Application.Current.MainPage = new NavigationPage(new Vistas.PaginaLogeo());
    }

    // --- MÉTODOS PARA CARGA DE ARCHIVOS (FEBRERO SEMANA 3) ---

    private async void OnSubirLicenciaClicked(object sender, EventArgs e)
    {
        await SeleccionarArchivoPdfAsync("Licencia Federal");
    }

    private async void OnSubirIneClicked(object sender, EventArgs e)
    {
        await SeleccionarArchivoPdfAsync("INE");
    }

    private async void OnSubirActaClicked(object sender, EventArgs e)
    {
        await SeleccionarArchivoPdfAsync("Acta de Nacimiento");
    }

    private async void OnSubirNssClicked(object sender, EventArgs e)
    {
        await SeleccionarArchivoPdfAsync("NSS");
    }

    private async void OnSubirCvClicked(object sender, EventArgs e)
    {
        await SeleccionarArchivoPdfAsync("CV");
    }

    // Método auxiliar para no repetir código
    private async Task SeleccionarArchivoPdfAsync(string tipoDocumento)
    {
        try
        {
            var opciones = new PickOptions
            {
                PickerTitle = $"Selecciona tu {tipoDocumento} (PDF)",
                FileTypes = FilePickerFileType.Pdf
            };

            var resultado = await FilePicker.Default.PickAsync(opciones);

            if (resultado != null)
            {
                // Aquí podrías guardar la ruta en una variable para el registro final
                await DisplayAlert("Archivo Seleccionado", $"Se cargó: {resultado.FileName}", "OK");
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"No se pudo cargar el archivo: {ex.Message}", "Cerrar");
        }
    }
    // --- NUEVOS MÉTODOS PARA DATOS FISCALES ---

    private async void OnSubirDomicilioClicked(object sender, EventArgs e)
    {
        await SeleccionarArchivoPdfAsync("Comprobante de Domicilio");
    }

    private async void OnSubirRfcClicked(object sender, EventArgs e)
    {
        await SeleccionarArchivoPdfAsync("RFC / Cédula Fiscal");
    }

    private async void OnSubirCurpClicked(object sender, EventArgs e)
    {
        await SeleccionarArchivoPdfAsync("CURP");
    }
    private async void OnProbarRegistroClicked(object sender, EventArgs e)
    {
        // --- CAPA 1: VALIDACIÓN DE CAMPOS VACÍOS ---
        if (string.IsNullOrWhiteSpace(NombreEntry.Text) || string.IsNullOrWhiteSpace(EdadEntry.Text))
        {
            await DisplayAlert("Campos Incompletos", "Todos los campos son obligatorios para el registro.", "Revisar");
            return;
        }

        // --- CAPA 2: VALIDACIÓN DE FORMATO (EDAD) ---
        // Usamos int.TryParse para evitar que la app truene si meten letras
        if (!int.TryParse(EdadEntry.Text, out int edad))
        {
            await DisplayAlert("Error de Formato", "La edad debe ser un número entero.", "Corregir");
            return;
        }

        // --- CAPA 3: LÓGICA DE NEGOCIO (RANGOS) ---
        if (edad < 18 || edad > 99)
        {
            await DisplayAlert("Edad no permitida", "El operador debe tener entre 18 y 99 años.", "OK");
            return;
        }

        // --- CAPA 4: REGLAS DE SEGURIDAD (NOMBRE) ---
        // Evitamos nombres demasiado cortos o con caracteres extraños si lo deseas
        if (NombreEntry.Text.Trim().Length < 3)
        {
            await DisplayAlert("Nombre Inválido", "Ingresa el nombre completo del operador.", "OK");
            return;
        }

        // --- NUEVO: BLOQUEO ANTIDUPLICADOS ---
        if (_estaProcesando) return; // Si ya hay un proceso en curso, ignora este clic

        // --- Validaciones de campos (Nombre, Edad) ---
        if (string.IsNullOrWhiteSpace(NombreEntry.Text)) return;

        try
        {
            _estaProcesando = true; // Iniciamos el bloqueo
            IsBusy = true;
            OnPropertyChanged(nameof(IsNotBusy));

            // Ejecución de APIs
            string idFormateado = await _sheets.GenerarIdUnicoFormateadoAsync();
            string folderId = await _drive.CrearEstructuraOperadorAsync($"{idFormateado} - {NombreEntry.Text}");
            await _sheets.RegistrarOperadorAsync(NombreEntry.Text, EdadEntry.Text, folderId, idFormateado);

            await DisplayAlert("Éxito", $"Registrado con Folio: {idFormateado}", "OK");
            Application.Current.MainPage = new NavigationPage(new Vistas.PaginaLogeo());

            NombreEntry.Text = string.Empty;
            EdadEntry.Text = string.Empty;
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", ex.Message, "Cerrar");
        }
        finally
        {
            IsBusy = false;
            _estaProcesando = false; // Liberamos el bloqueo al terminar
            OnPropertyChanged(nameof(IsNotBusy));
        }
    }
}