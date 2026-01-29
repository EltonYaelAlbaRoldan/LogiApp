using Google.Apis.Sheets.v4;
using Google.Apis.Sheets.v4.Data;

namespace LogiApp.Servicios;

public class ConsultasGoogleSheets
{
    private readonly SheetsService _service;
    private readonly string _spreadsheetId = "1wx2DSjhjzUeTxtxby0akYFTFul84DwSI1OMe5IYezb8";

    public ConsultasGoogleSheets(SheetsService service) => _service = service;

    // CREATE: Registrar nuevo operador
    // DEJA SOLO ESTE MÉTODO PARA REGISTRAR
    public async Task RegistrarOperadorAsync(params string[] datos)
    {
        try
        {
            // Asegúrate que la pestaña se llame 'Operadores' en tu Excel
            string rango = "Operadores!A:K";

            var valueRange = new ValueRange
            {
                Values = new List<IList<object>> { new List<object>(datos) }
            };

            var appendRequest = _service.Spreadsheets.Values.Append(valueRange, _spreadsheetId, rango);
            appendRequest.ValueInputOption = SpreadsheetsResource.ValuesResource.AppendRequest.ValueInputOptionEnum.USERENTERED;

            await appendRequest.ExecuteAsync();
            Console.WriteLine("Datos escritos correctamente en Sheets.");
        }
        catch (Google.GoogleApiException gEx)
        {
            // Esto te dirá si el error es de Permisos (403) o de ID no encontrado (404)
            // Esto imprimirá en la consola de Salida el motivo real (403 Forbidden, 404 Not Found, etc.)
            Console.WriteLine("--- ERROR DE GOOGLE DETECTADO ---");
            Console.WriteLine($"Mensaje: {gEx.Error.Message}");
            Console.WriteLine($"Código: {gEx.Error.Code}");
            Console.WriteLine("---------------------------------");
            throw;
        }
    }

    // READ: Obtener todos los operadores
    public async Task<IList<IList<object>>> LeerOperadoresAsync()
    {
        var response = await _service.Spreadsheets.Values.Get(_spreadsheetId, "Operadores!A:C").ExecuteAsync();
        return response.Values;
    }

    // UPDATE: Actualizar una celda específica (ej. cambiar nombre)
    public async Task ActualizarOperadorAsync(string rango, string nuevoValor)
    {
        var values = new ValueRange { Values = new List<IList<object>> { new List<object> { nuevoValor } } };
        var request = _service.Spreadsheets.Values.Update(values, _spreadsheetId, rango);
        request.ValueInputOption = SpreadsheetsResource.ValuesResource.UpdateRequest.ValueInputOptionEnum.USERENTERED;
        await request.ExecuteAsync();
    }

    // DELETE: Limpiar rango (borrar datos de una fila)
    public async Task BorrarDatosFilaAsync(string rango)
    {
        await _service.Spreadsheets.Values.Clear(new ClearValuesRequest(), _spreadsheetId, rango).ExecuteAsync();
    }


    // Cambiamos el retorno a Task<string> porque ahora devolvemos "OP-00X"
    public async Task<string> GenerarIdUnicoFormateadoAsync()
    {
        var rango = "Operadores!A:A";
        var response = await _service.Spreadsheets.Values.Get(_spreadsheetId, rango).ExecuteAsync();

        var idsExistentes = new HashSet<int>();
        if (response.Values != null)
        {
            foreach (var fila in response.Values.Skip(1))
            {
                string valorCelda = fila.FirstOrDefault()?.ToString() ?? "";

                // Extraemos solo el número del formato "OP-003"
                if (valorCelda.Contains("-"))
                {
                    string soloNumero = valorCelda.Split('-').Last();
                    if (int.TryParse(soloNumero, out int num))
                        idsExistentes.Add(num);
                }
            }
        }

        // Buscamos el máximo y sumamos 1
        int siguienteNum = idsExistentes.Count > 0 ? idsExistentes.Max() + 1 : 1;

        // Retornamos el formato profesional para tu Excel
        return $"OP-{siguienteNum:D3}";
    }  

    //Consutar usuarios y contraseñas
    public async Task<bool> ValidarUsuarioAsync(string usuario, string contrasena)
    {
        try
        {
            // Leemos el rango de la pestaña "Test"
            var response = await _service.Spreadsheets.Values.Get(_spreadsheetId, "Test!A:B").ExecuteAsync();
            var valores = response.Values;

            if (valores == null || valores.Count == 0) return false;

            // Recorremos las filas buscando coincidencia (saltando el encabezado)
            foreach (var fila in valores.Skip(1))
            {
                if (fila.Count >= 2)
                {
                    string u = fila[0]?.ToString();
                    string c = fila[1]?.ToString();

                    if (u == usuario && c == contrasena)
                        return true; // Credenciales correctas
                }
            }
        }
        catch (Exception)
        {
            return false;
        }
        return false;
    }

    public async Task<string> ValidarEstructuraAsync()
    {
        try
        {
            // 1. Intentamos obtener metadatos del archivo
            var request = _service.Spreadsheets.Get(_spreadsheetId);
            var spreadsheet = await request.ExecuteAsync();

            // 2. Revisamos si existe la pestaña 'Operadores'
            var existePestana = spreadsheet.Sheets.Any(s => s.Properties.Title == "Operadores");

            if (existePestana)
                return "✅ Conexión perfecta: Archivo y pestaña 'Operadores' localizados.";
            else
            {
                var nombres = string.Join(", ", spreadsheet.Sheets.Select(s => s.Properties.Title));
                return $"⚠️ Archivo encontrado, pero la pestaña 'Operadores' NO existe. Pestañas encontradas: {nombres}";
            }
        }
        catch (Google.GoogleApiException e) when (e.Error.Code == 404)
        {
            return "❌ ERROR 404: El ID del Spreadsheet es incorrecto o no tengo permisos para verlo.";
        }
        catch (Exception ex)
        {
            return $"❌ Error desconocido: {ex.Message}";
        }
    }

    // Generador automático de folios para Mantenimiento
    public async Task<string> GenerarFolioMantenimientoAsync()
    {
        var rango = "Mantenimiento!A:A"; // Columna de Folios
        var response = await _service.Spreadsheets.Values.Get(_spreadsheetId, rango).ExecuteAsync();

        var idsExistentes = new HashSet<int>();
        if (response.Values != null)
        {
            foreach (var fila in response.Values.Skip(1))
            {
                string valorCelda = fila.FirstOrDefault()?.ToString() ?? "";
                if (valorCelda.Contains("-"))
                {
                    string soloNumero = valorCelda.Split('-').Last();
                    if (int.TryParse(soloNumero, out int num))
                        idsExistentes.Add(num);
                }
            }
        }
        int siguienteNum = idsExistentes.Count > 0 ? idsExistentes.Max() + 1 : 1;
        return $"MT-{siguienteNum:D3}"; // Ejemplo: MT-001
    }

    // Envío de los 14 campos a la pestaña Mantenimiento
    public async Task RegistrarMantenimientoAsync(params string[] datos)
    {
        try
        {
            string rango = "Mantenimiento!A:N";
            var valueRange = new ValueRange { Values = new List<IList<object>> { new List<object>(datos) } };
            var appendRequest = _service.Spreadsheets.Values.Append(valueRange, _spreadsheetId, rango);
            appendRequest.ValueInputOption = SpreadsheetsResource.ValuesResource.AppendRequest.ValueInputOptionEnum.USERENTERED;
            await appendRequest.ExecuteAsync();
        }
        catch (Exception ex)
        {
            throw new Exception($"Error al escribir en pestaña Mantenimiento: {ex.Message}");
        }
    }
}