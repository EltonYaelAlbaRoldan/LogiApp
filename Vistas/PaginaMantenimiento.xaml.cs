using LogiApp.Servicios;
using Google.Apis.Sheets.v4.Data;

namespace LogiApp.Vistas;

// Controla el registro de mantenimiento, subida de evidencias a Drive y guardado en Google Sheets.
public partial class PaginaMantenimiento : ContentPage
{
    #region Atributos y Servicios

    private ConsultasGoogleSheets _sheets;
    private ConsultasGoogleDrive _driveService;
    private readonly ConexionGoogleAuth _authService = new();
    private string _rutaFotoLocal = string.Empty;

    #endregion

    #region Constructor

    public PaginaMantenimiento()
    {
        InitializeComponent();
        InicializarDatos();
    }

    #endregion

    #region Inicialización de Datos

    // Configura los servicios de Google y carga la información inicial.
    private async void InicializarDatos()
    {
        try
        {
            OperadorLabel.Text = Preferences.Get("UsuarioLogeado", "Operador no detectado");

            var (sService, dService) = await _authService.ObtenerServiciosAsync();
            _sheets = new ConsultasGoogleSheets(sService);
            _driveService = new ConsultasGoogleDrive(dService);

            await CargarUnidades();
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", ex.Message, "OK");
        }
    }

    // Obtiene la lista de unidades desde la base de datos de Sheets.
    private async Task CargarUnidades()
    {
        CargandoUnidades.IsVisible = true;
        CargandoUnidades.IsRunning = true;

        try
        {
            var listaUnidades = await _sheets.ObtenerListaUnidadesAsync();

            if (listaUnidades != null && listaUnidades.Count > 0)
            {
                UnidadPicker.ItemsSource = listaUnidades;
            }
            else
            {
                await DisplayAlert("Base de Datos", "No hay unidades registradas en Sheets.", "OK");
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", "No se pudieron cargar las unidades: " + ex.Message, "OK");
        }
        finally
        {
            CargandoUnidades.IsVisible = false;
            CargandoUnidades.IsRunning = false;
        }
    }

    #endregion

    #region Eventos de Formulario

    // Procesa el guardado del reporte, sube la foto a Drive y registra los datos en Sheets.
    private async void OnGuardarMantenimientoClicked(object sender, EventArgs e)
    {
        if (UnidadPicker.SelectedIndex == -1)
        {
            await DisplayAlert("Atención", "Selecciona una unidad de la lista.", "OK");
            return;
        }

        bool faltanDatos =
            string.IsNullOrWhiteSpace(LugarEntry.Text) ||
            string.IsNullOrWhiteSpace(EstablecimientoEntry.Text) ||
            string.IsNullOrWhiteSpace(TipoMantenimientoEntry.Text) ||
            string.IsNullOrWhiteSpace(PiezasCambiadasEntry.Text) ||
            string.IsNullOrWhiteSpace(KilometrajeEntry.Text) ||
            string.IsNullOrWhiteSpace(CostoEntry.Text);

        if (faltanDatos)
        {
            await DisplayAlert("Campos Vacíos", "Por favor completa todos los detalles del trabajo.", "OK");
            return;
        }

        try
        {
            BtnGuardarMantenimiento.IsEnabled = false;
            BtnGuardarMantenimiento.Text = "SUBIENDO... ESPERE";

            string nuevoFolio = await _sheets.GenerarFolioMantenimientoAsync();
            string unidadTexto = UnidadPicker.SelectedItem.ToString();
            string linkCarpeta = "Sin evidencia";

            if (!string.IsNullOrEmpty(_rutaFotoLocal))
            {
                string idOperador = Preferences.Get("IdOperadorLogeado", "");
                var resultadoDrive = await _driveService.SubirEvidenciaMantenimientoAsync(_rutaFotoLocal, idOperador, nuevoFolio);
                linkCarpeta = resultadoDrive.folderLink;
            }

            await _sheets.RegistrarMantenimientoAsync(
                nuevoFolio,
                DateTime.Now.ToString("g"),
                FechaMantenimientoPicker.Date.ToString("d"),
                OperadorLabel.Text,
                unidadTexto,
                LugarEntry.Text,
                EstablecimientoEntry.Text,
                TipoMantenimientoEntry.Text,
                PiezasCambiadasEntry.Text ?? "N/A",
                KilometrajeEntry.Text,
                CostoEntry.Text,
                CheckAceite.IsChecked ? "OK" : "REVISAR",
                CheckFrenos.IsChecked ? "OK" : "REVISAR",
                linkCarpeta
            );

            await DisplayAlert("Éxito", "Reporte guardado con link a evidencia.", "OK");
            Application.Current.MainPage = new NavigationPage(new PaginaMenu());
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", ex.Message, "OK");
        }
        finally
        {
            BtnGuardarMantenimiento.IsEnabled = true;
            BtnGuardarMantenimiento.Text = "GUARDAR REPORTE";
        }
    }

    // Abre el selector de archivos para adjuntar una imagen de evidencia.
    private async void OnAdjuntarEvidenciaClicked(object sender, EventArgs e)
    {
        try
        {
            FileResult photo = await MediaPicker.Default.PickPhotoAsync();

            if (photo != null)
            {
                _rutaFotoLocal = photo.FullPath;
                BtnAdjuntar.Text = "Imagen Cargada";
                BtnAdjuntar.BackgroundColor = Microsoft.Maui.Graphics.Color.FromArgb("#4CAF50");
                BtnAdjuntar.TextColor = Microsoft.Maui.Graphics.Color.FromArgb("#FFFFFF");
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", "No se pudo cargar la imagen: " + ex.Message, "OK");
        }
    }

    // Regresa al menú principal de la aplicación.
    private async void OnVolverClicked(object sender, EventArgs e)
    {
        Application.Current.MainPage = new NavigationPage(new Vistas.PaginaMenu());
    }

    #endregion
}