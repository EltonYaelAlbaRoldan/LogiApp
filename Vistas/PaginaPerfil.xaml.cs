using LogiApp.Servicios;
using LogiApp.Modelos;
using Google.Apis.Drive.v3;

namespace LogiApp.Vistas;

/// <summary>
/// Pantalla de perfil del usuario donde se muestran sus datos personales, 
/// vigencias de documentos y permite el cambio de contraseña.
/// </summary>
public partial class PaginaPerfil : ContentPage
{
    #region Atributos y Servicios

    private ConsultasGoogleSheets _sheets;
    private Operador _operadorActual;
    private DriveService _driveService;

    #endregion

    #region Constructor

    public PaginaPerfil()
    {
        InitializeComponent();
        CargarDatos();
    }

    #endregion

    #region Carga de Datos

    /// <summary>
    /// Inicializa los servicios de Google y recupera la información del operador logueado.
    /// </summary>
    private async void CargarDatos()
    {
        try
        {
            // 1. Primero creamos el servicio
            var auth = new ConexionGoogleAuth();
            var (sService, dService) = await auth.ObtenerServiciosAsync();
            _sheets = new ConsultasGoogleSheets(sService);
            _driveService = dService;

            // 2. Obtener ID del almacenamiento local y consultar en Sheets
            string id = Preferences.Default.Get("UsuarioLogeado", "");
            _operadorActual = await _sheets.ObtenerDatosOperadorAsync(id);
            
            if (_operadorActual != null)
            {
                // Actualizamos la UI en el hilo principal
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    LblNombre.Text = _operadorActual.Nombre;
                    LblId.Text = $"ID: {_operadorActual.Id}";
                    LblVigenciaLicencia.Text = _operadorActual.VigenciaLicencia.ToString("dd/MM/yyyy");
                    LblVigenciaMedico.Text = _operadorActual.VigenciaMedico.ToString("dd/MM/yyyy");

                    PickerLicencia.Date = _operadorActual.VigenciaLicencia;
                    PickerMedico.Date = _operadorActual.VigenciaMedico;

                    // Aplicamos la lógica visual de semáforo según las fechas obtenidas
                    PintarSemaforo(_operadorActual.VigenciaLicencia, FrameLicencia);
                    PintarSemaforo(_operadorActual.VigenciaMedico, FrameMedico);

                    Preferences.Default.Set("VigenciaLicencia", _operadorActual.VigenciaLicencia);
                    Preferences.Default.Set("VigenciaMedico", _operadorActual.VigenciaMedico);
                });
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", "Error al cargar: " + ex.Message, "OK");
        }
    }

    #endregion

    #region Acciones de Usuario

    /// <summary>
    /// Procesa la solicitud de cambio de contraseña del operador.
    /// </summary>
    private async void OnCambiarPassClicked(object sender, EventArgs e)
    {
        // Validación de campo vacío
        if (string.IsNullOrWhiteSpace(NuevaPassEntry.Text))
        {
            await DisplayAlert("Atención", "Escribe una nueva contraseña primero.", "OK");
            return;
        }

        bool confirmar = await DisplayAlert("Confirmar", "¿Deseas cambiar tu contraseña actual?", "Sí", "Cancelar");

        if (confirmar)
        {
            IsBusy = true; // Bloquea la UI mientras procesa el cambio en Sheets
            bool exito = await _sheets.ActualizarPasswordAsync(_operadorActual.Id, NuevaPassEntry.Text);
            IsBusy = false;

            if (exito)
            {
                await DisplayAlert("Éxito", "Tu contraseña ha sido actualizada correctamente.", "OK");
                NuevaPassEntry.Text = string.Empty;
            }
            else
            {
                await DisplayAlert("Error", "No se pudo actualizar la contraseña. Intenta más tarde.", "OK");
            }
        }
    }

    /// <summary>
    /// Navega de regreso al menú principal.
    /// </summary>
    private async void OnVolverClicked(object sender, EventArgs e)
    {
        Application.Current.MainPage = new NavigationPage(new Vistas.PaginaMenu());
    }

    #endregion

    #region Métodos Auxiliares Visuales

    /// <summary>
    /// Cambia el color de fondo de un contenedor según la proximidad de la fecha de vencimiento.
    /// </summary>
    /// <param name="fecha">Fecha de vencimiento a evaluar.</param>
    /// <param name="frame">Contenedor visual a colorear.</param>
    private void PintarSemaforo(DateTime fecha, Frame frame)
    {
        double dias = (fecha - DateTime.Now).TotalDays;

        // Lógica de colores según urgencia
        if (dias <= 0)
            frame.BackgroundColor = Color.FromArgb("#FFCDD2"); // Rojo (Crítico)
        else if (dias <= 90)
            frame.BackgroundColor = Color.FromArgb("#FFF9C4"); // Amarillo (Preventivo)
        else
            frame.BackgroundColor = Color.FromArgb("#C8E6C9"); // Verde (Vigente)
    }

    private async Task ProcesarActualizacion(string tipo, DateTime nuevaFecha)
    {
        if (IsBusy) return;

        try
        {
            var archivo = await FilePicker.Default.PickAsync(new PickOptions
            {
                PickerTitle = $"Selecciona el PDF de {tipo}",
                FileTypes = FilePickerFileType.Pdf
            });

            if (archivo == null) return;

            IsBusy = true;

            // PASO 1: Obtener el ID real de la carpeta (sin que afecte a Sheets)
            // Extraemos el ID del link que ya tenemos guardado en el objeto
            string linkLimpio = _operadorActual.LinkCarpeta.Split('?')[0];
            string carpetaId = linkLimpio.TrimEnd('/').Split('/').Last();

            // PASO 2: Subir el archivo a Drive (esto guarda el respaldo en la nube)
            // No guardaremos el link de este archivo en Sheets para no perder el link de la carpeta
            await SubirArchivoADrive(archivo, tipo, carpetaId);

            // PASO 3: Actualizar ÚNICAMENTE la fecha en Sheets
            // Pasamos el mismo _operadorActual.LinkCarpeta para que NO CAMBIE en el Excel
            bool exito = await _sheets.ActualizarDocumentoYFechaAsync(
                _operadorActual.Id,
                tipo,
                nuevaFecha,
                _operadorActual.LinkCarpeta);

            if (exito)
            {
                await DisplayAlert("Éxito", $"Fecha de {tipo} actualizada. El documento se guardó en tu carpeta de Drive.", "OK");
                CargarDatos(); // Recarga para actualizar labels y semáforos
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", "No se completó la carga: " + ex.Message, "OK");
        }
        finally
        {
            IsBusy = false;
        }
    }

    // ESTE ES EL MÉTODO QUE TE FALTABA (Soluciona el error CS0103)
    private async Task<string> SubirArchivoADrive(FileResult archivo, string tipo, string carpetaId)
    {
        var metadata = new Google.Apis.Drive.v3.Data.File()
        {
            Name = $"ACTUALIZACION_{tipo}_{_operadorActual.Nombre}_{DateTime.Now:yyyyMMdd}",
            Parents = new List<string> { carpetaId }
        };

        using (var stream = await archivo.OpenReadAsync())
        {
            var request = _driveService.Files.Create(metadata, stream, "application/pdf");
            request.Fields = "id, webViewLink";

            var progreso = await request.UploadAsync();

            if (progreso.Status == Google.Apis.Upload.UploadStatus.Failed)
                throw new Exception(progreso.Exception.Message);

            return request.ResponseBody.WebViewLink;
        }
    }

    private async void OnUpdateLicenciaClicked(object sender, EventArgs e) => await ProcesarActualizacion("Licencia", PickerLicencia.Date);
    private async void OnUpdateMedicoClicked(object sender, EventArgs e) => await ProcesarActualizacion("Medico", PickerMedico.Date);

    #endregion
}