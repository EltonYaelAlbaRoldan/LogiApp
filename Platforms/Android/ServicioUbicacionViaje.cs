using Android;
using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Locations;
using Android.OS;
using AndroidX.Core.App;
using AndroidX.Core.Content;
using LogiApp.Modelos;
using LogiApp.Servicios;

namespace LogiApp.Platforms.Android;

[Service(
    Exported = false,
    ForegroundServiceType = ForegroundService.TypeLocation)]
public sealed class ServicioUbicacionViaje : Service, ILocationListener
{
    private const int IdNotificacion = 2101;
    private const string CanalNotificacion = "logiapp_viaje_activo";
    private static readonly TimeSpan IntervaloCaptura = TimeSpan.FromMinutes(10);
    private readonly SemaphoreSlim _semaforoCaptura = new(1, 1);
    private LocationManager? _locationManager;

    public override void OnCreate()
    {
        base.OnCreate();
        CrearCanalNotificacion();
        StartForeground(IdNotificacion, CrearNotificacion());
    }

    public override StartCommandResult OnStartCommand(Intent? intent, StartCommandFlags flags, int startId)
    {
        string folio = Preferences.Default.Get("FolioPendiente", "");
        if (!Preferences.Default.Get("ViajeEnCurso", false) ||
            string.IsNullOrWhiteSpace(folio) ||
            !Preferences.Default.Get($"TrackingUbicacionActivo_{folio}", true))
        {
            StopSelf();
            return StartCommandResult.NotSticky;
        }

        IniciarEscuchaGps();
        return StartCommandResult.Sticky;
    }

    private void IniciarEscuchaGps()
    {
        if (ContextCompat.CheckSelfPermission(this, Manifest.Permission.AccessFineLocation) != Permission.Granted &&
            ContextCompat.CheckSelfPermission(this, Manifest.Permission.AccessCoarseLocation) != Permission.Granted)
        {
            StopSelf();
            return;
        }

        _locationManager ??= (LocationManager?)GetSystemService(LocationService);
        if (_locationManager == null) return;

        _locationManager.RemoveUpdates(this);
        const long diezMinutosMs = 10 * 60 * 1000;
        if (_locationManager.IsProviderEnabled(LocationManager.GpsProvider))
            _locationManager.RequestLocationUpdates(LocationManager.GpsProvider, diezMinutosMs, 0, this, Looper.MainLooper);
        if (_locationManager.IsProviderEnabled(LocationManager.NetworkProvider))
            _locationManager.RequestLocationUpdates(LocationManager.NetworkProvider, diezMinutosMs, 0, this, Looper.MainLooper);
    }

    public async void OnLocationChanged(global::Android.Locations.Location location)
    {
        if (location == null || !Preferences.Default.Get("ViajeEnCurso", false)) return;
        if (!await _semaforoCaptura.WaitAsync(0)) return;

        try
        {
            string folio = Preferences.Default.Get("FolioPendiente", "");
            string hoja = Preferences.Default.Get("HojaActual_Tracking", "");
            int fila = Preferences.Default.Get("FilaActual_Tracking", 0);
            if (string.IsNullOrWhiteSpace(folio) || string.IsNullOrWhiteSpace(hoja) || fila < 2 ||
                !Preferences.Default.Get($"TrackingUbicacionActivo_{folio}", true)) return;

            string llaveUltima = $"UltimaCapturaTrackingTicks_{folio}";
            long ticksUltima = Preferences.Default.Get(llaveUltima, 0L);
            DateTime ahora = DateTime.Now;
            if (ticksUltima > 0 && ahora - new DateTime(ticksUltima) < IntervaloCaptura - TimeSpan.FromSeconds(15))
                return;

            // SQLite conserva los datos estructurados. El texto dd/MM HH:mm se forma
            // únicamente al enviarlo a Sheets, evitando mezclar formatos de almacenamiento.
            var punto = new UbicacionViaje
            {
                FolioViaje = folio,
                Latitud = location.Latitude,
                Longitud = location.Longitude,
                FechaHora = ahora,
                Sincronizado = false,
                HojaTracking = hoja,
                FilaTracking = fila
            };

            await new BaseDeDatosLocal().GuardarUbicacionLocalAsync(punto);
            Preferences.Default.Set(llaveUltima, ahora.Ticks);

            // Si hay red intentamos vaciar la cola; si falla, el punto permanece intacto.
            _ = SincronizadorGlobal.IntentarSincronizacionSilenciosa();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error de tracking en segundo plano: {ex}");
        }
        finally
        {
            _semaforoCaptura.Release();
        }
    }

    private void CrearCanalNotificacion()
    {
        if (Build.VERSION.SdkInt < BuildVersionCodes.O) return;
        var manager = (NotificationManager?)GetSystemService(NotificationService);
        var canal = new NotificationChannel(
            CanalNotificacion,
            "Seguimiento de viaje",
            NotificationImportance.Low)
        {
            Description = "Mantiene el registro de ubicación durante un viaje activo"
        };
        manager?.CreateNotificationChannel(canal);
    }

    private Notification CrearNotificacion()
    {
        var abrirApp = PackageManager?.GetLaunchIntentForPackage(PackageName!);
        var pending = PendingIntent.GetActivity(
            this, 0, abrirApp,
            PendingIntentFlags.Immutable | PendingIntentFlags.UpdateCurrent);

        return new NotificationCompat.Builder(this, CanalNotificacion)
            .SetContentTitle("LogiApp — viaje en curso")
            .SetContentText("Registrando la ruta aun con la pantalla apagada")
            .SetSmallIcon(Resource.Mipmap.appicon)
            .SetOngoing(true)
            .SetOnlyAlertOnce(true)
            .SetContentIntent(pending)
            .Build();
    }

    public override void OnDestroy()
    {
        _locationManager?.RemoveUpdates(this);
        base.OnDestroy();
    }

    public override IBinder? OnBind(Intent? intent) => null;
    public void OnProviderDisabled(string provider) { }
    public void OnProviderEnabled(string provider) { }
    public void OnStatusChanged(string? provider, Availability status, Bundle? extras) { }
}
