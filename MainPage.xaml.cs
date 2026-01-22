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
            var tester = new GoogleServiceTest();
            await tester.ProbarConexion();
        }
    }
}
