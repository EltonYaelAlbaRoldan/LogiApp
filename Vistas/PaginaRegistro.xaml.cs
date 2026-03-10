using LogiApp.Servicios;
using System.Text.RegularExpressions;

namespace LogiApp.Vistas;

/// <summary>
/// Pantalla de registro integral para nuevos operadores.
/// Gestiona la creación de perfiles en Sheets, la estructura de carpetas en Drive 
/// y la subida de documentación obligatoria en PDF.
/// </summary>
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

        // RECUPERACIÓN DE SESIÓN: Si se cerró la app durante el proceso, retomamos datos guardados
        if (Preferences.ContainsKey("IdPendiente"))
        {
            _idOperadorGenerado = Preferences.Get("IdPendiente", "");
            _folderIdDestino = Preferences.Get("CarpetaPendiente", "");

            // Configuración de UI para estado pendiente (Subida de archivos)
            BtnRegistrarDatos.IsEnabled = false;
            BtnSubirArchivos.IsEnabled = true;
            BtnVolver.IsEnabled = false;
            BloquearFormulario(true);
            BtnVolver.Opacity = 0.5;

            DisplayAlert("Registro Pendiente", "Se detectó un registro sin finalizar. Por favor, sube los archivos correspondientes.", "Continuar");
        }
    }

    #endregion

    #region Configuración y Navegación

    /// <summary>
    /// Establece la conexión con los servicios de Google (Sheets y Drive).
    /// </summary>
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

    /// <summary>
    /// Regresa a la pantalla de selección de acceso.
    /// </summary>
    private async void OnVolverClicked(object sender, EventArgs e)
    {
        Application.Current.MainPage = new NavigationPage(new Vistas.PaginaLogeo());
    }

    /// <summary>
    /// Alterna la visibilidad del campo de contraseña del operador.
    /// </summary>
    private void OnTogglePasswordClicked(object sender, EventArgs e)
    {
        ContrasenaOperadorEntry.IsPassword = !ContrasenaOperadorEntry.IsPassword;
        TogglePasswordLabel.Text = ContrasenaOperadorEntry.IsPassword ? "VER" : "OCULTAR";
    }

    #endregion

    #region Selección de Archivos PDF

    // Disparadores individuales por documento
    private async void OnSubirLicenciaClicked(object sender, EventArgs e) => await SeleccionarArchivoPdfAsync("Licencia Federal");
    private async void OnSubirIneClicked(object sender, EventArgs e) => await SeleccionarArchivoPdfAsync("INE");
    private async void OnSubirActaClicked(object sender, EventArgs e) => await SeleccionarArchivoPdfAsync("Acta de Nacimiento");
    private async void OnSubirNssClicked(object sender, EventArgs e) => await SeleccionarArchivoPdfAsync("NSS");
    private async void OnSubirCvClicked(object sender, EventArgs e) => await SeleccionarArchivoPdfAsync("CV");
    private async void OnSubirDomicilioClicked(object sender, EventArgs e) => await SeleccionarArchivoPdfAsync("Comprobante de Domicilio");
    private async void OnSubirRfcClicked(object sender, EventArgs e) => await SeleccionarArchivoPdfAsync("RFC / Cédula Fiscal");
    private async void OnSubirCurpClicked(object sender, EventArgs e) => await SeleccionarArchivoPdfAsync("CURP");
    private async void OnSubirLicenciaFederalClicked(object sender, EventArgs e) => await SeleccionarArchivoPdfAsync("Licencia Federal Doc");
    private async void OnSubirMedicoClicked(object sender, EventArgs e) => await SeleccionarArchivoPdfAsync("Apto Medico Doc");

    /// <summary>
    /// Invoca el selector de archivos del sistema filtrando únicamente documentos PDF.
    /// </summary>
    private async Task SeleccionarArchivoPdfAsync(string tipoDocumento)
    {
        try
        {
            var opciones = new PickOptions { PickerTitle = $"Selecciona {tipoDocumento}", FileTypes = FilePickerFileType.Pdf };
            var resultado = await FilePicker.Default.PickAsync(opciones);
            if (resultado != null)
            {
                _rutasArchivos[tipoDocumento] = resultado.FullPath;
                await DisplayAlert("Listo", $"{tipoDocumento} seleccionado.", "OK");
            }
        }
        catch (Exception ex) { await DisplayAlert("Error", ex.Message, "Cerrar"); }
    }

    #endregion

    #region Procesos de Registro (Paso 1 y 2)

    /// <summary>
    /// PASO 1: Registra los datos textuales en Sheets y crea la estructura de carpetas en Drive.
    /// </summary>
    private async void OnProbarRegistroClicked(object sender, EventArgs e)
    {
        // Validación básica (mantener resto de validaciones manuales aquí)
        if (string.IsNullOrWhiteSpace(NombreEntry.Text)) return;
        if (_estaProcesando) return;

        try
        {
            _estaProcesando = true;
            IsBusy = true;
            BloquearFormulario(true);
            OnPropertyChanged(nameof(IsNotBusy));

            // Generación de identidad y almacenamiento en la nube
            _idOperadorGenerado = await _sheets.GenerarIdUnicoFormateadoAsync();
            _folderIdDestino = await _drive.CrearEstructuraOperadorAsync($"{_idOperadorGenerado} - {NombreEntry.Text}");

            string linkCarpeta = $"https://drive.google.com/drive/folders/{_folderIdDestino}";

            // Registro en base de datos
            await _sheets.RegistrarOperadorAsync(
                _idOperadorGenerado, NombreEntry.Text, EdadEntry.Text, CorreoEntry.Text ?? "",
                Tel1Entry.Text, Tel2Entry.Text ?? "", ClaveInterbancariaEntry.Text,
                "'" + NumLicenciaEntry.Text, VigenciaLicenciaPicker.Date.ToString("d"),
                VigenciaMedicoPicker.Date.ToString("d"), linkCarpeta, ContrasenaOperadorEntry.Text
            );

            // PERSISTENCIA: Guardamos datos para evitar pérdida por cierre de App
            Preferences.Set("IdPendiente", _idOperadorGenerado);
            Preferences.Set("CarpetaPendiente", _folderIdDestino);
            Preferences.Set("CorreoPendiente", CorreoEntry.Text);
            Preferences.Set("NombrePendiente", NombreEntry.Text);
            Preferences.Set("PassPendiente", ContrasenaOperadorEntry.Text);

            await DisplayAlert("Paso 1 Exitoso", "Datos guardados. Sube tus documentos para finalizar.", "Siguiente");

            BtnSubirArchivos.IsEnabled = true;
            BtnRegistrarDatos.IsEnabled = false;
            BtnVolver.IsEnabled = false;
            BtnVolver.Opacity = 0.5;
        }
        catch (Exception ex) { await DisplayAlert("Error", ex.Message, "OK"); }
        finally
        {
            IsBusy = false;
            _estaProcesando = false;
            OnPropertyChanged(nameof(IsNotBusy));
        }
    }

    /// <summary>
    /// PASO 2: Sube los archivos seleccionados a la carpeta de Drive y envía credenciales por correo.
    /// </summary>
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

            // Subida masiva de archivos
            foreach (var item in _rutasArchivos)
            {
                await _drive.SubirArchivoAsync(item.Value, item.Key, _folderIdDestino);
            }

            // Recuperación de datos para el envío de correo
            string correo = Preferences.Get("CorreoPendiente", CorreoEntry.Text);
            string nombre = Preferences.Get("NombrePendiente", NombreEntry.Text);
            string pass = Preferences.Get("PassPendiente", ContrasenaOperadorEntry.Text);

            try
            {
                await _emailService.EnviarCredencialesAsync(correo, nombre, _idOperadorGenerado, pass);
            }
            catch { /* Fallo en correo es silenciado para no bloquear el flujo */ }

            await DisplayAlert("Éxito", "Expediente completo y credenciales enviadas.", "Finalizar");

            // LIMPIEZA: El proceso terminó con éxito
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

    #region Métodos Auxiliares de UI

    /// <summary>
    /// Habilita o deshabilita todos los controles de entrada del formulario.
    /// </summary>
    /// <param name="bloquear">True para deshabilitar, False para habilitar.</param>
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