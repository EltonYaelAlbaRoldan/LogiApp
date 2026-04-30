using LogiApp.Servicios;
using LogiApp.Modelos;
using Microsoft.Maui.Devices.Sensors;
using Microsoft.Maui.Networking;

namespace LogiApp.Vistas;

// Gestiona el cierre de viajes, reportes de ubicación GPS y sincronización de datos offline.
public partial class PaginaTerminarViaje : ContentPage
{
    #region Atributos y Servicios

    private ConsultasGoogleSheets _sheets;
    private ConsultasGoogleDrive _drive;
    private readonly ConexionGoogleAuth _authService = new();
    private string _folioActual;
    private string _rutaFotoTemporal = string.Empty;
    private CancellationTokenSource _ctsTracking;
    private bool _isTrackingRunning = false;
    #endregion

    #region Constructor

    public PaginaTerminarViaje(string folio)
    {
        InitializeComponent();
        _folioActual = folio;
        LblFolio.Text = $"Folio: {folio}";

        string destinoGuardado = Preferences.Default.Get("Destino_" + folio, "Destino no especificado");
        LblDestino.Text = $"Destino: {destinoGuardado}";
    }

    #endregion

    #region Ciclo de Vida y Sincronización

    // Inicializa servicios y verifica si existen procesos pendientes de sincronización al entrar.
    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await InicializarServicio();
        if (!_isTrackingRunning)
        {
            _ = IniciarTimerTracking(); // El '_' es para que corra en paralelo sin trabar la pantalla
        }
        // Restaurar estado visual si el arribo ya fue confirmado localmente
        if (Preferences.Default.Get("ArriboConfirmado_" + _folioActual, false))
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                BtnReportar.IsEnabled = false;
                BtnArribo.IsEnabled = false;
                BtnArribo.Text = "LLEGADA REGISTRADA ✅";
                SeccionCierre.IsEnabled = true;
                SeccionCierre.Opacity = 1.0;
            });
        }

        Connectivity.Current.ConnectivityChanged += OnConnectivityChanged;

        // Dispara el sensor de sincronización en segundo plano
        _ = IntentarSincronizarDatosEnVivoAsync();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        Connectivity.Current.ConnectivityChanged -= OnConnectivityChanged;
    }

    // Detecta cambios en la red para intentar subir datos pendientes automáticamente.
    private async void OnConnectivityChanged(object sender, ConnectivityChangedEventArgs e)
    {
        if (e.NetworkAccess == NetworkAccess.Internet)
        {
            await IntentarSincronizarDatosEnVivoAsync();
        }
    }

    #endregion

    #region Lógica de Negocio (GPS y Sincronización Offline)

    // Configura la conexión a las APIs de Google solo si no han sido instanciadas.
    private async Task InicializarServicio()
    {
        if (_sheets != null) return;

        try
        {
            var (sService, dService) = await _authService.ObtenerServiciosAsync();
            _sheets = new ConsultasGoogleSheets(sService);
            _drive = new ConsultasGoogleDrive(dService);
        }
        catch { }
    }

    // "El Sensor": Revisa la bóveda de Preferences y sube GPS o Arribos que quedaron pendientes por falta de red.
    private async Task IntentarSincronizarDatosEnVivoAsync()
    {
        if (_sheets == null || Connectivity.Current.NetworkAccess != NetworkAccess.Internet) return;

        // 1. Sincronización de GPS pendiente
        bool ubiPendiente = Preferences.Default.Get($"Offline_UbiPendiente_{_folioActual}", false);
        if (ubiPendiente)
        {
            double lat = Preferences.Default.Get($"Offline_UbiLat_{_folioActual}", 0.0);
            double lon = Preferences.Default.Get($"Offline_UbiLon_{_folioActual}", 0.0);
            string horaCapturada = Preferences.Default.Get($"Offline_UbiHora_{_folioActual}", "");

            try
            {
                bool exito = await _sheets.ActualizarUbicacionViajeAsync(_folioActual, lat, lon, horaCapturada);
                if (exito)
                {
                    Preferences.Default.Remove($"Offline_UbiPendiente_{_folioActual}");
                    Preferences.Default.Remove($"Offline_UbiLat_{_folioActual}");
                    Preferences.Default.Remove($"Offline_UbiLon_{_folioActual}");
                    Preferences.Default.Remove($"Offline_UbiHora_{_folioActual}");
                }
            }
            catch { }
        }

        // 2. Sincronización de Arribo pendiente
        bool arriboPendiente = Preferences.Default.Get($"Offline_ArriboPendiente_{_folioActual}", false);
        if (arriboPendiente)
        {
            string horaLlegada = Preferences.Default.Get("HoraLlegadaReal_" + _folioActual, "");
            try
            {
                bool exito = await _sheets.ActualizarEstatusYLlegadaAsync(_folioActual, "EN DESTINO", horaLlegada);
                if (exito) Preferences.Default.Remove($"Offline_ArriboPendiente_{_folioActual}");
            }
            catch { }
        }
    }

    #endregion

    #region Eventos de Monitoreo y GPS

    // Obtiene las coordenadas actuales y decide si enviarlas a la nube o guardarlas localmente.
    private async void OnReportarUbicacionClicked(object sender, EventArgs e)
    {
        try
        {
            BtnReportar.IsEnabled = false;
            BtnReportar.Text = "Enviando...";

            // 1. Obtener ubicación (Lógica original)
            var request = new GeolocationRequest(GeolocationAccuracy.High, TimeSpan.FromSeconds(10));
            var location = await Geolocation.Default.GetLocationAsync(request);

            if (location == null)
            {
                await DisplayAlert("Error", "No se pudo obtener la ubicación GPS.", "OK");
                return;
            }

            // 2. Crear el objeto con la nueva estructura
            var punto = new UbicacionViaje
            {
                FolioViaje = _folioActual,
                Latitud = location.Latitude,
                Longitud = location.Longitude,
                FechaHora = DateTime.Now,
                Sincronizado = false
            };

            // 3. Recuperar las columnas reservadas y nombre de hoja
            string colA = Preferences.Default.Get("ColTracking_A", "A");
            string colB = Preferences.Default.Get("ColTracking_B", "B");
            int filaActual = Preferences.Default.Get("FilaActual_Tracking", 2);
            string hoja = $"Historial_{DateTime.Now.ToString("MMMM")}_{DateTime.Now.Year}";

            bool subioExitosamente = false;

            // 4. Lógica de envío con respaldo (La nueva inteligencia)
            if (Connectivity.Current.NetworkAccess == NetworkAccess.Internet)
            {
                // Intentar subir a Google Sheets
                subioExitosamente = await _sheets.SubirPuntoGPSTiempoRealAsync(hoja, colA, colB, filaActual, punto);
            }

            if (subioExitosamente)
            {
                // Éxito total: Feedback visual original
                Preferences.Default.Set("FilaActual_Tracking", filaActual + 1);
                BtnReportar.BackgroundColor = Colors.Green;
                BtnReportar.Text = "UBICACIÓN ENVIADA ✅";
            }
            else
            {
                // Respaldo: Guardar en SQLite si no hay red o falló el envío
                punto.Id = filaActual;
                var dbLocal = new BaseDeDatosLocal();
                await dbLocal.GuardarUbicacionLocalAsync(punto);

                Preferences.Default.Set("FilaActual_Tracking", filaActual + 1);

                BtnReportar.BackgroundColor = Colors.Orange;
                BtnReportar.Text = "GUARDADO LOCAL (SIN RED) ⚠️";

                // Opcional: Avisar al usuario que se sincronizará después
                await DisplayAlert("Modo Offline", "Ubicación guardada en el dispositivo. Se sincronizará al recuperar señal.", "OK");
            }

            // 5. Resetear botón después de 3 segundos (Lógica original)
            await Task.Delay(3000);
            BtnReportar.BackgroundColor = Color.FromArgb("#4CAF50");
            BtnReportar.Text = "REPORTAR UBICACIÓN ACTUAL";
        }
        catch (Exception ex)
        {
            await GestorErrores.GuardarErrorAsync(ex, "ReportarUbicacion", Preferences.Get("UsuarioLogeado", "Desconocido"));
            await DisplayAlert("Error", "Fallo al reportar: " + ex.Message, "OK");
        }
        finally
        {
            BtnReportar.IsEnabled = true;
        }
    }

    #endregion

    #region Temporizador de Sincronización
    private async Task IniciarTimerTracking()
    {
        _isTrackingRunning = true;
        _ctsTracking = new CancellationTokenSource();

        // El ciclo se repite mientras el viaje no termine
        while (_isTrackingRunning && !_ctsTracking.Token.IsCancellationRequested)
        {
            try
            {
                // Esperar 10 minutos (600 segundos)
                // Para pruebas puedes ponerlo en 1 minuto: TimeSpan.FromMinutes(1)
                await Task.Delay(TimeSpan.FromMinutes(10), _ctsTracking.Token);

                // EJECUTAR EL REPORTE AUTOMÁTICO
                await RealizarReporteAutomatico();
            }
            catch (TaskCanceledException) { break; }
        }
    }

    private async Task RealizarReporteAutomatico()
    {
        // Esta es la misma lógica que ya tienes en el botón, 
        // pero sin alertas para no molestar al operador
        var location = await Geolocation.Default.GetLocationAsync(new GeolocationRequest(GeolocationAccuracy.Medium));

        if (location != null)
        {
            var punto = new UbicacionViaje
            {
                FolioViaje = _folioActual,
                Latitud = location.Latitude,
                Longitud = location.Longitude,
                FechaHora = DateTime.Now
            };

            string colA = Preferences.Default.Get("ColTracking_A", "A");
            string colB = Preferences.Default.Get("ColTracking_B", "B");
            int fila = Preferences.Default.Get("FilaActual_Tracking", 2);
            string hoja = $"Historial_{DateTime.Now.ToString("MMMM")}_{DateTime.Now.Year}";

            bool ok = await _sheets.SubirPuntoGPSTiempoRealAsync(hoja, colA, colB, fila, punto);

            if (ok) Preferences.Default.Set("FilaActual_Tracking", fila + 1);
            else await new BaseDeDatosLocal().GuardarUbicacionLocalAsync(punto);
        }
    }
    #endregion

    
    #region Gestión de Arribo y Finalización

    // Registra la llegada al destino y calcula la duración total del trayecto.
    private async void OnMarcarLlegadaClicked(object sender, EventArgs e)
    {
        var confirmar = await DisplayAlert("Confirmar", "¿Has llegado a tu destino?", "Sí", "No");
        if (!confirmar) return;

        IndicadorCarga.IsVisible = true;

        string horaLlegadaReal = DateTime.Now.ToString("dd/MM/yyyy HH:mm");
        string horaInicio = Preferences.Default.Get("Inicio_" + _folioActual, horaLlegadaReal);
        string duracionStr = "N/A";

        if (DateTime.TryParseExact(horaInicio, "dd/MM/yyyy HH:mm", null, System.Globalization.DateTimeStyles.None, out DateTime dtInicio) &&
            DateTime.TryParseExact(horaLlegadaReal, "dd/MM/yyyy HH:mm", null, System.Globalization.DateTimeStyles.None, out DateTime dtFin))
        {
            duracionStr = $"{(int)(dtFin - dtInicio).TotalHours}h {(dtFin - dtInicio).Minutes}m";
        }

        Preferences.Default.Set("Duracion_" + _folioActual, duracionStr);
        Preferences.Default.Set("ArriboConfirmado_" + _folioActual, true);
        Preferences.Default.Set("HoraLlegadaReal_" + _folioActual, horaLlegadaReal);

        // Intento de actualización inmediata o marcado para sincronización posterior
        if (Connectivity.Current.NetworkAccess == NetworkAccess.Internet)
        {
            try { await _sheets.ActualizarEstatusYLlegadaAsync(_folioActual, "EN DESTINO", horaLlegadaReal); }
            catch { Preferences.Default.Set($"Offline_ArriboPendiente_{_folioActual}", true); }
        }
        else
        {
            Preferences.Default.Set($"Offline_ArriboPendiente_{_folioActual}", true);
        }

        MainThread.BeginInvokeOnMainThread(() =>
        {
            BtnReportar.IsEnabled = false;
            BtnArribo.IsEnabled = false;
            BtnArribo.Text = $"LLEGADA ✅ ({duracionStr})";
            SeccionCierre.IsEnabled = true;
            SeccionCierre.Opacity = 1.0;
        });

        IndicadorCarga.IsVisible = false;
    }

    // Valida datos finales, guarda evidencia fotográfica y cierra el folio en la base de datos.
    private async void OnTerminarViajeClicked(object sender, EventArgs e)
    {
        // 1. Validaciones iniciales de entrada
        if (string.IsNullOrWhiteSpace(KmLlegadaEntry.Text) ||
            string.IsNullOrWhiteSpace(CombustibleLlegadaEntry.Text) ||
            string.IsNullOrEmpty(_rutaFotoTemporal))
        {
            await DisplayAlert("Atención", "Captura kilometraje, combustible y foto obligatoria.", "OK");
            return;
        }

        // 2. Cálculos de lógica de negocio (KM y Combustible)
        double kmSalida = 0, fuelSalida = 0, kmLlegada = 0, fuelLlegada = 0;
        double.TryParse(Preferences.Default.Get("KmSalida_" + _folioActual, "0"), out kmSalida);
        double.TryParse(Preferences.Default.Get("FuelSalida_" + _folioActual, "0"), out fuelSalida);
        double.TryParse(KmLlegadaEntry.Text, out kmLlegada);
        double.TryParse(CombustibleLlegadaEntry.Text, out fuelLlegada);

        if (kmLlegada < kmSalida || fuelLlegada > fuelSalida)
        {
            await DisplayAlert("Error", "Los datos de KM o Combustible no son lógicos.", "OK");
            return;
        }

        // Interfaz en modo carga
        BtnFinalizar.IsEnabled = false;
        IndicadorCarga.IsVisible = true;

        // 3. Preparación de datos para guardado
        string recorridoKmStr = (kmLlegada - kmSalida).ToString("0.##");
        string consumoLitrosStr = (fuelSalida - fuelLlegada).ToString("0.##");
        string duracionViaje = Preferences.Default.Get("Duracion_" + _folioActual, "N/A");
        string fechaFinReal = DateTime.Now.ToString("dd/MM/yyyy HH:mm");
        string horaLlegadaReal = Preferences.Default.Get("HoraLlegadaReal_" + _folioActual, fechaFinReal);

        // Mover foto a directorio seguro de la App
        string rutaSeguraFoto = Path.Combine(FileSystem.AppDataDirectory, $"evidencia_{_folioActual}.jpg");
        File.Copy(_rutaFotoTemporal, rutaSeguraFoto, true);

        // 4. PERSISTENCIA DE EMERGENCIA (Bóveda Offline)
        // Guardamos todo localmente PRIMERO por si falla el internet
        Preferences.Default.Set($"Offline_Pendiente_{_folioActual}", true);
        Preferences.Default.Set($"Offline_HoraLlegada_{_folioActual}", horaLlegadaReal);
        Preferences.Default.Set($"Offline_FechaFin_{_folioActual}", fechaFinReal);
        Preferences.Default.Set($"Offline_KmLlegada_{_folioActual}", KmLlegadaEntry.Text);
        Preferences.Default.Set($"Offline_FuelLlegada_{_folioActual}", CombustibleLlegadaEntry.Text);
        Preferences.Default.Set($"Offline_Obs_{_folioActual}", ObservacionesEditor.Text ?? "N/A");
        Preferences.Default.Set($"Offline_RutaFoto_{_folioActual}", rutaSeguraFoto);
        Preferences.Default.Set($"Offline_Duracion_{_folioActual}", duracionViaje);
        Preferences.Default.Set($"Offline_Recorrido_{_folioActual}", recorridoKmStr);
        Preferences.Default.Set($"Offline_Consumo_{_folioActual}", consumoLitrosStr);

        // Agregar a la cola de pendientes
        string foliosAtorados = Preferences.Default.Get("ListaFoliosOffline", "");
        if (!foliosAtorados.Contains(_folioActual))
        {
            Preferences.Default.Set("ListaFoliosOffline", foliosAtorados + _folioActual + ",");
        }

        // --- PASO CRÍTICO PARA EL MENÚ ---
        // Liberamos los candados inmediatamente para que el menú principal sepa que YA NO hay viaje activo
        Preferences.Default.Set("ViajeEnCurso", false);
        Preferences.Default.Remove("FolioPendiente");
        // ---------------------------------

        // 5. INTENTO DE SINCRONIZACIÓN EN TIEMPO REAL
        if (Connectivity.Current.NetworkAccess == NetworkAccess.Internet)
        {
            try
            {
                string idOp = Preferences.Default.Get("UsuarioLogeado", "SinID");

                // Subir a Drive y obtener Link
                string linkCarpeta = await _drive.SubirEvidenciaViajeRapidoAsync(rutaSeguraFoto, idOp, _folioActual);

                // Actualizar Sheets
                await _sheets.ActualizarEstatusYLlegadaAsync(_folioActual, "EN DESTINO", horaLlegadaReal);
                bool exito = await _sheets.FinalizarViajeAsync(_folioActual, fechaFinReal, KmLlegadaEntry.Text, CombustibleLlegadaEntry.Text, ObservacionesEditor.Text, linkCarpeta, duracionViaje, recorridoKmStr, consumoLitrosStr);

                if (exito)
                {
                    // Si se subió, limpiamos la bóveda offline
                    Preferences.Default.Remove($"Offline_Pendiente_{_folioActual}");
                    string listaActualizada = Preferences.Default.Get("ListaFoliosOffline", "");
                    listaActualizada = listaActualizada.Replace(_folioActual + ",", "");
                    Preferences.Default.Set("ListaFoliosOffline", listaActualizada);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Sincronización fallida (se queda en offline): " + ex.Message);
            }
        }

        _isTrackingRunning = false;
        _ctsTracking?.Cancel();
        // 6. FINALIZACIÓN Y RETORNO
        IndicadorCarga.IsVisible = false;
        await DisplayAlert("Viaje Terminado", "Datos capturados con éxito. El viaje se sincronizará automáticamente.", "OK");

        // Cambiamos a la raíz del menú para refrescar todo el estado
        Application.Current.MainPage = new NavigationPage(new PaginaMenu());
    }

    #endregion

    #region Manejo de Evidencias

    // Captura una fotografía utilizando la cámara del dispositivo para evidencia de llegada.
    private async void OnTomarFotoClicked(object sender, EventArgs e)
    {
        if (MediaPicker.Default.IsCaptureSupported)
        {
            FileResult photo = await MediaPicker.Default.CapturePhotoAsync();
            if (photo != null)
            {
                _rutaFotoTemporal = photo.FullPath;
                var stream = await photo.OpenReadAsync();
                ImgPreview.Source = ImageSource.FromStream(() => stream);
                ImgPreview.IsVisible = true;
            }
        }
    }

    #endregion
}