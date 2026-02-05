using LogiApp.Servicios;
using Google.Apis.Sheets.v4.Data;
namespace LogiApp.Vistas;

public partial class PaginaMantenimiento : ContentPage
{
    // Servicio para interactuar con Google Sheets
    private ConsultasGoogleSheets _sheets;
    private readonly ConexionGoogleAuth _authService = new();

    // Constructor de la página
    public PaginaMantenimiento()
	{
        InitializeComponent();
        InicializarDatos();
    }
    /// <summary>
    /// Configura la sesión del operador y conecta con Google Sheets.
    /// </summary>
    private async void InicializarDatos()
    {
        try
        {
            // 1. Obtener automáticamente el nombre del operador logueado
            // Se asume que se guardó en el Login como Preferences.Set("UsuarioLogeado", ...)
            string operadorActual = Preferences.Get("UsuarioLogeado", "Operador no detectado");
            OperadorLabel.Text = operadorActual;

            // 2. Inicializar servicios de Google
            var (sService, _) = await _authService.ObtenerServiciosAsync();
            _sheets = new ConsultasGoogleSheets(sService);

            // 3. Cargar la lista de unidades desde la pestaña "Unidades"
            await CargarUnidades();
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error de Inicio", "No se pudo preparar el formulario: " + ex.Message, "OK");
        }
    }

    /// <summary>
    /// Carga las unidades de la BD y las muestra como "Nombre - Placas"
    /// </summary>
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
            BtnGuardarMantenimiento.Text = "ENVIANDO REPORTE...";

            // Generar datos automáticos y de negocio
            string nuevoFolio = await _sheets.GenerarFolioMantenimientoAsync();
            string estatusKpi = (CheckAceite.IsChecked && CheckFrenos.IsChecked) ? "OPERATIVO" : "CRÍTICO";

            // Obtener el valor seleccionado del Picker (Nombre - Placas)
            string unidadSeleccionada = UnidadPicker.SelectedItem.ToString();

            // Enviar a Google Sheets
            await _sheets.RegistrarMantenimientoAsync(
                nuevoFolio,                                   // A: Folio
                DateTime.Now.ToString("g"),                   // B: Registro automático
                FechaMantenimientoPicker.Date.ToString("d"),  // C: Fecha del taller
                OperadorLabel.Text,                           // D: Operador (Automático)
                unidadSeleccionada,                           // E: Unidad y Placa (Picker)
                LugarEntry.Text,                              // F: Ciudad/Estado
                EstablecimientoEntry.Text,                    // G: Taller
                TipoMantenimientoEntry.Text,                  // H: Descripción
                PiezasCambiadasEntry.Text,                    // I: Refacciones
                KilometrajeEntry.Text,                        // J: KM
                CostoEntry.Text,                              // K: Costo ($)
                CheckAceite.IsChecked ? "OK" : "REVISAR",     // L: Aceite
                CheckFrenos.IsChecked ? "OK" : "REVISAR",     // M: Frenos
                estatusKpi                                    // N: Resultado KPI
            );

            await DisplayAlert("Éxito", $"Reporte {nuevoFolio} guardado en la nube.", "OK");
            await Navigation.PopAsync();
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error de Sincronización", "No se pudo guardar: " + ex.Message, "Cerrar");
        }
        finally
        {
            BtnGuardarMantenimiento.IsEnabled = true;
            BtnGuardarMantenimiento.Text = "GUARDAR REPORTE COMPLETO";
        }
    }
}