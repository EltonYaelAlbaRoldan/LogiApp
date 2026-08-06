using Microsoft.Maui.Networking;
using LogiApp.Servicios;
using LogiApp.Vistas;

namespace LogiApp
{
    public partial class App : Application
    {
        public App()
        {
            InitializeComponent();
            Connectivity.Current.ConnectivityChanged += OnGlobalConnectivityChanged;
            _ = SincronizadorGlobal.IntentarSincronizacionSilenciosa();

            // Revisamos si el usuario ya estaba logeado
            string usuario = Preferences.Default.Get("UsuarioLogeado", "");
            // Revisamos si dejó un viaje a medias
            bool hayViaje = Preferences.Default.Get("ViajeEnCurso", false);
            string folio = Preferences.Default.Get("FolioPendiente", "");

            if (!string.IsNullOrEmpty(usuario))
            {
                if (hayViaje && !string.IsNullOrEmpty(folio))
                {
                    // RESCATE: Si hay viaje activo, lo mandamos directo a finalizar
                    MainPage = new NavigationPage(new Vistas.PaginaTerminarViaje(folio));
                }
                else
                {
                    // Caso normal: al Menú
                    MainPage = new NavigationPage(new Vistas.PaginaMenu());
                }
            }
            else
            {
                // No hay sesión: al Logeo
                MainPage = new NavigationPage(new Vistas.PaginaLogeo());
            }
        }

        // EVENTO DEL SENSOR: Se dispara automáticamente a nivel sistema operativo
        // en cuanto el teléfono detecta que hay WiFi o 4G.
        private async void OnGlobalConnectivityChanged(object sender, ConnectivityChangedEventArgs e)
        {
            if (e.NetworkAccess == NetworkAccess.Internet)
            {
                // Ejecutamos a nuestro trabajador silencioso
                await SincronizadorGlobal.IntentarSincronizacionSilenciosa();
            }
        }
    }
}
