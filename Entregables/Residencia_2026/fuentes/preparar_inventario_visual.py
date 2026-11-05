from pathlib import Path
from PIL import Image
import hashlib
import json
import shutil

ROOT=Path(__file__).resolve().parents[3]
OUT=Path(__file__).resolve().parent
IMG=OUT/'imagenes'
IMG.mkdir(exist_ok=True)
selection=[
    ('Resources/AppIcon/appicon.png','tlb_identidad_appicon.png','Identidad gráfica de TLB Transportes utilizada por la aplicación. Logo sobre fondo blanco; no es una captura del sistema ni evidencia del desempeño.','identidad'),
    ('Resources/Images/principal.png','tlb_ilustracion_principal.png','Ilustración promocional de tractocamión, paisaje y marca TLB. Aparece también en diapositivas 1 y 2 de Presentacion_LogiApp_Evolucion_v1_a_v2.pptx. Su procedencia creativa no está acreditada; no debe presentarse como fotografía de instalaciones, unidad real o trabajo de campo.','ilustracion'),
]
inventory=[]
for src,name,description,kind in selection:
    source=ROOT/src
    target=IMG/name
    shutil.copyfile(source,target)
    inventory.append({'archivo':name,'fuente':src,'clasificacion':kind,'dimensiones_px':list(Image.open(source).size),'sha256':hashlib.sha256(source.read_bytes()).hexdigest(),'descripcion':description,'uso':'Solo identidad o recurso visual existente; no evidencia operativa','transformacion':'Ninguna; copia binaria del archivo original'})
excluded=[
    {'fuente':'Presentacion_LogiApp_Evolucion_v1_a_v2.pptx','slide':5,'media':'ppt/media/image3.png','dimensiones_px':[500,426],'descripcion':'Icono ilustrado de acceso de usuario','motivo':'Decorativo y redundante para el informe técnico; no demuestra funcionamiento'},
    {'fuente':'Presentacion_LogiApp_Evolucion_v1_a_v2.pptx','slide':6,'media':'ppt/media/image4.png','dimensiones_px':[716,1600],'descripcion':'Captura real de pantalla de Registro de viaje con diálogo de error de límite de columnas de Sheets','motivo':'Contiene información de cliente y unidad sin anonimizar. No se incorpora al informe ni a la selección reutilizable'},
    {'fuente':'Presentacion_LogiApp_Evolucion_v1_a_v2.pptx','slide':7,'media':'ppt/media/image5.png','dimensiones_px':[772,59],'descripcion':'Captura real de celdas de historial GPS con fechas, horas, folio y coordenadas','motivo':'Contiene localización y datos operativos sin anonimizar. No se incorpora al informe ni a la selección reutilizable'},
    {'fuente':'Presentacion_LogiApp_Evolucion_v2.pptx','slide':4,'media':'ppt/media/image.png','dimensiones_px':[716,1600],'descripcion':'Duplicado de captura de error de registro de viaje','motivo':'Mismo contenido operativo sin anonimizar que en diapositiva 6 de Evolución v1 a v2'},
    {'fuente':'Presentacion_LogiApp_Evolucion_v2.pptx','slide':5,'media':'ppt/media/image2.png','dimensiones_px':[772,59],'descripcion':'Duplicado de captura de historial GPS','motivo':'Mismo contenido geográfico sin anonimizar que en diapositiva 7 de Evolución v1 a v2'}
]
data={'criterio':'Se inspeccionaron los PPTX originales y recursos PNG existentes. No se generaron, alteraron ni reconstruyeron capturas. Las presentaciones de Entregables no contienen imágenes raster incorporadas. Las diapositivas ya renderizadas pueden corresponder a revisiones anteriores y no se toman como fuente de versión actual.','seleccion':inventory,'capturas_excluidas_y_recursos_no_seleccionados':excluded,'datos_textuales':[{'dato':'Proyecto de Educación Dual ITESA 2026 - TLB Transportes','fuente':'App.xaml','linea':32},{'dato':'Trabajo presentado como proyecto de dos integrantes','fuente':'Presentacion_LogiApp.pptx','slide':1},{'dato':'Los cambios de octubre se presentan como commits del 13 al 15 de octubre de 2026','fuente':'Entregables/LogiApp_Cambios_Octubre_2026.pptx','slides':[1,5]},{'dato':'El estado técnico señala que no había resultados comparativos registrados todavía','fuente':'Entregables/LogiApp_Estado_Tecnico_Octubre_2026.pptx','slide':6}],'datos_no_encontrados_en_presentaciones':['Nombre completo de escuela','Nombre de asesores','Periodo formal de residencia completo','Título académico definitivo de residencia']}
(OUT/'inventario_imagenes.json').write_text(json.dumps(data,ensure_ascii=False,indent=2),encoding='utf-8')
lines=['# Inventario visual revisado','',data['criterio'],'','## Selección para identidad','']
for item in inventory:
    lines+=['- '+item['archivo']+': '+item['descripcion']+' Fuente: '+item['fuente']+'. Dimensiones: '+str(item['dimensiones_px'])+'.']
lines+=['','## Capturas reales excluidas','']
for item in excluded:
    lines+=['- '+item['fuente']+', diapositiva '+str(item['slide'])+', '+item['media']+': '+item['descripcion']+'. '+item['motivo']+'.']
lines+=['','No hay capturas reales reutilizables sin tratamiento de información en los archivos inspeccionados. El logo y la ilustración no acreditan pruebas operativas.','']
(OUT/'inventario_imagenes.md').write_text('\n'.join(lines),encoding='utf-8')
print('Inventario terminado: '+str(len(inventory))+' recursos conservados para identidad; capturas operativas excluidas.')
