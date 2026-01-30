using Google.Apis.Sheets.v4;
using Google.Apis.Sheets.v4.Data;

namespace LogiApp.Servicios;

/// <summary>
/// Gestiona todas las operaciones de lectura y escritura en el Spreadsheet de Google.
/// </summary>
public class ConsultasGoogleSheets
{
    private readonly SheetsService _service;
    private readonly string _spreadsheetId = "1wx2DSjhjzUeTxtxby0akYFTFul84DwSI1OMe5IYezb8";

    public ConsultasGoogleSheets(SheetsService service) => _service = service;

    #region SECCIÓN 1: OPERACIONES DE OPERADORES

    /// <summary>
    /// Registra una nueva fila en la pestaña 'Operadores' (ID, Nombre, Edad, Correo, Tel1, Tel2, Clave, Licencia, VigenciaL, VigenciaM, Carpeta, Contraseña).
    /// </summary>
    public async Task RegistrarOperadorAsync(params string[] datos)
    {
        try
        {
            string rango = "Operadores!A:L";
            var valueRange = new ValueRange { Values = new List<IList<object>> { new List<object>(datos) } };

            var appendRequest = _service.Spreadsheets.Values.Append(valueRange, _spreadsheetId, rango);
            appendRequest.ValueInputOption = SpreadsheetsResource.ValuesResource.AppendRequest.ValueInputOptionEnum.USERENTERED;

            await appendRequest.ExecuteAsync();
        }
        catch (Google.GoogleApiException gEx)
        {
            Console.WriteLine($"--- ERROR SHEETS API --- \nCódigo: {gEx.Error.Code} \nMensaje: {gEx.Error.Message}");
            throw;
        }
    }

    /// <summary>
    /// Genera un folio único incremental con formato profesional (Ej: OP-001).
    /// </summary>
    public async Task<string> GenerarIdUnicoFormateadoAsync()
    {
        return await GenerarFolioSecuencialAsync("Operadores!A:A", "OP");
    }

    #endregion

    #region SECCIÓN 2: SEGURIDAD Y ACCESO

    /// <summary>
    /// Valida las credenciales comparándolas con la pestaña 'Test'.
    /// </summary>
    public async Task<bool> ValidarUsuarioAsync(string usuario, string contrasena)
    {
        try
        {
            var response = await _service.Spreadsheets.Values.Get(_spreadsheetId, "Operadores!A:L").ExecuteAsync();
            var valores = response.Values;

            if (valores == null) return false;

            return valores.Skip(1).Any(fila => fila.Count >= 2 &&
                                              fila[0]?.ToString() == usuario &&
                                              fila[11]?.ToString() == contrasena);
        }
        catch { return false; }
    }

    /// <summary>
    /// Método silenciado para evitar ventanas emergentes molestas.
    /// </summary>
    //public async Task<string> ValidarEstructuraAsync()
    //{

        /* Lógica anterior comentada por mantenimiento
        try
        {
            var spreadsheet = await _service.Spreadsheets.Get(_spreadsheetId).ExecuteAsync();
            bool existePestana = spreadsheet.Sheets.Any(s => s.Properties.Title == "Operadores");
            return existePestana ? "✅ OK" : "⚠️ Error";
        }
        catch (Exception ex) { return $"❌ Error: {ex.Message}"; }
        */
    //}

    #endregion

    #region SECCIÓN 3: MANTENIMIENTO

    /// <summary>
    /// Genera un folio incremental para la pestaña de Mantenimiento (Ej: MT-001).
    /// </summary>
    public async Task<string> GenerarFolioMantenimientoAsync()
    {
        return await GenerarFolioSecuencialAsync("Mantenimiento!A:A", "MT");
    }

    /// <summary>
    /// Registra los 14 campos técnicos en la pestaña 'Mantenimiento'.
    /// </summary>
    public async Task RegistrarMantenimientoAsync(params string[] datos)
    {
        try
        {
            string rango = "Mantenimiento!A:N";
            var valueRange = new ValueRange { Values = new List<IList<object>> { new List<object>(datos) } };

            var request = _service.Spreadsheets.Values.Append(valueRange, _spreadsheetId, rango);
            request.ValueInputOption = SpreadsheetsResource.ValuesResource.AppendRequest.ValueInputOptionEnum.USERENTERED;

            await request.ExecuteAsync();
        }
        catch (Exception ex) { throw new Exception($"Fallo en registro de Mantenimiento: {ex.Message}"); }
    }

    #endregion

    #region SECCIÓN 4: MÉTODOS PRIVADOS DE APOYO

    private async Task<string> GenerarFolioSecuencialAsync(string rangoColumna, string prefijo)
    {
        var response = await _service.Spreadsheets.Values.Get(_spreadsheetId, rangoColumna).ExecuteAsync();
        int maxId = 0;

        if (response.Values != null)
        {
            foreach (var fila in response.Values.Skip(1))
            {
                string valor = fila.FirstOrDefault()?.ToString() ?? "";
                if (valor.Contains("-") && int.TryParse(valor.Split('-').Last(), out int num))
                {
                    if (num > maxId) maxId = num;
                }
            }
        }
        return $"{prefijo}-{(maxId + 1):D3}";
    }

    #endregion
}