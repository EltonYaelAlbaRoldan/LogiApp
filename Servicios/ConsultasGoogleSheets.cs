using Google.Apis.Sheets.v4;
using Google.Apis.Sheets.v4.Data;
using LogiApp.Modelos;

namespace LogiApp.Servicios;

public class ConsultasGoogleSheets
{
    #region CONFIGURACIÓN
    private readonly SheetsService _service;
    private readonly string _spreadsheetId = "1wx2DSjhjzUeTxtxby0akYFTFul84DwSI1OMe5IYezb8";

    public ConsultasGoogleSheets(SheetsService service) => _service = service;
    #endregion

    #region SECCIÓN 1: OPERACIONES DE OPERADORES
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

    public async Task<string> GenerarIdUnicoFormateadoAsync()
    {
        return await GenerarFolioSecuencialAsync("Operadores!A:A", "OP");
    }

    public async Task<Operador> ObtenerDatosOperadorAsync(string idOperador)
    {
        var range = "Operadores!A:L";
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
                        VigenciaLicencia = ParsearFechaSegura(row[8]?.ToString()),
                        VigenciaMedico = ParsearFechaSegura(row[9]?.ToString()),
                        LinkCarpeta = row.Count > 10 ? row[10].ToString() : ""
                    };
                }
            }
        }
        return null;
    }
    /// <summary>
    /// Intenta convertir el texto de Sheets a DateTime aceptando varios formatos comunes.
    /// </summary>
    private DateTime ParsearFechaSegura(string fechaTexto)
    {
        if (string.IsNullOrWhiteSpace(fechaTexto)) return DateTime.MinValue;

        // Definimos los formatos que Sheets suele enviar (con y sin ceros iniciales)
        string[] formatosAceptados = { "d/M/yyyy", "dd/MM/yyyy", "d/MM/yyyy", "dd/M/yyyy", "yyyy-MM-dd" };

        if (DateTime.TryParseExact(fechaTexto, formatosAceptados,
            System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.None, out DateTime fechaResult))
        {
            return fechaResult;
        }

        // Si falla el parseo exacto, intentamos el parseo general por si la cultura del sistema ayuda
        return DateTime.TryParse(fechaTexto, out DateTime fechaGeneral) ? fechaGeneral : DateTime.MinValue;
    }
    #endregion

    #region SECCIÓN 2: SEGURIDAD Y ACCESO
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

    public async Task<bool> ActualizarPasswordAsync(string idOperador, string nuevaPass)
    {
        try
        {
            int fila = await ObtenerNumeroFilaPorIdAsync(idOperador);
            if (fila == -1) return false;

            string rango = $"Operadores!L{fila}";
            var body = new ValueRange { Values = new List<IList<object>> { new List<object> { nuevaPass } } };

            var updateRequest = _service.Spreadsheets.Values.Update(body, _spreadsheetId, rango);
            updateRequest.ValueInputOption = SpreadsheetsResource.ValuesResource.UpdateRequest.ValueInputOptionEnum.RAW;

            await updateRequest.ExecuteAsync();
            return true;
        }
        catch { return false; }
    }
    #endregion

    #region SECCIÓN 3: ACTUALIZACIÓN DE PERFIL (DOCUMENTOS Y FECHAS)

    public async Task<bool> ActualizarDocumentoYFechaAsync(string id, string tipo, DateTime fecha, string link)
    {
        try
        {
            int fila = await ObtenerNumeroFilaPorIdAsync(id);
            if (fila == -1) return false;

            // I = Licencia (9), J = Médico (10), K = Link de documentos (11)
            string columnaFecha = (tipo == "Licencia") ? "I" : "J";
            string columnaLink = "K";

            var bodyFecha = new ValueRange { Values = new List<IList<object>> { new List<object> { fecha.ToString("dd/MM/yyyy") } } };
            var bodyLink = new ValueRange { Values = new List<IList<object>> { new List<object> { link } } };

            // Actualizamos la fecha
            var updateFecha = _service.Spreadsheets.Values.Update(bodyFecha, _spreadsheetId, $"Operadores!{columnaFecha}{fila}");
            updateFecha.ValueInputOption = SpreadsheetsResource.ValuesResource.UpdateRequest.ValueInputOptionEnum.USERENTERED;
            await updateFecha.ExecuteAsync();

            // Actualizamos el link del documento
            var updateLink = _service.Spreadsheets.Values.Update(bodyLink, _spreadsheetId, $"Operadores!{columnaLink}{fila}");
            updateLink.ValueInputOption = SpreadsheetsResource.ValuesResource.UpdateRequest.ValueInputOptionEnum.USERENTERED;
            await updateLink.ExecuteAsync();

            return true;
        }
        catch { return false; }
    }

    #endregion

    #region SECCIÓN 4: MANTENIMIENTO Y UNIDADES
    public async Task RegistrarMantenimientoAsync(
        string folio, string fechaRegistro, string fechaManto, string operador,
        string unidad, string lugar, string taller, string tipo, string piezas,
        string km, string costo, string aceite, string frenos, string linkDrive)
    {
        var range = "Mantenimiento!A:N";
        var valueRange = new ValueRange { Values = new List<IList<object>>() };

        var nuevaFila = new List<object> { folio, fechaRegistro, fechaManto, operador, unidad, lugar, taller, tipo, piezas, km, costo, aceite, frenos, linkDrive };
        valueRange.Values.Add(nuevaFila);

        var appendRequest = _service.Spreadsheets.Values.Append(valueRange, _spreadsheetId, range);
        appendRequest.ValueInputOption = SpreadsheetsResource.ValuesResource.AppendRequest.ValueInputOptionEnum.USERENTERED;
        await appendRequest.ExecuteAsync();
    }

    public async Task<List<string>> ObtenerListaUnidadesAsync()
    {
        string rango = "Unidades!A2:B100";
        var response = await _service.Spreadsheets.Values.Get(_spreadsheetId, rango).ExecuteAsync();
        var listaFormateada = new List<string>();

        if (response.Values != null)
        {
            foreach (var fila in response.Values)
            {
                if (fila.Count >= 2)
                    listaFormateada.Add($"{fila[0]} - {fila[1]}");
                else if (fila.Count == 1)
                    listaFormateada.Add(fila[0].ToString());
            }
        }
        return listaFormateada;
    }

    public async Task<string> GenerarFolioMantenimientoAsync()
    {
        return await GenerarFolioSecuencialAsync("Mantenimiento!A:A", "MT");
    }
    #endregion

    #region SECCIÓN 5: MÉTODOS PRIVADOS DE APOYO
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

    private async Task<int> ObtenerNumeroFilaPorIdAsync(string idOperador)
    {
        var response = await _service.Spreadsheets.Values.Get(_spreadsheetId, "Operadores!A:A").ExecuteAsync();
        var values = response.Values;

        if (values != null)
        {
            for (int i = 0; i < values.Count; i++)
            {
                if (values[i].Count > 0 && values[i][0].ToString() == idOperador)
                    return i + 1;
            }
        }
        return -1;
    }
    #endregion

    #region SECCIÓN 6: GESTIÓN DE VIAJES

    public async Task<List<string>> ObtenerListaDestinosAsync()
    {
        try
        {
            string rango = "Destinos!A2:A100";
            var response = await _service.Spreadsheets.Values.Get(_spreadsheetId, rango).ExecuteAsync();
            var lista = new List<string>();
            if (response.Values != null)
            {
                foreach (var fila in response.Values)
                {
                    if (fila.Count > 0) lista.Add(fila[0].ToString());
                }
            }
            return lista;
        }
        catch { return new List<string>(); }
    }

    public async Task<bool> RegistrarInicioViajeAsync(params string[] datos)
    {
        try
        {
            string rango = "Viajes!A:J";
            var valueRange = new ValueRange { Values = new List<IList<object>> { new List<object>(datos) } };
            var appendRequest = _service.Spreadsheets.Values.Append(valueRange, _spreadsheetId, rango);
            appendRequest.ValueInputOption = SpreadsheetsResource.ValuesResource.AppendRequest.ValueInputOptionEnum.USERENTERED;
            await appendRequest.ExecuteAsync();
            return true;
        }
        catch { return false; }
    }

    public async Task<string> GenerarFolioViajeAsync()
    {
        return await GenerarFolioSecuencialAsync("Viajes!A:A", "VJ");
    }
    // Actualizamos el monitoreo para incluir la hora en la celda
    public async Task<bool> ActualizarUbicacionViajeAsync(string folio, string nuevaUbicacion)
    {
        try
        {
            int fila = await ObtenerNumeroFilaPorIdViajeAsync(folio);
            if (fila == -1) return false;

            string horaReporte = DateTime.Now.ToString("HH:mm");
            string estatusConHora = $"EN TRANSITO - ({nuevaUbicacion}) - {horaReporte}"; //

            var valueRange = new ValueRange { Values = new List<IList<object>> { new List<object> { estatusConHora } } };
            var updateRequest = _service.Spreadsheets.Values.Update(valueRange, _spreadsheetId, $"Viajes!J{fila}");
            updateRequest.ValueInputOption = SpreadsheetsResource.ValuesResource.UpdateRequest.ValueInputOptionEnum.USERENTERED;

            await updateRequest.ExecuteAsync();
            return true;
        }
        catch { return false; }
    }

    public async Task<bool> FinalizarViajeAsync(string folio, string fechaFin, string kmFin, string fuelFin, string observaciones)
    {
        try
        {
            int fila = await ObtenerNumeroFilaPorIdViajeAsync(folio);
            if (fila == -1) return false;

            // Limpieza de datos: Evitamos celdas vacías [cite: 2026-02-17]
            string obsFinal = string.IsNullOrWhiteSpace(observaciones) ? "N/A" : observaciones;

            // 1. Preparamos el rango de cierre (L: KM_FIN, M: FUEL_FIN, N: OBSERVACIONES)
            var datosCierre = new List<object> { kmFin, fuelFin, obsFinal };
            var valueRange = new ValueRange { Values = new List<IList<object>> { datosCierre } };

            // Actualizamos el bloque de datos técnicos
            var updateCierre = _service.Spreadsheets.Values.Update(valueRange, _spreadsheetId, $"Viajes!L{fila}:N{fila}");
            updateCierre.ValueInputOption = SpreadsheetsResource.ValuesResource.UpdateRequest.ValueInputOptionEnum.USERENTERED;
            var resultadoCierre = await updateCierre.ExecuteAsync();

            // 2. Actualizamos el ESTATUS a FINALIZADO (Columna J)
            var statusBody = new ValueRange { Values = new List<IList<object>> { new List<object> { "FINALIZADO" } } };
            var updateStatus = _service.Spreadsheets.Values.Update(statusBody, _spreadsheetId, $"Viajes!J{fila}");
            updateStatus.ValueInputOption = SpreadsheetsResource.ValuesResource.UpdateRequest.ValueInputOptionEnum.USERENTERED;
            var resultadoStatus = await updateStatus.ExecuteAsync();

            // Si ambas peticiones devolvieron un ETag o una respuesta, confirmamos éxito
            return resultadoCierre != null && resultadoStatus != null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error de confirmación: {ex.Message}");
            return false;
        }
    }


    public async Task<bool> ActualizarEstatusYLlegadaAsync(string folio, string nuevoEstatus, string horaLlegada)
    {
        try
        {
            int fila = await ObtenerNumeroFilaPorIdViajeAsync(folio);
            if (fila == -1) throw new Exception("Folio no encontrado en la base de datos.");

            // J = Estatus, K = Fecha Llegada
            var bodyJ = new ValueRange { Values = new List<IList<object>> { new List<object> { nuevoEstatus } } };
            var bodyK = new ValueRange { Values = new List<IList<object>> { new List<object> { horaLlegada } } };

            // Actualizamos Estatus
            var reqJ = _service.Spreadsheets.Values.Update(bodyJ, _spreadsheetId, $"Viajes!J{fila}");
            reqJ.ValueInputOption = SpreadsheetsResource.ValuesResource.UpdateRequest.ValueInputOptionEnum.USERENTERED;
            await reqJ.ExecuteAsync();

            // Actualizamos Fecha
            var reqK = _service.Spreadsheets.Values.Update(bodyK, _spreadsheetId, $"Viajes!K{fila}");
            reqK.ValueInputOption = SpreadsheetsResource.ValuesResource.UpdateRequest.ValueInputOptionEnum.USERENTERED;
            await reqK.ExecuteAsync();

            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ERROR SHEETS: {ex.Message}");
            return false;
        }
    }

    private async Task<int> ObtenerNumeroFilaPorIdViajeAsync(string folio)
    {
        // Aseguramos que el rango sea solo la columna A
        var response = await _service.Spreadsheets.Values.Get(_spreadsheetId, "Viajes!A:A").ExecuteAsync();
        var values = response.Values;

        if (values != null)
        {
            for (int i = 0; i < values.Count; i++)
            {
                if (values[i].Count > 0)
                {
                    // Comparamos limpiando espacios y convirtiendo a mayúsculas
                    string valorCelda = values[i][0].ToString().Trim().ToUpper();
                    string folioBuscado = folio.Trim().ToUpper();

                    if (valorCelda == folioBuscado)
                        return i + 1; // Retorna la fila real en Sheets
                }
            }
        }
        return -1;
    }

    #endregion
}