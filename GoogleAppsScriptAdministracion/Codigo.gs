const LOGIAPP = Object.freeze({
  HOJA_OPERADORES: 'Operadores',
  HOJA_VIAJES: 'Viajes',
  HOJA_MANTENIMIENTO: 'Mantenimiento',
  HOJA_LOGS: 'Logs_App',
  HOJA_AUDITORIA: 'Auditoria_Administrativa',
  COL_ID_OPERADOR: 1,
  COL_ESTADO_OPERADOR: 13,
  COL_LINK_CARPETA: 11,
  COL_OPERADOR_VIAJE: 3,
  COL_FOLIO_VIAJE: 1,
  COL_OPERADOR_MANTENIMIENTO: 4,
  COL_OPERADOR_LOG: 5
});

function onOpen() {
  SpreadsheetApp.getUi()
    .createMenu('Administración LogiApp')
    .addItem('Revisar operador', 'revisarOperador')
    .addItem('Desactivar operador', 'desactivarOperador')
    .addSeparator()
    .addItem('Eliminar operador (solo sin datos)', 'eliminarOperadorSinDatos')
    .addItem('Eliminar operador y TODOS sus datos', 'eliminarOperadorCompleto')
    .addSeparator()
    .addItem('Proteger hoja Operadores', 'protegerHojaOperadores')
    .addToUi();
}

function revisarOperador() {
  const id = solicitarIdOperador_();
  if (!id) return;
  const resumen = obtenerResumenOperador_(id);
  SpreadsheetApp.getUi().alert('Revisión de ' + id, textoResumen_(resumen), SpreadsheetApp.getUi().ButtonSet.OK);
}

function desactivarOperador() {
  const id = solicitarIdOperador_();
  if (!id) return;
  const resumen = obtenerResumenOperador_(id);
  if (!resumen.existe) return avisar_('No existe el operador ' + id + '.');

  const ui = SpreadsheetApp.getUi();
  const respuesta = ui.alert(
    'Desactivar ' + id,
    textoResumen_(resumen) + '\n\nEl operador ya no podrá iniciar sesión, pero se conservarán todos sus datos.',
    ui.ButtonSet.YES_NO);
  if (respuesta !== ui.Button.YES) return;

  hojaRequerida_(LOGIAPP.HOJA_OPERADORES)
    .getRange(resumen.filaOperador, LOGIAPP.COL_ESTADO_OPERADOR)
    .setValue('INACTIVO');
  registrarAuditoria_('DESACTIVAR', id, resumen, 'COMPLETADO');
  avisar_('Operador ' + id + ' desactivado. Sus viajes y evidencias se conservaron.');
}

function eliminarOperadorSinDatos() {
  const id = solicitarIdOperador_();
  if (!id) return;
  const resumen = obtenerResumenOperador_(id);
  if (!resumen.existe) return avisar_('No existe el operador ' + id + '.');

  if (resumen.viajes.length || resumen.mantenimientos.length || resumen.tieneCarpeta) {
    SpreadsheetApp.getUi().alert(
      'Eliminación bloqueada',
      textoResumen_(resumen) +
      '\n\nEste operador tiene información relacionada. Usa “Desactivar operador” o “Eliminar operador y TODOS sus datos”.',
      SpreadsheetApp.getUi().ButtonSet.OK);
    registrarAuditoria_('ELIMINAR_SIMPLE', id, resumen, 'BLOQUEADO_POR_DEPENDENCIAS');
    return;
  }

  const ui = SpreadsheetApp.getUi();
  if (ui.alert('Eliminar ' + id, 'El operador no tiene datos relacionados. ¿Eliminar su fila?', ui.ButtonSet.YES_NO) !== ui.Button.YES) return;
  hojaRequerida_(LOGIAPP.HOJA_OPERADORES).deleteRow(resumen.filaOperador);
  registrarAuditoria_('ELIMINAR_SIMPLE', id, resumen, 'COMPLETADO');
  avisar_('Operador ' + id + ' eliminado.');
}

function eliminarOperadorCompleto() {
  const id = solicitarIdOperador_();
  if (!id) return;
  const lock = LockService.getDocumentLock();
  lock.waitLock(30000);

  try {
    const resumen = obtenerResumenOperador_(id);
    if (!resumen.existe) return avisar_('No existe el operador ' + id + '.');

    const ui = SpreadsheetApp.getUi();
    const primera = ui.alert(
      'ELIMINACIÓN TOTAL',
      textoResumen_(resumen) +
      '\n\nSe eliminarán la fila del operador, sus viajes, mantenimientos, logs, columnas GPS y toda su carpeta de Drive. La carpeta se enviará a la papelera de Drive.',
      ui.ButtonSet.YES_NO);
    if (primera !== ui.Button.YES) return;

    const validacion = ui.prompt(
      'Confirmación final',
      'Escribe exactamente: ELIMINAR ' + id,
      ui.ButtonSet.OK_CANCEL);
    if (validacion.getSelectedButton() !== ui.Button.OK ||
        normalizar_(validacion.getResponseText()) !== 'ELIMINAR ' + id) {
      return avisar_('Confirmación incorrecta. No se eliminó nada.');
    }

    // La carpeta se manda primero a la papelera porque esta operación es recuperable.
    if (resumen.tieneCarpeta) enviarCarpetaOperadorAPapelera_(resumen.linkCarpeta);

    eliminarTrackingDeFolios_(resumen.viajes.map(v => v.folio));
    eliminarFilas_(LOGIAPP.HOJA_VIAJES, resumen.viajes.map(v => v.fila));
    eliminarFilas_(LOGIAPP.HOJA_MANTENIMIENTO, resumen.mantenimientos.map(v => v.fila));
    eliminarFilas_(LOGIAPP.HOJA_LOGS, resumen.logs.map(v => v.fila), true);
    hojaRequerida_(LOGIAPP.HOJA_OPERADORES).deleteRow(resumen.filaOperador);

    registrarAuditoria_('ELIMINAR_TOTAL', id, resumen, 'COMPLETADO');
    avisar_('Se eliminó completamente ' + id + '. La carpeta quedó en la papelera de Drive y puede recuperarse temporalmente.');
  } catch (error) {
    registrarAuditoria_('ELIMINAR_TOTAL', id, {}, 'ERROR: ' + error.message);
    SpreadsheetApp.getUi().alert(
      'Proceso incompleto',
      'Ocurrió un error y el proceso se detuvo: ' + error.message +
      '\nRevisa Auditoria_Administrativa antes de volver a intentarlo.',
      SpreadsheetApp.getUi().ButtonSet.OK);
    throw error;
  } finally {
    lock.releaseLock();
  }
}

function protegerHojaOperadores() {
  const hoja = hojaRequerida_(LOGIAPP.HOJA_OPERADORES);
  const existentes = hoja.getProtections(SpreadsheetApp.ProtectionType.SHEET)
    .filter(p => p.getDescription() === 'LogiApp - usar menú administrativo');
  const proteccion = existentes[0] || hoja.protect().setDescription('LogiApp - usar menú administrativo');
  proteccion.setWarningOnly(true);
  avisar_('Protección activada. Sheets mostrará una advertencia antes de editar o borrar manualmente la hoja Operadores. Usa el menú Administración LogiApp.');
}

function obtenerResumenOperador_(id) {
  const operadores = hojaRequerida_(LOGIAPP.HOJA_OPERADORES);
  const datosOperadores = operadores.getDataRange().getDisplayValues();
  let filaOperador = 0;
  let linkCarpeta = '';
  for (let i = 1; i < datosOperadores.length; i++) {
    if (normalizar_(datosOperadores[i][LOGIAPP.COL_ID_OPERADOR - 1]) === id) {
      filaOperador = i + 1;
      linkCarpeta = datosOperadores[i][LOGIAPP.COL_LINK_CARPETA - 1] || '';
      break;
    }
  }

  const viajes = buscarFilas_(LOGIAPP.HOJA_VIAJES, LOGIAPP.COL_OPERADOR_VIAJE, id, LOGIAPP.COL_FOLIO_VIAJE);
  return {
    existe: filaOperador > 0,
    filaOperador,
    linkCarpeta,
    tieneCarpeta: Boolean(extraerIdDrive_(linkCarpeta)),
    viajes,
    mantenimientos: buscarFilas_(LOGIAPP.HOJA_MANTENIMIENTO, LOGIAPP.COL_OPERADOR_MANTENIMIENTO, id),
    logs: buscarFilas_(LOGIAPP.HOJA_LOGS, LOGIAPP.COL_OPERADOR_LOG, id, null, true)
  };
}

function buscarFilas_(nombreHoja, columnaId, id, columnaFolio, opcional) {
  const hoja = SpreadsheetApp.getActive().getSheetByName(nombreHoja);
  if (!hoja) {
    if (opcional) return [];
    throw new Error('No existe la hoja requerida: ' + nombreHoja);
  }
  const datos = hoja.getDataRange().getDisplayValues();
  const resultado = [];
  for (let i = 1; i < datos.length; i++) {
    if (normalizar_(datos[i][columnaId - 1]) === id) {
      resultado.push({ fila: i + 1, folio: columnaFolio ? datos[i][columnaFolio - 1] : '' });
    }
  }
  return resultado;
}

function eliminarTrackingDeFolios_(folios) {
  if (!folios.length) return;
  const buscados = new Set(folios.map(f => 'FOLIO: ' + normalizar_(f)));
  SpreadsheetApp.getActive().getSheets()
    .filter(h => h.getName().startsWith('Historial_'))
    .forEach(hoja => {
      const ultimaCol = hoja.getLastColumn();
      if (!ultimaCol) return;
      const encabezados = hoja.getRange(1, 1, 1, ultimaCol).getDisplayValues()[0];
      const columnas = [];
      encabezados.forEach((valor, indice) => {
        if (buscados.has(normalizar_(valor))) columnas.push(indice + 1);
      });
      // De derecha a izquierda para que los índices pendientes no cambien.
      columnas.sort((a, b) => b - a).forEach(columna => {
        // Cada viaje usa coordenadas/fecha en dos columnas. Se limpia también el
        // separador vacío de la derecha sin tocar el siguiente bloque.
        const cantidad = Math.min(3, hoja.getMaxColumns() - columna + 1);
        hoja.getRange(1, columna, hoja.getMaxRows(), cantidad).clearContent();
      });
  });
}

function eliminarFilas_(nombreHoja, filas, opcional) {
  if (!filas.length) return;
  const hoja = SpreadsheetApp.getActive().getSheetByName(nombreHoja);
  if (!hoja) {
    if (opcional) return;
    throw new Error('No existe la hoja requerida: ' + nombreHoja);
  }
  [...new Set(filas)].sort((a, b) => b - a).forEach(fila => hoja.deleteRow(fila));
}

function enviarCarpetaOperadorAPapelera_(link) {
  const idDocumentos = extraerIdDrive_(link);
  if (!idDocumentos) throw new Error('El enlace de carpeta del operador no es válido.');
  const carpetaGuardada = DriveApp.getFolderById(idDocumentos);
  let carpetaOperador = carpetaGuardada;
  if (normalizar_(carpetaGuardada.getName()) === 'DOCUMENTOS') {
    const padres = carpetaGuardada.getParents();
    if (!padres.hasNext()) throw new Error('La carpeta documentos no tiene carpeta de operador padre.');
    carpetaOperador = padres.next();
  }
  carpetaOperador.setTrashed(true);
}

function extraerIdDrive_(texto) {
  const coincidencia = String(texto || '').match(/[-\w]{20,}/);
  return coincidencia ? coincidencia[0] : '';
}

function solicitarIdOperador_() {
  const respuesta = SpreadsheetApp.getUi().prompt('Operador', 'Ingresa el ID, por ejemplo OP-001:', SpreadsheetApp.getUi().ButtonSet.OK_CANCEL);
  if (respuesta.getSelectedButton() !== SpreadsheetApp.getUi().Button.OK) return '';
  const id = normalizar_(respuesta.getResponseText());
  if (!/^OP-\d{3,}$/.test(id)) {
    avisar_('ID inválido. Usa el formato OP-001.');
    return '';
  }
  return id;
}

function textoResumen_(r) {
  if (!r.existe) return 'El operador no existe.';
  return [
    'Viajes: ' + r.viajes.length,
    'Mantenimientos: ' + r.mantenimientos.length,
    'Logs: ' + r.logs.length,
    'Carpeta de Drive: ' + (r.tieneCarpeta ? 'Sí' : 'No')
  ].join('\n');
}

function registrarAuditoria_(accion, id, resumen, resultado) {
  const libro = SpreadsheetApp.getActive();
  let hoja = libro.getSheetByName(LOGIAPP.HOJA_AUDITORIA);
  if (!hoja) {
    hoja = libro.insertSheet(LOGIAPP.HOJA_AUDITORIA);
    hoja.appendRow(['Fecha', 'Usuario administrativo', 'Acción', 'Operador', 'Viajes', 'Mantenimientos', 'Logs', 'Resultado']);
    hoja.setFrozenRows(1);
  }
  hoja.appendRow([
    new Date(), Session.getEffectiveUser().getEmail(), accion, id,
    (resumen.viajes || []).length, (resumen.mantenimientos || []).length,
    (resumen.logs || []).length, resultado
  ]);
}

function hojaRequerida_(nombre) {
  const hoja = SpreadsheetApp.getActive().getSheetByName(nombre);
  if (!hoja) throw new Error('No existe la hoja requerida: ' + nombre);
  return hoja;
}

function normalizar_(valor) {
  return String(valor || '').trim().toUpperCase();
}

function avisar_(mensaje) {
  SpreadsheetApp.getUi().alert('Administración LogiApp', mensaje, SpreadsheetApp.getUi().ButtonSet.OK);
}
