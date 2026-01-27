namespace LogiApp.Vistas;

public partial class InicioSesion : ContentPage
{
    public InicioSesion()
    {
        InitializeComponent();
    }

    private async void OnLoginClicked(object sender, EventArgs e)
    {
        // Validación básica de campos
        if (string.IsNullOrWhiteSpace(UsuarioEntry.Text) || string.IsNullOrWhiteSpace(ContrasenaEntry.Text))
        {
            await DisplayAlert("Campos Vacíos", "Por favor ingresa tu usuario y contraseña.", "Aceptar");
            return;
        }

        CargandoIndicator.IsVisible = true;
        CargandoIndicator.IsRunning = true;

        // Por ahora, validación "quemada" para que pruebes tu flujo
        // Usuario: admin | Contraseña: 123
        if (UsuarioEntry.Text == "admin" && ContrasenaEntry.Text == "123")
        {
            // Navegamos al Menú Principal que ya tienes listo
            Application.Current.MainPage = new NavigationPage(new PaginaMenu());
        }
        else
        {
            await DisplayAlert("Error", "Usuario o contraseña incorrectos.", "Reintentar");
        }

        CargandoIndicator.IsRunning = false;
        CargandoIndicator.IsVisible = false;
    }

    private async void OnVolverClicked(object sender, EventArgs e)
    {
        // Esto te regresa a la pantalla anterior en la pila de navegación
        Application.Current.MainPage = new NavigationPage(new Vistas.PaginaLogeo());
    }

}