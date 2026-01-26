using LogiApp.Servicios;

namespace LogiApp.Vistas;

public partial class PaginaRegistro : ContentPage
{
    // Usamos los nombres de clase correctos de tus archivos de servicio
    private ConsultasGoogleSheets _sheets;
    private ConsultasGoogleDrive _drive;
    private readonly ConexionGoogleAuth _authService = new();
    public bool IsNotBusy => !IsBusy;
    public PaginaRegistro()
    {
        InitializeComponent(); // Esto ya no debería dar error si el XAML está bien
        InicializarConexion();

    }

    private async void InicializarConexion()
    {
        try
        {
            // Obtenemos los servicios reales desde el JSON
            var (sService, dService) = await _authService.ObtenerServiciosAsync();

            // Ahora sí, inicializamos tus consultas con servicios reales
            _sheets = new ConsultasGoogleSheets(sService);
            _drive = new ConsultasGoogleDrive(dService);
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error de Auth", ex.Message, "OK");
        }
    }

    private async void OnProbarRegistroClicked(object sender, EventArgs e)
    {
        // --- CAPA 1: VALIDACIÓN DE CAMPOS VACÍOS ---
        if (string.IsNullOrWhiteSpace(NombreEntry.Text) || string.IsNullOrWhiteSpace(EdadEntry.Text))
        {
            await DisplayAlert("Campos Incompletos", "Todos los campos son obligatorios para el registro.", "Revisar");
            return;
        }

        // --- CAPA 2: VALIDACIÓN DE FORMATO (EDAD) ---
        // Usamos int.TryParse para evitar que la app truene si meten letras
        if (!int.TryParse(EdadEntry.Text, out int edad))
        {
            await DisplayAlert("Error de Formato", "La edad debe ser un número entero.", "Corregir");
            return;
        }

        // --- CAPA 3: LÓGICA DE NEGOCIO (RANGOS) ---
        if (edad < 18 || edad > 99)
        {
            await DisplayAlert("Edad no permitida", "El operador debe tener entre 18 y 99 años.", "OK");
            return;
        }

        // --- CAPA 4: REGLAS DE SEGURIDAD (NOMBRE) ---
        // Evitamos nombres demasiado cortos o con caracteres extraños si lo deseas
        if (NombreEntry.Text.Trim().Length < 3)
        {
            await DisplayAlert("Nombre Inválido", "Ingresa el nombre completo del operador.", "OK");
            return;
        }

        try
        {
            // 1. ACTIVAR ESTADO DE CARGA
            IsBusy = true; // Activa el ActivityIndicator en el XAML
            OnPropertyChanged(nameof(IsNotBusy)); // Notifica al botón que debe desactivarse

            // 2. EJECUCIÓN DE APIs (Febrero Semana 1 y 2)
            string idFormateado = await _sheets.GenerarIdUnicoFormateadoAsync();

            string nombreCarpeta = $"{idFormateado} - {NombreEntry.Text.Trim()}";
            string folderId = await _drive.CrearEstructuraOperadorAsync(nombreCarpeta);

            await _sheets.RegistrarOperadorAsync(NombreEntry.Text.Trim(), EdadEntry.Text, folderId, idFormateado);

            await DisplayAlert("Registro Exitoso", $"Operador {idFormateado} guardado en la nube.", "Finalizar");

            // Limpieza de campos
            NombreEntry.Text = string.Empty;
            EdadEntry.Text = string.Empty;
        }
        catch (Exception ex)
        {
            await DisplayAlert("Fallo de Conexión", "Error al conectar con Google: " + ex.Message, "Cerrar");
        }
        finally
        {
            // 3. DESACTIVAR ESTADO DE CARGA
            IsBusy = false; // Oculta el ActivityIndicator
            OnPropertyChanged(nameof(IsNotBusy)); // Reactiva el botón de registro
        }
    }
}