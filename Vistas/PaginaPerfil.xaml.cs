using LogiApp.Servicios;
using LogiApp.Modelos;

namespace LogiApp.Vistas;

public partial class PaginaPerfil : ContentPage
{
    private ConsultasGoogleSheets _sheets;
    private Operador _operadorActual;

    public PaginaPerfil()
    {
        InitializeComponent();
        CargarDatos();
    }

    private async void CargarDatos()
    {
        try
        {
            // 1. INICIALIZACIÓN CRÍTICA: Primero creamos el servicio
            var auth = new ConexionGoogleAuth();
            var (sService, _) = await auth.ObtenerServiciosAsync();
            _sheets = new ConsultasGoogleSheets(sService); // <--- Aquí se deja de ser null

            // 2. Obtener ID y consultar
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

                    PintarSemaforo(_operadorActual.VigenciaLicencia, FrameLicencia);
                    PintarSemaforo(_operadorActual.VigenciaMedico, FrameMedico);
                });
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", "Error al cargar: " + ex.Message, "OK");
        }
    }

    private void PintarSemaforo(DateTime fecha, Frame frame)
    {
        double dias = (fecha - DateTime.Now).TotalDays;
        if (dias < 0) frame.BackgroundColor = Color.FromArgb("#FFCDD2"); // Rojo
        else if (dias <= 30) frame.BackgroundColor = Color.FromArgb("#FFF9C4"); // Amarillo
        else frame.BackgroundColor = Color.FromArgb("#C8E6C9"); // Verde
    }
    private async void OnVolverClicked(object sender, EventArgs e)
    {
        // Regresa a la página anterior de forma segura
        Application.Current.MainPage = new NavigationPage(new Vistas.PaginaMenu());
    }

    private async void OnCambiarPassClicked(object sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(NuevaPassEntry.Text))
        {
            await DisplayAlert("Atención", "Escribe una nueva contraseña primero.", "OK");
            return;
        }

        bool confirmar = await DisplayAlert("Confirmar", "¿Deseas cambiar tu contraseña actual?", "Sí", "Cancelar");

        if (confirmar)
        {
            IsBusy = true; // Bloquea la UI mientras procesa
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
}