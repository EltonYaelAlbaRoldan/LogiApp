using LogiApp.Servicios;
using LogiApp.Modelos;
using Google.Apis.Drive.v3;

namespace LogiApp.Vistas;

// Gestiona el perfil del operador, la visualización de vigencias y la actualización de documentos en Drive y Sheets.
public partial class PaginaPerfil : ContentPage
{
    #region Atributos y Servicios

    private ConsultasGoogleSheets _sheets;
    private Operador _operadorActual;
    private DriveService _driveService;

    // Variables temporales para archivos
    private FileResult _archivoLicenciaTemp;
    private FileResult _archivoMedicoTemp;

    #endregion

    #region Constructor

    public PaginaPerfil()
    {
        InitializeComponent();
        CargarDatos();
    }

    #endregion

    #region Métodos de Carga y Lógica

    // Consulta los datos del operador y configura los servicios de Google.
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

    // Procesa el guardado de documentos con validación de regla de negocio (90 días).
    private async Task ProcesarGuardado(string tipo, DateTime nuevaFecha, FileResult archivo)
    {
        // REGLA DE NEGOCIO: No permitir si vence en menos de 90 días
        double diasParaVencer = (nuevaFecha - DateTime.Now).TotalDays;

        if (diasParaVencer <= 90)
        {
            await DisplayAlert("⚠️ FECHA RECHAZADA",
                $"No puedes registrar un documento que vence en {(int)diasParaVencer} días. " +
                "La vigencia debe ser mayor a 3 meses (90 días) para ser aceptada.", "Entendido");
            return;
        }

        if (archivo == null) return;

        try
        {
            IsBusy = true;

            string linkLimpio = _operadorActual.LinkCarpeta.Split('?')[0];
            string carpetaId = linkLimpio.TrimEnd('/').Split('/').Last();

            // 1. Subir a Drive (Sustituir)
            await SubirArchivoADrive(archivo, tipo, carpetaId);

            // 2. Actualizar Google Sheets
            bool exito = await _sheets.ActualizarDocumentoYFechaAsync(_operadorActual.Id, tipo, nuevaFecha, _operadorActual.LinkCarpeta);

            if (exito)
            {
                await DisplayAlert("Éxito", $"La {tipo} ha sido actualizada en la base de datos.", "OK");

                // Recargamos los datos desde la nube para actualizar la interfaz
                CargarDatos();

                // Limpiar campos de selección
                if (tipo == "Licencia") { _archivoLicenciaTemp = null; BtnCargarLicencia.Text = "📎 SELECCIONAR PDF"; BtnGuardarLicencia.IsEnabled = false; }
                else { _archivoMedicoTemp = null; BtnCargarMedico.Text = "📎 SELECCIONAR PDF"; BtnGuardarMedico.IsEnabled = false; }
            }
        }
        catch (Exception ex) { await DisplayAlert("Error", ex.Message, "OK"); }
        finally { IsBusy = false; }
    }

    // Gestiona la subida física del archivo PDF a la carpeta correspondiente en Google Drive.
    private async Task<string> SubirArchivoADrive(FileResult archivo, string tipo, string carpetaId)
    {
        string nombreDocumento = (tipo == "Licencia") ? "Licencia Federal Doc" : "Apto Medico Doc";

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
                var progreso = await updateRequest.UploadAsync();
                if (progreso.Status != Google.Apis.Upload.UploadStatus.Completed)
                    throw new Exception(progreso.Exception?.Message ?? "No se pudo actualizar el documento en Drive.");
                return archivoExistente.Id;
            }
            else
            {
                var meta = new Google.Apis.Drive.v3.Data.File() { Name = nombreDocumento, Parents = new List<string> { carpetaId } };
                var createRequest = _driveService.Files.Create(meta, stream, "application/pdf");
                createRequest.Fields = "id";
                var progreso = await createRequest.UploadAsync();
                if (progreso.Status != Google.Apis.Upload.UploadStatus.Completed || createRequest.ResponseBody == null)
                    throw new Exception(progreso.Exception?.Message ?? "No se pudo crear el documento en Drive.");
                return createRequest.ResponseBody.Id;
            }
        }
    }

    // Actualiza el color de los contenedores según los días de vigencia restantes.
    private void PintarSemaforo(DateTime fecha, Frame frame)
    {
        double dias = (fecha - DateTime.Now).TotalDays;
        if (dias <= 0) frame.BackgroundColor = Color.FromArgb("#FFCDD2");
        else if (dias <= 90) frame.BackgroundColor = Color.FromArgb("#FFF9C4");
        else frame.BackgroundColor = Color.FromArgb("#C8E6C9");
    }

    #endregion

    #region Eventos de UI

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

    private async void OnGuardarLicenciaClicked(object sender, EventArgs e) => await ProcesarGuardado("Licencia", PickerLicencia.Date, _archivoLicenciaTemp);

    private async void OnGuardarMedicoClicked(object sender, EventArgs e) => await ProcesarGuardado("Medico", PickerMedico.Date, _archivoMedicoTemp);

    private void OnDateSelected(object sender, DateChangedEventArgs e)
    {
        if (_archivoLicenciaTemp != null) BtnGuardarLicencia.IsEnabled = true;
        if (_archivoMedicoTemp != null) BtnGuardarMedico.IsEnabled = true;
    }

    private async void OnCambiarPassClicked(object sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(NuevaPassEntry.Text)) return;
        IsBusy = true;
        bool exito = await _sheets.ActualizarPasswordAsync(_operadorActual.Id, NuevaPassEntry.Text);
        IsBusy = false;
        if (exito) await DisplayAlert("Éxito", "Contraseña actualizada en la base de datos.", "OK");
    }

    private async void OnVolverClicked(object sender, EventArgs e) => Application.Current.MainPage = new NavigationPage(new PaginaMenu());

    #endregion

}
