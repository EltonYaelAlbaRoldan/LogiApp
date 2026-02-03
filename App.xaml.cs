using LogiApp.Vistas;

namespace LogiApp
{
    public partial class App : Application
    {
        public App()
        {
            InitializeComponent();
            //MainPage = new NavigationPage(new PaginaLogeo());

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

        //protected override Window CreateWindow(IActivationState? activationState)
        //{
        //    return new Window(new AppShell());
        //}
    }
}