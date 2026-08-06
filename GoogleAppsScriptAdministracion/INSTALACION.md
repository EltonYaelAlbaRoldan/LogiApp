# Administración segura de operadores en Google Sheets

Este script agrega el menú **Administración LogiApp** al archivo de Google Sheets.

## Instalación

1. Abre el Google Sheets de LogiApp con la cuenta administrativa.
2. Entra en **Extensiones > Apps Script**.
3. Sustituye el contenido de `Código.gs` por el contenido del archivo `Codigo.gs` de esta carpeta.
4. En Apps Script abre **Configuración del proyecto** y activa **Mostrar el archivo de manifiesto `appsscript.json` en el editor**.
5. Abre `appsscript.json` y conserva cualquier configuración o permiso que ya utilicen tus otros scripts. Agrega solamente los alcances que falten; el archivo de esta carpeta ya combina `spreadsheets`, `gmail.send`, `userinfo.email`, `drive` y `script.container.ui`.
6. Pulsa **Guardar** y ejecuta manualmente la función `onOpen` una vez.
7. Google solicitará nuevamente autorización para administrar el Sheet y la carpeta de Drive. Concédela con la cuenta propietaria.
8. Regresa al Sheet y actualiza la página.
9. Abre **Administración LogiApp > Proteger hoja Operadores** una vez.

Si el script ya se había autorizado antes de agregar el manifiesto, ejecuta cualquier función desde el editor. Google detectará el nuevo permiso `drive` y mostrará otra vez la pantalla de autorización.

## Uso

- **Revisar operador:** muestra cuántos viajes, mantenimientos, logs y carpeta tiene.
- **Desactivar operador:** cambia su estado a `INACTIVO` sin borrar información.
- **Eliminar operador (solo sin datos):** se bloquea si encuentra cualquier dependencia.
- **Eliminar operador y TODOS sus datos:** exige dos confirmaciones y escribir `ELIMINAR OP-001`.

La eliminación total manda la carpeta completa del operador a la papelera de Drive, limpia sus bloques GPS de todas las hojas `Historial_*`, elimina sus viajes, mantenimientos y logs, y solamente al final elimina la fila de `Operadores`.

Todas las acciones quedan registradas en `Auditoria_Administrativa`.

## Importante

Google Sheets no permite impedir que el propietario del archivo borre filas por medios manuales. La protección genera una advertencia para desalentar ese borrado. Para garantizar el flujo, los administrativos deben usar siempre el menú y no eliminar filas directamente.
