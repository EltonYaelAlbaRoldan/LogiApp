# Hallazgos de las referencias locales

Inspección solo lectura realizada el 23 de octubre de 2026. Los archivos originales no fueron modificados. Los documentos se tratan como evidencia y referencia de formato; el encargo del usuario es elaborar el Anexo VIII y el informe técnico con un mínimo de 50 páginas. El mínimo de 50 páginas procede del usuario, no de las dos referencias.

## Fuentes

- PDF: `C:/Users/elton/Downloads/Informe Tecnico de Residencia Profesional__.pdf`, 12 páginas. Es una guía metodológica; no es un informe existente de LogiApp ni una portada con datos del alumno.
- Libro: `C:/Users/elton/Documents/Copia_2026/Modelo Dual/Anexos_Dual/Elton_TLB_2 Formatos Modelo Dual 2026_27_04_26.xlsx`, 12 hojas: Anexo I, II, III, IV, V AB, V AAIA, V DE, V MTM, V AD, VI, VII y VIII.
- Texto completo del PDF: `informe_tecnico_referencia.txt`; texto por página: `pdf_paginas.json`.
- Inventario de celdas del libro: `libro_celdas.json` y `libro_celdas.txt`; estructura, rangos combinados e impresión: `libro_estructura.json`.
- Inventario específico del Anexo VIII: `anexo_viii_celdas.json`.
- No hay imágenes ni logos embebidos en el libro (ZIP sin `xl/media/`; cero imágenes en todas las hojas).

## Interpretación del Anexo VIII

La hoja Anexo VIII se titula «ESTRUCTURA DEL REPORTE INTEGRAL DEL PROYECTO DE EDUCACIÓN DUAL». Contiene un esquema de 19 apartados, no casillas de datos personales, evaluación ni firmas que haya que llenar. Lo que corresponde elaborar es el reporte integral conforme a esa estructura. La hoja de Excel puede conservarse sin cambios.

El reporte integral y el informe técnico son muy compatibles. La diferencia explícita es que el Anexo VIII exige «Descripción del PED y periodos en que se realizó», apartado 7 dentro de Generalidades. Puede elaborarse una memoria técnica integral que cumpla ambos, o dos documentos con portadas y organización adaptadas, sin inventar una segunda intervención distinta.

## Metadatos documentados

| Dato | Valor respaldado | Fuente exacta |
| --- | --- | --- |
| Alumno | Elton Yael Alba Roldan | Anexo II D3; III E6; IV E7; VI F9 |
| Matrícula | 22030013 | Anexo II D4; III F7; IV E8; VI F10 |
| Programa | Ingeniería en Sistemas Computacionales | Anexo II D6; III E10 |
| Plan | ISIC-2010-224 | Anexo II D5; III E8 |
| Unidad económica | TLB Transportes | Anexo I C5; II D8; III E12; VI F7 |
| Área | Sistemas Computacionales | Anexo I C6; VI F6 |
| Asesor interno | DCC. René Cruz Guerrero | Anexo II D7; III E11 usa «D.C.C.»; VI F11 |
| Asesor externo | Ing. Raúl Armando Balmaceda Pérez | Anexo II D9; III E13; VI F8 |
| Nombre del proyecto | Sistema de Control para la Optimización de Ciclos de Viaje y Tiempos de Traslado | Anexo I C4; II D10; III E14; IV E9 |
| Periodo PED completo | Enero - Diciembre 2026 | Anexo I C8; II D11; III E15 |
| Periodo evaluado | Enero - Junio 2026 | Anexo IV E10; hojas V C9; VI F5 |
| Fecha del plan | 5 de febrero del 2026 | Anexo III E16 |
| Horas declaradas | 1000 horas | Anexo II D12 |
| Horario de áreas previas | 8:00 - 16:00 | Anexo III F35:F38 |
| Institución | Solo se encuentra la sigla ITESA en la nota de Anexo VIII B16 | No se documenta nombre completo ni ciudad de portada |

El nombre oficial del proyecto tiene 14 palabras y cumple el máximo de 20 palabras de la guía. Puede pasarse a tipo oración sin cambiar las palabras: «Sistema de control para la optimización de ciclos de viaje y tiempos de traslado».

## Información funcional ya redactada en el libro

El objetivo de Anexo I B12 y Anexo III B21 es desarrollar una solución tecnológica integral con una aplicación móvil y herramientas del ecosistema de Google para automatizar el ciclo de viaje, recopilar datos operativos y de mantenimiento, monitorear GPS y gestionar evidencias en TLB Transportes.

La problemática de Anexo I B16 y III B25 es la dependencia de registros manuales y mensajería convencional, el retraso del flujo de información, la transcripción manual de datos y los errores de captura. Estos son antecedentes documentados en la planeación, no mediciones cuantitativas del resultado.

Anexo I B18/B20 y III B27/B29 plantean como resultados esperados: autonomía del operador, correos de hitos del viaje, firmas y fotografías, chequeo preventivo de la unidad, control visual de vigencias de licencia y apto médico, manuales técnicos, diagramas y guías de usuario. La redacción es prospectiva. Para afirmar que se logró cada punto hace falta contrastarlo con código, pruebas o evidencia.

Anexo III B35:F38 describe las áreas previas: Operaciones y logística (bitácoras/kilometraje), Administración y recursos humanos (vigencias), Liquidaciones y facturación (combustible/evidencias de carga), Mantenimiento y taller mecánico (reportes/costos). Es suficiente para explicar el contexto de trabajo; no acredita un organigrama formal.

## Asignaturas y evidencias planeadas

| Asignatura | Clave en Anexo II | Docente en Anexo II | Horas |
| --- | --- | --- | --- |
| Digitalización Empresarial | ETD2502 | DCC. German Cuaya Simbro | 80 |
| Analítica de Datos para la Transformación Digital | ETD2503 | DR. Lauro Vargas Ruiz | 80 |
| Aplicaciones Avanzadas de Inteligencia Artificial | ETD2504 | DCC. René Cruz Guerrero | 80 |
| Competencias Blandas en la Gestión Tecnológica | ETD2505 | MAC. Mónica Jiménez Gutiérrez | 80 |
| Tecnologías Móviles para la Transformación Digital | ETB2501 | MTI Antonio Martínez González | 80 |

Las claves se transcriben como aparecen. No corregir ETB2501 ni nombres/grados sin confirmación institucional. Las hojas V contienen evidencias propuestas y ponderaciones de 20% por tres evidencias (60%); no registran puntuaciones ni niveles seleccionados. Anexo VI tiene la lista de competencias blandas sin puntajes. Anexo VII está vacío de datos del alumno/proyecto y de calificaciones. No afirmar acreditación, evaluación favorable, validación o firmas obtenidas.

Anexo I B25:E33 programa: backend Google (enero-marzo), digitalización documental (enero-abril), motor de vigencias (marzo-mayo), ETL Pentaho (abril-junio), clustering (abril-junio), BI (mayo-junio), monitoreo geoespacial (julio-septiembre), documentación (septiembre-octubre) y capacitación (todo el periodo). Pentaho, clustering y dashboards se deben presentar como planeados si no se encuentran sus evidencias. Un motor de reglas de fecha no demuestra aprendizaje automático ni deep learning.

## Estructura exacta de Anexo VIII

| Capítulo | Celdas | Apartados exigidos |
| --- | --- | --- |
| Preliminares | B5, D5 | 1 Portada; 2 Agradecimientos; 3 Resumen; 4 Índice |
| Generalidades del proyecto | B6, D6 | 5 Introducción; 6 Descripción de unidad económica y puesto/área; 7 Descripción del PED y periodos; 8 Problemas priorizados; 9 Objetivos general y específicos; 10 Justificación |
| Marco teórico | B7, D7 | 11 Fundamentos teóricos |
| Desarrollo | B8, D8 | 12 Procedimiento y descripción de actividades realizadas |
| Resultados | B9, D9 | 13 Resultados y productos pertinentes; 14 Actividades sociales si es el caso |
| Conclusiones | B10, D10 | 15 Conclusiones, recomendaciones y experiencia personal profesional |
| Competencias desarrolladas | B11, D11 | 16 Competencias desarrolladas/aplicadas |
| Referencias | B12, D12 | 17 Fuentes de información |
| Anexos | B13, D13 | 18 Anexos, incluida carta de autorización de empresa para titulación y otros necesarios; 19 Registros de productos si existen |

La nota de B16 indica que ITESA puede proponer una extensión máxima con sustento académico; no establece un límite numérico. Hay 23 celdas con valor. Merges: B2:D2, C4:D4 y B16:D16. Hoja orientada vertical, sin área de impresión ni tamaño de papel ni ajuste a páginas; salto de fila en 42. Márgenes registrados en pulgadas: izquierdo/derecho 0.23622, superior 0.74803, inferior 0.31496, encabezado/pie 0. No hay logos ni imágenes. Fuente de la tabla Noto Sans (11 para etiquetas, 10 para contenidos). El formato del Excel no sustituye las especificaciones de Word del PDF.

## Estructura del informe técnico en la guía PDF

Página 1 establece: portada; agradecimientos; resumen; índices; I Generalidades (1.1 Introducción, 1.2 Empresa, 1.2.1 Puesto/área, 1.3 Problemas, 1.4 Objetivos, 1.4.1 General, 1.4.2 Específicos, 1.5 Justificación); II Marco teórico; III Desarrollo (3.1 Procedimiento y descripción); IV Resultados (4.1 Productos, 4.2 Actividades sociales si aplica); V Conclusiones (5.1 Proyecto, 5.2 Recomendaciones, 5.3 Experiencia profesional); VI Competencias; VII Fuentes; VIII Anexos y registros de productos.

La guía no exige realizar estudio de mercado, económico, modelos matemáticos ni registros de patentes para todos los proyectos; se incluyen cuando corresponden. Tampoco acredita actividades sociales: se dejan como «no documentadas» o se omiten justificadamente si no aplican.

## Requisitos editoriales del PDF

- P. 2, portada: logos de Secretaría, TecNM e institución; nombre de institución superior centrado en mayúsculas; «Informe Técnico de Residencia Profesional»; título; alumno; carrera; asesor interno; ciudad y mes/año de finalización en margen inferior derecho. El título: máximo 20 palabras, centrado, tipo oración, sin abreviaturas, paréntesis ni guiones.
- P. 3, agradecimientos: máximo una cuartilla. Primero contribuciones académicas, después personales; considerar financiamiento si existió. No inventar familiares, creencias ni patrocinio.
- P. 3, resumen: máximo una cuartilla, descriptivo, conciso, pasado e impersonal; qué, por qué, cómo, hallazgos y significado/ventajas de prototipo.
- Pp. 3-4: índice al terminar y paginar, encabezado ÍNDICE centrado mayúsculas; paginación arábiga desde Introducción, inferior derecha. Índice de figuras/cuadros cuando aplica.
- P. 4: figuras y cuadros consecutivos con números arábigos; rótulo «Figura [n]. [Nombre]» / «Cuadro [n]. [Nombre]», tipo oración, tamaño 10, centrado, sin punto final; interlineado sencillo y separación consistente. Material que ocupe hoja completa va en anexos, referenciado desde texto.
- P. 5, introducción: máximo 2 cuartillas; antecedentes, justificación, objetivos, área, problema, alcance, limitaciones, reseña y metodología; no adelantar resultados/conclusiones.
- P. 5, empresa: datos generales (nombre, giro, dirección, teléfono, web y correo), misión/visión/políticas/valores, organigrama con área señalada, funciones de área.
- Pp. 5-6: problemas en lista priorizada, de mayor a menor; objetivos claros, medibles, observables, alcanzables e iniciados con infinitivo.
- P. 8: marco teórico con citas APA, mínimo 10 autores y por lo menos 30% de fuentes de no más de 5 años de antigüedad. A 2026, buscar al menos 2021-2026; preferir 2022-2026 para evitar ambigüedad de mes. Incluye citas directas e indirectas según p. 12.
- P. 9: desarrollo en orden lógico y conforme a cronograma, actividades, métodos, instrumentos/equipos y condiciones de aplicación.
- Pp. 10-11: resultados acompañados por análisis e interpretación; conclusiones específicas relacionadas con objetivos/problemas; recomendaciones y experiencia profesional. Tabla de competencias con columnas «Competencia desarrollada y/o aplicada» y «¿Dónde se aplicó?».
- P. 11: anexos citados desde cuerpo y ordenados por aparición.
- P. 12: índice creado con herramienta de Word, interlineado sencillo; cuerpo interlineado 1.5; listas sencillo; sin sangría inicial; viñeta de punto negro; márgenes superior/izquierdo 3 cm, derecho/inferior 2.5 cm; Arial 12; títulos mayúsculas negrita 14 centrados; subtítulos negrita 12 tipo oración; cuadros/figuras sencillo; sin texto de encabezado/pie salvo número de página inferior derecho. Bibliografía con herramienta Word recomendada/exigida en p. 12. Espaciado después de párrafo «a doble espacio»: conservar interpretación explícita (por ejemplo 12 pt después), ya que la guía no da puntos exactos.

## Datos por confirmar y límites materiales

1. Nombre completo de institución, logos vigentes y ciudad de portada. Solo ITESA aparece en el libro. No inventar logos.
2. Periodo efectivo de residencia (distinto del PED enero-diciembre 2026) y fecha final de entrega. Al corte 23/10/2026 el periodo PED aún no concluye; redactar lo realizado a la fecha y separar pendiente noviembre-diciembre.
3. Inconsistencia de horas: Anexo II D12 dice 1000, pero F32:F36 tiene 5 × 80 = 400; F37 fórmula SUM con cache 400. La guía del mismo libro considera 500 horas de residencia; 400 + 500 = 900. No corregir el original ni afirmar 1000 horas realizadas. Se necesita desglose validado de al menos 100 horas adicionales si la institución exige 1000.
4. Datos empresariales oficiales, misión/visión, organigrama, ubicación y tamaño real de flota/personal, si deben constar.
5. Autorización firmada para titulación, firmas de asesores y sellos no están en las fuentes locales. Los nombres escritos no son firmas ni aprobación.
6. Evidencias reales de validación, mediciones antes/después, despliegue, capacitación, uso productivo, ETL/clustering/BI. Sin ellas describir implementación observada y pruebas reproducibles, sin atribuir impactos porcentuales ni calificaciones.

