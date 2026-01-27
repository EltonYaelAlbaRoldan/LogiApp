using System;
using Microsoft.Maui.Controls;

namespace LogiApp.Vistas;

public partial class PaginaLogeo : ContentPage
{
    public PaginaLogeo()
    {
        InitializeComponent();
    }

    private async void OnLoginClicked(object sender, EventArgs e)
    {
        Application.Current.MainPage = new NavigationPage(new Vistas.InicioSesion());
    }

    private async void OnRegisterClicked(object sender, EventArgs e)
    {
        //await DisplayAlert("Registro", "Abriendo...", "OK");
        //await Navigation.PushAsync(new Vistas.PaginaRegistro());
        Application.Current.MainPage = new NavigationPage(new Vistas.PaginaRegistro());
    }
}