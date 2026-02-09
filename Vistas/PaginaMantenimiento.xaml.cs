using LogiApp.Servicios;
using Google.Apis.Sheets.v4.Data;
namespace LogiApp.Vistas;

public partial class PaginaMantenimiento : ContentPage
{
    private ConsultasGoogleSheets _sheets;
    private ConsultasGoogleDrive _driveService; 
    private readonly ConexionGoogleAuth _authService = new();
    private string _rutaFotoLocal = string.Empty;

    // Constructor de la página
    public PaginaMantenimiento()
	{
        InitializeComponent();
        InicializarDatos();
    }

    // Configura la sesión del operador y conecta con Google Sheets.
    private async void InicializarDatos()
    {
        try
        {
            OperadorLabel.Text = Preferences.Get("UsuarioLogeado", "Operador no detectado");

            // Capturamos Sheets y Drive del servicio de autenticación
            var (sService, dService) = await _authService.ObtenerServiciosAsync();
            _sheets = new ConsultasGoogleSheets(sService);
            _driveService = new ConsultasGoogleDrive(dService);

            await CargarUnidades();
        }
        catch (Exception ex) { await DisplayAlert("Error", ex.Message, "OK"); }
    }

    // Carga las unidades de la BD y las muestra como "Nombre - Placas"
    private async Task CargarUnidades()
    {
        CargandoUnidades.IsVisible = true;
        CargandoUnidades.IsRunning = true;

        try
        {
            // Llama al método que lee las columnas A y B de la pestaña Unidades
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

    private async void OnVolverClicked(object sender, EventArgs e)
    {
        // Regresa al menú principal (ajustar según tu flujo de navegación)
        Application.Current.MainPage = new NavigationPage(new Vistas.PaginaMenu());
    }

    /// <summary>
    /// Procesa el guardado del reporte con los datos manuales y automáticos.
    /// </summary>
    private async void OnGuardarMantenimientoClicked(object sender, EventArgs e)
    {
        // 1. Validaciones de selección
        if (UnidadPicker.SelectedIndex == -1)
        {
            await DisplayAlert("Atención", "Selecciona una unidad de la lista.", "OK");
            return;
        }

        // 2. Validación de campos de texto
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

            // 1. Subir a Drive y obtener el link
            if (!string.IsNullOrEmpty(_rutaFotoLocal))
            {
                string idOperador = Preferences.Get("IdOperadorLogeado", "");
                var resultadoDrive = await _driveService.SubirEvidenciaMantenimientoAsync(_rutaFotoLocal, idOperador, nuevoFolio);
                linkCarpeta = resultadoDrive.folderLink; // Guardamos el link de la carpeta
            }

            // 2. Registrar en Sheets pasando el link al final
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
                linkCarpeta // <--- Pasamos el link aquí
            );

            await DisplayAlert("Éxito", "Reporte guardado con link a evidencia.", "OK");
            Application.Current.MainPage = new NavigationPage(new PaginaMenu());
        }
        catch (Exception ex) { await DisplayAlert("Error", ex.Message, "OK"); }
        finally { BtnGuardarMantenimiento.IsEnabled = true; }
    }

    private async void OnAdjuntarEvidenciaClicked(object sender, EventArgs e)
    {
        try
        {
            // Abrir la galería de imágenes
            FileResult photo = await MediaPicker.Default.PickPhotoAsync();

            if (photo != null)
            {
                _rutaFotoLocal = photo.FullPath;

                // Solución al error de ambigüedad de Color
                BtnAdjuntar.Text = "Imagen Cargada";
                BtnAdjuntar.BackgroundColor = Microsoft.Maui.Graphics.Color.FromArgb("#4CAF50");
                BtnAdjuntar.TextColor = Microsoft.Maui.Graphics.Color.FromArgb("#FFFFFF");
            }
        }
        catch (Exception ex) { await DisplayAlert("Error", "No se pudo cargar la imagen: " + ex.Message, "OK"); }
    }
}