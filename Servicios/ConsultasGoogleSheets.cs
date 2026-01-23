using Google.Apis.Sheets.v4;
using Google.Apis.Sheets.v4.Data;

namespace LogiApp.Servicios;

public class ConsultasGoogleSheets
{
    private readonly SheetsService _service;
    private readonly string _spreadsheetId = "1wx2DSjhjzUeTxtxby0akYFTFul84DwSI1OMe5IYezb8";

    public ConsultasGoogleSheets(SheetsService service) => _service = service;

    // CREATE: Registrar nuevo operador
    public async Task RegistrarOperadorAsync(string nombre, string idCarpeta)
    {
        var values = new ValueRange { Values = new List<IList<object>> { new List<object> { nombre, idCarpeta, DateTime.Now.ToString("G") } } };
        var request = _service.Spreadsheets.Values.Append(values, _spreadsheetId, "Operadores!A:C");
        request.ValueInputOption = SpreadsheetsResource.ValuesResource.AppendRequest.ValueInputOptionEnum.USERENTERED;
        await request.ExecuteAsync();
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