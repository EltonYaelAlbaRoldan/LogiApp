using LogiApp.Servicios;
using LogiApp.Modelos;

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
            var (sService, _) = await auth.ObtenerServiciosAsync();
            _sheets = new ConsultasGoogleSheets(sService);

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

                    // Aplicamos la lógica visual de semáforo según las fechas obtenidas
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
        if (dias < 30)
            frame.BackgroundColor = Color.FromArgb("#FFCDD2"); // Rojo (Crítico)
        else if (dias <= 90)
            frame.BackgroundColor = Color.FromArgb("#FFF9C4"); // Amarillo (Preventivo)
        else
            frame.BackgroundColor = Color.FromArgb("#C8E6C9"); // Verde (Vigente)
    }

    #endregion
}