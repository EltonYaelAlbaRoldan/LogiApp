using LogiApp.Servicios;
using Google.Apis.Sheets.v4.Data;
using System.Collections.ObjectModel;

namespace LogiApp.Vistas;

// Controla el registro de mantenimiento, subida de evidencias a Drive y guardado en Google Sheets.
public partial class PaginaMantenimiento : ContentPage
{
    #region Atributos y Servicios

    private ConsultasGoogleSheets _sheets;
    private ConsultasGoogleDrive _driveService;
    private readonly ConexionGoogleAuth _authService = new();
    private readonly ObservableCollection<string> _rutasFotos = new();

    #endregion

    #region Constructor

    public PaginaMantenimiento()
    {
        InitializeComponent();
        ListaFotos.ItemsSource = _rutasFotos;
        BtnGuardarMantenimiento.IsEnabled = false;
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
            BtnGuardarMantenimiento.IsEnabled = true;
        }
        catch (Exception ex)
        {
            await GestorErrores.GuardarErrorAsync(ex, "InicializarDatos - Mantenimiento", Preferences.Get("UsuarioLogeado", "Desconocido"));

            string error = ex.Message.ToLower();
            string msj = "Ocurrió un problema al conectar con el sistema. Intenta abrir la pantalla nuevamente.";

            if (error.Contains("timedout") || error.Contains("network") || error.Contains("100"))
            {
                msj = "Error de conexión. Revisa tu acceso a internet e intenta de nuevo.";
            }

            await DisplayAlert("Problema de conexión", msj, "Entendido");
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
                await DisplayAlert("Aviso", "No hay unidades registradas en el sistema por el momento.", "Entendido");
            }
        }
        catch (Exception ex)
        {
            await GestorErrores.GuardarErrorAsync(ex, "CargarUnidades - Mantenimiento", Preferences.Get("UsuarioLogeado", "Desconocido"));

            string error = ex.Message.ToLower();
            string msj = "No se pudieron cargar las unidades. Intenta salir y volver a entrar.";

            if (error.Contains("timedout") || error.Contains("network") || error.Contains("100"))
            {
                msj = "Internet inestable. No se pudo cargar la lista de unidades.";
            }

            await DisplayAlert("Aviso", msj, "Entendido");
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
            await DisplayAlert("Falta Unidad", "Por favor, selecciona una unidad de la lista antes de continuar.", "Entendido");
            return;
        }

        bool faltanDatos =
            string.IsNullOrWhiteSpace(LugarEntry.Text) ||
            string.IsNullOrWhiteSpace(EstablecimientoEntry.Text) ||
            string.IsNullOrWhiteSpace(TipoMantenimientoEntry.Text) ||
            string.IsNullOrWhiteSpace(PiezasCambiadasEntry.Text) ||
            string.IsNullOrWhiteSpace(KilometrajeEntry.Text);

        if (faltanDatos)
        {
            await DisplayAlert("Datos incompletos", "Por favor, llena todos los campos de texto para poder guardar el reporte.", "Entendido");
            return;
        }

        if (!double.TryParse(KilometrajeEntry.Text, out _))
        {
            Exception exFormato = new Exception("Format: El kilometraje contiene letras.");
            await GestorErrores.GuardarErrorAsync(exFormato, "ValidaciónMantenimiento", idOperador);

            await DisplayAlert("Verifica el Kilometraje", "El kilometraje solo debe contener números, sin letras ni símbolos.", "Entendido");
            return;
        }

        try
        {
            // Bloqueamos el botón y mostramos la pantalla oscura con la ruedita
            BtnGuardarMantenimiento.IsEnabled = false;
            OverlayCarga.IsVisible = true;
            CargandoGuardar.IsRunning = true;

            string nuevoFolio = await _sheets.GenerarFolioMantenimientoAsync();
            string unidadTexto = UnidadPicker.SelectedItem.ToString();
            string linkCarpeta = "Sin evidencia";

            if (_rutasFotos.Count > 0)
            {
                // Cambiamos el texto para que el operador sepa qué está haciendo la app
                MensajeCarga.Text = "Subiendo fotos, por favor espera...";
                linkCarpeta = await _driveService.SubirEvidenciasMantenimientoAsync(_rutasFotos, idOperador, nuevoFolio);
            }

            MensajeCarga.Text = "Guardando reporte...";

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
                linkCarpeta
            );

            _ = GestorErrores.SincronizarLogsPendientesAsync(_sheets);

            await DisplayAlert("Éxito", "El reporte se guardó correctamente.", "OK");
            Application.Current.MainPage = new NavigationPage(new PaginaMenu());
        }
        catch (Exception ex)
        {
            await GestorErrores.GuardarErrorAsync(ex, "GuardarMantenimiento", idOperador);

            string error = ex.Message.ToLower();
            string msj = "Ocurrió un problema al guardar el reporte. Intenta nuevamente.";

            if (error.Contains("timedout") || error.Contains("network") || error.Contains("100"))
            {
                msj = "Error de conexión. Revisa tu acceso a internet para poder guardar el reporte y subir las fotos.";
            }

            await DisplayAlert("Revisa tu conexión", msj, "Entendido");
        }
        finally
        {
            // Ocultamos la capa oscura si hay error (si hay éxito, la página cambia al Menú así que no importa)
            BtnGuardarMantenimiento.IsEnabled = true;
            OverlayCarga.IsVisible = false;
            CargandoGuardar.IsRunning = false;
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
                _rutasFotos.Add(photo.FullPath);
                BtnAdjuntar.Text = $"📷 Agregar otra foto ({_rutasFotos.Count})";
                BtnAdjuntar.BackgroundColor = Microsoft.Maui.Graphics.Color.FromArgb("#4CAF50");
                BtnAdjuntar.TextColor = Microsoft.Maui.Graphics.Color.FromArgb("#FFFFFF");
            }
        }
        catch (Exception ex)
        {
            await GestorErrores.GuardarErrorAsync(ex, "AdjuntarEvidencia", Preferences.Get("UsuarioLogeado", "Desconocido"));
            await DisplayAlert("Error al cargar", "No se pudo cargar la imagen desde tu galería. Intenta elegir otra.", "Entendido");
        }
    }

    private void OnEliminarFotoClicked(object sender, EventArgs e)
    {
        if (sender is Button { CommandParameter: string ruta })
        {
            _rutasFotos.Remove(ruta);
            BtnAdjuntar.Text = _rutasFotos.Count == 0
                ? "📷 Adjuntar Evidencia"
                : $"📷 Agregar otra foto ({_rutasFotos.Count})";
        }
    }

    // Regresa al menú principal de la aplicación.
    private async void OnVolverClicked(object sender, EventArgs e)
    {
        Application.Current.MainPage = new NavigationPage(new Vistas.PaginaMenu());
    }

    #endregion
}