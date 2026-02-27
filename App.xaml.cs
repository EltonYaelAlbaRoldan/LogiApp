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

            // 1. ENCENDEMOS EL SENSOR GLOBAL (Escucha en segundo plano)
            Connectivity.Current.ConnectivityChanged += OnGlobalConnectivityChanged;

            // 2. LÓGICA DE NAVEGACIÓN (Persistencia de sesión)
            // Verificamos si hay una sesión activa guardada localmente
            if (Preferences.Default.ContainsKey("UsuarioLogeado"))
            {
                // Si ya hay usuario, saltamos directo al Menú
                MainPage = new NavigationPage(new Vistas.PaginaMenu());
            }
            else
            {
                // Si es la primera vez o cerró sesión, va al inicio
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