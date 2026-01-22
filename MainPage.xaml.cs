namespace LogiApp
{
    public partial class MainPage : ContentPage
    {
        int count = 0;

        public MainPage()
        {
            InitializeComponent();
        }

        private async void OnCounterClicked(object? sender, EventArgs e)
        {
            try
            {
                var tester = new GoogleServiceTest();
                await tester.ProbarConexion();
                await DisplayAlert("Éxito", "Conexión establecida correctamente", "OK");
            }
            catch (Exception ex)
            {
                // Esto les dirá si el archivo no existe o si no hay internet
                await DisplayAlert("Error de Conexión", ex.Message, "Cerrar");
            }
        }
    }
}
