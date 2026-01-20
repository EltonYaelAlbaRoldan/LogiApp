using System;
using Microsoft.Maui.Controls;

namespace LogiApp
{
    public partial class LoginPage : ContentPage
    {
        public LoginPage()
        {
            InitializeComponent();
        }

        private async void OnLoginClicked(object? sender, EventArgs e)
        {
            var user = UsernameEntry.Text?.Trim();
            var pass = PasswordEntry.Text;

            // Ejemplo simple de validación
            if (string.IsNullOrEmpty(user) || string.IsNullOrEmpty(pass))
            {
                await DisplayAlert("Error", "Introduce usuario y contraseña", "OK");
                return;
            }

            // Aquí iría la validación real (API, servicio, etc.)
            // Si login correcto, navegar a la página principal:
            await Shell.Current.GoToAsync("//MainPage");
        }
    }
}