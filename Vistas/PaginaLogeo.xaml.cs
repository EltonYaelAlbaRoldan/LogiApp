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

        private async void OnLoginClicked(object sender, EventArgs e)
        {
            await DisplayAlert("Login", "Entrando...", "OK");
        }

        private async void OnRegisterClicked(object sender, EventArgs e)
        {
            await DisplayAlert("Registro", "Abriendo...", "OK");
        }
    }
}