using LogiApp.Servicios;
using Google.Apis.Sheets.v4.Data;

namespace LogiApp.Vistas;

/// <summary>
/// Pantalla encargada de registrar los reportes de mantenimiento, 
/// vinculando datos en Google Sheets y evidencias fotográficas en Google Drive.
/// </summary>
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

    /// <summary>
    /// Configura la sesión del operador y recupera los servicios de Google necesarios.
    /// </summary>
    private async void InicializarDatos()
    {
        try
        {
            // Recuperar el nombre del usuario desde las preferencias locales
            OperadorLabel.Text = Preferences.Get("UsuarioLogeado", "Operador no detectado");

            // Capturamos los servicios de Sheets y Drive desde el motor de autenticación
            var (sService, dService) = await _authService.ObtenerServiciosAsync();
            _sheets = new ConsultasGoogleSheets(sService);
            _driveService = new ConsultasGoogleDrive(dService);

            // Cargar la lista de vehículos disponibles
            await CargarUnidades();
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", ex.Message, "OK");
        }
    }

    /// <summary>
    /// Recupera las unidades registradas en la base de datos de Google Sheets.
    /// </summary>
    private async Task CargarUnidades()
    {
        CargandoUnidades.IsVisible = true;
        CargandoUnidades.IsRunning = true;

        try
        {
            // Llama al método que lee las columnas de la pestaña Unidades
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

    /// <summary>
    /// Procesa el guardado del reporte, subiendo la imagen a Drive y registrando los datos en Sheets.
    /// </summary>
    private async void OnGuardarMantenimientoClicked(object sender, EventArgs e)
    {
        // 1. Validaciones de selección de unidad
        if (UnidadPicker.SelectedIndex == -1)
        {
            await DisplayAlert("Atención", "Selecciona una unidad de la lista.", "OK");
            return;
        }

        // 2. Validación de campos obligatorios
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

            string linkCarpeta = "Sin evidencia"; // Valor por defecto si no hay foto

            // 1. Subir a Drive si existe una ruta de foto local
            if (!string.IsNullOrEmpty(_rutaFotoLocal))
            {
                string idOperador = Preferences.Get("IdOperadorLogeado", "");
                var resultadoDrive = await _driveService.SubirEvidenciaMantenimientoAsync(_rutaFotoLocal, idOperador, nuevoFolio);
                linkCarpeta = resultadoDrive.folderLink; // Se recupera el link de la carpeta creada
            }

            // 2. Registrar la fila en Google Sheets con toda la información recolectada
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

            // Regresar al menú principal
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

    /// <summary>
    /// Permite al usuario seleccionar una fotografía de la galería como evidencia.
    /// </summary>
    private async void OnAdjuntarEvidenciaClicked(object sender, EventArgs e)
    {
        try
        {
            // Invoca el selector de medios del dispositivo
            FileResult photo = await MediaPicker.Default.PickPhotoAsync();

            if (photo != null)
            {
                _rutaFotoLocal = photo.FullPath;

                // Actualización visual del botón (Uso de espacio de nombres explícito para evitar ambigüedad)
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

    /// <summary>
    /// Cancela la operación y regresa al menú principal.
    /// </summary>
    private async void OnVolverClicked(object sender, EventArgs e)
    {
        Application.Current.MainPage = new NavigationPage(new Vistas.PaginaMenu());
    }

    #endregion
}