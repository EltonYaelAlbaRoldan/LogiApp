using System;
using Microsoft.Maui.Controls;

namespace LogiApp.Servicios
{
    public static class SeguridadApp
    {
        private const string FirmaOriginalB64 = "RGVzYXJyb2xsYWRvIHBvcjogRWx0b24gWWFlbCBBbGJhIFJvbGRhbiAmIE5pbGkgRXN0ZWZhbmlhIExvcGV6IEd1dGllcnJleg==";

        public static string CreditosDesarrollador { get; private set; }

        static SeguridadApp()
        {
            byte[] bytes = Convert.FromBase64String(FirmaOriginalB64);
            CreditosDesarrollador = System.Text.Encoding.UTF8.GetString(bytes);
        }

        public static void VerificarIntegridad()
        {
            byte[] bytes = Convert.FromBase64String(FirmaOriginalB64);
            string firmaIntocable = System.Text.Encoding.UTF8.GetString(bytes);

            if (CreditosDesarrollador != firmaIntocable)
            {
                throw new Exception("ERROR CRÍTICO: Violación de derechos de autor en el código fuente.");
            }

            Page paginaPrincipal = Application.Current?.MainPage;

            if (paginaPrincipal is NavigationPage navPage)
            {
                paginaPrincipal = navPage.CurrentPage;
            }

            if (paginaPrincipal is ContentPage paginaVisible)
            {
                string nombrePagina = paginaVisible.GetType().Name;

                if ((nombrePagina == "PaginaLogeo" || nombrePagina == "PaginaMenu") && paginaVisible.ControlTemplate == null)
                {
                    throw new Exception("ACCESO DENEGADO: Se intentó ocultar el pie de página con los nombres de los desarrolladores originales en una pantalla principal. Conexión a base de datos rechazada.");
                }
            }
        }
    }
}