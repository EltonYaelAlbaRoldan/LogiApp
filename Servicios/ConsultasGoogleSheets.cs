using Google.Apis.Sheets.v4;
using Google.Apis.Sheets.v4.Data;

namespace LogiApp.Servicios;

public class ConsultasGoogleSheets
{
    private readonly SheetsService _service;
    private readonly string _spreadsheetId = "1wx2DSjhjzUeTxtxby0akYFTFul84DwSI1OMe5IYezb8";

    public ConsultasGoogleSheets(SheetsService service) => _service = service;

    // CREATE: Registrar nuevo operador
    public async Task RegistrarOperadorAsync(string nombre, string edad, string folderId, int nuevoId)
    {
        var rango = "Operadores!A:E"; // Ajusta según tus columnas
        var objetoLista = new List<object> { nuevoId, nombre, edad, folderId, DateTime.Now.ToString("dd/MM/yyyy") };

        var valueRange = new ValueRange { Values = new List<IList<object>> { objetoLista } };

        var appendRequest = _service.Spreadsheets.Values.Append(valueRange, _spreadsheetId, rango);
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

    // También actualiza la firma de este método para que acepte el ID como string
    public async Task RegistrarOperadorAsync(string nombre, string edad, string folderId, string nuevoId)
    {
        var rango = "Operadores!A:E";
        var objetoLista = new List<object> { nuevoId, nombre, edad, folderId, DateTime.Now.ToString("dd/MM/yyyy") };

        var valueRange = new ValueRange { Values = new List<IList<object>> { objetoLista } };

        var appendRequest = _service.Spreadsheets.Values.Append(valueRange, _spreadsheetId, rango);
        appendRequest.ValueInputOption = SpreadsheetsResource.ValuesResource.AppendRequest.ValueInputOptionEnum.USERENTERED;
        await appendRequest.ExecuteAsync();
    }
}