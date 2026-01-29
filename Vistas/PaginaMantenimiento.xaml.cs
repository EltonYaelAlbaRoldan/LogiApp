using LogiApp.Servicios;
using Google.Apis.Sheets.v4.Data;
namespace LogiApp.Vistas;

public partial class PaginaMantenimiento : ContentPage
{
    // 1. Declaración de variables de servicio (Corrige CS0103)
    private ConsultasGoogleSheets _sheets;
    private readonly ConexionGoogleAuth _authService = new();
    public PaginaMantenimiento()
	{
		InitializeComponent();
        InicializarConexion();
    }
    private async void InicializarConexion()
    {
        try
        {
            var (sService, _) = await _authService.ObtenerServiciosAsync();
            _sheets = new ConsultasGoogleSheets(sService); // Asignación de instancia
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error de Conexión", ex.Message, "OK");
        }
    }
    private async void OnVolverClicked(object sender, EventArgs e)
    {
        // Regresa a la pagina de menú principal
        Application.Current.MainPage = new NavigationPage(new Vistas.PaginaMenu());
    }

    private async void OnGuardarMantenimientoClicked(object sender, EventArgs e)
    {
        // Validación de controles del XAML
        if (string.IsNullOrWhiteSpace(PlacasEntry.Text) || string.IsNullOrWhiteSpace(CostoEntry.Text))
        {
            await DisplayAlert("Atención", "Placas y Costo son obligatorios.", "OK");
            return;
        }

        try
        {
            IsBusy = true;
            await DisplayAlert("Procesando", "Sincronizando con Analítica...", "OK");

            // Generar folio y estatus KPI
            string nuevoFolio = await _sheets.GenerarFolioMantenimientoAsync();
            string estatusKpi = (CheckAceite.IsChecked && CheckFrenos.IsChecked) ? "OPERATIVO" : "CRÍTICO";

            // Enviar los 14 datos configurados en tu tabla de Sheets
            await _sheets.RegistrarMantenimientoAsync(
                nuevoFolio,                                   // A: Folio
                DateTime.Now.ToString("g"),                   // B: Registro
                FechaMantenimientoPicker.Date.ToString("d"),  // C: Fecha Servicio
                OperadorEntry.Text,                           // D: Operador
                PlacasEntry.Text,                             // E: Placas
                LugarEntry.Text,                              // F: Lugar
                EstablecimientoEntry.Text,                    // G: Taller
                TipoMantenimientoEntry.Text,                  // H: Tipo
                PiezasCambiadasEntry.Text,                    // I: Piezas
                KilometrajeEntry.Text,                        // J: KM
                CostoEntry.Text,                              // K: Costo
                CheckAceite.IsChecked ? "OK" : "FALLA",       // L: Aceite
                CheckFrenos.IsChecked ? "OK" : "FALLA",       // M: Frenos
                estatusKpi                                    // N: KPI
            );

            await DisplayAlert("Éxito", $"Reporte {nuevoFolio} guardado correctamente.", "OK");
            await Navigation.PopAsync();
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", "No se pudo sincronizar: " + ex.Message, "Cerrar");
        }
        finally { IsBusy = false; }
    }
}