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

    private async void OnRegresarClicked(object sender, EventArgs e)
    {
        // Opción A: Si entraste con PushAsync, esto te regresa con una animación suave
        // await Navigation.PopAsync();

        // Opción B: Si quieres forzar el regreso a una página específica (como el Logeo inicial)
        // Application.Current.MainPage = new NavigationPage(new Vistas.PaginaLogeo());

        // Para tu flujo actual de prueba, usemos un simple aviso o retorno:
        bool answer = await DisplayAlert("Salir", "¿Deseas volver a la pantalla anterior?", "Sí", "No");
        if (answer)
        {
            // Esto asume que tienes una página previa en el stack de navegación
            Application.Current.MainPage = new NavigationPage(new Vistas.PaginaLogeo());
        }
    }
}