namespace LogiApp.Servicios;

/// <summary>
/// Puente pequeño entre la aplicación compartida y el servicio nativo de Android.
/// En las demás plataformas no hace nada y conserva la compilación multiplataforma.
/// </summary>
public static class ControlTrackingSegundoPlano
{
    public static async Task<bool> SolicitarPermisoNotificacionesAsync()
    {
#if ANDROID
        if (Android.OS.Build.VERSION.SdkInt < Android.OS.BuildVersionCodes.Tiramisu)
            return true;

        var estado = await Permissions.CheckStatusAsync<PermisoNotificacionesAndroid>();
        if (estado != PermissionStatus.Granted)
            estado = await Permissions.RequestAsync<PermisoNotificacionesAndroid>();
        return estado == PermissionStatus.Granted;
#else
        await Task.CompletedTask;
        return true;
#endif
    }

    public static void Iniciar()
    {
#if ANDROID
        var contexto = Android.App.Application.Context;
        var intent = new Android.Content.Intent(contexto, typeof(LogiApp.Platforms.Android.ServicioUbicacionViaje));
        if (Android.OS.Build.VERSION.SdkInt >= Android.OS.BuildVersionCodes.O)
            contexto.StartForegroundService(intent);
        else
            contexto.StartService(intent);
#endif
    }

    public static void Detener()
    {
#if ANDROID
        var contexto = Android.App.Application.Context;
        contexto.StopService(new Android.Content.Intent(
            contexto, typeof(LogiApp.Platforms.Android.ServicioUbicacionViaje)));
#endif
    }
}

#if ANDROID
internal sealed class PermisoNotificacionesAndroid : Permissions.BasePlatformPermission
{
    public override (string androidPermission, bool isRuntime)[] RequiredPermissions =>
        new[] { (Android.Manifest.Permission.PostNotifications, true) };
}
#endif
