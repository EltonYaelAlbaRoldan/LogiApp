using Google.Apis.Sheets.v4;
using Google.Apis.Sheets.v4.Data;

namespace LogiApp.Servicios;

public class ConsultasGoogleSheets
{
    private readonly SheetsService _service;
    private readonly string _spreadsheetId = "1wx2DSjhjzUeTxtxby0akYFTFul84DwSI1OMe5IYezb8";

    public ConsultasGoogleSheets(SheetsService service) => _service = service;

    // CREATE: Registrar nuevo operador
    public async Task RegistrarOperadorAsync(string nombre, string edad, string idCarpeta)
    {
        // 1. Obtenemos los datos actuales para saber cuántos hay y generar el ID
        var response = await _service.Spreadsheets.Values.Get(_spreadsheetId, "Operadores!A:A").ExecuteAsync();
        int siguienteId = (response.Values != null) ? response.Values.Count : 1;

        string idOperador = $"OP-{siguienteId:D3}"; // Ejemplo: OP-001, OP-002

        // 2. Preparamos la fila con el ID en la primera columna y el Nombre en la segunda
        var values = new ValueRange
        {
            Values = new List<IList<object>>
        {
            new List<object>
            {
                idOperador,    // Columna A: ID de Operador
                nombre,        // Columna B: Nombre Completo
                edad,          // Columna C: Edad
                idCarpeta,     // Columna D: ID de Carpeta Drive
                DateTime.Now.ToString("G") // Columna D: Fecha de Registro
            }
        }
        };

        var appendRequest = _service.Spreadsheets.Values.Append(values, _spreadsheetId, "Operadores!A:E");
        appendRequest.ValueInputOption = SpreadsheetsResource.ValuesResource.AppendRequest.ValueInputOptionEnum.USERENTERED;

        await appendRequest.ExecuteAsync();
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
}