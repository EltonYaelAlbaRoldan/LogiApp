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
            // Blindaje: Registra error de inicialización
            await GestorErrores.GuardarErrorAsync(ex, "InicializarDatos - Mantenimiento", Preferences.Get("UsuarioLogeado", "Desconocido"));
            await DisplayAlert("Error", "No se pudo conectar con el servidor.", "OK");
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
            // Blindaje: Registra error de carga de unidades
            await GestorErrores.GuardarErrorAsync(ex, "CargarUnidades - Mantenimiento", Preferences.Get("UsuarioLogeado", "Desconocido"));
            await DisplayAlert("Error", "No se pudieron cargar las unidades.", "OK");
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
        string idOperador = Preferences.Get("UsuarioLogeado", "Desconocido");

        if (UnidadPicker.SelectedIndex == -1)
        {
            await DisplayAlert("Atención", "Selecciona una unidad de la lista.", "OK");
            return;
        }

        // 1. Primero validamos que no estén vacíos
        bool faltanDatos =
            string.IsNullOrWhiteSpace(LugarEntry.Text) ||
            string.IsNullOrWhiteSpace(EstablecimientoEntry.Text) ||
            string.IsNullOrWhiteSpace(TipoMantenimientoEntry.Text) ||
            string.IsNullOrWhiteSpace(PiezasCambiadasEntry.Text) ||
            string.IsNullOrWhiteSpace(KilometrajeEntry.Text) ||
            string.IsNullOrWhiteSpace(CostoEntry.Text);

        if (faltanDatos)
        {
            await DisplayAlert("Campos Vacíos", "Por favor completa todos los detalles.", "OK");
            return;
        }

        // 2. Aseguramos que kilometraje y costo sean números
        if (!double.TryParse(KilometrajeEntry.Text, out _) || !double.TryParse(CostoEntry.Text, out _))
        {
            // Lanzamos un error controlado para que el Gestor de Errores lo capture
            Exception exFormato = new Exception("Format: El kilometraje o costo contienen letras.");
            await GestorErrores.GuardarErrorAsync(exFormato, "ValidaciónMantenimiento", idOperador);

            await DisplayAlert("Error de Formato", "El Kilometraje y el Costo deben ser números.", "OK");
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

            // Blindaje: Aprovechamos el éxito para sincronizar errores previos pendientes
            _ = GestorErrores.SincronizarLogsPendientesAsync(_sheets);

            await DisplayAlert("Éxito", "Reporte guardado con link a evidencia.", "OK");
            Application.Current.MainPage = new NavigationPage(new PaginaMenu());
        }
        catch (Exception ex)
        {
            // Blindaje: Registra error de guardado en base de datos local
            await GestorErrores.GuardarErrorAsync(ex, "GuardarMantenimiento", idOperador);
            await DisplayAlert("Error", "No se pudo guardar el reporte. El error ha sido registrado.", "OK");
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
            // Blindaje: Registra error de acceso a galería
            await GestorErrores.GuardarErrorAsync(ex, "AdjuntarEvidencia", Preferences.Get("UsuarioLogeado", "Desconocido"));
            await DisplayAlert("Error", "No se pudo cargar la imagen.", "OK");
        }
    }

    // Regresa al menú principal de la aplicación.
    private async void OnVolverClicked(object sender, EventArgs e)
    {
        Application.Current.MainPage = new NavigationPage(new Vistas.PaginaMenu());
    }

    #endregion
}