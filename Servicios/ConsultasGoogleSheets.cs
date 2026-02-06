using Google.Apis.Sheets.v4;
using Google.Apis.Sheets.v4.Data;
using LogiApp.Modelos;

namespace LogiApp.Servicios;

// Gestiona todas las operaciones de lectura y escritura en el Spreadsheet de Google.
public class ConsultasGoogleSheets
{
    private readonly SheetsService _service;
    private readonly string _spreadsheetId = "1wx2DSjhjzUeTxtxby0akYFTFul84DwSI1OMe5IYezb8";

    public ConsultasGoogleSheets(SheetsService service) => _service = service;

    #region SECCIÓN 1: OPERACIONES DE OPERADORES


    // Registra una nueva fila en la pestaña 'Operadores' (ID, Nombre, Edad, Correo, Tel1, Tel2, Clave, Licencia, VigenciaL, VigenciaM, Carpeta, Contraseña).

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

    /// Genera un folio único incremental con formato profesional (Ej: OP-001).
    public async Task<string> GenerarIdUnicoFormateadoAsync()
    {
        return await GenerarFolioSecuencialAsync("Operadores!A:A", "OP");
    }

    #endregion

    #region SECCIÓN 2: SEGURIDAD Y ACCESO

    /// Valida las credenciales comparándolas con la pestaña 'Test'.
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

    //Genera un folio incremental para la pestaña de Mantenimiento (Ej: MT-001).
    public async Task<string> GenerarFolioMantenimientoAsync()
    {
        return await GenerarFolioSecuencialAsync("Mantenimiento!A:A", "MT");
    }

    /// <summary>
    /// Registra los 14 campos técnicos en la pestaña 'Mantenimiento'.
    /// </summary>
    public async Task RegistrarMantenimientoAsync(
    string folio, string fechaRegistro, string fechaManto, string operador,
    string unidad, string lugar, string taller, string tipo, string piezas,
    string km, string costo, string aceite, string frenos, string linkDrive) // <--- Nuevo parámetro
    {
        var range = "Mantenimiento!A:N"; // Extendemos el rango a la columna N (14)
        var valueRange = new ValueRange { Values = new List<IList<object>>() };

        var nuevaFila = new List<object>
    {
        folio, fechaRegistro, fechaManto, operador, unidad,
        lugar, taller, tipo, piezas, km, costo, aceite, frenos,
        linkDrive // <--- Columna 14
    };

        valueRange.Values.Add(nuevaFila);

        var appendRequest = _service.Spreadsheets.Values.Append(valueRange, _spreadsheetId, range);
        appendRequest.ValueInputOption = SpreadsheetsResource.ValuesResource.AppendRequest.ValueInputOptionEnum.USERENTERED;
        await appendRequest.ExecuteAsync();
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

    //Información del operador
    public async Task<Operador> ObtenerDatosOperadorAsync(string idOperador)
    {
        var range = "Operadores!A:L"; // Ajusta según tus columnas
        var response = await _service.Spreadsheets.Values.Get(_spreadsheetId, range).ExecuteAsync();
        var values = response.Values;

        if (values != null)
        {
            foreach (var row in values)
            {
                if (row[0].ToString() == idOperador)
                {
                    return new Operador
                    {
                        Id = row[0].ToString(),
                        Nombre = row[1].ToString(),
                        VigenciaLicencia = DateTime.Parse(row[8].ToString()),
                        VigenciaMedico = DateTime.Parse(row[9].ToString()),
                        LinkCarpeta = row[10].ToString()
                    };
                }
            }
        }
        return null;
    }
    // Método para actualizar la contraseña en el Excel
    public async Task<bool> ActualizarPasswordAsync(string idOperador, string nuevaPass)
    {
        try
        {
            int fila = await ObtenerNumeroFilaPorIdAsync(idOperador);
            if (fila == -1) return false;

            // IMPORTANTE: Usamos "Operadores" que es el nombre real de tu pestaña
            string rango = $"Operadores!L{fila}";

            var body = new ValueRange { Values = new List<IList<object>> { new List<object> { nuevaPass } } };

            // CORRECCIÓN CS0117: UpdateRequest
            var updateRequest = _service.Spreadsheets.Values.Update(body, _spreadsheetId, rango);
            updateRequest.ValueInputOption = SpreadsheetsResource.ValuesResource.UpdateRequest.ValueInputOptionEnum.RAW;

            await updateRequest.ExecuteAsync();
            return true;
        }
        catch { return false; }
    }

    // Método auxiliar para encontrar el número de fila real en Google Sheets
    private async Task<int> ObtenerNumeroFilaPorIdAsync(string idOperador)
    {
        var response = await _service.Spreadsheets.Values.Get(_spreadsheetId, "Operadores!A:A").ExecuteAsync();
        var values = response.Values;

        if (values != null)
        {
            for (int i = 0; i < values.Count; i++)
            {
                if (values[i].Count > 0 && values[i][0].ToString() == idOperador)
                {
                    // Sumamos 1 porque las filas en Sheets empiezan en 1, no en 0
                    return i + 1;
                }
            }
        }
        return -1;
    }


    public async Task<List<string>> ObtenerListaUnidadesAsync()
    {
        // Leemos las columnas A (Nombre) y B (Placas)
        string rango = "Unidades!A2:B100";

        var request = _service.Spreadsheets.Values.Get(_spreadsheetId, rango);
        var response = await request.ExecuteAsync();

        var listaFormateada = new List<string>();

        if (response.Values != null && response.Values.Count > 0)
        {
            foreach (var fila in response.Values)
            {
                // Verificamos que existan al menos las dos columnas
                if (fila.Count >= 2)
                {
                    string nombre = fila[0]?.ToString() ?? "S/N";
                    string placas = fila[1]?.ToString() ?? "S/P";

                    // Concatenamos para que el usuario vea ambos datos
                    listaFormateada.Add($"{nombre} - {placas}");
                }
                else if (fila.Count == 1)
                {
                    listaFormateada.Add(fila[0].ToString());
                }
            }
        }
        return listaFormateada;
    }


}