using LogiApp.Servicios;
using Microsoft.Maui.Controls;
using System.Text.RegularExpressions;

namespace LogiApp.Vistas;

// Pantalla de registro integral que gestiona la creación de perfiles en Sheets, 
// carpetas en Drive y subida de documentación obligatoria en PDF.
public partial class PaginaRegistro : ContentPage
{
    #region Atributos y Servicios

    private ConsultasGoogleSheets _sheets;
    private ConsultasGoogleDrive _drive;
    private readonly ServicioEmail _emailService = new ServicioEmail();
    private readonly ConexionGoogleAuth _authService = new();

    // Estado de procesamiento
    public bool IsNotBusy => !IsBusy;
    private bool _estaProcesando = false;

    // Almacenamiento temporal de datos de registro
    private Dictionary<string, string> _rutasArchivos = new Dictionary<string, string>();
    private string _folderIdDestino = "";
    private string _idOperadorGenerado = "";

    #endregion

    #region Constructor

    public PaginaRegistro()
    {
        InitializeComponent();
        InicializarConexion();

        // Recuperación de sesión pendiente si el proceso se interrumpió
        if (Preferences.ContainsKey("IdPendiente"))
        {
            _idOperadorGenerado = Preferences.Get("IdPendiente", "");
            _folderIdDestino = Preferences.Get("CarpetaPendiente", "");

            // Habilitar Fase 2 (Documentos) inmediatamente
            SeccionDocumentos.IsEnabled = true;
            SeccionDocumentos.Opacity = 1.0;
            BtnSubirArchivos.IsEnabled = true;

            // Bloquear Fase 1 (Datos textuales)
            BloquearFormulario(true);
            BtnRegistrarDatos.IsEnabled = false;
            BtnRegistrarDatos.Opacity = 0.5;

            NombreEntry.Text = Preferences.Get("NombrePendiente", "");
        }
    }

    #endregion

    #region Configuración Inicial

    // Establece la conexión con los servicios de API de Google Sheets y Drive.
    private async void InicializarConexion()
    {
        try
        {
            var (sService, dService) = await _authService.ObtenerServiciosAsync();
            _sheets = new ConsultasGoogleSheets(sService);
            _drive = new ConsultasGoogleDrive(dService);
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error de Auth", ex.Message, "OK");
        }
    }

    #endregion

    #region Lógica de Validación

    // Verifica que la vigencia de los documentos sea mayor a 90 días naturales.
    private bool ValidarFecha90Dias(DateTime fecha, string tipo)
    {
        double diasRestantes = (fecha - DateTime.Now).TotalDays;

        if (diasRestantes <= 90)
        {
            DisplayAlert("⚠️ FECHA NO PERMITIDA",
                $"La vigencia de {tipo} debe ser mayor a 90 días (3 meses). " +
                $"Faltan {(int)diasRestantes} días.", "Entendido");
            return false;
        }
        return true;
    }

    // Valida que el texto ingresado cumpla con una longitud de caracteres exacta.
    private bool ValidarLongitudExacta(string texto, string nombreCampo, int largoRequerido)
    {
        if (string.IsNullOrEmpty(texto) || texto.Length != largoRequerido)
        {
            DisplayAlert("Formato Incorrecto", $"El campo {nombreCampo} debe tener exactamente {largoRequerido} dígitos.", "Reintentar");
            return false;
        }
        return true;
    }

    #endregion

    #region Procesos de Registro (Fase 1 y 2)

    // PASO 1: Registro de datos en Sheets y creación de estructura de carpetas en Drive.
    private async void OnProbarRegistroClicked(object sender, EventArgs e)
    {
        // 1. Validación de campos obligatorios
        if (string.IsNullOrWhiteSpace(NombreEntry.Text) ||
            string.IsNullOrWhiteSpace(NumLicenciaEntry.Text) ||
            string.IsNullOrWhiteSpace(ContrasenaOperadorEntry.Text) ||
            string.IsNullOrWhiteSpace(CorreoEntry.Text) ||
            string.IsNullOrWhiteSpace(ClaveInterbancariaEntry.Text))
        {
            await DisplayAlert("Campos Obligatorios", "Rellena todos los campos marcados con (*) antes de continuar.", "Entendido");
            return;
        }

        // 2. Validación de mayoría de edad
        if (int.TryParse(EdadEntry.Text, out int edadActual) && edadActual < 18)
        {
            await DisplayAlert("Registro Denegado", "El operador debe ser mayor de 18 años.", "OK");
            return;
        }

        // 3. Validación de formato de correo y longitudes
        string emailPattern = @"^[^@\s]+@[^@\s]+\.[^@\s]+$";
        if (!string.IsNullOrWhiteSpace(CorreoEntry.Text) && !Regex.IsMatch(CorreoEntry.Text, emailPattern))
        {
            await DisplayAlert("Correo Inválido", "Por favor ingresa un correo electrónico real.", "OK");
            return;
        }

        if (!ValidarLongitudExacta(Tel1Entry.Text, "Teléfono", 10) ||
            !ValidarLongitudExacta(ClaveInterbancariaEntry.Text, "Clave Interbancaria", 18))
        {
            return;
        }

        // 4. Validación de fechas (Regla de 90 días)
        if (!ValidarFecha90Dias(VigenciaLicenciaPicker.Date, "Licencia"))
        {
            return;
        }
        if (!ValidarFecha90Dias(VigenciaMedicoPicker.Date, "Apto Médico"))
        {
            return;
        }

        if (_estaProcesando) return;

        try
        {
            _estaProcesando = true;
            IsBusy = true;
            OnPropertyChanged(nameof(IsNotBusy));

            // Generación de identidad y estructura en nube
            _idOperadorGenerado = await _sheets.GenerarIdUnicoFormateadoAsync();
            _folderIdDestino = await _drive.CrearEstructuraOperadorAsync($"{_idOperadorGenerado} - {NombreEntry.Text}");
            string linkCarpeta = $"https://drive.google.com/drive/folders/{_folderIdDestino}";

            // Registro en Base de Datos (Sheets)
            await _sheets.RegistrarOperadorAsync(
                _idOperadorGenerado, NombreEntry.Text, EdadEntry.Text, CorreoEntry.Text ?? "",
                Tel1Entry.Text, Tel2Entry.Text ?? "", ClaveInterbancariaEntry.Text,
                "'" + NumLicenciaEntry.Text, VigenciaLicenciaPicker.Date.ToString("d"),
                VigenciaMedicoPicker.Date.ToString("d"), linkCarpeta, ContrasenaOperadorEntry.Text
            );

            // Persistencia de seguridad y cambio de fase en UI
            Preferences.Set("IdPendiente", _idOperadorGenerado);
            Preferences.Set("CarpetaPendiente", _folderIdDestino);
            Preferences.Set("CorreoPendiente", CorreoEntry.Text ?? "");
            Preferences.Set("NombrePendiente", NombreEntry.Text);
            Preferences.Set("PassPendiente", ContrasenaOperadorEntry.Text);

            BloquearFormulario(true);
            BtnRegistrarDatos.IsEnabled = false;
            BtnRegistrarDatos.Opacity = 0.5;

            SeccionDocumentos.IsEnabled = true;
            SeccionDocumentos.Opacity = 1.0;
            BtnSubirArchivos.IsEnabled = true;
        }
        catch (Exception ex) { await DisplayAlert("Error Crítico", ex.Message, "OK"); }
        finally
        {
            IsBusy = false;
            _estaProcesando = false;
            OnPropertyChanged(nameof(IsNotBusy));
        }
    }

    // PASO 2: Sube los archivos PDF a Drive y envía credenciales por correo electrónico.
    private async void OnSubirArchivosClicked(object sender, EventArgs e)
    {
        if (_estaProcesando) return;

        // Validación de carga completa de documentos
        string[] docs = { "Licencia Federal Doc", "Apto Medico Doc", "INE", "Acta de Nacimiento", "NSS", "CV", "Comprobante de Domicilio", "RFC / Cédula Fiscal", "CURP" };
        foreach (var doc in docs) { if (!_rutasArchivos.ContainsKey(doc)) { await DisplayAlert("Faltante", $"Selecciona: {doc}", "OK"); return; } }

        try
        {
            _estaProcesando = true;
            IsBusy = true;
            BtnSubirArchivos.IsEnabled = false;

            await DisplayAlert("Subida", "Enviando archivos a Drive...", "OK");

            foreach (var item in _rutasArchivos)
            {
                await _drive.SubirArchivoAsync(item.Value, item.Key, _folderIdDestino);
            }

            // Envío de credenciales finales
            string correo = Preferences.Get("CorreoPendiente", CorreoEntry.Text);
            string nombre = Preferences.Get("NombrePendiente", NombreEntry.Text);
            string pass = Preferences.Get("PassPendiente", ContrasenaOperadorEntry.Text);

            try { await _emailService.EnviarCredencialesAsync(correo, nombre, _idOperadorGenerado, pass); }
            catch { /* Silenciado para no bloquear finalización */ }

            await DisplayAlert("Éxito", "Expediente completo y credenciales enviadas.", "Finalizar");

            Preferences.Clear();
            _rutasArchivos.Clear();
            Application.Current.MainPage = new NavigationPage(new Vistas.PaginaLogeo());
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", ex.Message, "OK");
            BtnSubirArchivos.IsEnabled = true;
        }
        finally
        {
            _estaProcesando = false;
            IsBusy = false;
        }
    }

    #endregion

    #region Selección de Archivos

    private async void OnSubirLicenciaClicked(object sender, EventArgs e) => await SeleccionarArchivoPdfAsync("Licencia Federal Doc", BtnPdfLicencia);
    private async void OnSubirMedicoClicked(object sender, EventArgs e) => await SeleccionarArchivoPdfAsync("Apto Medico Doc", BtnPdfMedico);
    private async void OnSubirIneClicked(object sender, EventArgs e) => await SeleccionarArchivoPdfAsync("INE", BtnPdfIne);
    private async void OnSubirActaClicked(object sender, EventArgs e) => await SeleccionarArchivoPdfAsync("Acta de Nacimiento", BtnPdfActa);
    private async void OnSubirNssClicked(object sender, EventArgs e) => await SeleccionarArchivoPdfAsync("NSS", BtnPdfNss);
    private async void OnSubirCurpClicked(object sender, EventArgs e) => await SeleccionarArchivoPdfAsync("CURP", BtnPdfCurp);
    private async void OnSubirRfcClicked(object sender, EventArgs e) => await SeleccionarArchivoPdfAsync("RFC / Cédula Fiscal", BtnPdfRfc);
    private async void OnSubirDomClicked(object sender, EventArgs e) => await SeleccionarArchivoPdfAsync("Comprobante de Domicilio", BtnPdfDom);
    private async void OnSubirCvClicked(object sender, EventArgs e) => await SeleccionarArchivoPdfAsync("CV", BtnPdfCv);

    // Abre el selector de archivos y marca el botón visualmente si se elige un PDF válido.
    private async Task SeleccionarArchivoPdfAsync(string nombreDoc, Button boton)
    {
        try
        {
            var archivo = await FilePicker.Default.PickAsync(new PickOptions
            {
                PickerTitle = $"Seleccionar {nombreDoc}",
                FileTypes = FilePickerFileType.Pdf
            });

            if (archivo != null)
            {
                _rutasArchivos[nombreDoc] = archivo.FullPath;
                boton.BackgroundColor = Color.FromArgb("#C8E6C9");
                boton.TextColor = Color.FromArgb("#1B5E20");
                boton.Text = $"✅ {nombreDoc}";
            }
        }
        catch (Exception ex) { await DisplayAlert("Error", "No se pudo cargar: " + ex.Message, "OK"); }
    }

    #endregion

    #region Eventos de UI y Auxiliares

    private void OnDateSelected(object sender, DateChangedEventArgs e)
    {
    }

    private void OnTogglePasswordClicked(object sender, EventArgs e)
    {
        ContrasenaOperadorEntry.IsPassword = !ContrasenaOperadorEntry.IsPassword;
        BtnTogglePass.Text = ContrasenaOperadorEntry.IsPassword ? "Ver" : "Ocultar";
    }

    private async void OnVolverClicked(object sender, EventArgs e)
    {
        Application.Current.MainPage = new NavigationPage(new Vistas.PaginaLogeo());
    }

    // Habilita o deshabilita los controles de entrada del formulario de datos personales.
    private void BloquearFormulario(bool bloquear)
    {
        bool estado = !bloquear;
        NombreEntry.IsEnabled = estado;
        EdadEntry.IsEnabled = estado;
        CorreoEntry.IsEnabled = estado;
        Tel1Entry.IsEnabled = estado;
        Tel2Entry.IsEnabled = estado;
        ClaveInterbancariaEntry.IsEnabled = estado;
        NumLicenciaEntry.IsEnabled = estado;
        VigenciaLicenciaPicker.IsEnabled = estado;
        VigenciaMedicoPicker.IsEnabled = estado;
        ContrasenaOperadorEntry.IsEnabled = estado;
    }

    #endregion
}