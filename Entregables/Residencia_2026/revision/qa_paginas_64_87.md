# QA visual de páginas físicas 64 a 87

## Reinspección final del PDF de 86 páginas

Se inspeccionaron las **23 páginas finales, físicas 64 a 86**, exclusivamente desde `paginas_finales/page-64.png` a `paginas_finales/page-86.png`. Cada imagen nueva se abrió con `view_image` y `detail="original"`, a tamaño original, sin utilizar los renders previos para esta comprobación. El rango final revisado corresponde a los folios impresos 57 a 79.

Los cinco defectos de composición de la primera revisión quedaron resueltos:

| Defecto anterior | Comprobación en render final | Estado |
|---|---|---|
| V64-01, cierre de capítulo en página casi vacía | El capítulo VI termina dentro de la página física 69, folio 62. La página física 70 inicia las referencias. | Resuelto |
| V64-02, grandes huecos por justificación de referencias | Las referencias de las páginas físicas 70 a 72 están alineadas a la izquierda y conservan espaciado legible. | Resuelto |
| V64-02, referencia partida entre páginas | Las referencias se mantienen completas; no queda una línea de cierre o URL aislada procedente de la página anterior. | Resuelto |
| V64-03, título y encabezado del Cuadro 17 sin datos | La sección D.2, el título, el encabezado y todas las filas del Cuadro 17 empiezan juntos en la página física 85, folio 78. | Resuelto |
| V64-04, palabra partida en Cuadro 12 | La etiqueta «Observación» aparece completa y legible en la página física 78, folio 71. | Resuelto |

### Registro de las páginas finales vistas

| Página física final | Folio | Contenido y comprobación |
|---|---|---|
| 64 | 57 | Conclusiones y recomendaciones: texto legible, dentro de márgenes. |
| 65 | 58 | Recomendaciones y experiencia: títulos y párrafos legibles. |
| 66 | 59 | Cierre de experiencia profesional: sin recortes. |
| 67 | 60 | Competencias y Cuadro 8: columnas y filas legibles. |
| 68 | 61 | Continuación de Cuadro 8: encabezado repetido y texto sin solapes. |
| 69 | 62 | Cierre de competencias completo en esta página; no crea página residual. |
| 70 | 63 | Inicio de referencias con alineación izquierda y entradas completas. |
| 71 | 64 | Continuación de referencias legibles, sin espacios excesivos. |
| 72 | 65 | Dos referencias finales completas; sin fragmentos de entradas previas. |
| 73 | 66 | Anexos y Cuadro 9, apartados 1 a 8, legibles. |
| 74 | 67 | Cuadro 9, apartados 9 a 19, con encabezado repetido y sin recortes. |
| 75 | 68 | A.1 y anexo B: títulos y texto dentro de márgenes. |
| 76 | 69 | Cuadros 10 y 11: encabezados y primeras filas correctamente acompañados. |
| 77 | 70 | Cuadro 11, pasos 3 a 12: cuatro columnas legibles y filas completas. |
| 78 | 71 | B.3 y Cuadro 12: etiqueta «Observación» completa. |
| 79 | 72 | Continuación del Cuadro 12 y anexo C: composición legible. |
| 80 | 73 | Cuadro 13 y capacitación: sin recortes ni desbordes. |
| 81 | 74 | Cuadro 14 y C.3: títulos y filas legibles. |
| 82 | 75 | Cuadro 15 y mantenimiento: filas completas y texto dentro de márgenes. |
| 83 | 76 | Anexo D y Cuadro 16: cuatro columnas legibles. |
| 84 | 77 | Continuación del Cuadro 16: sin encabezado huérfano del cuadro siguiente. |
| 85 | 78 | D.2 y Cuadro 17 completo: título, encabezado y datos juntos; D.3 legible. |
| 86 | 79 | Cuadro 18 y cierre del informe: legibles, sin recortes ni solapes. |

**Resultado final del rango revisado:** no se detectaron nuevos defectos accionables de legibilidad, márgenes, desbordes o tablas. La comprobación de pies de figuras no corresponde a este rango, que no contiene figuras.

## Registro histórico de la primera revisión

Revisión de las 24 imágenes originales `paginas/page-64.png` a `paginas/page-87.png`, renderizadas a 110 dpi. Cada página se abrió individualmente mediante `view_image` con `detail="original"`; no se usó un contacto reducido como sustituto de la inspección. El rango de folios impresos observado es 57 a 80. Esta revisión corresponde al render de 87 páginas recibido y no acredita cambios posteriores.

## Hallazgos accionables

| ID | Página física / folio | Hallazgo | Corrección recomendada |
|---|---|---|---|
| V64-01 | 70 / 63 | La página contiene solo las dos líneas finales del capítulo VI y deja casi toda la hoja vacía. El cierre continúa desde la página 69. | Reequilibrar o condensar el último párrafo de competencias para cerrar el capítulo en la página anterior. |
| V64-02 | 71 a 73 / 64 a 66 | Las referencias están justificadas y producen espacios excesivos entre palabras, especialmente en las entradas TLB 2026b, 2026d y 2026f de la página 72. Una referencia comienza al final de la página 72 y deja «proyecto].» y su URL al principio de la 73. | Alinear las referencias a la izquierda y mantener cada entrada unida para evitar el corte de una referencia entre páginas. |
| V64-03 | 85 a 86 / 78 a 79 | El Cuadro 17 empieza al pie de la página 85 con título y encabezado, sin ninguna fila de datos. El contenido comienza en la página 86. | Mantener título, encabezado y primera fila juntos; mover el inicio completo del cuadro a la siguiente página. |
| V64-04 | 79 / 72 | En el Cuadro 12, la primera columna divide la etiqueta «Comportamiento observado» como «Comportamient / o observado». | Usar una etiqueta breve como «Observación» o ampliar ligeramente la primera columna. |

## Registro de páginas vistas

| Página física | Folio impreso | Contenido observado | Resultado visual |
|---|---|---|---|
| 64 | 57 | Conclusiones y comienzo de recomendaciones | Legible; sin recorte, desborde o solape. |
| 65 | 58 | Recomendaciones y comienzo de experiencia profesional | Legible; títulos y texto dentro de márgenes. |
| 66 | 59 | Cierre de experiencia profesional | Legible; cierre de capítulo sin recortes. |
| 67 | 60 | Inicio de competencias y Cuadro 8 | Cuadro legible; la continuación conserva la estructura. |
| 68 | 61 | Continuación de Cuadro 8 y competencias | Encabezado repetido legible; texto dentro de márgenes. |
| 69 | 62 | Competencias y párrafo final del capítulo | Legible; el cierre continúa innecesariamente en la página 70. |
| 70 | 63 | Dos líneas finales de competencias | Hallazgo V64-01. |
| 71 | 64 | Inicio de fuentes de información | Legible; hallazgo de espaciado V64-02. |
| 72 | 65 | Continuación de referencias | Hallazgo V64-02; sin texto recortado. |
| 73 | 66 | Cierre de referencias | Hallazgo V64-02; parte de una referencia quedó separada. |
| 74 | 67 | VIII Anexos y Cuadro 9, apartados 1 a 8 | Tabla de cuatro columnas legible y dentro de márgenes. |
| 75 | 68 | Cuadro 9, apartados 9 a 19 | Encabezados repetidos; sin cortes de texto o filas. |
| 76 | 69 | A.1 y comienzo del protocolo propuesto B | Jerarquía y párrafos legibles; sin solapes. |
| 77 | 70 | Cuadro 10 e inicio del Cuadro 11 | Tablas legibles; el Cuadro 11 empieza con dos filas de datos. |
| 78 | 71 | Pasos 3 a 12 del Cuadro 11 | Cuatro columnas legibles, filas completas y encabezado repetido. |
| 79 | 72 | B.3 y comienzo del Cuadro 12 | Hallazgo menor V64-04; sin recortes ni desbordes. |
| 80 | 73 | Cierre de Cuadro 12 y comienzo del anexo C | Tabla y texto legibles; sin solapes. |
| 81 | 74 | Cuadro 13 y explicación de capacitación | Tabla de cuatro columnas legible y dentro de márgenes. |
| 82 | 75 | C.2, Cuadro 14 y C.3 | Tabla y títulos legibles; sin recortes. |
| 83 | 76 | Cuadro 15 y mantenimiento | Tabla legible; sus filas y párrafos permanecen dentro de márgenes. |
| 84 | 77 | Anexo D y comienzo del Cuadro 16 | Datos y cuatro columnas legibles; sin recortes. |
| 85 | 78 | Continuación del Cuadro 16 y comienzo del Cuadro 17 | Hallazgo V64-03; identificadores largos visibles, sin pérdida de contenido. |
| 86 | 79 | Datos del Cuadro 17 y D.3 | Contenido legible; el encabezado huérfano precedente requiere corrección. |
| 87 | 80 | Cuadro 18 y cierre del reporte | Tabla y cierre legibles; sin recortes, desbordes o solapes. |

La revisión no modificó el contenido ni los archivos de entrega. Después de aplicar correcciones de composición corresponde inspeccionar los renders afectados y comprobar la nueva paginación.
