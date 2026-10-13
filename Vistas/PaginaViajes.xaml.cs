using LogiApp.Servicios;
using LogiApp.Modelos;

namespace LogiApp.Vistas;

// Gestiona el despacho de nuevos viajes, validando recursos (unidad, remolque) y registrando el inicio en Sheets.
public partial class PaginaViajes : ContentPage
{
    #region Atributos y Servicios

    private ConsultasGoogleSheets _sheets;
    private readonly ConexionGoogleAuth _authService = new();

    #endregion

    #region Constructor

    public PaginaViajes()
    {
        InitializeComponent();
        BtnGuardarViaje.IsEnabled = false;
        InicializarDatos();
    }

    #endregion

    #region Inicialización y Carga de Datos

    // Establece la conexión con Google y dispara la carga simultánea de catálogos.
    private async void InicializarDatos()
    {
        try
        {
            var (sService, _) = await _authService.ObtenerServiciosAsync();
            _sheets = new ConsultasGoogleSheets(sService);

            // Optimización: Carga de 4 listas en paralelo para reducir tiempo de espera
            await Task.WhenAll(
                CargarUnidades(),
                CargarRemolques(),
                CargarDestinos(),
                CargarClientes()
            );
            BtnGuardarViaje.IsEnabled = true;
        }
        catch (Exception ex)
        {
            string error = ex.Message.ToLower();
            string msj = "Ocurrió un problema al cargar los datos. Intenta abrir la pantalla nuevamente.";

            if (error.Contains("timedout") || error.Contains("network") || error.Contains("100"))
            {
                msj = "Error de conexión. Revisa tu acceso a internet para poder cargar los datos.";
            }

            await DisplayAlert("Problema de conexión", msj, "OK");
        }
    }

    // Consulta y puebla la lista de unidades disponibles desde Sheets.
    private async Task CargarUnidades()
    {
        CargandoUnidades.IsVisible = true;
        CargandoUnidades.IsRunning = true;
        try
        {
            var lista = await _sheets.ObtenerListaUnidadesAsync();
            UnidadPicker.ItemsSource = lista;
        }
        finally
        {
            CargandoUnidades.IsVisible = false;
            CargandoUnidades.IsRunning = false;
        }
    }

    // Consulta y puebla la lista de remolques/cajas disponibles.
    private async Task CargarRemolques()
    {
        CargandoRemolques.IsVisible = true;
        CargandoRemolques.IsRunning = true;
        try
        {
            var lista = await _sheets.ObtenerListaRemolquesAsync();
            RemolquePicker.ItemsSource = lista;
        }
        finally
        {
            CargandoRemolques.IsVisible = false;
            CargandoRemolques.IsRunning = false;
        }
    }

    // Consulta y puebla los catálogos de Origen y Destino.
    private async Task CargarDestinos()
    {
        try
        {
            var destinos = await _sheets.ObtenerListaDestinosAsync();
            OrigenPicker.ItemsSource = destinos;
            DestinoPicker.ItemsSource = destinos;
        }
        catch (Exception ex)
        {
            string error = ex.Message.ToLower();
            string msj = "No se pudieron cargar los destinos.";

            if (error.Contains("timedout") || error.Contains("network") || error.Contains("100"))
            {
                msj = "Internet inestable. No se pudieron cargar los destinos.";
            }

            await DisplayAlert("Aviso", msj, "OK");
        }
    }

    // Consulta y puebla la lista de clientes activos.
    private async Task CargarClientes()
    {
        try
        {
            var clientes = await _sheets.ObtenerListaClientesAsync();
            ClientePicker.ItemsSource = clientes;
        }
        catch (Exception ex)
        {
            string error = ex.Message.ToLower();
            string msj = "No se pudieron cargar los clientes.";

            if (error.Contains("timedout") || error.Contains("network") || error.Contains("100"))
            {
                msj = "Internet inestable. No se pudieron cargar los clientes.";
            }

            await DisplayAlert("Aviso", msj, "OK");
        }
    }

    #endregion

    #region Lógica de Negocio y Guardado

    // Valida la integridad de los datos, genera un folio y registra el inicio del viaje en la nube.
    private async void OnGuardarViajeClicked(object sender, EventArgs e)
    {
        // Validación de campos obligatorios
        if (OrigenPicker.SelectedIndex == -1 || DestinoPicker.SelectedIndex == -1 ||
            UnidadPicker.SelectedIndex == -1 || ClientePicker.SelectedIndex == -1 ||
            string.IsNullOrWhiteSpace(KmSalidaEntry.Text) || string.IsNullOrWhiteSpace(CombustibleEntry.Text))
        {
            await DisplayAlert("Datos Incompletos", "Por favor, completa todos los campos para continuar.", "Aceptar");
            return;
        }

        // 1. BLOQUEAMOS EL BOTÓN, CAMBIAMOS EL TEXTO Y MOSTRAMOS EL CIRCULITO
        BtnGuardarViaje.IsEnabled = false;
        BtnGuardarViaje.Text = "INICIANDO...";
        CargandoGuardar.IsVisible = true;
        CargandoGuardar.IsRunning = true;
        IsBusy = true;

        try
        {
            // Preparación de datos para el registro
            string folio = await _sheets.GenerarFolioViajeAsync();
            string idOp = Preferences.Default.Get("UsuarioLogeado", "Sin ID");
            string fechaInicio = DateTime.Now.ToString("dd/MM/yyyy HH:mm");
            string remolqueSeleccionado = CheckLlevaRemolque.IsChecked
                ? (RemolquePicker.SelectedItem?.ToString() ?? "N/A")
                : "SIN REMOLQUE";

            // Escritura en Google Sheets
            bool exito = await _sheets.RegistrarInicioViajeAsync(
                folio,
                fechaInicio,
                idOp,
                UnidadPicker.SelectedItem.ToString(),
                remolqueSeleccionado,
                OrigenPicker.SelectedItem.ToString(),
                DestinoPicker.SelectedItem.ToString(),
                ClientePicker.SelectedItem.ToString(),
                KmSalidaEntry.Text,
                CombustibleEntry.Text,
                "EN TRANSITO"
            );

            if (exito)
            {
                // Persistencia local para el seguimiento del viaje activo
                Preferences.Default.Set("Destino_" + folio, DestinoPicker.SelectedItem.ToString());
                Preferences.Default.Set("ViajeEnCurso", true);
                Preferences.Default.Set("FolioPendiente", folio);
                Preferences.Default.Set($"TrackingUbicacionActivo_{folio}", true);
                Preferences.Default.Set("Inicio_" + folio, fechaInicio);
                Preferences.Default.Set("KmSalida_" + folio, KmSalidaEntry.Text);
                Preferences.Default.Set("FuelSalida_" + folio, CombustibleEntry.Text);

                string hojaMes = $"Historial_{DateTime.Now.ToString("MMMM")}_{DateTime.Now.Year}";
                var (filaTracking, sId) = await _sheets.ObtenerOCrearFilaTrackingAsync(hojaMes, folio);
                bool vinculoOk = await _sheets.CrearVinculoHistorialAsync(folio, hojaMes, sId, filaTracking);

                if (!vinculoOk)
                {
                    // Opcional: registrar si no se pudo crear el link
                    await DisplayAlert("Aviso", "Viaje iniciado, pero no se pudo crear el acceso directo.", "OK");
                }

                Preferences.Default.Set("HojaActual_Tracking", hojaMes);
                Preferences.Default.Set("FilaActual_Tracking", filaTracking);
                Preferences.Default.Remove("ColTracking_A");
                Preferences.Default.Remove("ColTracking_B");

                try
                {
                    var request = new GeolocationRequest(GeolocationAccuracy.High, TimeSpan.FromSeconds(10));
                    var location = await Geolocation.Default.GetLocationAsync(request);

                    if (location != null)
                    {
                        var primeraUbi = new UbicacionViaje
                        {
                            FolioViaje = folio,
                            Latitud = location.Latitude,
                            Longitud = location.Longitude,
                            FechaHora = DateTime.Now
                        };

                        // Mandamos a la Fila 2 inmediatamente
                        primeraUbi.HojaTracking = hojaMes;
                        primeraUbi.FilaTracking = filaTracking;

                        var dbLocal = new BaseDeDatosLocal();
                        await dbLocal.GuardarUbicacionLocalAsync(primeraUbi);
                        bool primeraSubidaOk = await _sheets.SubirPuntoGPSTiempoRealAsync(hojaMes, folio, filaTracking, primeraUbi);
                        if (primeraSubidaOk && primeraUbi.Id > 0)
                            await dbLocal.BorrarUbicacionAsync(primeraUbi.Id);
                        Preferences.Default.Set($"UltimaCapturaTrackingTicks_{folio}", primeraUbi.FechaHora.Ticks);
                    }
                }
                catch (Exception ex)
                {
                    // Si falla el GPS aquí, guardamos el error pero dejamos que el viaje inicie
                    await GestorErrores.GuardarErrorAsync(ex, "PrimeraUbicacionAuto", Preferences.Get("UsuarioLogeado", "Desconocido"));
                }
                bool permisoNotificaciones = await ControlTrackingSegundoPlano.SolicitarPermisoNotificacionesAsync();
                if (!permisoNotificaciones)
                {
                    Preferences.Default.Set($"AvisoNotificacionTracking_{folio}", true);
                    await DisplayAlert(
                        "Permiso recomendado",
                        "Permite las notificaciones de LogiApp para ver el aviso de viaje en curso mientras se registra la ruta.",
                        "Entendido");
                }
                ControlTrackingSegundoPlano.Iniciar();
                await DisplayAlert("Éxito", $"Viaje {folio} registrado. ¡Buen camino!", "OK");

                // Navegación automática a la pantalla de monitoreo/cierre
                Application.Current.MainPage = new NavigationPage(new Vistas.PaginaTerminarViaje(folio));
            }
        }
        catch (Exception ex)
        {
            string error = ex.Message.ToLower();
            string msj = "Ocurrió un problema al guardar el viaje. Intenta nuevamente.";

            if (error.Contains("timedout") || error.Contains("network") || error.Contains("100"))
            {
                msj = "Error de conexión. Revisa tu acceso a internet e intenta iniciar el viaje de nuevo.";
            }

            await DisplayAlert("Revisa tu conexión", msj, "Entendido");
        }
        finally
        {
            // 2. REGRESAMOS EL BOTÓN Y EL CIRCULITO A LA NORMALIDAD (Incluso si hay error)
            BtnGuardarViaje.IsEnabled = true;
            BtnGuardarViaje.Text = "INICIAR VIAJE";
            CargandoGuardar.IsVisible = false;
            CargandoGuardar.IsRunning = false;
            IsBusy = false;
        }
    }

    #endregion

    #region Eventos de UI

    // Gestiona la visibilidad condicional del selector de remolque.
    private void OnRemolqueCheckedChanged(object sender, CheckedChangedEventArgs e)
    {
        SeccionRemolque.IsVisible = e.Value;

        if (!e.Value)
        {
            RemolquePicker.SelectedItem = null;
        }
    }

    private void OnVolverClicked(object sender, EventArgs e)
    {
        Application.Current.MainPage = new NavigationPage(new Vistas.PaginaMenu());
    }

    #endregion
}