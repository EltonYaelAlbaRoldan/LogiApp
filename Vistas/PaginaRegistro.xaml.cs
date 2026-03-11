using LogiApp.Servicios;
using Microsoft.Maui.Controls;
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

        // RECUPERACIÓN DE SESIÓN:
        if (Preferences.ContainsKey("IdPendiente"))
        {
            _idOperadorGenerado = Preferences.Get("IdPendiente", "");
            _folderIdDestino = Preferences.Get("CarpetaPendiente", "");

            // --- AQUÍ ESTÁ LA SOLUCIÓN ---
            // Si hay un ID pendiente, significa que la Fase 1 ya se hizo.
            // Debemos habilitar la Fase 2 inmediatamente al entrar.

            SeccionDocumentos.IsEnabled = true;
            SeccionDocumentos.Opacity = 1.0;
            BtnSubirArchivos.IsEnabled = true;

            // También bloqueamos la Fase 1 para que no intenten registrarse de nuevo
            BloquearFormulario(true);
            BtnRegistrarDatos.IsEnabled = false;
            BtnRegistrarDatos.Opacity = 0.5;

            // Opcional: Llenar el nombre para que el usuario sepa de quién es el registro
            NombreEntry.Text = Preferences.Get("NombrePendiente", "");
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
        BtnTogglePass.Text = ContrasenaOperadorEntry.IsPassword ? "Ver" : "Ocultar";
    }

    #endregion

    #region Selección de Archivos PDF

    // Métodos para cada botón (Pasamos el botón como parámetro)
    private async void OnSubirLicenciaClicked(object sender, EventArgs e) => await SeleccionarArchivoPdfAsync("Licencia Federal Doc", BtnPdfLicencia);
    private async void OnSubirMedicoClicked(object sender, EventArgs e) => await SeleccionarArchivoPdfAsync("Apto Medico Doc", BtnPdfMedico);
    private async void OnSubirIneClicked(object sender, EventArgs e) => await SeleccionarArchivoPdfAsync("INE", BtnPdfIne);
    private async void OnSubirActaClicked(object sender, EventArgs e) => await SeleccionarArchivoPdfAsync("Acta de Nacimiento", BtnPdfActa);
    private async void OnSubirNssClicked(object sender, EventArgs e) => await SeleccionarArchivoPdfAsync("NSS", BtnPdfNss);
    private async void OnSubirCurpClicked(object sender, EventArgs e) => await SeleccionarArchivoPdfAsync("CURP", BtnPdfCurp);
    private async void OnSubirRfcClicked(object sender, EventArgs e) => await SeleccionarArchivoPdfAsync("RFC / Cédula Fiscal", BtnPdfRfc);
    private async void OnSubirDomClicked(object sender, EventArgs e) => await SeleccionarArchivoPdfAsync("Comprobante de Domicilio", BtnPdfDom);
    private async void OnSubirCvClicked(object sender, EventArgs e) => await SeleccionarArchivoPdfAsync("CV", BtnPdfCv);

    /// <summary>
    /// Abre el selector de archivos y marca el botón visualmente si se elige un PDF.
    /// </summary>
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
                // Guardar ruta
                _rutasArchivos[nombreDoc] = archivo.FullPath;

                // CAMBIO VISUAL: Botón verde y check ✅
                boton.BackgroundColor = Color.FromArgb("#C8E6C9"); // Verde claro
                boton.TextColor = Color.FromArgb("#1B5E20");      // Verde oscuro
                boton.Text = $"✅ {nombreDoc}";
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", "No se pudo cargar el archivo: " + ex.Message, "OK");
        }
    }

    #endregion

    #region Validaciones de Fecha (90 días)

    private void OnDateSelected(object sender, DateChangedEventArgs e)
    {
        ValidarFecha90Dias(VigenciaLicenciaPicker.Date, "Licencia");
        ValidarFecha90Dias(VigenciaMedicoPicker.Date, "Apto Médico");
    }

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

    #endregion

    #region Procesos de Registro (Paso 1 y 2)

    /// <summary>
    /// PASO 1: Registra los datos textuales en Sheets y crea la estructura de carpetas en Drive.
    /// </summary>
    private async void OnProbarRegistroClicked(object sender, EventArgs e)
    {
        // 1. VALIDACIÓN DE CAMPOS OBLIGATORIOS
        if (string.IsNullOrWhiteSpace(NombreEntry.Text) ||
            string.IsNullOrWhiteSpace(NumLicenciaEntry.Text) ||
            string.IsNullOrWhiteSpace(ContrasenaOperadorEntry.Text) ||
            string.IsNullOrWhiteSpace(CorreoEntry.Text) ||
            string.IsNullOrWhiteSpace(ClaveInterbancariaEntry.Text))
        {
            await DisplayAlert("Campos Obligatorios",
            "Por favor, rellena todos los campos marcados con (*) antes de continuar.",
            "Entendido");
            return;
        }

        // 2. VALIDACIÓN DE MAYORÍA DE EDAD (18+)
        if (int.TryParse(EdadEntry.Text, out int edadActual))
        {
            if (edadActual < 18)
            {
                await DisplayAlert("Registro Denegado", "El operador debe ser mayor de 18 años.", "OK");
                return;
            }
        }

        // 3. Validar Correo (Regex)
        string emailPattern = @"^[^@\s]+@[^@\s]+\.[^@\s]+$";
        if (!string.IsNullOrWhiteSpace(CorreoEntry.Text) && !Regex.IsMatch(CorreoEntry.Text, emailPattern))
        {
            await DisplayAlert("Correo Inválido", "Por favor ingresa un correo electrónico real.", "OK");
            return;
        }

        if (!ValidarLongitudExacta(Tel1Entry.Text, "Teléfono", 10) ||
        !ValidarLongitudExacta(ClaveInterbancariaEntry.Text, "Clave Interbancaria", 18))
        {
            return; // Detiene el proceso si alguno falla
        }

        // 4. VALIDACIÓN DE FECHAS (Regla de 90 días)
        // Se debe validar antes de cualquier proceso en la nube
        if (!ValidarFecha90Dias(VigenciaLicenciaPicker.Date, "Licencia") ||
            !ValidarFecha90Dias(VigenciaMedicoPicker.Date, "Apto Médico"))
        {
            return;
        }

        if (_estaProcesando) return;

        try
        {
            _estaProcesando = true;
            IsBusy = true;
            OnPropertyChanged(nameof(IsNotBusy));

            // 3. GENERACIÓN DE IDENTIDAD Y ESTRUCTURA EN DRIVE
            _idOperadorGenerado = await _sheets.GenerarIdUnicoFormateadoAsync();
            _folderIdDestino = await _drive.CrearEstructuraOperadorAsync($"{_idOperadorGenerado} - {NombreEntry.Text}");

            string linkCarpeta = $"https://drive.google.com/drive/folders/{_folderIdDestino}";

            // 4. REGISTRO EN BASE DE DATOS
            // Para solucionar el error CS0029, llamamos al método sin asignar el bool 
            // o asegúrate de que RegistrarOperadorAsync devuelva Task<bool> en tu servicio.
            await _sheets.RegistrarOperadorAsync(
                _idOperadorGenerado, NombreEntry.Text, EdadEntry.Text, CorreoEntry.Text ?? "",
                Tel1Entry.Text, Tel2Entry.Text ?? "", ClaveInterbancariaEntry.Text,
                "'" + NumLicenciaEntry.Text, VigenciaLicenciaPicker.Date.ToString("d"),
                VigenciaMedicoPicker.Date.ToString("d"), linkCarpeta, ContrasenaOperadorEntry.Text
            );

            // 5. PERSISTENCIA Y BLOQUEO DE CASCADA (Parte 1 terminada con éxito)
            Preferences.Set("IdPendiente", _idOperadorGenerado);
            Preferences.Set("CarpetaPendiente", _folderIdDestino);
            Preferences.Set("CorreoPendiente", CorreoEntry.Text ?? "");
            Preferences.Set("NombrePendiente", NombreEntry.Text);
            Preferences.Set("PassPendiente", ContrasenaOperadorEntry.Text);

            // Bloqueo de Parte 1
            BloquearFormulario(true);
            BtnRegistrarDatos.IsEnabled = false;
            BtnRegistrarDatos.Opacity = 0.5;
            BtnVolver.IsEnabled = false;
            BtnVolver.Opacity = 0.5;

            // Desbloqueo de Parte 2 (Documentos)
            SeccionDocumentos.IsEnabled = true;
            SeccionDocumentos.Opacity = 1.0;
            BtnSubirArchivos.IsEnabled = true;
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error Crítico", ex.Message, "OK");
        }
        finally
        {
            IsBusy = false;
            _estaProcesando = false;
            OnPropertyChanged(nameof(IsNotBusy));
        }
    }

    private bool ValidarLongitudExacta(string texto, string nombreCampo, int largoRequerido)
    {
        if (string.IsNullOrEmpty(texto) || texto.Length != largoRequerido)
        {
            DisplayAlert("Formato Incorrecto", $"El campo {nombreCampo} debe tener exactamente {largoRequerido} dígitos.", "Reintentar");
            return false;
        }
        return true;
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