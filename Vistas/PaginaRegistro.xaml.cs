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

    private string _folderIdDestino = "";
    private string _idOperadorGenerado = "";

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
    private async void OnSubirLicenciaFederalClicked(object sender, EventArgs e)
    {
        await SeleccionarArchivoPdfAsync("Licencia Federal Doc");
    }
    private async void OnProbarRegistroClicked(object sender, EventArgs e)
    {

        string diagnostico = await _sheets.ValidarEstructuraAsync();
        await DisplayAlert("Diagnóstico de Conexión", diagnostico, "OK");

        if (diagnostico.Contains("✅"))
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
            
            // Validación de Número de Licencia
            if (string.IsNullOrWhiteSpace(NumLicenciaEntry.Text))
            {
                await DisplayAlert("Campo Incompleto", "El número de licencia es obligatorio.", "Revisar");
                return;
            }

            if (string.IsNullOrWhiteSpace(NombreEntry.Text) || !int.TryParse(EdadEntry.Text, out _)) return;

            if (_estaProcesando) return;

            try
            {
                _estaProcesando = true;
                IsBusy = true;
                OnPropertyChanged(nameof(IsNotBusy));

                // --- MEJORA: PANTALLA EMERGENTE INICIAL ---
                await DisplayAlert("Registrando", "Estamos creando el folio y el expediente del operador. Espera un momento...", "Entendido");

                // A. Generar ID y Carpeta principal
                _idOperadorGenerado = await _sheets.GenerarIdUnicoFormateadoAsync();
                _folderIdDestino = await _drive.CrearEstructuraOperadorAsync($"{_idOperadorGenerado} - {NombreEntry.Text}");

                // Generamos el link directo a la carpeta de Drive
                string linkCarpeta = $"https://drive.google.com/drive/folders/{_folderIdDestino}";

                // B. Registro en Sheets (Sin los "Pendiente", solo datos y el link de la carpeta)
                await _sheets.RegistrarOperadorAsync(
                    _idOperadorGenerado,
                    NombreEntry.Text,
                    EdadEntry.Text,
                    CorreoEntry.Text ?? "",
                    Tel1Entry.Text,
                    Tel2Entry.Text ?? "",
                    ClaveInterbancariaEntry.Text,
                    "'" + NumLicenciaEntry.Text,
                    VigenciaLicenciaPicker.Date.ToString("d"),
                    VigenciaMedicoPicker.Date.ToString("d"),
                    linkCarpeta
                );

                await DisplayAlert("Paso 1 Exitoso", $"Folio {_idOperadorGenerado} creado. Procede a subir los PDF.", "Siguiente");

                BtnSubirArchivos.IsEnabled = true;
                BtnRegistrarDatos.IsEnabled = false;
            }
            catch (Exception ex)
            {
                await DisplayAlert("¡ERROR DETECTADO!", ex.ToString(), "OK");
            }
            finally
            {
                IsBusy = false;
                _estaProcesando = false;
                OnPropertyChanged(nameof(IsNotBusy));
            }
        }
        
    }

    private async void OnSubirArchivosClicked(object sender, EventArgs e)
    {
        // Lista de documentos que deben estar sí o sí
        string[] documentosObligatorios = {
        "Licencia Federal Doc", "INE", "Acta de Nacimiento",
        "NSS", "CV", "Comprobante de Domicilio", "RFC / Cédula Fiscal", "CURP"
    };

        // Capa de Validación: Revisamos si falta alguno
        foreach (var doc in documentosObligatorios)
        {
            if (!_rutasArchivos.ContainsKey(doc))
            {
                await DisplayAlert("Documento Faltante", $"Por favor, selecciona el archivo de: {doc}", "Entendido");
                return; // Detiene el proceso si falta uno
            }
        }

        try
        {
            IsBusy = true;
            OnPropertyChanged(nameof(IsNotBusy));

            await DisplayAlert("Subida en Progreso", "Enviando documentos a la nube...", "Entendido");

            foreach (var item in _rutasArchivos)
            {
                await _drive.SubirArchivoAsync(item.Value, item.Key, _folderIdDestino);
            }

            // MENSAJE FINAL Y REGRESO A LOGEO
            await DisplayAlert("Éxito Total", "El expediente digital se ha creado correctamente.", "Finalizar");

            _rutasArchivos.Clear();

            // Regresamos a la página de Logeo
            Application.Current.MainPage = new NavigationPage(new Vistas.PaginaLogeo());
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error de Subida", ex.Message, "Cerrar");
        }
        finally
        {
            IsBusy = false;
            OnPropertyChanged(nameof(IsNotBusy));
        }
    }


}