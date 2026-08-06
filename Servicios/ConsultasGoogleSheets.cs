using Google.Apis.Sheets.v4;
using Google.Apis.Sheets.v4.Data;
using LogiApp.Modelos;
using System.Security.Cryptography;

namespace LogiApp.Servicios;

public class ConsultasGoogleSheets
{
    #region CONFIGURACIÓN
    private readonly SheetsService _service;
    private readonly string _spreadsheetId = "1Gl41wM-3OPepAxE6K6TjRGbCJsfjjYOSlubkIaCTjs0";

    public ConsultasGoogleSheets(SheetsService service) => _service = service;
    #endregion

    #region SECCIÓN 1: OPERACIONES DE OPERADORES
    public async Task RegistrarOperadorAsync(params string[] datos)
    {
        try
        {
            // 1. Obtener metadatos de la hoja para conocer el ID interno y las filas actuales
            var spreadsheet = await _service.Spreadsheets.Get(_spreadsheetId).ExecuteAsync();
            var sheet = spreadsheet.Sheets.FirstOrDefault(s => s.Properties.Title == "Operadores");
            int sheetId = (int)sheet.Properties.SheetId;

            // Obtenemos la última fila con datos para saber dónde insertar
            var response = await _service.Spreadsheets.Values.Get(_spreadsheetId, "Operadores!A:A").ExecuteAsync();
            int filaDondeInsertar = (response.Values?.Count ?? 0);

            // 2. Preparar los datos (Aseguramos 13 columnas para llegar a la M)
            var listaDatos = new List<object>(datos);

            // CORRECCIÓN DEL ERROR CS0019: Relleno simple hasta la columna L (índice 11)
            while (listaDatos.Count < 12)
            {
                listaDatos.Add("");
            }
            listaDatos.Add("VALIDACION"); // Estado inicial en la M (índice 12)

            // 3. Crear la petición BatchUpdate
            var batchRequest = new BatchUpdateSpreadsheetRequest
            {
                Requests = new List<Request>()
            };

            // Abrimos una fila física para no sobrescribir contenido o formato existente.
            batchRequest.Requests.Add(new Request
            {
                InsertDimension = new InsertDimensionRequest
                {
                    Range = new DimensionRange
                    {
                        SheetId = sheetId,
                        Dimension = "ROWS",
                        StartIndex = filaDondeInsertar,
                        EndIndex = filaDondeInsertar + 1
                    },
                    InheritFromBefore = filaDondeInsertar > 0
                }
            });

            // A) Insertar los valores
            batchRequest.Requests.Add(new Request
            {
                UpdateCells = new UpdateCellsRequest
                {
                    Range = new GridRange
                    {
                        SheetId = sheetId,
                        StartRowIndex = filaDondeInsertar,
                        EndRowIndex = filaDondeInsertar + 1,
                        StartColumnIndex = 0,
                        EndColumnIndex = 13
                    },
                    Rows = new List<RowData>
                {
                    new RowData {
                        Values = listaDatos.Select(d => new CellData {
                            UserEnteredValue = new ExtendedValue { StringValue = d?.ToString() ?? "" }
                        }).ToList()
                    }
                },
                    Fields = "userEnteredValue"
                }
            });

            // B) Aplicar el Menú Desplegable (Data Validation)
            batchRequest.Requests.Add(new Request
            {
                SetDataValidation = new SetDataValidationRequest
                {
                    Range = new GridRange
                    {
                        SheetId = sheetId,
                        StartRowIndex = filaDondeInsertar,
                        EndRowIndex = filaDondeInsertar + 1,
                        StartColumnIndex = 12, // Columna M
                        EndColumnIndex = 13
                    },
                    Rule = new DataValidationRule
                    {
                        Condition = new BooleanCondition
                        {
                            Type = "ONE_OF_LIST",
                            Values = new List<ConditionValue>
                        {
                            new ConditionValue { UserEnteredValue = "VALIDACION" },
                            new ConditionValue { UserEnteredValue = "ACTIVO" },
                            new ConditionValue { UserEnteredValue = "INACTIVO" }
                        }
                        },
                        ShowCustomUi = true,
                        Strict = true
                    }
                }
            });

            // Ejecutar todo de un solo golpe
            await _service.Spreadsheets.BatchUpdate(batchRequest, _spreadsheetId).ExecuteAsync();
        }
        catch (Exception ex)
        {
            throw new Exception("Error al registrar operador: " + ex.Message);
        }
    }

    public async Task<string> GenerarIdUnicoFormateadoAsync()
    {
        var response = await _service.Spreadsheets.Values.Get(_spreadsheetId, "Operadores!A:A").ExecuteAsync();
        int maxId = 0;

        if (response.Values != null)
        {
            foreach (var fila in response.Values.Skip(1))
            {
                string valor = fila.FirstOrDefault()?.ToString() ?? string.Empty;
                string[] partes = valor.Split('-');
                if (partes.Length >= 2 && partes[0].Equals("OP", StringComparison.OrdinalIgnoreCase) &&
                    int.TryParse(partes[1], out int numero))
                {
                    maxId = Math.Max(maxId, numero);
                }
            }
        }

        return $"OP-{maxId + 1:D3}";
    }

    public async Task<Operador> ObtenerDatosOperadorAsync(string idOperador)
    {
        var range = "Operadores!A:M";
        var response = await _service.Spreadsheets.Values.Get(_spreadsheetId, range).ExecuteAsync();
        var values = response.Values;
        string idNormalizado = idOperador?.Trim().ToUpperInvariant() ?? string.Empty;

        if (values != null)
        {
            foreach (var row in values)
            {
                if (row.Count > 0 && row[0]?.ToString()?.Trim().ToUpperInvariant() == idNormalizado)
                {
                    return new Operador
                    {
                        Id = row[0].ToString(),
                        Nombre = row[1].ToString(),
                        VigenciaLicencia = ParsearFechaSegura(row[8]?.ToString()),
                        VigenciaMedico = ParsearFechaSegura(row[9]?.ToString()),
                        Correo = row.Count > 3 ? row[3].ToString() : "",
                        LinkCarpeta = row.Count > 10 ? row[10].ToString() : "",
                        Estado = row.Count > 12 ? row[12].ToString().Trim().ToUpperInvariant() : "VALIDACION"
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
    public async Task<string> ValidarUsuarioAsync(string usuario, string contrasena)
    {
        // Leemos hasta la columna M (índice 12)
        var response = await _service.Spreadsheets.Values.Get(_spreadsheetId, "Operadores!A:M").ExecuteAsync();
        var values = response.Values;

        if (values == null || values.Count <= 0) return "USUARIO_NO_EXISTE";

        foreach (var fila in values.Skip(1))
        {
            // Columna A (0): ID | Columna L (11): Password | Columna M (12): Estado
            if (fila.Count > 0 && fila[0].ToString().Trim().ToUpper() == usuario.Trim().ToUpper())
            {
                if (fila.Count > 11 && SeguridadPassword.Verificar(contrasena, fila[11].ToString()))
                {
                    // Migración transparente de contraseñas antiguas almacenadas en texto plano.
                    if (!SeguridadPassword.EsHash(fila[11].ToString()))
                        await ActualizarPasswordAsync(fila[0].ToString(), contrasena);

                    string estado = fila.Count > 12 ? fila[12].ToString().ToUpper() : "VALIDACION";

                    if (estado == "ACTIVO") return "EXITO";
                    if (estado == "INACTIVO") return "CUENTA_DESACTIVADA";
                    return "EN_VALIDACION"; // Para el estado "VALIDACION"
                }
                return "CONTRASEÑA_INCORRECTA";
            }
        }
        return "USUARIO_NO_EXISTE";
    }
    public async Task<bool> ActualizarPasswordAsync(string idOperador, string nuevaPass)
    {
        try
        {
            int fila = await ObtenerNumeroFilaPorIdAsync(idOperador);
            if (fila == -1) return false;

            string rango = $"Operadores!L{fila}";
            var body = new ValueRange { Values = new List<IList<object>> { new List<object> { SeguridadPassword.CrearHash(nuevaPass) } } };

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
        appendRequest.InsertDataOption = SpreadsheetsResource.ValuesResource.AppendRequest.InsertDataOptionEnum.INSERTROWS;
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

    public async Task<List<string>> ObtenerListaRemolquesAsync()
    {
        string rango = "Remolques!A2:B100";
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
                string[] partes = valor.Split('-');
                if (partes.Length >= 2 && int.TryParse(partes[1], out int num))
                {
                    if (num > maxId) maxId = num;
                }
            }
        }
        // El sufijo aleatorio evita colisiones cuando dos dispositivos leen el mismo máximo.
        return $"{prefijo}-{(maxId + 1):D3}-{RandomNumberGenerator.GetHexString(8)}";
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

    public async Task<List<string>> ObtenerListaClientesAsync()
    {
        try
        {
            string rango = "Clientes!A2:A100"; // Apunta a tu nueva hoja
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
            const string rango = "Viajes!A:K";
            var valueRange = new ValueRange { Values = new List<IList<object>> { new List<object>(datos) } };
            var appendRequest = _service.Spreadsheets.Values.Append(valueRange, _spreadsheetId, rango);
            appendRequest.ValueInputOption = SpreadsheetsResource.ValuesResource.AppendRequest.ValueInputOptionEnum.USERENTERED;

            // Append puede usar OVERWRITE sobre la fila que Sheets detecta como el final
            // de la tabla. INSERT_ROWS garantiza que cada viaje conserve su propia fila.
            appendRequest.InsertDataOption = SpreadsheetsResource.ValuesResource.AppendRequest.InsertDataOptionEnum.INSERTROWS;

            await appendRequest.ExecuteAsync();
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error al registrar el inicio del viaje: {ex.Message}");
            throw;
        }
    }

    public async Task<string> GenerarFolioViajeAsync()
    {
        return await GenerarFolioSecuencialAsync("Viajes!A:A", "VJ");
    }

    // Le agregamos el parámetro opcional 'horaCaptura'
    public async Task<bool> ActualizarUbicacionViajeAsync(string folio, double latitud, double longitud, string horaCaptura = null)
    {
        try
        {
            int fila = await ObtenerNumeroFilaPorIdViajeAsync(folio);
            if (fila == -1) return false;

            string ubicacionFinal = $"{latitud:F4}, {longitud:F4}";

            try
            {
                var placemarks = await Geocoding.Default.GetPlacemarksAsync(latitud, longitud);
                var lugar = placemarks?.FirstOrDefault();

                if (lugar != null)
                {
                    if (!string.IsNullOrEmpty(lugar.Locality))
                        ubicacionFinal = lugar.Locality;
                    else if (!string.IsNullOrEmpty(lugar.AdminArea))
                        ubicacionFinal = lugar.AdminArea;
                }
            }
            catch { /* Si no hay internet, el Geocoding falla pero nos quedamos con las coordenadas */ }

            // Usamos la hora congelada, o la hora actual si viene vacío
            string horaReporte = horaCaptura ?? DateTime.Now.ToString("HH:mm");
            string estatusConLugar = $"EN TRANSITO - ({ubicacionFinal}) - {horaReporte}";

            var valueRange = new ValueRange { Values = new List<IList<object>> { new List<object> { estatusConLugar } } };
            var updateRequest = _service.Spreadsheets.Values.Update(valueRange, _spreadsheetId, $"Viajes!K{fila}");
            updateRequest.ValueInputOption = SpreadsheetsResource.ValuesResource.UpdateRequest.ValueInputOptionEnum.USERENTERED;

            await updateRequest.ExecuteAsync();
            return true;
        }
        catch { return false; }
    }

    public async Task<bool> ActualizarEstatusYLlegadaAsync(string folio, string nuevoEstatus, string horaLlegada)
    {
        try
        {
            int fila = await ObtenerNumeroFilaPorIdViajeAsync(folio);
            if (fila == -1) return false;

            var bodyJ = new ValueRange { Values = new List<IList<object>> { new List<object> { nuevoEstatus } } };
            var reqJ = _service.Spreadsheets.Values.Update(bodyJ, _spreadsheetId, $"Viajes!K{fila}");
            reqJ.ValueInputOption = SpreadsheetsResource.ValuesResource.UpdateRequest.ValueInputOptionEnum.USERENTERED;
            await reqJ.ExecuteAsync();

            var bodyK = new ValueRange { Values = new List<IList<object>> { new List<object> { horaLlegada } } };
            var reqK = _service.Spreadsheets.Values.Update(bodyK, _spreadsheetId, $"Viajes!L{fila}");
            reqK.ValueInputOption = SpreadsheetsResource.ValuesResource.UpdateRequest.ValueInputOptionEnum.USERENTERED;
            await reqK.ExecuteAsync();

            return true;
        }
        catch { return false; }
    }

    public async Task<bool> FinalizarViajeAsync(string folio, string fechaFin, string kmFin, string fuelFin, string observaciones, string linkCarpeta, string duracion, string recorridoKm, string consumoLitros)
    {
        try
        {
            int fila = await ObtenerNumeroFilaPorIdViajeAsync(folio);
            if (fila == -1) return false;

            string obsFinal = string.IsNullOrWhiteSpace(observaciones) ? "N/A" : observaciones;

            // Rango actualizado L:R (Q=Recorrido KM, R=Consumo Litros)
            var datosCierre = new List<object> { kmFin, fuelFin, obsFinal, linkCarpeta, duracion, recorridoKm, consumoLitros };
            var bodyCierre = new ValueRange { Values = new List<IList<object>> { datosCierre } };

            var req1 = _service.Spreadsheets.Values.Update(bodyCierre, _spreadsheetId, $"Viajes!M{fila}:S{fila}");
            req1.ValueInputOption = SpreadsheetsResource.ValuesResource.UpdateRequest.ValueInputOptionEnum.USERENTERED;
            await req1.ExecuteAsync();

            await Task.Delay(500);

            var statusBody = new ValueRange { Values = new List<IList<object>> { new List<object> { "FINALIZADO" } } };
            var req2 = _service.Spreadsheets.Values.Update(statusBody, _spreadsheetId, $"Viajes!K{fila}");
            req2.ValueInputOption = SpreadsheetsResource.ValuesResource.UpdateRequest.ValueInputOptionEnum.USERENTERED;
            await req2.ExecuteAsync();

            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error real en Sheets: {ex.Message}");
            return false;
        }
    }

    private async Task<int> ObtenerNumeroFilaPorIdViajeAsync(string folio)
    {
        var response = await _service.Spreadsheets.Values.Get(_spreadsheetId, "Viajes!A:A").ExecuteAsync();
        var values = response.Values;
        if (values != null)
        {
            for (int i = 0; i < values.Count; i++)
            {
                if (values[i].Count > 0 && values[i][0].ToString().Trim().ToUpper() == folio.Trim().ToUpper())
                    return i + 1;
            }
        }
        return -1;
    }

    #endregion

    #region SECCIÓN 7: TRACKING HORIZONTAL (A-B, D-E...)

    // 1. Algoritmo para letras de columnas (Maneja A, B, AA, ZZZ...)
    private string ObtenerLetraColumna(int columna)
    {
        string nombreColumna = string.Empty;
        while (columna > 0)
        {
            int residuo = (columna - 1) % 26;
            nombreColumna = (char)('A' + residuo) + nombreColumna;
            columna = (columna - residuo) / 26;
        }
        return nombreColumna;
    }

    // 2. Busca o crea la hoja y devuelve las columnas gemelas (A-B, D-E, etc.)
    public async Task<(string colA, string colB, string sheetId)> ObtenerColumnasLibresAsync(string nombreHoja)
    {
        // Verificar si la hoja existe
        var spreadsheet = await _service.Spreadsheets.Get(_spreadsheetId).ExecuteAsync();
        var hoja = spreadsheet.Sheets.FirstOrDefault(s => s.Properties.Title == nombreHoja);

        string sId;

        if (hoja == null)
        {
            // Si no existe, la creamos
            var addSheetRequest = new Request { AddSheet = new AddSheetRequest { Properties = new SheetProperties { Title = nombreHoja } } };
            var batchUpdate = new BatchUpdateSpreadsheetRequest { Requests = new List<Request> { addSheetRequest } };
            var nuevaHojaRes = await _service.Spreadsheets.BatchUpdate(batchUpdate, _spreadsheetId).ExecuteAsync();
            sId = nuevaHojaRes.Replies[0].AddSheet.Properties.SheetId.ToString();
            return (ObtenerLetraColumna(1), ObtenerLetraColumna(2), sId);
        }

        sId = hoja.Properties.SheetId.ToString();
        var response = await _service.Spreadsheets.Values.Get(_spreadsheetId, $"{nombreHoja}!1:1").ExecuteAsync();
        int numCols = (response.Values != null && response.Values.Count > 0) ? response.Values[0].Count : 0;

        // Calculamos el inicio: si hay datos, saltamos 2 columnas + 1 de espacio
        int indiceInicio = (numCols == 0) ? 1 : numCols + 2;

        return (ObtenerLetraColumna(indiceInicio), ObtenerLetraColumna(indiceInicio + 1), sId);
    }

    // 3. Escribe un punto GPS en una FILA y COLUMNA específica (Solución a image_70ab21)
    public async Task<bool> SubirPuntoGPSTiempoRealAsync(string hoja, string colA, string colB, int fila, UbicacionViaje p)
    {
        try
        {
            var valores = new List<IList<object>> {
            new List<object> { p.FechaHora.ToString("dd/MM HH:mm:ss"), $"{p.Latitud}, {p.Longitud}" }
        };

            var valueRange = new ValueRange { Values = valores };
            string rangoExacto = $"{hoja}!{colA}{fila}:{colB}{fila}";

            var request = _service.Spreadsheets.Values.Update(valueRange, _spreadsheetId, rangoExacto);
            request.ValueInputOption = SpreadsheetsResource.ValuesResource.UpdateRequest.ValueInputOptionEnum.USERENTERED;

            await request.ExecuteAsync();
            return true;
        }
        catch { return false; }
    }

    // 4. Inserta el encabezado del Folio en la fila 1
    public async Task InsertarFolioEncabezadoAsync(string hoja, string colA, string folio)
    {
        var body = new ValueRange { Values = new List<IList<object>> { new List<object> { $"FOLIO: {folio}" } } };
        var req = _service.Spreadsheets.Values.Update(body, _spreadsheetId, $"{hoja}!{colA}1");
        req.ValueInputOption = SpreadsheetsResource.ValuesResource.UpdateRequest.ValueInputOptionEnum.USERENTERED;
        await req.ExecuteAsync();
    }
    #endregion

    #region SECCIÓN 8: VÍNCULOS DINÁMICOS AL HISTORIAL

    public async Task<bool> CrearVinculoHistorialAsync(string folio, string nombreHojaMes, string sheetId, string colLetra)
    {
        try
        {
            // 1. Buscamos la fila donde está el folio en la pestaña "Viajes" (Columna A)
            var response = await _service.Spreadsheets.Values.Get(_spreadsheetId, "Viajes!A:A").ExecuteAsync();
            int filaDestino = -1;

            if (response.Values != null)
            {
                for (int i = 0; i < response.Values.Count; i++)
                {
                    if (response.Values[i].Count > 0 && response.Values[i][0].ToString().Trim().ToUpper() == folio.Trim().ToUpper())
                    {
                        filaDestino = i + 1; // Sheets usa índice basado en 1
                        break;
                    }
                }
            }

            if (filaDestino == -1) return false;

            // 2. Construimos la URL interna de Google Sheets
            // gid: ID único de la pestaña | range: Celda donde inicia el folio (ej: D1)
            string urlInterna = $"https://docs.google.com/spreadsheets/d/{_spreadsheetId}/edit#gid={sheetId}&range={colLetra}1";

            // 3. Creamos la fórmula de Hipervínculo (Usamos sintaxis estándar de la API)
            string formula = $"=HYPERLINK(\"{urlInterna}\"; \"📍 VER RUTA\")";

            var body = new ValueRange { Values = new List<IList<object>> { new List<object> { formula } } };

            // Asumimos que la columna de Historial es la 'Q' (puedes ajustarla según tu diseño)
            var request = _service.Spreadsheets.Values.Update(body, _spreadsheetId, $"Viajes!T{filaDestino}");
            request.ValueInputOption = SpreadsheetsResource.ValuesResource.UpdateRequest.ValueInputOptionEnum.USERENTERED;

            await request.ExecuteAsync();
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("Error al crear vínculo: " + ex.Message);
            return false;
        }
    }

    public async Task<bool> ActualizarVinculoMapaRutaAsync(string folio, string hoja, string colA, string colB)
    {
        try
        {
            int filaViaje = await ObtenerNumeroFilaPorIdViajeAsync(folio);
            if (filaViaje == -1) return false;

            var response = await _service.Spreadsheets.Values
                .Get(_spreadsheetId, $"{hoja}!{colB}2:{colB}")
                .ExecuteAsync();

            var coordenadas = response.Values?
                .Where(f => f.Count > 0 && !string.IsNullOrWhiteSpace(f[0]?.ToString()))
                .Select(f => f[0].ToString().Trim())
                .Where(c => c.Contains(','))
                .ToList() ?? new List<string>();

            if (coordenadas.Count < 2) return false;

            string origen = Uri.EscapeDataString(coordenadas.First());
            string destino = Uri.EscapeDataString(coordenadas.Last());
            var interiores = coordenadas.Skip(1).Take(coordenadas.Count - 2).ToList();
            var waypoints = new List<string>();
            const int maxWaypoints = 9;

            if (interiores.Count <= maxWaypoints)
                waypoints.AddRange(interiores);
            else
                for (int i = 1; i <= maxWaypoints; i++)
                    waypoints.Add(interiores[(int)Math.Round(i * (interiores.Count - 1d) / (maxWaypoints + 1))]);

            string url = $"https://www.google.com/maps/dir/?api=1&origin={origen}&destination={destino}&travelmode=driving";
            if (waypoints.Count > 0)
                url += "&waypoints=" + Uri.EscapeDataString(string.Join("|", waypoints));

            string formula = $"=HYPERLINK(\"{url}\"; \"🗺️ VER MAPA\")";
            var body = new ValueRange { Values = new List<IList<object>> { new List<object> { formula } } };
            var request = _service.Spreadsheets.Values.Update(body, _spreadsheetId, $"Viajes!U{filaViaje}");
            request.ValueInputOption = SpreadsheetsResource.ValuesResource.UpdateRequest.ValueInputOptionEnum.USERENTERED;
            await request.ExecuteAsync();
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("Error al crear mapa de ruta: " + ex.Message);
            return false;
        }
    }

    #endregion
    public async Task<List<ViajeHistorial>> ObtenerHistorialPorOperadorAsync(string idOperador)
    {
        // Pedimos hasta la columna S para tener todos los datos de cierre
        var response = await _service.Spreadsheets.Values.Get(_spreadsheetId, "Viajes!A:S").ExecuteAsync();
        var values = response.Values;
        var lista = new List<ViajeHistorial>();

        if (values != null)
        {
            for (int i = values.Count - 1; i >= 1; i--)
            {
                var fila = values[i];
                // El ID del operador está en la columna C (índice 2)
                if (fila.Count > 2 && fila[2].ToString() == idOperador)
                {
                    // Cálculo de rendimiento (Km / L)
                    string rendimientoCalculado = "N/A";
                    if (fila.Count > 18) // Si tiene datos de cierre (R y S)
                    {
                        double.TryParse(fila[17]?.ToString(), out double kms);
                        double.TryParse(fila[18]?.ToString(), out double lts);
                        if (lts > 0) rendimientoCalculado = (kms / lts).ToString("0.##") + " Km/L";
                    }

                    lista.Add(new ViajeHistorial
                    {
                        Folio = fila.Count > 0 ? fila[0]?.ToString() : "",
                        Fecha = fila.Count > 1 ? fila[1]?.ToString() : "",
                        Unidad = fila.Count > 3 ? fila[3]?.ToString() : "",
                        Origen = fila.Count > 5 ? fila[5]?.ToString() : "",
                        Destino = fila.Count > 6 ? fila[6]?.ToString() : "",
                        Estado = fila.Count > 10 ? fila[10]?.ToString() : "PENDIENTE",
                        FechaLlegada = fila.Count > 11 ? fila[11]?.ToString() : "Pendiente",
                        Duracion = fila.Count > 16 ? fila[16]?.ToString() : "N/A",
                        KmRecorrido = fila.Count > 17 ? fila[17]?.ToString() + " Km" : "0 Km",
                        CombConsumido = fila.Count > 18 ? fila[18]?.ToString() + " L" : "0 L",
                        Rendimiento = rendimientoCalculado
                    });
                }
                if (lista.Count >= 10) break;
            }
        }
        return lista;
    }

    // Envía un registro de error a la pestaña Logs_App en Google Sheets
    public async Task<bool> GuardarLogEnSheetsAsync(LogRecord log)
    {
        try
        {
            var rango = "Logs_App!A:E"; 
            var valores = new List<IList<object>>
            {
                new List<object>
                {
                    log.FechaHora.ToString("dd/MM/yyyy HH:mm:ss"),
                    log.TipoError,
                    log.Mensaje,
                    log.StackTrace,
                    log.IdOperador 
                }
            };

            var request = _service.Spreadsheets.Values.Append(
                new Google.Apis.Sheets.v4.Data.ValueRange { Values = valores },
                _spreadsheetId,
                rango
            );

            request.ValueInputOption = SpreadsheetsResource.ValuesResource.AppendRequest.ValueInputOptionEnum.USERENTERED;
            request.InsertDataOption = SpreadsheetsResource.ValuesResource.AppendRequest.InsertDataOptionEnum.INSERTROWS;
            await request.ExecuteAsync();
            return true;
        }
        catch
        {
            return false; // Si no hay internet, devuelve false para reintentar luego
        }
    }

}
