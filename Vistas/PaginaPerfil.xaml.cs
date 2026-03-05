using LogiApp.Servicios;
using LogiApp.Modelos;
using Google.Apis.Drive.v3;

namespace LogiApp.Vistas;

public partial class PaginaPerfil : ContentPage
{
    private ConsultasGoogleSheets _sheets;
    private Operador _operadorActual;
    private DriveService _driveService;

    // Variables temporales para archivos
    private FileResult _archivoLicenciaTemp;
    private FileResult _archivoMedicoTemp;

    public PaginaPerfil()
    {
        InitializeComponent();
        CargarDatos();
    }

    private async void CargarDatos()
    {
        try
        {
            var auth = new ConexionGoogleAuth();
            var (sService, dService) = await auth.ObtenerServiciosAsync();
            _sheets = new ConsultasGoogleSheets(sService);
            _driveService = dService;

            string id = Preferences.Default.Get("UsuarioLogeado", "");
            _operadorActual = await _sheets.ObtenerDatosOperadorAsync(id);

            if (_operadorActual != null)
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    LblNombre.Text = _operadorActual.Nombre;
                    LblId.Text = $"ID: {_operadorActual.Id}";
                    LblVigenciaLicencia.Text = _operadorActual.VigenciaLicencia.ToString("dd/MM/yyyy");
                    LblVigenciaMedico.Text = _operadorActual.VigenciaMedico.ToString("dd/MM/yyyy");

                    PickerLicencia.Date = _operadorActual.VigenciaLicencia;
                    PickerMedico.Date = _operadorActual.VigenciaMedico;

                    PintarSemaforo(_operadorActual.VigenciaLicencia, FrameLicencia);
                    PintarSemaforo(_operadorActual.VigenciaMedico, FrameMedico);
                });
            }
        }
        catch (Exception ex) { await DisplayAlert("Error", ex.Message, "OK"); }
    }

    #region Selección de Archivos
    private async void OnSeleccionarLicenciaClicked(object sender, EventArgs e)
    {
        _archivoLicenciaTemp = await FilePicker.Default.PickAsync(new PickOptions
        {
            PickerTitle = "PDF de Licencia",
            FileTypes = FilePickerFileType.Pdf
        });
        if (_archivoLicenciaTemp != null)
        {
            BtnCargarLicencia.Text = "✅ ARCHIVO LISTO";
            BtnGuardarLicencia.IsEnabled = true;
        }
    }

    private async void OnSeleccionarMedicoClicked(object sender, EventArgs e)
    {
        _archivoMedicoTemp = await FilePicker.Default.PickAsync(new PickOptions
        {
            PickerTitle = "PDF de Médico",
            FileTypes = FilePickerFileType.Pdf
        });
        if (_archivoMedicoTemp != null)
        {
            BtnCargarMedico.Text = "✅ ARCHIVO LISTO";
            BtnGuardarMedico.IsEnabled = true;
        }
    }
    #endregion

    #region Guardado con Validación de 90 días
    private async void OnGuardarLicenciaClicked(object sender, EventArgs e) => await ProcesarGuardado("Licencia", PickerLicencia.Date, _archivoLicenciaTemp);
    private async void OnGuardarMedicoClicked(object sender, EventArgs e) => await ProcesarGuardado("Medico", PickerMedico.Date, _archivoMedicoTemp);

    private async Task ProcesarGuardado(string tipo, DateTime nuevaFecha, FileResult archivo)
    {
        // REGLA DE NEGOCIO: No permitir si vence en menos de 90 días
        double diasParaVencer = (nuevaFecha - DateTime.Now).TotalDays;

        if (diasParaVencer <= 90)
        {
            await DisplayAlert("⚠️ FECHA RECHAZADA",
                $"No puedes registrar un documento que vence en {Math.Max(0, (int)diasParaVencer)} días. " +
                "La vigencia debe ser mayor a 3 meses (90 días) para ser aceptada.", "Entendido");
            return;
        }

        if (archivo == null) return;

        try
        {
            IsBusy = true;

            string linkLimpio = _operadorActual.LinkCarpeta.Split('?')[0];
            string carpetaId = linkLimpio.TrimEnd('/').Split('/').Last();

            // 1. Subir a Drive y 2. Actualizar Sheets (Ya lo tienes)
            await SubirArchivoADrive(archivo, tipo, carpetaId);
            bool exito = await _sheets.ActualizarDocumentoYFechaAsync(_operadorActual.Id, tipo, nuevaFecha, _operadorActual.LinkCarpeta);

            if (exito)
            {
                // ¡ESTO ES LO QUE FALTABA! Actualizar la memoria local
                if (tipo == "Licencia")
                    Preferences.Default.Set("VigenciaLicencia", nuevaFecha);
                else
                    Preferences.Default.Set("VigenciaMedico", nuevaFecha);

                await DisplayAlert("Éxito", $"La {tipo} ha sido actualizada. El bloqueo del menú se ha revisado.", "OK");

                CargarDatos(); // Esto repinta los semáforos en el Perfil

                // Limpiar selección
                if (tipo == "Licencia") { _archivoLicenciaTemp = null; BtnCargarLicencia.Text = "📎 SELECCIONAR PDF"; BtnGuardarLicencia.IsEnabled = false; }
                else { _archivoMedicoTemp = null; BtnCargarMedico.Text = "📎 SELECCIONAR PDF"; BtnGuardarMedico.IsEnabled = false; }
            }
        }
        catch (Exception ex) { await DisplayAlert("Error", ex.Message, "OK"); }
        finally { IsBusy = false; }
    }
    #endregion

    private async Task<string> SubirArchivoADrive(FileResult archivo, string tipo, string carpetaId)
    {
        string nombreDocumento = (tipo == "Licencia") ? "Licencia Federal Doc" : "CURP";

        var listRequest = _driveService.Files.List();
        listRequest.Q = $"name = '{nombreDocumento}' and '{carpetaId}' in parents and trashed = false";
        listRequest.Fields = "files(id)";
        var res = await listRequest.ExecuteAsync();
        var archivoExistente = res.Files.FirstOrDefault();

        using (var stream = await archivo.OpenReadAsync())
        {
            if (archivoExistente != null)
            {
                var updateRequest = _driveService.Files.Update(new Google.Apis.Drive.v3.Data.File(), archivoExistente.Id, stream, "application/pdf");
                await updateRequest.UploadAsync();
                return archivoExistente.Id;
            }
            else
            {
                var meta = new Google.Apis.Drive.v3.Data.File() { Name = nombreDocumento, Parents = new List<string> { carpetaId } };
                var createRequest = _driveService.Files.Create(meta, stream, "application/pdf");
                await createRequest.UploadAsync();
                return createRequest.ResponseBody.Id;
            }
        }
    }

    private void PintarSemaforo(DateTime fecha, Frame frame)
    {
        double dias = (fecha - DateTime.Now).TotalDays;
        if (dias <= 0) frame.BackgroundColor = Color.FromArgb("#FFCDD2");
        else if (dias <= 90) frame.BackgroundColor = Color.FromArgb("#FFF9C4");
        else frame.BackgroundColor = Color.FromArgb("#C8E6C9");
    }

    private void OnDateSelected(object sender, DateChangedEventArgs e)
    {
        // Al mover la fecha, si ya hay un archivo, recordamos que el botón de guardar debe estar activo
        if (_archivoLicenciaTemp != null) BtnGuardarLicencia.IsEnabled = true;
        if (_archivoMedicoTemp != null) BtnGuardarMedico.IsEnabled = true;
    }

    private async void OnVolverClicked(object sender, EventArgs e) => Application.Current.MainPage = new NavigationPage(new PaginaMenu());

    private async void OnCambiarPassClicked(object sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(NuevaPassEntry.Text)) return;
        IsBusy = true;
        bool exito = await _sheets.ActualizarPasswordAsync(_operadorActual.Id, NuevaPassEntry.Text);
        IsBusy = false;
        if (exito) await DisplayAlert("Éxito", "Contraseña actualizada", "OK");
    }
}