using LogiApp.Servicios;
using System.Text.RegularExpressions;
namespace LogiApp.Vistas;

public partial class PaginaRegistro : ContentPage
{
   
    private ConsultasGoogleSheets _sheets;
    private ConsultasGoogleDrive _drive;
    private readonly ConexionGoogleAuth _authService = new();
    public bool IsNotBusy => !IsBusy;
    private bool _estaProcesando = false; // Bloqueo manual de seguridad
                                          // 1. Al inicio de la clase, añade este diccionario
    private Dictionary<string, string> _rutasArchivos = new Dictionary<string, string>();
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
        // Esto regresa a la pantalla anterior en la pila de navegación
        Application.Current.MainPage = new NavigationPage(new Vistas.PaginaLogeo());
    }

    // --- MÉTODOS PARA CARGA DE ARCHIVOS

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

    // 2. Actualiza el método auxiliar para guardar la ruta
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
                // Guardamos la ruta física del PDF
                _rutasArchivos[tipoDocumento] = resultado.FullPath;
                await DisplayAlert("Listo", $"{tipoDocumento} seleccionado.", "OK");
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", ex.Message, "Cerrar");
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
        // 1. VALIDACIÓN DE CAMPOS VACÍOS BÁSICOS
        if (string.IsNullOrWhiteSpace(NombreEntry.Text) || string.IsNullOrWhiteSpace(EdadEntry.Text))
        {
            await DisplayAlert("Campos Incompletos", "Nombre y Edad son obligatorios.", "Revisar");
            return;
        }

        // 2. VALIDACIÓN DE CORREO ELECTRÓNICO
        string emailPattern = @"^[^@\s]+@[^@\s]+\.[^@\s]+$";
        if (!string.IsNullOrWhiteSpace(CorreoEntry.Text) && !Regex.IsMatch(CorreoEntry.Text, emailPattern))
        {
            await DisplayAlert("Correo Inválido", "El formato del correo electrónico no es correcto.", "Corregir");
            return;
        }

        // 3. VALIDACIÓN DE TELÉFONOS (10 DÍGITOS)
        string telPattern = @"^\d{10}$";
        if (!Regex.IsMatch(Tel1Entry.Text ?? "", telPattern))
        {
            await DisplayAlert("Teléfono 1 Inválido", "El número debe tener exactamente 10 dígitos numéricos.", "Corregir");
            return;
        }

        // El segundo teléfono es opcional, pero si lo pone, debe ser válido
        if (!string.IsNullOrWhiteSpace(Tel2Entry.Text) && !Regex.IsMatch(Tel2Entry.Text, telPattern))
        {
            await DisplayAlert("Teléfono 2 Inválido", "El segundo número también debe ser de 10 dígitos.", "Corregir");
            return;
        }

        // 4. VALIDACIÓN DE CLAVE INTERBANCARIA (18 DÍGITOS)
        string clavePattern = @"^\d{18}$";
        if (!Regex.IsMatch(ClaveInterbancariaEntry.Text ?? "", clavePattern))
        {
            await DisplayAlert("Clave Inválida", "La clave interbancaria debe tener exactamente 18 dígitos.", "Corregir");
            return;
        }

        if (_estaProcesando) return; // Si ya hay un proceso en curso, ignora este clic

        try
        {
            _estaProcesando = true;
            IsBusy = true;
            OnPropertyChanged(nameof(IsNotBusy));

            string idOp = await _sheets.GenerarIdUnicoFormateadoAsync();
            string folderId = await _drive.CrearEstructuraOperadorAsync($"{idOp} - {NombreEntry.Text}");

            // --- PROCESO DE SUBIDA DE ARCHIVOS ---
            var linksDrive = new Dictionary<string, string>();
            foreach (var item in _rutasArchivos)
            {
                // Subimos y generamos el link público/clicable
                string fileId = await _drive.SubirArchivoAsync(item.Value, item.Key, folderId);
                linksDrive[item.Key] = $"https://drive.google.com/open?id={fileId}";
            }

            // --- REGISTRO EN SHEETS CON LINKS REALES ---
            await _sheets.RegistrarOperadorAsync(
                idOp,
                NombreEntry.Text,
                EdadEntry.Text,
                CorreoEntry.Text ?? "",
                Tel1Entry.Text,
                Tel2Entry.Text ?? "",
                ClaveInterbancariaEntry.Text,
                VigenciaLicenciaPicker.Date.ToString("d"),
                linksDrive.GetValueOrDefault("Licencia Federal", "Pendiente"),
                VigenciaMedicoPicker.Date.ToString("d"),
                linksDrive.GetValueOrDefault("Comprobante de Domicilio", "Pendiente"),
                linksDrive.GetValueOrDefault("RFC / Cédula Fiscal", "Pendiente"),
                linksDrive.GetValueOrDefault("CURP", "Pendiente"),
                linksDrive.GetValueOrDefault("INE", "Pendiente"),
                linksDrive.GetValueOrDefault("Acta de Nacimiento", "Pendiente"),
                linksDrive.GetValueOrDefault("NSS", "Pendiente"),
                linksDrive.GetValueOrDefault("CV", "Pendiente")
            );

            await DisplayAlert("Éxito", "Operador y documentos registrados correctamente.", "OK");
            _rutasArchivos.Clear();
            await Navigation.PopAsync();
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