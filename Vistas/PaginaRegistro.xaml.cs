using LogiApp.Servicios;
using System.Text.RegularExpressions;
namespace LogiApp.Vistas;

public partial class PaginaRegistro : ContentPage
{
    // Servicios de Google
    private ConsultasGoogleSheets _sheets;
    private ConsultasGoogleDrive _drive;
    private readonly ConexionGoogleAuth _authService = new();
    public bool IsNotBusy => !IsBusy; // Propiedad para enlazar con la UI
    private bool _estaProcesando = false; // Bloqueo manual de seguridad
                                          
    private Dictionary<string, string> _rutasArchivos = new Dictionary<string, string>(); // Almacena las rutas físicas de los PDFs seleccionados

    // Variables para el ID y carpeta del operador
    private string _folderIdDestino = "";
    private string _idOperadorGenerado = "";

    // Constructor
    public PaginaRegistro()
    {
        InitializeComponent(); 
        InicializarConexion();

    }

    // Inicialización de servicios Google
    private async void InicializarConexion()
    {
        try
        {
            // Autenticación y obtención de servicios
            var (sService, dService) = await _authService.ObtenerServiciosAsync();
            _sheets = new ConsultasGoogleSheets(sService);
            _drive = new ConsultasGoogleDrive(dService);
        }
        // Error general de autenticación
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

    // --- MÉTODO PARA TOGGLE DE CONTRASEÑA ---
    private void OnTogglePasswordClicked(object sender, EventArgs e)
    {
        ContrasenaOperadorEntry.IsPassword = !ContrasenaOperadorEntry.IsPassword;
        TogglePasswordLabel.Text = ContrasenaOperadorEntry.IsPassword ? "VER" : "OCULTAR";
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

    // Método genérico para seleccionar un archivo PDF
    private async Task SeleccionarArchivoPdfAsync(string tipoDocumento)
    {
        try
        {
            // Configuramos las opciones del selector de archivos
            var opciones = new PickOptions
            {
                PickerTitle = $"Selecciona tu {tipoDocumento} (PDF)",
                FileTypes = FilePickerFileType.Pdf
            };

            // Abrimos el selector de archivos
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
            // Error al seleccionar el archivo
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

    // --- MÉTODO PRINCIPAL DE REGISTRO ---
    private async void OnProbarRegistroClicked(object sender, EventArgs e)
    {
        // Primero, validamos la conexión y estructura de Sheets
        string diagnostico = await _sheets.ValidarEstructuraAsync();
        await DisplayAlert("Diagnóstico de Conexión", diagnostico, "OK");

        // Si todo está bien, procedemos con las validaciones de campos
        if (diagnostico.Contains("✅"))
        {
            // --- CAPA 1: VALIDACIÓN DE CAMPOS VACÍOS ---
            if (string.IsNullOrWhiteSpace(NombreEntry.Text) || string.IsNullOrWhiteSpace(EdadEntry.Text))
            {
                await DisplayAlert("Campos Incompletos", "Todos los campos son obligatorios para el registro.", "Revisar");
                return;
            }

            // --- CAPA 2: VALIDACIÓN DE FORMATO (EDAD) ---
            if (!int.TryParse(EdadEntry.Text, out int edad))
            {
                await DisplayAlert("Error de Formato", "La edad debe ser un número entero.", "Corregir");
                return;
            }

            // --- CAPA 3: LÓGICA DE NEGOCIO (RANGOS DE EDAD) ---
            if (edad < 18 || edad > 99)
            {
                await DisplayAlert("Edad no permitida", "El operador debe tener entre 18 y 99 años.", "OK");
                return;
            }

            // --- CAPA 4: REGLAS DE SEGURIDAD (NOMBRE) ---
            if (NombreEntry.Text.Trim().Length < 3)
            {
                await DisplayAlert("Nombre Inválido", "Ingresa el nombre completo del operador.", "OK");
                return;
            }

            // --- CAPA 5: VALIDACIONES DE CORREO ---
            string emailPattern = @"^[^@\s]+@[^@\s]+\.[^@\s]+$";
            if (!string.IsNullOrWhiteSpace(CorreoEntry.Text) && !Regex.IsMatch(CorreoEntry.Text, emailPattern))
            {
                await DisplayAlert("Correo Inválido", "El formato del correo electrónico no es correcto.", "Corregir");
                return;
            }

            // --- CAPA 6: VERIFICAR TELÉFONO ---
            string telPattern = @"^\d{10}$";
            if (!Regex.IsMatch(Tel1Entry.Text ?? "", telPattern))
            {
                await DisplayAlert("Teléfono 1 Inválido", "El número debe tener exactamente 10 dígitos numéricos.", "Corregir");
                return;
            }
            // El segundo teléfono es opcional, pero si lo pone, debe ser válido
            if (!string.IsNullOrWhiteSpace(Tel2Entry.Text) && !Regex.IsMatch(Tel2Entry.Text, telPattern))
            {
                await DisplayAlert("Teléfono 2 Inválido", "\"El segundo número debe tener exactamente 10 dígitos numéricos.", "Corregir");
                return;
            }

            // -- CAPA 7: VALIDACIÓN DE CLAVE INTERBANCARIA (18 DÍGITOS) --
            string clavePattern = @"^\d{18}$";
            if (!Regex.IsMatch(ClaveInterbancariaEntry.Text ?? "", clavePattern))
            {
                await DisplayAlert("Clave Inválida", "La clave interbancaria debe tener exactamente 18 dígitos.", "Corregir");
                return;
            }

            // -- CAPA 8: VALIDACIÓN DE NÚMERO DE LICENCIA ---
            if (string.IsNullOrWhiteSpace(NumLicenciaEntry.Text))
            {
                await DisplayAlert("Campo Incompleto", "El número de licencia es obligatorio.", "Revisar");
                return;
            }

            // --- CAPA 9: LA CONTRASEÑA NO PUEDE ESTAR VACÍA ---
            if (string.IsNullOrWhiteSpace(ContrasenaOperadorEntry.Text))
            {
                await DisplayAlert("Campo Incompleto", "Debes asignar una contraseña al operador.", "OK");
                return;
            }

            // Evitar clics dobles si ya se está procesando
            if (_estaProcesando) return;

            try
            {
                // Bloqueamos todo
                _estaProcesando = true;
                IsBusy = true;
                OnPropertyChanged(nameof(IsNotBusy));
                // Desactivamos el botón físicamente
                await DisplayAlert("Registrando", "Estamos creando el folio y el expediente del operador. Espera un momento...", "Entendido");

                // Generar ID y Carpeta principal
                _idOperadorGenerado = await _sheets.GenerarIdUnicoFormateadoAsync();
                _folderIdDestino = await _drive.CrearEstructuraOperadorAsync($"{_idOperadorGenerado} - {NombreEntry.Text}");

                // Generamos el link directo a la carpeta de Drive
                string linkCarpeta = $"https://drive.google.com/drive/folders/{_folderIdDestino}";

                // Registro en Sheets (Sin los "Pendiente", solo datos y el link de la carpeta)
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
                    linkCarpeta,
                    ContrasenaOperadorEntry.Text
                );

                // Éxito en el primer paso
                await DisplayAlert("Paso 1 Exitoso", $"Folio {_idOperadorGenerado} creado. Procede a subir los PDF.", "Siguiente");
                // Habilitamos el botón de subir archivos y deshabilitamos el de registrar datos
                BtnSubirArchivos.IsEnabled = true;
                BtnRegistrarDatos.IsEnabled = false;
            }
            catch (Exception ex)
            {
                // Error general de registro
                await DisplayAlert("¡ERROR DETECTADO!", ex.ToString(), "OK");
            }
            finally
            {
                // Liberamos los bloqueos
                IsBusy = false;
                _estaProcesando = false;
                OnPropertyChanged(nameof(IsNotBusy));
            }
        }
        
    }

    private async void OnSubirArchivosClicked(object sender, EventArgs e)
    {
        // Evitar clics dobles si ya se está procesando
        if (_estaProcesando) return;
        string[] documentosObligatorios = {
            "Licencia Federal Doc", "INE", "Acta de Nacimiento",
            "NSS", "CV", "Comprobante de Domicilio", "RFC / Cédula Fiscal", "CURP"
        };

        // Verificamos que todos los documentos obligatorios estén seleccionados
        foreach (var doc in documentosObligatorios)
        {
            if (!_rutasArchivos.ContainsKey(doc))
            {
                await DisplayAlert("Documento Faltante", $"Por favor, selecciona: {doc}", "Entendido");
                return;
            }
        }

        try
        {
            // Bloqueamos todo
            _estaProcesando = true;
            IsBusy = true;
            BtnSubirArchivos.IsEnabled = false; // Desactivar botón físicamente
            OnPropertyChanged(nameof(IsNotBusy));

            await DisplayAlert("Subida en Progreso", "Enviando expediente a Drive. No cierres la App...", "Entendido");

            foreach (var item in _rutasArchivos)
            {
                // Subimos cada archivo a la carpeta del operador
                await _drive.SubirArchivoAsync(item.Value, item.Key, _folderIdDestino);
            }
            await DisplayAlert("Éxito Total", "Expediente digital completado correctamente.", "Finalizar");
            // Limpiamos las rutas almacenadas
            _rutasArchivos.Clear();

            // Regreso automático al logeo tras el OK
            Application.Current.MainPage = new NavigationPage(new Vistas.PaginaLogeo());
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error de Subida", "Hubo un problema: " + ex.Message, "Cerrar");
            // En caso de error, reactivamos para que pueda reintentar
            BtnSubirArchivos.IsEnabled = true;
        }
        finally
        {
            // Liberamos los bloqueos
            _estaProcesando = false;
            IsBusy = false;
            OnPropertyChanged(nameof(IsNotBusy));
        }
    }


}