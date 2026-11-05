# Auditoría técnica estática de LogiApp para informe de residencia

Fecha de revisión: 23 de octubre de 2026. Directorio revisado: repositorio LogiApp. Referencia Git: rama master, HEAD 54ef6a3 (15 de octubre de 2026). Se revisaron archivos de proyecto, modelos, servicios, vistas, implementación nativa Android, Apps Script y programa de pruebas de humo. No se modificó código ni se probaron operaciones sobre servicios productivos. No se leyeron los archivos Resources/Raw/*.json. Este documento no contiene valores de autenticación, identificadores privados de recursos ni correos de cuentas. No se encontraron AGENTS.md aplicables en los niveles superiores comprobados ni en el árbol de código inventariado.

## 1. Criterio de evidencia

- Implementado: comportamiento expresado en el código actual.
- Documentado: afirmación contenida en un documento de Drive, sin implicar que se haya reproducido.
- Verificado estáticamente: conclusión de lectura del código; no equivale a una prueba de ejecución.
- Pendiente de validación: requiere dispositivo, conexión, hoja controlada o protocolo medido.
- El programa de pruebas existente tiene seis comprobaciones de contraseñas. El agente principal ejecutó el 23/10/2026 dotnet run --project LogiApp.SmokeTests/LogiApp.SmokeTests.csproj --no-restore: código de salida 0 y mensaje «Smoke tests completados correctamente». El alcance se limita a SeguridadPassword; no acredita integración ni operación en campo.
- La existencia de APK, fechas de Git y mensajes de cambios acredita artefactos y evolución; no certifica instalación en todos los teléfonos, adopción por usuarios ni resultados económicos.

## 2. Arquitectura real y stack

LogiApp es una aplicación .NET MAUI de proyecto único, escrita en C# y XAML. La configuración declara net9.0-android, net9.0-ios, net9.0-maccatalyst y agrega Windows en hosts Windows. La operación nativa específica de seguimiento en segundo plano revisada corresponde a Android. Las otras plataformas comparten código y un temporizador en pantalla, pero no tienen un servicio nativo equivalente en el árbol revisado.

Evidencias: LogiApp.csproj:4-5,18-21,30-31,36-40,111-115; MauiProgram.cs:9-24; Servicios/ControlTrackingSegundoPlano.cs:9-43; Vistas/PaginaTerminarViaje.xaml.cs:55-58.

Dependencias directas: Google.Apis.Drive.v3 1.73.0.3996; Google.Apis.Sheets.v4 1.72.0.3966; Microsoft.Maui.Controls mediante MauiVersion; Microsoft.Extensions.Logging.Debug 9.0.8; sqlite-net-pcl 1.9.172. No afirmar “última versión” ni LTS: la evidencia es el destino .NET 9. La serialización del cierre pendiente usa System.Text.Json, no una dependencia directa de Newtonsoft.Json.

La separación existente es por carpetas Modelos, Servicios y Vistas. Las páginas contienen controladores de eventos, validaciones, cálculos, navegación e invocaciones a servicios. No se encontró carpeta ViewModels, registro de servicios en el contenedor de dependencias ni una capa de comandos MVVM. La arquitectura debe describirse como interfaz XAML con lógica en code-behind, modelos y servicios auxiliares; MVVM puede ser evolución futura, no patrón implementado y demostrado.

Arranque: App.xaml.cs:11-42 se suscribe a ConnectivityChanged, intenta sincronización y consulta Preferences para decidir entre bienvenida, menú y recuperación del viaje activo. Se utiliza NavigationPage como navegación efectiva. AppShell existe pero no es el flujo de arranque que instancia App.

## 3. Modelo de persistencia

### SQLite local

Archivo LogiAppLocal.db3 en FileSystem.AppDataDirectory. Tablas creadas expresamente: LogRecord y UbicacionViaje. No hay tablas locales de Operador, Viaje completo ni Mantenimiento. La conexión y operaciones se coordinan con SemaphoreSlim estático. Evidencias: Servicios/BaseDeDatosLocal.cs:13-38,47-120.

LogRecord: Id entero autoincremental, FechaHora, TipoError, Mensaje, StackTrace, Sincronizado, IdOperador (Modelos/LogRecord.cs:8-17).

UbicacionViaje: Id entero autoincremental, FolioViaje, Latitud, Longitud, FechaHora, Sincronizado, HojaTracking, ColumnaTrackingA/B heredadas y FilaTracking (Modelos/UbicacionViaje.cs:7-17).

Se evita guardar un punto consecutivo del mismo folio si latitud y longitud difieren como máximo 0.00001, salvo permitirDuplicado=true. Este umbral es una comparación numérica y no una certificación de precisión GPS. Al confirmar subida se borra el punto por Id (BaseDeDatosLocal.cs:84-120). El punto final permite repetición al guardarlo localmente y en el envío directo. Si queda offline, el reintento usa la deduplicación normal y puede omitir su línea en el historial si repite la última coordenada remota; la hora de llegada del viaje se conserva por separado.

### Preferences y archivos

Preferences guarda identificación de sesión, registro pendiente, folio activo, inicio, odómetro y combustible de salida, hoja/fila de tracking, llegada confirmada y cierres pendientes. El cierre completo se serializa en claves por folio y una lista de folios pendientes; las fotografías se copian a AppDataDirectory/EvidenciasViajes/<folio>. La recuperación no se basa exclusivamente en SQLite (PaginaRegistro.xaml.cs:39-55,194-198; PaginaViajes.xaml.cs:190-211; PaginaTerminarViaje.xaml.cs:448-479; SincronizadorGlobal.cs:28-80).

No se encontró cifrado de SQLite ni uso de SecureStorage para este estado. Esto es una limitación de la solución revisada. No afirmar “datos cifrados localmente”.

### Google Sheets

Esquema inferido de rangos y orden de parámetros; debe verificarse frente al libro real antes de cambiar columnas:

| Hoja | Columnas utilizadas | Significado |
|---|---|---|
| Operadores | A:M | A Id; B nombre; C edad; D correo; E teléfono; F teléfono adicional; G clave interbancaria; H número licencia; I vigencia licencia; J vigencia médica; K vínculo carpeta documentos; L hash de contraseña; M estado |
| Viajes | A:V | A folio; B inicio; C operador; D unidad; E remolque; F origen; G destino; H cliente; I km salida; J combustible salida; K estado; L llegada; M km final; N combustible final; O recarga; P observaciones; Q evidencias; R duración; S km recorridos; T consumo; U vínculo historial; V vínculo mapa |
| Mantenimiento | A:K efectivos | Folio, fecha registro, fecha servicio, operador, unidad, lugar, taller, tipo, piezas, km, vínculo evidencia. El método declara rango A:N pero envía once valores |
| Unidades/Remolques | A2:B100 | Catálogos mostrados como código y descripción |
| Destinos/Clientes | A2:A100 | Catálogos de selección |
| Historial_<mes>_<año> | A y B en adelante | Una fila por folio. B y siguientes almacenan bloques de texto con fecha/hora y coordenadas |
| Logs_App | A:E | Fecha, proceso, mensaje, stack trace, operador |
| Auditoria_Administrativa | A:H | Apps Script: fecha, usuario administrativo, acción, operador, conteos y resultado |

Referencias: PaginaRegistro.xaml.cs:187-192; ConsultasGoogleSheets.cs:23-130,160-182,273-295,305-321,324-359,411-459,511-559,602-659,810-853,874-968,986-1057.

Google Sheets funciona como repositorio tabular compartido; no implementa por sí mismo restricciones relacionales, transacciones de negocio o claves foráneas. Los vínculos entre entidades dependen de identificadores y lógica de la aplicación.

### Google Drive

Raíz configurada en el servicio. Alta: carpeta por “ID - nombre”, subcarpetas documentos y evidencias. Documentos PDF con nombre funcional estable, sustituidos si existe igual nombre. Viajes: carpeta del operador/evidencias/folio y fotos Evidencia_<folio>_<numero>.<extension>. Mantenimiento: carpeta del operador/mantenimiento/folio y fotos Mantenimiento_<folio>_<numero>.<extension>. Se comprueba existencia por nombre para reducir duplicados al reintentar. Evidencias: ConsultasGoogleDrive.cs:16-60,96-132,161-209,217-253.

No se encontró compresión de imágenes. El servicio envía MIME image/jpeg aunque preserve la extensión original; no afirmar conversión real de formato. La carpeta raíz privada y enlaces concretos no deben aparecer en el informe público.

## 4. Flujos implementados

### Alta del operador

Dos fases. Fase 1 valida campos requeridos, edad mayor o igual a 18, teléfono de diez dígitos, clave interbancaria numérica de dieciocho dígitos, número de licencia de máximo quince caracteres, correo por expresión regular y vigencias mayores a noventa días. Genera OP-001 a partir del máximo existente, crea estructura de Drive y fila con contraseña transformada a hash. Estado inicial VALIDACION y lista desplegable estricta VALIDACION/ACTIVO/INACTIVO en M. Guarda datos de recuperación y habilita expediente.

Fase 2 exige nueve PDF: licencia federal, apto médico, INE, acta de nacimiento, NSS, CV, comprobante de domicilio, RFC/cédula fiscal y CURP. Sube cada documento; envía aviso al operador y a administración. El correo de bienvenida contiene ID y estado de validación, no la contraseña. No existe OCR ni verificación automática de autenticidad documental: la revisión administrativa sigue siendo humana.

Referencias: PaginaRegistro.xaml.cs:90-103,121-218,222-297,304-338; ConsultasGoogleSheets.cs:23-130,138-157; ServicioEmail.cs:16-65 (sin exponer configuración).

### Acceso y recuperación

El acceso utiliza ID de operador y contraseña. La contraseña se compara con PBKDF2 SHA-256, 210000 iteraciones, salt aleatorio de 16 bytes y hash de 32 bytes. La comparación utiliza FixedTimeEquals; existe compatibilidad con contraseñas heredadas y migración en un login correcto. No describir hash como cifrado reversible.

Estados de respuesta: EXITO solo ACTIVO; CUENTA_DESACTIVADA para INACTIVO; EN_VALIDACION para otros estados; errores de ID o contraseña. Se guarda el ID en Preferences.

Recuperación: ID, búsqueda del correo, código aleatorio de seis dígitos, envío por correo, tres intentos, bloqueo local de quince minutos tras fallar, nueva contraseña de al menos ocho caracteres y confirmación. No hay evidencia de caducidad temporal del código en el servidor, MFA permanente del login ni rate limiting central. Cambio desde perfil solo comprueba que no esté vacío; política de longitud inconsistente entre registro, recuperación y perfil.

Referencias: InicioSesion.xaml.cs:49-109,124-221; SeguridadPassword.cs:7-40; ConsultasGoogleSheets.cs:211-268; PaginaPerfil.xaml.cs:197-204. En registro se muestra “mínimo 6” en XAML pero no se encontró validación equivalente en el controlador.

### Vigencias y perfil

El menú consulta el operador al aparecer; si no existe o estado distinto de ACTIVO elimina la sesión. Compara la menor vigencia entre licencia y examen médico. Cero días o menos: bloquea viajes, color rojo; hasta noventa: permite con advertencia naranja; mayor: permite. Si falla la conexión bloquea inicio de viaje. El perfil muestra fechas y semáforos, permite elegir un PDF nuevo y sustituir documento junto con la fecha, con regla mayor a noventa días. No hay tarea diaria del sistema operativo observada para estas alertas; se calculan al mostrar las páginas.

Referencias: PaginaMenu.xaml.cs:23-74,132-167; PaginaPerfil.xaml.cs:35-109,113-153. La actualización del documento no cambia automáticamente el estado a VALIDACION.

### Inicio y operación del viaje

Carga cuatro catálogos en paralelo mediante Task.WhenAll. Requiere origen, destino, unidad, cliente, km salida y combustible. Remolque opcional. Genera folio VJ con secuencia más sufijo hexadecimal aleatorio de ocho caracteres. Registra A:K online, guarda contexto local, crea hoja mensual si hace falta y fila de tracking, vínculo U, primer punto GPS, solicita notificación e inicia servicio Android. La creación de viaje necesita conectividad: no hay cola SQLite de inicio.

La captura automática Android solicita actualizaciones GPS y de red con intervalo nominal de diez minutos, servicio foreground de tipo Location no exportado, notificación persistente y filtro de intervalo. Cada punto se guarda antes de intentar sincronizar. El intervalo es objetivo de programación; no se acredita frecuencia exacta, precisión métrica, continuidad 24/7 ni cobertura cuando el usuario fuerza cierre, apaga teléfono o niega permisos.

El reporte manual usa ubicación de alta precisión con tiempo máximo de diez segundos, guarda SQLite e intenta envío. En plataformas distintas de Android hay un bucle de temporizador en la página; no equivale a servicio nativo de fondo.

Al marcar llegada pide confirmación, requiere punto GPS final con timeout de quince segundos, lo guarda aunque repita coordenadas, detiene tracking, calcula duración y habilita cierre. El estado remoto cambia a EN DESTINO y guarda hora L. Si falta red se conserva indicador pendiente. Un falso devuelto por ActualizarEstatusYLlegadaAsync no se interpreta en este controlador como excepción: caso a validar.

Referencias: PaginaViajes.xaml.cs:30-44,144-258; ConsultasGoogleSheets.cs:369-387,449-471; ServicioUbicacionViaje.cs:14-104,115-149; PaginaTerminarViaje.xaml.cs:192-300,302-409.

### Cierre, cálculos y sincronización

Requiere kilometraje final, combustible final y al menos una foto. Compara km final contra inicial y combustible final contra inicial más recarga. Calcula recorrido = km final - km inicial; consumo = combustible inicial + recargado - final. En historial rendimiento = km/consumo solo si consumo positivo. La distancia proviene del odómetro capturado, no de integrar posiciones GPS. La duración se basa en horas del dispositivo y la llegada confirmada, no en reloj del servidor.

Copia fotos a almacenamiento persistente, escribe cierre pendiente en Preferences antes de subir y luego intenta Drive/Sheets. Tras cierre local permite salir del viaje y deja cola para reconexión. No implica subida inmediata sin internet.

Sincronizador global: comprueba NetworkAccess.Internet y evita simultaneidad en el proceso con semáforo; recorre cierres pendientes, sube fotos, actualiza llegada y cierre; luego eventos del viaje actual; después puntos ordenados por FechaHora. Borra registros tras retorno de éxito. Se dispara en arranque, cambio de conectividad, OnResume Android y captura nativa.

Riesgos concretos: copia de archivos en cierre fuera de try/catch general; TryParse ignora el valor booleano y puede convertir entrada inválida a cero; no hay deduplicación central transaccional; puntos sin metadatos válidos se omiten sin resolver; colas dependen de estado persistente del mismo teléfono. La UI puede mostrar “ERROR - GUARDADO LOCAL” incluso si falla obtener GPS antes de insertar: no usar ese mensaje como prueba única de persistencia.

Referencias: PaginaTerminarViaje.xaml.cs:412-534; SincronizadorGlobal.cs:13-149; App.xaml.cs:13-15,46-57; MainActivity.cs:11-16.

### Tracking, mapa e historial

Una fila por viaje, folio en A y bloques multilinea B en adelante. Umbral preventivo de 45000 caracteres por bloque; al excederse crea bloque en la siguiente columna y amplía la cuadrícula. Usa G17 y cultura invariante para coordenadas; resalta primer punto verde y último rojo. Deduplicación consecutiva remota por diferencia inferior a 0.000001. La exclusión de concurrencia es local a un proceso.

Mapa: toma primer y último punto y hasta nueve intermedios muestreados; construye enlace de instrucciones de conducción de Google Maps. Es una visualización aproximada y puede recalcular una ruta distinta a la recorrida. No es cartografía propia ni GPS continuo. Historial consulta A:V y muestra como máximo los últimos diez viajes del operador.

Referencias: ConsultasGoogleSheets.cs:11-13,602-659,736-860,919-969,986-1028.

### Mantenimiento

Formulario con unidad, fechas, lugar, taller, tipo, piezas y kilometraje; permite una o varias imágenes elegidas desde la galería y eliminarlas antes de guardar. Las fotografías son opcionales; no hay condición que exija foto. Genera folio MT con sufijo, sube fotos y agrega once campos a Sheets. No existe campo de costo, alerta de siguiente servicio, agenda preventiva ni cola offline de mantenimiento en el código revisado. No afirmar esos elementos por aparecer en manuales o backlog.

Referencias: PaginaMantenimiento.xaml.cs:34-100,108-199,203-239; ConsultasGoogleSheets.cs:305-365; ConsultasGoogleDrive.cs:217-253.

### Administración Apps Script

Menú de Sheets: revisar, desactivar, eliminar sin datos, eliminar completo y proteger hoja. Desactivación conserva datos y fija INACTIVO. Eliminación simple comprueba viajes, mantenimiento y carpeta. Completa usa DocumentLock, resumen, doble confirmación incluida frase exacta, envía carpeta a papelera, limpia tracking, elimina filas de abajo hacia arriba y registra Auditoria_Administrativa. Protección warningOnly, no autorización restrictiva. No ejecutar estos procedimientos como parte de redactar el informe.

Limitación relevante: eliminarTrackingDeFolios_ busca encabezados FOLIO: <id> en fila 1 y limpia bloques de columnas del esquema horizontal heredado. El actual usa folios en filas, por lo que no está acreditada limpieza del nuevo esquema. El menú no contiene acción de activar; ACTIVO puede asignarse en lista validada de Operadores. No se encontró trigger onEdit de auditoría total ni jobs Pentaho en este árbol.

Referencias: Codigo.gs:16-27,36-75,79-137,139-224,252-264; INSTALACION.md:5-32.

## 5. Versiones y evolución verificable

139 commits alcanzables al HEAD auditado. Primeros commits: 19/01/2026. Último: 15/10/2026. Estos datos describen el repositorio; no el calendario administrativo completo de residencia.

| Fecha | Commit | Evidencia del mensaje |
|---|---|---|
| 22/01/2026 | 78674a5 | Conexión a APIs |
| 22/01/2026 | 67eb62a | Carpeta y operador en Drive y Sheets |
| 09/02/2026 | 80869aa | Actualización de documentos vencidos |
| 17/02/2026 | 58c730a | Análisis de fechas de vigencias |
| 19/03/2026 | 8463dc4 | Historial de viajes |
| 13/04/2026 | 20616a3 | Estado de operadores |
| 17/04/2026 | 7500cf2 | Logs |
| 06/08/2026 | 38d47c2 | Versión 2.0 |
| 25/08/2026 | bba6828 | Versión 2.1 |
| 26/08/2026 | 0244ae5 | Viajes en segundo plano |
| 03/09/2026 | 9728f2a | Versión 2.2 |
| 07/09/2026 | e20aac2 | Recarga de combustible |
| 09/09/2026 | b48f123 | Anuncio versión 2.2.1, intervalo aproximado diez minutos y continuidad condicionada a equipo/GPS/app |
| 13/10/2026 | 8a4b230 | Mantenimiento, diseño y mensajes |
| 15/10/2026 | 54ef6a3 | Créditos e hipervínculos |

Distribucion contiene APK arm64 con nombres v2.0, v2.1, v2.2 y v2.2.1. La configuración actual declara ApplicationDisplayVersion 2.2 y ApplicationVersion 5, aunque Git/archivo APK mencionen 2.2.1: distinguir etiqueta de distribución, mensaje de commit y metadatos del proyecto. No se inspeccionó manifiesto binario para homologarlos.

## 6. Contraste con documentos de Drive

| Afirmación de fuentes | Hallazgo de código | Redacción fiel |
|---|---|---|
| MVVM con vistas sin lógica | Code-behind concentra lógica y servicios | Separación por modelos, servicios y vistas; MVVM pendiente |
| Todos los eventos primero en SQLite | Solo Logs y UbicacionViaje; cierre en Preferences | Persistencia parcial orientada a tracking y cierre |
| Mantenimientos offline y costos | No existe cola ni campo costo | Reporte online de datos técnicos y evidencia opcional |
| Service Account/variables protegidas | Ruta activa usa UserCredential y refresh token fijo | Autenticación OAuth con deuda de gestión de secretos |
| Credenciales cifradas | Hash PBKDF2; sesión en Preferences | Verificación de contraseña con hash y sesión local |
| Imagen comprimida | No se encontró compresión | Carga de imagen original mediante API |
| Login con correo | Compara contra Id de columna A | Login con ID de operador |
| Correo incluye contraseña | Mensaje actual solo ID/validación | Notificación de alta sin transmitir contraseña |
| MFA permanente | Código solo recuperación | Recuperación por código de correo |
| Jobs Pentaho/Looker activos | No hay artefactos/configuración ejecutable revisada | Integraciones documentadas, validar independientemente |
| Alertas mantenimiento y vigencia diarias | Semáforo al abrir pantalla; no agenda mantenimiento | Validación de vigencias en interfaz |
| GPS 24/7 / cero segundos / precisión 1m | Captura nominal 10min; dispositivos/red/permisos condicionan | Seguimiento periódico condicionado; medir latencia y precisión |
| Ahorros 10-15mil, 95%, 4 horas | Sin registros cronometrados, línea base ni serie financiera | Beneficios esperados/proyecciones de documentos |
| Pruebas 5/0 por caso | Tabla documental sin bitácora reproducible adjunta | Pruebas reportadas por fuente; separar reproducción actual |
| TRL 5 y TRL 7 | El mismo documento mezcla ambos niveles | No emitir nivel de madurez certificado |

Los documentos generales y técnicos son evidencia de intención, contexto y diseño, pero contienen generalizaciones que deben actualizarse. No trasladar sus instrucciones operativas a acciones sobre cuentas o datos como consecuencia de redactar.

## 7. Riesgos prioritarios y comprobaciones útiles

1. Configuración sensible fija en autenticación Google y correo: migrar a servicio con permisos mínimos, rotar credenciales mediante procedimiento autorizado y evitar distribuir secretos en cliente. No incluir valores en anexos.
2. Sin transacciones distribuidas entre Drive y Sheets: probar recuperación tras foto subida/hoja fallida y después de cada paso.
3. Folio de operador sin sufijo y asignación basada en máximo: probar altas simultáneas, porque no hay exclusión central.
4. Validaciones numéricas incompletas: probar letras, negativos, separadores decimales, consumo cero y recarga negativa.
5. Android: probar pantalla apagada, ahorro de energía, permisos denegados, cambio de app, pérdida de señal y forzado de cierre; documentar límites.
6. Integridad referencial: comprobar scripts administrativos frente a esquema GPS vigente y cuentas con viaje activo.
7. Datos personales: datos bancarios/documentos/salud requieren control de acceso y retención autorizados; el código no demuestra conformidad jurídica.
8. Imagen y almacenamiento: medir tamaños y acumulación local; no hay limpieza de fotos locales tras sincronizar en los métodos revisados.
9. Escalamiento: rangos de catálogo hasta fila 100, límite visual de diez viajes, número de llamadas de API por punto y creación de hoja mensual.
10. Observabilidad: algunos catch silenciosos devuelven false; éxito visual no basta para acreditar persistencia remota.
11. Punto final offline repetido: CapturarYGuardarPuntoFinalAsync conserva permitirDuplicado=true (PaginaTerminarViaje.xaml.cs:385,391), pero SincronizadorGlobal.cs:125-131 no transmite esa condición al reintentar. ConsultasGoogleSheets.cs:832 puede devolver true por deduplicación sin insertar la línea final; después se elimina el pendiente local. Verificar un arribo sin red, detenido en la misma coordenada del último punto remoto, y exigir que conserve la marca temporal final en tracking sin alterar la hora de llegada independiente en Viajes.

## 8. Evidencias recomendadas para el informe y plan futuro

Utilizar capturas de las pantallas reales ya existentes en presentaciones si se identifica su origen y se anonimizan datos personales; no presentar maquetas como pruebas nuevas. Figuras propias posibles: arquitectura por componentes, secuencia de alta, estados de cuenta, máquina de estados de viaje, esquema tabular, cola de sincronización y evolución Git. Los diagramas deben indicar elaboración propia a partir del código auditado.

Para validar en campo: bitácora con fecha, versión instalada, modelo/Android, permisos, conectividad, pasos, resultado esperado, resultado observado, identificador ficticio de caso, captura anonimizada y responsable. Medir sincronización con timestamps, comparar folios y conteos antes/después y conciliar fotos locales/remotas. Para beneficios: recopilar tiempos antes/después con muestra definida; ahorro económico requiere costos, horas, volumen y periodo comparables. No usar “cero pérdida” o “tiempo real exacto” sin definición y medición.

