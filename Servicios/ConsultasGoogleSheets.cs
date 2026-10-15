using Google.Apis.Sheets.v4;
using Google.Apis.Sheets.v4.Data;
using LogiApp.Modelos;
using System.Security.Cryptography;
using System.Globalization;

namespace LogiApp.Servicios;

public class ConsultasGoogleSheets
{
    private static readonly SemaphoreSlim _semaforoTracking = new(1, 1);
    private const int MaxCaracteresBloqueTracking = 45000;
    private const int PrimeraColumnaRecorrido = 2;
    #region CONFIGURACIÓN
    private readonly SheetsService _service;
    private readonly string _spreadsheetId = "1Gl41wM-3OPepAxE6K6TjRGbCJsfjjYOSlubkIaCTjs0";
    public string UltimoError { get; private set; } = string.Empty;

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
                        Values = listaDatos.Select(d => {
                            string valor = d?.ToString() ?? "";
                            return new CellData {
                                // Si el texto empieza con "=" lo inyecta como fórmula, sino como texto normal.
                                UserEnteredValue = valor.StartsWith("=")
                                    ? new ExtendedValue { FormulaValue = valor }
                                    : new ExtendedValue { StringValue = valor }
                            };
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
        UltimoError = string.Empty;
        try
        {
            int fila = await ObtenerNumeroFilaPorIdAsync(idOperador);
            if (fila == -1)
            {
                UltimoError = $"No se encontró la fila del operador {idOperador?.Trim().ToUpperInvariant()}.";
                return false;
            }

            string rango = $"Operadores!L{fila}";
            var body = new ValueRange { Values = new List<IList<object>> { new List<object> { SeguridadPassword.CrearHash(nuevaPass) } } };

            var updateRequest = _service.Spreadsheets.Values.Update(body, _spreadsheetId, rango);
            updateRequest.ValueInputOption = SpreadsheetsResource.ValuesResource.UpdateRequest.ValueInputOptionEnum.RAW;

            await updateRequest.ExecuteAsync();
            return true;
        }
        catch (Exception ex)
        {
            UltimoError = ex.Message;
            System.Diagnostics.Debug.WriteLine($"Error al actualizar contraseña: {ex}");
            return false;
        }
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
        string km, string linkDrive)
    {
        string formula = $"=HYPERLINK(\"{linkDrive}\"; \"VER EVIDENCIA\")";

        var range = "Mantenimiento!A:N";
        var valueRange = new ValueRange { Values = new List<IList<object>>() };

        var nuevaFila = new List<object> { folio, fechaRegistro, fechaManto, operador, unidad, lugar, taller, tipo, piezas, km, formula };
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
        string idNormalizado = idOperador?.Trim().ToUpperInvariant() ?? string.Empty;

        if (values != null)
        {
            for (int i = 0; i < values.Count; i++)
            {
                if (values[i].Count > 0 &&
                    values[i][0]?.ToString()?.Trim().ToUpperInvariant() == idNormalizado)
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
            string rango = "Clientes!A2:A100";
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
            catch { /* Si falla el Geocoding por falta de red, conservamos las coordenadas */ }

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

    // Se agrega 'fuelRecarga' para incluir el registro de recargas de combustible durante el viaje
    public async Task<bool> FinalizarViajeAsync(string folio, string fechaFin, string kmFin, string fuelFin, string fuelRecarga, string observaciones, string linkCarpeta, string duracion, string recorridoKm, string consumoLitros)
    {
        try
        {
            int fila = await ObtenerNumeroFilaPorIdViajeAsync(folio);
            if (fila == -1) return false;

            string obsFinal = string.IsNullOrWhiteSpace(observaciones) ? "N/A" : observaciones;
            string enlaceEvidencias = string.IsNullOrWhiteSpace(linkCarpeta)
                ? ""
                : $"=HYPERLINK(\"{linkCarpeta.Replace("\"", "\"\"")}\"; \"VER EVIDENCIAS\")";

            // Rango M:T (KmFin, FuelFin, FuelRecarga, Observaciones, Evidencias, Duración, Recorrido KM, Consumo Litros)
            var datosCierre = new List<object> { kmFin, fuelFin, fuelRecarga, obsFinal, enlaceEvidencias, duracion, recorridoKm, consumoLitros };
            var bodyCierre = new ValueRange { Values = new List<IList<object>> { datosCierre } };

            var req1 = _service.Spreadsheets.Values.Update(bodyCierre, _spreadsheetId, $"Viajes!M{fila}:T{fila}");
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

    #region SECCIÓN 7: TRACKING VERTICAL (UNA FILA POR VIAJE)

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

    public async Task<(int fila, string sheetId)> ObtenerOCrearFilaTrackingAsync(string nombreHoja, string folio)
    {
        var spreadsheet = await _service.Spreadsheets.Get(_spreadsheetId).ExecuteAsync();
        var hoja = spreadsheet.Sheets.FirstOrDefault(s => s.Properties.Title == nombreHoja);

        if (hoja == null)
        {
            var addSheet = new Request
            {
                AddSheet = new AddSheetRequest
                {
                    Properties = new SheetProperties
                    {
                        Title = nombreHoja,
                        GridProperties = new GridProperties { RowCount = 1000, ColumnCount = 26 }
                    }
                }
            };
            var creada = await _service.Spreadsheets.BatchUpdate(
                new BatchUpdateSpreadsheetRequest { Requests = new List<Request> { addSheet } }, _spreadsheetId).ExecuteAsync();
            hoja = new Sheet { Properties = creada.Replies[0].AddSheet.Properties };

            var encabezados = new ValueRange
            {
                Values = new List<IList<object>> { new List<object> { "FOLIO", "RECORRIDO" } }
            };
            var reqEncabezados = _service.Spreadsheets.Values.Update(encabezados, _spreadsheetId, $"{nombreHoja}!A1:B1");
            reqEncabezados.ValueInputOption = SpreadsheetsResource.ValuesResource.UpdateRequest.ValueInputOptionEnum.RAW;
            await reqEncabezados.ExecuteAsync();
        }

        int sheetId = (int)hoja.Properties.SheetId;
        var response = await _service.Spreadsheets.Values.Get(_spreadsheetId, $"{nombreHoja}!A:A").ExecuteAsync();
        int fila = -1;
        if (response.Values != null)
        {
            for (int i = 1; i < response.Values.Count; i++)
            {
                if (response.Values[i].Count > 0 &&
                    string.Equals(response.Values[i][0]?.ToString()?.Trim(), folio.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    fila = i + 1;
                    break;
                }
            }
        }

        if (fila == -1)
        {
            fila = Math.Max(2, (response.Values?.Count ?? 1) + 1);
            var body = new ValueRange { Values = new List<IList<object>> { new List<object> { folio } } };
            var req = _service.Spreadsheets.Values.Update(body, _spreadsheetId, $"{nombreHoja}!A{fila}");
            req.ValueInputOption = SpreadsheetsResource.ValuesResource.UpdateRequest.ValueInputOptionEnum.RAW;
            await req.ExecuteAsync();
        }

        await FormatearFilaTrackingAsync(sheetId, fila);
        return (fila, sheetId.ToString(CultureInfo.InvariantCulture));
    }

    private async Task FormatearFilaTrackingAsync(int sheetId, int fila)
    {
        var requests = new List<Request>
        {
            new Request
            {
                UpdateDimensionProperties = new UpdateDimensionPropertiesRequest
                {
                    Range = new DimensionRange { SheetId = sheetId, Dimension = "COLUMNS", StartIndex = 1, EndIndex = 2 },
                    Properties = new DimensionProperties { PixelSize = 520 }, Fields = "pixelSize"
                }
            },
            new Request
            {
                UpdateDimensionProperties = new UpdateDimensionPropertiesRequest
                {
                    Range = new DimensionRange { SheetId = sheetId, Dimension = "ROWS", StartIndex = fila - 1, EndIndex = fila },
                    Properties = new DimensionProperties { PixelSize = 30 }, Fields = "pixelSize"
                }
            },
            new Request
            {
                RepeatCell = new RepeatCellRequest
                {
                    Range = new GridRange { SheetId = sheetId, StartRowIndex = fila - 1, EndRowIndex = fila, StartColumnIndex = 1, EndColumnIndex = 2 },
                    Cell = new CellData { UserEnteredFormat = new CellFormat { WrapStrategy = "CLIP", VerticalAlignment = "TOP" } },
                    Fields = "userEnteredFormat.wrapStrategy,userEnteredFormat.verticalAlignment"
                }
            }
        };
        await _service.Spreadsheets.BatchUpdate(
            new BatchUpdateSpreadsheetRequest { Requests = requests }, _spreadsheetId).ExecuteAsync();
    }

    private async Task AsegurarColumnasAsync(int sheetId, int columnasActuales, int columnaNecesaria)
    {
        if (columnaNecesaria <= columnasActuales) return;
        var request = new Request
        {
            AppendDimension = new AppendDimensionRequest
            {
                SheetId = sheetId, Dimension = "COLUMNS", Length = Math.Max(5, columnaNecesaria - columnasActuales)
            }
        };
        await _service.Spreadsheets.BatchUpdate(
            new BatchUpdateSpreadsheetRequest { Requests = new List<Request> { request } }, _spreadsheetId).ExecuteAsync();
    }

    private async Task FormatearCeldaRecorridoAsync(int sheetId, int fila, int columna)
    {
        var requests = new List<Request>
        {
            new Request
            {
                UpdateDimensionProperties = new UpdateDimensionPropertiesRequest
                {
                    Range = new DimensionRange { SheetId = sheetId, Dimension = "COLUMNS", StartIndex = columna - 1, EndIndex = columna },
                    Properties = new DimensionProperties { PixelSize = 520 }, Fields = "pixelSize"
                }
            },
            new Request
            {
                RepeatCell = new RepeatCellRequest
                {
                    Range = new GridRange { SheetId = sheetId, StartRowIndex = fila - 1, EndRowIndex = fila, StartColumnIndex = columna - 1, EndColumnIndex = columna },
                    Cell = new CellData { UserEnteredFormat = new CellFormat { WrapStrategy = "CLIP", VerticalAlignment = "TOP" } },
                    Fields = "userEnteredFormat.wrapStrategy,userEnteredFormat.verticalAlignment"
                }
            }
        };
        await _service.Spreadsheets.BatchUpdate(
            new BatchUpdateSpreadsheetRequest { Requests = requests }, _spreadsheetId).ExecuteAsync();
    }

    private static bool TryParsearLineaTracking(string linea, out double latitud, out double longitud)
    {
        latitud = longitud = 0;
        int separador = linea.LastIndexOf(':');
        if (separador < 0) return false;
        string[] partes = linea[(separador + 1)..].Split(',', StringSplitOptions.TrimEntries);
        return partes.Length == 2 &&
            double.TryParse(partes[0], NumberStyles.Float, CultureInfo.InvariantCulture, out latitud) &&
            double.TryParse(partes[1], NumberStyles.Float, CultureInfo.InvariantCulture, out longitud);
    }

    private static bool EsDuplicadoConsecutivo(string ultimaLinea, UbicacionViaje punto) =>
        TryParsearLineaTracking(ultimaLinea, out double lat, out double lon) &&
        Math.Abs(lat - punto.Latitud) < 0.000001 && Math.Abs(lon - punto.Longitud) < 0.000001;

    private async Task ColorearExtremosTrackingAsync(int sheetId, int fila, IReadOnlyList<string> bloques)
    {
        int totalLineas = bloques.Sum(b => b.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length);
        if (totalLineas == 0) return;

        var negro = new TextFormat { ForegroundColor = new Google.Apis.Sheets.v4.Data.Color { Red = 0.12f, Green = 0.12f, Blue = 0.12f } };
        var verde = new TextFormat { ForegroundColor = new Google.Apis.Sheets.v4.Data.Color { Red = 0.10f, Green = 0.55f, Blue = 0.20f }, Bold = true };
        var rojo = new TextFormat { ForegroundColor = new Google.Apis.Sheets.v4.Data.Color { Red = 0.85f, Green = 0.12f, Blue = 0.12f }, Bold = true };
        var requests = new List<Request>();
        int ultimaCelda = -1;
        for (int i = bloques.Count - 1; i >= 0; i--)
        {
            if (!string.IsNullOrEmpty(bloques[i])) { ultimaCelda = i; break; }
        }

        for (int i = 0; i < bloques.Count; i++)
        {
            string contenido = bloques[i];
            if (string.IsNullOrEmpty(contenido)) continue;
            var estilos = new SortedDictionary<int, TextFormat> { [0] = negro };
            string[] lineas = contenido.Split('\n', StringSplitOptions.RemoveEmptyEntries);

            if (i == 0)
            {
                estilos[0] = verde;
                if (contenido.Length > lineas[0].Length)
                    estilos[lineas[0].Length] = negro;
            }

            if (totalLineas > 1 && i == ultimaCelda)
            {
                int inicioUltima = contenido.LastIndexOf(lineas[^1], StringComparison.Ordinal);
                estilos[inicioUltima] = rojo;
            }

            var cellData = new CellData
            {
                TextFormatRuns = estilos.Select(e => new TextFormatRun { StartIndex = e.Key, Format = e.Value }).ToList()
            };
            requests.Add(new Request
            {
                UpdateCells = new UpdateCellsRequest
                {
                    Range = new GridRange
                    {
                        SheetId = sheetId, StartRowIndex = fila - 1, EndRowIndex = fila,
                        StartColumnIndex = i + 1, EndColumnIndex = i + 2
                    },
                    Rows = new List<RowData> { new RowData { Values = new List<CellData> { cellData } } },
                    Fields = "textFormatRuns"
                }
            });
        }

        if (requests.Count > 0)
            await _service.Spreadsheets.BatchUpdate(
                new BatchUpdateSpreadsheetRequest { Requests = requests }, _spreadsheetId).ExecuteAsync();
    }

    public async Task<bool> SubirPuntoGPSTiempoRealAsync(
        string hoja, string folio, int fila, UbicacionViaje p, bool permitirDuplicado = false)
    {
        await _semaforoTracking.WaitAsync();
        try
        {
            var spreadsheet = await _service.Spreadsheets.Get(_spreadsheetId).ExecuteAsync();
            var sheet = spreadsheet.Sheets.FirstOrDefault(s => s.Properties.Title == hoja);
            if (sheet == null) return false;
            int sheetId = (int)sheet.Properties.SheetId;
            int columnasActuales = sheet.Properties.GridProperties?.ColumnCount ?? 26;

            if (fila < 2) (fila, _) = await ObtenerOCrearFilaTrackingAsync(hoja, folio);

            string ultimaColumna = ObtenerLetraColumna(columnasActuales);
            var response = await _service.Spreadsheets.Values.Get(
                _spreadsheetId, $"{hoja}!B{fila}:{ultimaColumna}{fila}").ExecuteAsync();
            var bloques = response.Values?.FirstOrDefault()?.Select(x => x?.ToString() ?? "").ToList()
                ?? new List<string>();
            int indiceBloque = Math.Max(0, bloques.Count - 1);
            string bloqueActual = bloques.Count == 0 ? string.Empty : bloques[indiceBloque];
            string ultimaLinea = bloqueActual.Split('\n', StringSplitOptions.RemoveEmptyEntries).LastOrDefault()?.Trim() ?? string.Empty;
            if (!permitirDuplicado && EsDuplicadoConsecutivo(ultimaLinea, p)) return true;

            string linea = $"{p.FechaHora:dd/MM HH:mm} : {p.Latitud.ToString("G17", CultureInfo.InvariantCulture)}, {p.Longitud.ToString("G17", CultureInfo.InvariantCulture)}";
            string contenido = string.IsNullOrEmpty(bloqueActual) ? linea : $"{bloqueActual}\n{linea}";
            if (contenido.Length > MaxCaracteresBloqueTracking)
            {
                indiceBloque++;
                contenido = linea;
            }

            int columnaDestino = PrimeraColumnaRecorrido + indiceBloque;
            await AsegurarColumnasAsync(sheetId, columnasActuales, columnaDestino);
            await FormatearCeldaRecorridoAsync(sheetId, fila, columnaDestino);
            string letraDestino = ObtenerLetraColumna(columnaDestino);
            var valueRange = new ValueRange { Values = new List<IList<object>> { new List<object> { contenido } } };
            var request = _service.Spreadsheets.Values.Update(valueRange, _spreadsheetId, $"{hoja}!{letraDestino}{fila}");
            request.ValueInputOption = SpreadsheetsResource.ValuesResource.UpdateRequest.ValueInputOptionEnum.RAW;
            await request.ExecuteAsync();
            while (bloques.Count <= indiceBloque) bloques.Add(string.Empty);
            bloques[indiceBloque] = contenido;
            await ColorearExtremosTrackingAsync(sheetId, fila, bloques);
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error al guardar tracking: {ex.Message}");
            return false;
        }
        finally { _semaforoTracking.Release(); }
    }

    // Migra silenciosamente puntos offline creados por la versión horizontal anterior.
    public async Task<bool> SubirPuntoGPSTiempoRealAsync(string hoja, string colA, string colB, int fila, UbicacionViaje p)
    {
        var (filaNueva, _) = await ObtenerOCrearFilaTrackingAsync(hoja, p.FolioViaje ?? "SIN FOLIO");
        p.FilaTracking = filaNueva;
        return await SubirPuntoGPSTiempoRealAsync(hoja, p.FolioViaje ?? "SIN FOLIO", filaNueva, p);
    }
    #endregion

    #region SECCIÓN 8: VÍNCULOS DINÁMICOS AL HISTORIAL

    public async Task<bool> CrearVinculoHistorialAsync(string folio, string nombreHojaMes, string sheetId, int filaTracking)
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
            // Cada viaje ocupa una fila; el enlace abre directamente su folio en la columna A.
            string urlInterna = $"https://docs.google.com/spreadsheets/d/{_spreadsheetId}/edit#gid={sheetId}&range=A{filaTracking}";

            // 3. Creamos la fórmula de Hipervínculo (Usamos sintaxis estándar de la API)
            string formula = $"=HYPERLINK(\"{urlInterna}\"; \"📍 VER RUTA\")";

            var body = new ValueRange { Values = new List<IList<object>> { new List<object> { formula } } };

            // Asumimos que la columna de Historial es la 'Q' (puedes ajustarla según tu diseño)
            var request = _service.Spreadsheets.Values.Update(body, _spreadsheetId, $"Viajes!U{filaDestino}");
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

    public async Task<bool> ActualizarVinculoMapaRutaAsync(string folio, string hoja, int filaTracking)
    {
        try
        {
            int filaViaje = await ObtenerNumeroFilaPorIdViajeAsync(folio);
            if (filaViaje == -1) return false;

            var spreadsheet = await _service.Spreadsheets.Get(_spreadsheetId).ExecuteAsync();
            var sheet = spreadsheet.Sheets.FirstOrDefault(s => s.Properties.Title == hoja);
            if (sheet == null) return false;
            int columnas = sheet.Properties.GridProperties?.ColumnCount ?? 26;
            string ultimaColumna = ObtenerLetraColumna(columnas);
            var response = await _service.Spreadsheets.Values
                .Get(_spreadsheetId, $"{hoja}!B{filaTracking}:{ultimaColumna}{filaTracking}")
                .ExecuteAsync();

            var coordenadas = new List<string>();
            foreach (var celda in response.Values?.FirstOrDefault() ?? new List<object>())
            {
                foreach (string linea in (celda?.ToString() ?? string.Empty)
                    .Split('\n', StringSplitOptions.RemoveEmptyEntries))
                {
                    if (TryParsearLineaTracking(linea.Trim(), out double lat, out double lon))
                        coordenadas.Add($"{lat.ToString("G17", CultureInfo.InvariantCulture)},{lon.ToString("G17", CultureInfo.InvariantCulture)}");
                }
            }

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
            var request = _service.Spreadsheets.Values.Update(body, _spreadsheetId, $"Viajes!V{filaViaje}");
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

    // Compatibilidad para llamadas o datos de la versión horizontal anterior.
    public async Task<bool> ActualizarVinculoMapaRutaAsync(string folio, string hoja, string colA, string colB)
    {
        var (fila, _) = await ObtenerOCrearFilaTrackingAsync(hoja, folio);
        return await ActualizarVinculoMapaRutaAsync(folio, hoja, fila);
    }

    #endregion
    public async Task<List<ViajeHistorial>> ObtenerHistorialPorOperadorAsync(string idOperador)
    {
        // Pedimos hasta la columna S para tener todos los datos de cierre
        var response = await _service.Spreadsheets.Values.Get(_spreadsheetId, "Viajes!A:V").ExecuteAsync();
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
                    if (fila.Count > 19) // Si tiene recorrido y consumo (S y T)
                    {
                        double.TryParse(fila[18]?.ToString(), out double kms);
                        double.TryParse(fila[19]?.ToString(), out double lts);
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
                        Duracion = fila.Count > 17 ? fila[17]?.ToString() : "N/A",
                        KmRecorrido = fila.Count > 18 ? fila[18]?.ToString() + " Km" : "0 Km",
                        CombConsumido = fila.Count > 19 ? fila[19]?.ToString() + " L" : "0 L",
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
