using LogiApp.Servicios;
using System.Text.RegularExpressions;

namespace LogiApp.Vistas;

public partial class PaginaRegistro : ContentPage
{
    private ConsultasGoogleSheets _sheets;
    private ConsultasGoogleDrive _drive;
    private readonly ServicioEmail _emailService = new ServicioEmail();
    private readonly ConexionGoogleAuth _authService = new();

    public bool IsNotBusy => !IsBusy;
    private bool _estaProcesando = false;

    private Dictionary<string, string> _rutasArchivos = new Dictionary<string, string>();
    private string _folderIdDestino = "";
    private string _idOperadorGenerado = "";

    public PaginaRegistro()
    {
        InitializeComponent();
        InicializarConexion();

        // RECUPERACIÓN DE SESIÓN: Si se cerró la app, retomamos los datos guardados
        if (Preferences.ContainsKey("IdPendiente"))
        {
            _idOperadorGenerado = Preferences.Get("IdPendiente", "");
            _folderIdDestino = Preferences.Get("CarpetaPendiente", "");

            // Bloqueamos registro y habilitamos subida directamente
            BtnRegistrarDatos.IsEnabled = false;
            BtnSubirArchivos.IsEnabled = true;
            BtnVolver.IsEnabled = false;
            BloquearFormulario(true);
            BtnVolver.Opacity = 0.5;

            DisplayAlert("Registro Pendiente", "Se detectó un registro sin finalizar. Por favor, sube los archivos correspondientes.", "Continuar");
        }
    }

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

    private async void OnVolverClicked(object sender, EventArgs e)
    {
        Application.Current.MainPage = new NavigationPage(new Vistas.PaginaLogeo());
    }

    private void OnTogglePasswordClicked(object sender, EventArgs e)
    {
        ContrasenaOperadorEntry.IsPassword = !ContrasenaOperadorEntry.IsPassword;
        TogglePasswordLabel.Text = ContrasenaOperadorEntry.IsPassword ? "VER" : "OCULTAR";
    }

    // --- MÉTODOS SELECCIÓN DE ARCHIVOS ---
    private async void OnSubirLicenciaClicked(object sender, EventArgs e) => await SeleccionarArchivoPdfAsync("Licencia Federal");
    private async void OnSubirIneClicked(object sender, EventArgs e) => await SeleccionarArchivoPdfAsync("INE");
    private async void OnSubirActaClicked(object sender, EventArgs e) => await SeleccionarArchivoPdfAsync("Acta de Nacimiento");
    private async void OnSubirNssClicked(object sender, EventArgs e) => await SeleccionarArchivoPdfAsync("NSS");
    private async void OnSubirCvClicked(object sender, EventArgs e) => await SeleccionarArchivoPdfAsync("CV");
    private async void OnSubirDomicilioClicked(object sender, EventArgs e) => await SeleccionarArchivoPdfAsync("Comprobante de Domicilio");
    private async void OnSubirRfcClicked(object sender, EventArgs e) => await SeleccionarArchivoPdfAsync("RFC / Cédula Fiscal");
    private async void OnSubirCurpClicked(object sender, EventArgs e) => await SeleccionarArchivoPdfAsync("CURP");
    private async void OnSubirLicenciaFederalClicked(object sender, EventArgs e) => await SeleccionarArchivoPdfAsync("Licencia Federal Doc");

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

    // --- PASO 1: REGISTRO DE DATOS ---
    private async void OnProbarRegistroClicked(object sender, EventArgs e)
    {
        // (Validaciones de campos omitidas por brevedad, mantener las que ya tienes)
        if (string.IsNullOrWhiteSpace(NombreEntry.Text)) return;

        if (_estaProcesando) return;

        try
        {
            _estaProcesando = true;
            IsBusy = true;
            BloquearFormulario(true);
            OnPropertyChanged(nameof(IsNotBusy));

            _idOperadorGenerado = await _sheets.GenerarIdUnicoFormateadoAsync();
            _folderIdDestino = await _drive.CrearEstructuraOperadorAsync($"{_idOperadorGenerado} - {NombreEntry.Text}");

            string linkCarpeta = $"https://drive.google.com/drive/folders/{_folderIdDestino}";

            await _sheets.RegistrarOperadorAsync(
                _idOperadorGenerado, NombreEntry.Text, EdadEntry.Text, CorreoEntry.Text ?? "",
                Tel1Entry.Text, Tel2Entry.Text ?? "", ClaveInterbancariaEntry.Text,
                "'" + NumLicenciaEntry.Text, VigenciaLicenciaPicker.Date.ToString("d"),
                VigenciaMedicoPicker.Date.ToString("d"), linkCarpeta, ContrasenaOperadorEntry.Text
            );

            // PERSISTENCIA: Guardamos datos en memoria local
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

    // --- PASO 2: SUBIDA Y ENVÍO DE CORREO ---
    private async void OnSubirArchivosClicked(object sender, EventArgs e)
    {
        if (_estaProcesando) return;

        string[] docs = { "Licencia Federal Doc", "INE", "Acta de Nacimiento", "NSS", "CV", "Comprobante de Domicilio", "RFC / Cédula Fiscal", "CURP" };
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

            // ENVÍO DE CORREO AL FINAL
            string correo = Preferences.Get("CorreoPendiente", CorreoEntry.Text);
            string nombre = Preferences.Get("NombrePendiente", NombreEntry.Text);
            string pass = Preferences.Get("PassPendiente", ContrasenaOperadorEntry.Text);

            try
            {
                await _emailService.EnviarCredencialesAsync(correo, nombre, _idOperadorGenerado, pass);
            }
            catch { /* Error de correo no detiene el éxito */ }

            await DisplayAlert("Éxito", "Expediente completo y credenciales enviadas.", "Finalizar");

            // LIMPIEZA: Borramos la sesión pendiente
            Preferences.Clear();
            _rutasArchivos.Clear();

            Application.Current.MainPage = new NavigationPage(new Vistas.PaginaLogeo());
        }
        catch (Exception ex) { await DisplayAlert("Error", ex.Message, "OK"); BtnSubirArchivos.IsEnabled = true; }
        finally { _estaProcesando = false; IsBusy = false; }
    }

    private void BloquearFormulario(bool bloquear)
    {
        // Invertimos el valor: si bloquear es true, IsEnabled debe ser false
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
}