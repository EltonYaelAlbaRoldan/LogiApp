import json, re, math, zipfile, uuid, shutil
from pathlib import Path
from xml.sax.saxutils import escape
from lxml import etree
from docx import Document
from docx.shared import Inches, Cm, Pt, RGBColor
from docx.enum.text import WD_ALIGN_PARAGRAPH, WD_BREAK
from docx.enum.section import WD_SECTION_START
from docx.enum.table import WD_TABLE_ALIGNMENT, WD_CELL_VERTICAL_ALIGNMENT
from docx.oxml import OxmlElement
from docx.oxml.ns import qn
from PIL import Image, ImageDraw, ImageFont, ImageChops

ROOT=Path(__file__).resolve().parents[1]
SRC=ROOT/'fuentes'
QA=ROOT/'revision'
FIG=SRC/'diagramas'
for d in (ROOT,QA,FIG): d.mkdir(parents=True,exist_ok=True)
OUTPUT=ROOT/'Informe_integrado_LogiApp_Anexo_VIII_2026.docx'
font_path=Path('C:/Windows/Fonts')
fonts={s:ImageFont.truetype(str(font_path/f),s) for s,f in [(32,'arial.ttf'),(34,'arialbd.ttf'),(40,'arialbd.ttf'),(27,'arial.ttf')]}

def diagram(name,boxes,edges,size=(1600,920)):
    im=Image.new('RGB',size,'white'); d=ImageDraw.Draw(im)
    for a,b in edges:
        x1,y1,x2,y2=boxes[a][:4]; u1,v1,u2,v2=boxes[b][:4]
        c=((x1+x2)/2,(y1+y2)/2); z=((u1+u2)/2,(v1+v2)/2)
        dx=z[0]-c[0];dy=z[1]-c[1]
        if abs(dx)>abs(dy):
            p=(x2 if dx>0 else x1,c[1]);q=(u1 if dx>0 else u2,z[1])
        else: p=(c[0],y2 if dy>0 else y1);q=(z[0],v1 if dy>0 else v2)
        if name=='modelo_relaciones' and a==3 and b==5:
            p=(c[0],y2);q=(z[0],v2)
            route=[p,(p[0],855),(q[0],855),q]
            d.line(route,fill='#424242',width=5)
            p=route[-2]
        else:d.line([p,q],fill='#424242',width=5)
        ang=math.atan2(q[1]-p[1],q[0]-p[0]);length=21
        pts=[q,(q[0]-length*math.cos(ang-.5),q[1]-length*math.sin(ang-.5)),(q[0]-length*math.cos(ang+.5),q[1]-length*math.sin(ang+.5))]
        d.polygon(pts,fill='#424242')
    for x1,y1,x2,y2,title,lines in boxes:
        d.rounded_rectangle((x1,y1,x2,y2),radius=10,fill='#f5f5f5',outline='#333333',width=3)
        ls=[title]+lines; fs=[fonts[34]]+[fonts[32]]*len(lines)
        for i,line in enumerate(ls):
            n=34 if i==0 else 32
            while d.textlength(line,font=fs[i])>x2-x1-40 and n>24:
                n-=1;fs[i]=ImageFont.truetype(str(font_path/('arialbd.ttf' if i==0 else 'arial.ttf')),n)
        heights=[f.getbbox('Ágj')[3]+8 for f in fs];y=(y1+y2-sum(heights))/2
        for line,f,hh in zip(ls,fs,heights):
            d.text(((x1+x2-d.textlength(line,font=f))/2,y),line,font=f,fill='black');y+=hh
    bbox=ImageChops.difference(im,Image.new('RGB',im.size,'white')).getbbox()
    if bbox:im=im.crop((max(0,bbox[0]-25),max(0,bbox[1]-25),min(im.width,bbox[2]+25),min(im.height,bbox[3]+25)))
    path=FIG/(name+'.png');im.save(path);return str(path)

architecture=diagram('arquitectura',[
    (40,310,390,570,'Operador',['Captura y consulta','Teléfono Android']),
    (500,280,975,600,'LogiApp',['Páginas XAML y C#','Modelos y servicios','Seguimiento Android']),
    (520,30,955,200,'Persistencia local',['SQLite y Preferences','Archivos de evidencia']),
    (1120,80,1560,310,'Google Sheets',['Operadores y catálogos','Viajes y mantenimiento','Tracking y logs']),
    (1120,390,1560,620,'Google Drive',['Expedientes PDF','Fotografías por folio']),
    (1120,700,1560,900,'Administración',['Revisión y activación','Menú Apps Script'])],[(0,1),(1,2),(1,3),(1,4),(5,3),(5,4)])
states=diagram('estados_viaje',[
    (60,190,490,400,'Sin viaje activo',['Capturar datos de salida','Requiere conexión']),
    (600,190,1030,400,'En tránsito',['Seguimiento periódico','Reporte manual GPS']),
    (1140,190,1570,400,'En destino',['Hora y posición de llegada','Se detiene seguimiento']),
    (1140,580,1570,790,'Cierre local pendiente',['Datos finales y fotografías','Conservar antes de enviar']),
    (600,580,1030,790,'Cierre confirmado',['Evidencias en Drive','Campos finales en Sheets']),
    (60,580,490,790,'Consulta de historial',['Últimos viajes visibles','Rendimiento y vínculos'])],[(0,1),(1,2),(2,3),(3,4),(4,5)],(1630,920))
sync=diagram('sincronizacion',[
    (40,70,500,290,'Captura local',['Punto GPS o cierre','Folio y fecha']),
    (590,70,1040,290,'Conservación',['SQLite o Preferences','Fotografía persistente']),
    (1130,70,1570,290,'Intento de envío',['Conectividad disponible','Exclusión local']),
    (1130,540,1570,780,'Servicios remotos',['Subir evidencia','Actualizar registro']),
    (590,540,1040,780,'Confirmación',['Revisar respuesta','Retirar pendiente']),
    (40,540,500,780,'Reintento',['Conservar si falla','Recuperar al reconectar'])],[(0,1),(1,2),(2,3),(3,4),(3,5),(5,2)])
model=diagram('modelo_relaciones',[
    (45,70,470,300,'Operador',['ID y estado','Vigencias y expediente']),
    (590,70,1030,300,'Viaje',['Folio y operador','Unidad y cliente','Inicio llegada y cierre']),
    (1140,70,1570,300,'Catálogos',['Unidades y remolques','Orígenes y destinos','Clientes']),
    (45,570,470,800,'Mantenimiento',['Folio y operador','Datos técnicos y evidencia']),
    (590,570,1030,800,'Posiciones',['Folio fecha y coordenadas','SQLite e historial mensual']),
    (1140,570,1570,800,'Archivos',['PDF por operador','Fotos por folio'])],[(0,1),(2,1),(0,3),(1,4),(1,5),(3,5)])
functional=diagram('areas_funcionales',[
    (580,300,1030,590,'Sistemas Computacionales',['Desarrollo de LogiApp','Integración y documentación']),
    (40,70,490,300,'Operaciones y logística',['Salidas y llegadas','Seguimiento de viajes']),
    (1120,70,1570,300,'Administración',['Expediente y vigencias','Revisión de operadores']),
    (40,600,490,840,'Liquidaciones',['Combustible y kilometraje','Evidencias de entrega']),
    (1120,600,1570,840,'Mantenimiento',['Reportes de servicio','Datos de taller y piezas'])],[(1,0),(2,0),(3,0),(4,0)])

def read(n): return json.loads((SRC/n).read_text(encoding='utf-8-sig'))
ctx=read('capitulos_contexto.json');theory=read('capitulos_teoria.json');dev=read('capitulos_desarrollo.json');ann=read('capitulos_anexos.json');refs=read('referencias.json')
for b in ctx:
    if b['kind']=='p' and b['text'].startswith('De manera transversal se aplican'):
        b['text']='De manera transversal se aplican análisis de problemas, modelado de información, razonamiento lógico y documentación de decisiones. Estas capacidades relacionan las necesidades operativas con datos, reglas y criterios de verificación, y permiten continuar la mejora del producto con evidencia técnica.'
for b in ann:
    if b['kind']=='table':
        for row in b['rows']:
            for i,value in enumerate(row):
                if value=='Comportamiento observado':row[i]='Observación'
# Assign year suffixes in alphabetical title order within each corporate author.
for ref in refs:
    old=ref['tag']
    swap={'Google2026a':'2026c','Google2026b':'2026d','Google2026c':'2026b','Google2026d':'2026a','Microsoft2025a':'2025b','Microsoft2025b':'2025a','TLB2026d':'2026f','TLB2026f':'2026d'}
    if old in swap:ref['year']=swap[old]
refs.sort(key=lambda r:(r['author'].casefold(),r['year'],r['title'].casefold()))
for b in ctx+theory+dev+ann:
    if b['kind']!='p':continue
    s=b['text']
    replacements={
        '(Google, 2026b, 2026c)':'(Google, 2026b, 2026d)',
        'Google (2026a)':'Google (2026c)',
        'Google (2026d)':'Google (2026a)',
        'Microsoft (2025a)':'Microsoft (__2025B__)',
        'Microsoft (2025b)':'Microsoft (2025a)',
        '(TLB Transportes, 2026d)':'(TLB Transportes, __2026F__)',
        '(TLB Transportes, 2026f)':'(TLB Transportes, 2026d)'}
    for old,new in replacements.items():s=s.replace(old,new)
    b['text']=s.replace('__2025B__','2025b').replace('__2026F__','2026f')
for b in dev:
    if b['kind']=='p':b['text']=b['text'].replace('El informe conserva esas cifras únicamente como antecedentes de expectativas del proyecto, sin incorporarlas al balance de logros.','Esas estimaciones se consideran antecedentes de expectativas del proyecto, sin incorporarlas al balance de logros.')
    if b['kind']=='table' and 'objetivos' in b.get('caption','').lower():
        b['rows'].append(['Documentar funcionamiento y continuidad','Manuales y memoria integrada con diagramas','Productos técnicos disponibles para revisión','Recepción y aprobación requieren constancias propias'])
dev.extend([
    {'kind':'heading','level':2,'text':'4.6 Vinculación social y transferencia del conocimiento'},
    {'kind':'p','text':'El componente social del proyecto se relaciona con facilitar el acceso de operadores y administrativos a información comprensible sobre sus actividades. Los materiales de responsabilidad social del proyecto identifican pertinencia, inclusión digital, uso ético de información y sostenibilidad como dimensiones de seguimiento (TLB Transportes, 2026g). En la operación, estas dimensiones se traducen en explicar los estados del sistema, ofrecer acompañamiento al aprender los formularios y reducir barreras para recuperar documentos. Son criterios para orientar la transferencia tecnológica y valorar sus efectos; su inclusión no acredita por sí misma la realización de una campaña, una jornada comunitaria o una capacitación específica.'},
    {'kind':'p','text':'Al corte del informe no se integran constancias de actividades sociales externas ni listas de asistencia verificadas que permitan atribuir fechas, participantes y resultados a esas acciones. El protocolo de capacitación propuesto en el Anexo C organiza tareas observables y evidencia de aprendizaje para las siguientes etapas. La matriz del Anexo A relaciona la memoria con los diecinueve apartados del Anexo VIII; el Anexo B desarrolla la comprobación de continuidad ante fallos, y el Anexo D identifica los productos y documentos disponibles para la entrega. Esta organización permite completar la vinculación y el cierre documental con registros auténticos conforme se generen.'}
])
split=next(i for i,b in enumerate(ctx) if b.get('level')==1 and b.get('text','').startswith('V '))
blocks=ctx[:split]+theory+dev+ctx[split:]
blocks += [{'kind':'heading','level':1,'text':'VII FUENTES DE INFORMACIÓN'}]
for ref in refs:
    author=ref['author'].rstrip('.')+'.'
    text=f"{author} ({ref['year']}). {ref['title']}."
    if ref['type']: text+=' ['+ref['type']+'].'
    if ref['url']: text+=' '+ref['url']
    blocks.append({'kind':'reference','text':text})
blocks += ann

# Insert original technical diagrams at their first relevant section.
seen=set();enriched=[]
for b in blocks:
    enriched.append(b)
    if b['kind']!='heading':continue
    text=b['text'].lower()
    key=None;img=None;caption=None
    if text.startswith('1.2.1'):
        key='areas';img=functional;caption='Relación funcional de las áreas vinculadas con LogiApp'
    elif b.get('level',0)>1 and text.startswith('3.') and 'arquitectura' in text:
        key='arquitectura';img=architecture;caption='Arquitectura de componentes de LogiApp'
    elif b.get('level',0)>1 and text.startswith('3.') and any(s in text for s in ['modelo de datos','modelos y','modelos de información','modelo de información','persistencia y datos']):
        key='modelo';img=model;caption='Relaciones lógicas entre entidades y archivos'
    elif b.get('level',0)>1 and text.startswith('3.') and 'sincroniza' in text:
        key='sync';img=sync;caption='Conservación y confirmación de información pendiente'
    elif b.get('level',0)>1 and text.startswith('3.') and any(s in text for s in ['ciclo de viaje','inicio de viaje','apertura del viaje','apertura y continuidad','operación del viaje','flujo de viaje']):
        key='estados';img=states;caption='Secuencia operativa y cierre del viaje'
    if key and key not in seen:
        seen.add(key);enriched.append({'kind':'figure','path':img,'caption':caption,'source':'Elaboración propia a partir del código de LogiApp y los formatos del PED'})
blocks=enriched

doc=Document();sec=doc.sections[0]
sec.page_width=Inches(8.5);sec.page_height=Inches(11)
sec.top_margin=Cm(3);sec.left_margin=Cm(3);sec.right_margin=Cm(2.5);sec.bottom_margin=Cm(2.5)
sec.header_distance=Cm(1.2);sec.footer_distance=Cm(1.25)
normal=doc.styles['Normal'];normal.font.name='Arial';normal.font.size=Pt(12);normal.font.color.rgb=RGBColor(0,0,0)
normal.paragraph_format.line_spacing=1.5;normal.paragraph_format.space_after=Pt(12);normal.paragraph_format.first_line_indent=Pt(0)
normal.paragraph_format.widow_control=True
for name in ['Title','Subtitle','Heading 1','Heading 2','Heading 3','Caption']:
    s=doc.styles[name];s.font.name='Arial';s.font.color.rgb=RGBColor(0,0,0)
    if s.element.rPr is not None:
        for c in s.element.rPr.findall(qn('w:color')):
            c.attrib.pop(qn('w:themeColor'),None)
for k in (1,2,3):
    s=doc.styles[f'Heading {k}'];s.font.size=Pt(14 if k==1 else 12);s.font.bold=True
    s.paragraph_format.alignment=WD_ALIGN_PARAGRAPH.CENTER if k==1 else WD_ALIGN_PARAGRAPH.LEFT
    s.paragraph_format.keep_with_next=True;s.paragraph_format.space_before=Pt(12);s.paragraph_format.space_after=Pt(12)
    s.paragraph_format.line_spacing=1.2
    if k==1:s.paragraph_format.page_break_before=True
doc.styles['Caption'].font.size=Pt(10);doc.styles['Caption'].paragraph_format.line_spacing=1
doc.styles['Caption'].paragraph_format.alignment=WD_ALIGN_PARAGRAPH.CENTER
doc.styles['Caption'].paragraph_format.space_after=Pt(10)
doc.styles['Title'].font.size=Pt(16);doc.styles['Title'].font.bold=True
doc.styles['Title'].paragraph_format.line_spacing=1.2

def field(p,code,value=''):
    r=OxmlElement('w:r');begin=OxmlElement('w:fldChar');begin.set(qn('w:fldCharType'),'begin');r.append(begin);p._p.append(r)
    r=OxmlElement('w:r');it=OxmlElement('w:instrText');it.set(qn('xml:space'),'preserve');it.text=code;r.append(it);p._p.append(r)
    r=OxmlElement('w:r');sep=OxmlElement('w:fldChar');sep.set(qn('w:fldCharType'),'separate');r.append(sep);p._p.append(r)
    if value:p.add_run(value)
    r=OxmlElement('w:r');end=OxmlElement('w:fldChar');end.set(qn('w:fldCharType'),'end');r.append(end);p._p.append(r)

def front_heading(text):
    p=doc.add_paragraph();p.alignment=WD_ALIGN_PARAGRAPH.CENTER
    r=p.add_run(text);r.bold=True;r.font.size=Pt(14)
    p.paragraph_format.space_after=Pt(18);p.paragraph_format.keep_with_next=True
    return p
def cover_line(text,size=12,bold=False,after=12):
    p=doc.add_paragraph();p.alignment=WD_ALIGN_PARAGRAPH.CENTER;p.paragraph_format.line_spacing=1.2;p.paragraph_format.space_after=Pt(after)
    r=p.add_run(text);r.bold=bold;r.font.size=Pt(size);return p

cover_line('INSTITUTO TECNOLÓGICO SUPERIOR DEL ORIENTE DEL ESTADO DE HIDALGO',14,True,18)
cover_line('Ingeniería en Sistemas Computacionales',12,False,30)
cover_line('Informe técnico de residencia profesional',14,True,6)
cover_line('Reporte integral del Proyecto de Educación Dual conforme al Anexo VIII',12,False,28)
p=doc.add_paragraph(style='Title');p.alignment=WD_ALIGN_PARAGRAPH.CENTER
p.add_run('Sistema de control para la optimización de ciclos de viaje y tiempos de traslado')
cover_line('LogiApp',14,True,24)
cover_line('TLB Transportes',12,False,18)
cover_line('Presenta',12,False,2)
cover_line('Elton Yael Alba Roldan',12,True,4)
cover_line('Matrícula 22030013',12,False,16)
cover_line('Asesor interno DCC. René Cruz Guerrero',12,False,4)
cover_line('Asesor externo Ing. Raúl Armando Balmaceda Pérez',12,False,16)
cover_line('Periodo del PED enero a diciembre de 2026',12,False,18)
p=doc.add_paragraph('Apan, Hidalgo\nOctubre de 2026');p.alignment=WD_ALIGN_PARAGRAPH.RIGHT;p.paragraph_format.line_spacing=1.2
doc.add_page_break()
front_heading('AGRADECIMIENTOS')
doc.add_paragraph('Agradezco al Instituto Tecnológico Superior del Oriente del Estado de Hidalgo y a quienes acompañan mi formación en Ingeniería en Sistemas Computacionales por la oportunidad de vincular los conocimientos de la carrera con un problema de operación empresarial.')
doc.add_paragraph('Expreso mi agradecimiento al asesor interno, DCC. René Cruz Guerrero, y al asesor externo, Ing. Raúl Armando Balmaceda Pérez, por su participación en el Proyecto de Educación Dual. A TLB Transportes, por el contexto de trabajo que dio origen a LogiApp y por permitir relacionar el desarrollo de software con necesidades de información del transporte de carga.')
doc.add_paragraph('Reconozco a las personas que colaboraron en la construcción y documentación del proyecto. Los intercambios sobre requisitos, interfaces y operación contribuyeron a comprender que una solución tecnológica debe poder explicarse, revisarse y mantenerse por quienes la utilizan.')
doc.add_page_break()
front_heading('RESUMEN')
doc.add_paragraph('Se desarrolló LogiApp como una solución móvil para relacionar la gestión de operadores, el registro de viajes, el seguimiento de ubicación y las evidencias de operación de TLB Transportes. El proyecto respondió a la dispersión de información entre registros manuales y mensajería, que dificultaba reconstruir servicios y mantener expedientes vinculados con sus responsables.')
doc.add_paragraph('La aplicación se construyó con C# y XAML sobre .NET MAUI y se integró con Google Sheets para información tabular y Google Drive para archivos. Se incorporaron controles de acceso, estados de cuenta, validación de vigencias, captura de mantenimiento y un ciclo de viaje con inicio, llegada y cierre. La continuidad de información se apoyó en SQLite, preferencias y archivos locales, con mecanismos de sincronización para posiciones y cierres pendientes. La implementación Android incluyó un servicio de seguimiento en primer plano.')
doc.add_paragraph('La revisión del producto permitió identificar módulos, estructuras de datos, paquetes de distribución y documentación de usuario. Se ejecutaron satisfactoriamente las seis comprobaciones del programa local de pruebas de contraseñas. Se distinguió esa verificación del plan de pruebas operativas y de las mediciones de impacto, cuya confirmación requiere bitácoras de campo. El producto aportó una base técnica para centralizar registros y conservar relaciones entre folios y evidencias. La continuidad, la seguridad de configuración y la consistencia ante fallos parciales se establecieron como prioridades de validación y mejora. El periodo académico se programó de enero a diciembre de 2026 y la memoria integró evidencia disponible al 23 de octubre de 2026.')
p=doc.add_paragraph('Palabras clave: logística, aplicación móvil, trazabilidad, persistencia local, sincronización');p.paragraph_format.space_after=Pt(0)
doc.add_page_break()
front_heading('ÍNDICE GENERAL')
p=doc.add_paragraph();p.paragraph_format.line_spacing=1;field(p,' TOC \\o "1-3" \\h \\z \\u ','Actualizar tabla de contenido en Word')
doc.add_page_break();front_heading('ÍNDICE DE FIGURAS')
p=doc.add_paragraph();p.paragraph_format.line_spacing=1;field(p,' TOC \\h \\z \\c "Figura" ','Índice de figuras')
doc.add_page_break();front_heading('ÍNDICE DE CUADROS')
p=doc.add_paragraph();p.paragraph_format.line_spacing=1;field(p,' TOC \\h \\z \\c "Cuadro" ','Índice de cuadros')

body=doc.add_section(WD_SECTION_START.NEW_PAGE);body.footer.is_linked_to_previous=False
body.header.is_linked_to_previous=False
pg=OxmlElement('w:pgNumType');pg.set(qn('w:start'),'1');body._sectPr.append(pg)
p=body.footer.paragraphs[0];p.alignment=WD_ALIGN_PARAGRAPH.RIGHT;field(p,' PAGE ','1')
for r in p.runs:r.font.name='Arial';r.font.size=Pt(10)
counts={'Figura':0,'Cuadro':0};first_heading=True;captions=[]

def caption(label,text):
    counts[label]+=1;p=doc.add_paragraph(style='Caption');p.add_run(label+' ')
    field(p,' SEQ '+label+' \\* ARABIC ',str(counts[label]));p.add_run('. '+text.rstrip('.'))
    p.paragraph_format.keep_with_next=True;captions.append({'label':label,'number':counts[label],'text':text})
    return p

for b in blocks:
    kind=b['kind']
    if kind=='heading':
        text=b['text'];level=min(int(b.get('level',2)),3)
        p=doc.add_paragraph(text,style=f'Heading {level}')
        if first_heading:p.paragraph_format.page_break_before=False;first_heading=False
    elif kind in ['p','reference']:
        text=b['text'].replace('\u0002','').replace('\u2011','-').replace('\u2013','-').replace('\u2014','-')
        p=doc.add_paragraph(text);p.alignment=WD_ALIGN_PARAGRAPH.JUSTIFY
        if text.startswith(('La primera prioridad','La segunda prioridad','La tercera prioridad','La cuarta prioridad')):
            ordinals={'La primera prioridad':'1','La segunda prioridad':'2','La tercera prioridad':'3','La cuarta prioridad':'4'}
            key=next(k for k in ordinals if text.startswith(k))
            if 'La quinta prioridad' in text:
                a,z=text.split('La quinta prioridad',1)
                p.text=ordinals[key]+'. '+a.replace(key,'La prioridad',1).strip()
                p=doc.add_paragraph('5. La prioridad'+z);p.alignment=WD_ALIGN_PARAGRAPH.JUSTIFY
            else:p.text=ordinals[key]+'. '+text.replace(key,'La prioridad',1)
            p.paragraph_format.line_spacing=1.5
        if kind=='reference':
            p.paragraph_format.line_spacing=1.15;p.paragraph_format.left_indent=Cm(.75);p.paragraph_format.first_line_indent=Cm(-.75);p.paragraph_format.space_after=Pt(10)
            p.alignment=WD_ALIGN_PARAGRAPH.LEFT;p.paragraph_format.keep_together=True
    elif kind=='table':
        caption('Cuadro',b['caption']);headers=b['headers'];rows=b['rows'];n=len(headers)
        table=doc.add_table(rows=1,cols=n);table.alignment=WD_TABLE_ALIGNMENT.CENTER;table.autofit=False
        width=6.33;ratios=([.18,.82] if n==2 else ([.22,.39,.39] if n==3 else ([.08,.27,.28,.37] if headers[0] in ['Núm.','Núm','ID'] else [.19,.27,.27,.27])))
        for col,ratio in zip(table.columns,ratios): col.width=Inches(width*ratio)
        for i,s in enumerate(headers):table.rows[0].cells[i].text=str(s)
        rep=OxmlElement('w:tblHeader');table.rows[0]._tr.get_or_add_trPr().append(rep)
        for row in rows:
            cells=table.add_row().cells
            for i,s in enumerate(row):cells[i].text=str(s)
        for ri,row in enumerate(table.rows):
            cant=OxmlElement('w:cantSplit');row._tr.get_or_add_trPr().append(cant)
            for ci,cell in enumerate(row.cells):
                cell.width=Inches(width*ratios[ci]);cell.vertical_alignment=WD_CELL_VERTICAL_ALIGNMENT.CENTER
                tcPr=cell._tc.get_or_add_tcPr();shade=OxmlElement('w:shd');shade.set(qn('w:fill'),'E7E7E7' if ri==0 else ('F7F7F7' if ri%2==0 else 'FFFFFF'));tcPr.append(shade)
                mar=OxmlElement('w:tcMar')
                for side in ['top','left','bottom','right']:
                    e=OxmlElement('w:'+side);e.set(qn('w:w'),'85');e.set(qn('w:type'),'dxa');mar.append(e)
                tcPr.append(mar)
                borders=OxmlElement('w:tcBorders')
                for side in ['top','left','bottom','right']:
                    e=OxmlElement('w:'+side);e.set(qn('w:val'),'single');e.set(qn('w:sz'),'4');e.set(qn('w:color'),'BFBFBF');borders.append(e)
                tcPr.append(borders)
                for p in cell.paragraphs:
                    p.paragraph_format.line_spacing=1.05;p.paragraph_format.space_before=Pt(2);p.paragraph_format.space_after=Pt(2)
                    p.alignment=WD_ALIGN_PARAGRAPH.LEFT
                    if ri==0:p.paragraph_format.keep_with_next=True
                    if n==4 and ci==0 and len(cell.text)<10:p.alignment=WD_ALIGN_PARAGRAPH.CENTER
                    for r in p.runs:r.font.name='Arial';r.font.size=Pt(10);r.bold=ri==0;r.font.color.rgb=RGBColor(0,0,0)
        p=doc.add_paragraph();p.paragraph_format.space_after=Pt(2);p.paragraph_format.space_before=Pt(0);p.paragraph_format.line_spacing=.4
    elif kind=='figure':
        p=doc.add_paragraph();p.alignment=WD_ALIGN_PARAGRAPH.CENTER;p.paragraph_format.keep_with_next=True
        p.add_run().add_picture(b['path'],width=Inches(6.2))
        caption('Figura',b['caption'])
        p=doc.add_paragraph(b.get('source','Elaboración propia a partir del código de LogiApp'));p.alignment=WD_ALIGN_PARAGRAPH.CENTER
        p.paragraph_format.line_spacing=1;p.paragraph_format.space_after=Pt(12)
        for r in p.runs:r.font.size=Pt(10)
    else:raise ValueError('Tipo de bloque no soportado '+kind)

# Bibliography sources available in Word source manager; rendered APA references remain explicit.
doc.core_properties.author='Elton Yael Alba Roldan'
doc.core_properties.title='Sistema de control para la optimización de ciclos de viaje y tiempos de traslado'
doc.core_properties.subject='Informe técnico de residencia profesional y reporte integral de Educación Dual'
doc.core_properties.keywords='LogiApp, TLB Transportes, ITESA, Anexo VIII, 2026'
doc.core_properties.comments=''
for style in doc.styles:
    if style.type==1 and style.name.startswith('TOC'):
        style.font.name='Arial';style.font.size=Pt(11);style.paragraph_format.line_spacing=1;style.paragraph_format.space_after=Pt(5)
settings=doc.settings.element
update=OxmlElement('w:updateFields');update.set(qn('w:val'),'true');settings.append(update)
doc.save(OUTPUT)

nsb='http://schemas.openxmlformats.org/officeDocument/2006/bibliography'
root=etree.Element('{'+nsb+'}Sources',nsmap={'b':nsb});root.set('SelectedStyle','/APA.XSL');root.set('StyleName','APA')
for ref in refs:
    item=etree.SubElement(root,'{'+nsb+'}Source')
    for k,v in [('Tag',ref['tag']),('SourceType','InternetSite' if ref['url'] else 'Misc'),('Guid','{'+str(uuid.uuid4()).upper()+'}'),('Title',ref['title']),('Year',re.sub('[a-z]','',ref['year'])),('URL',ref['url'])]:
        if v:etree.SubElement(item,'{'+nsb+'}'+k).text=v
    a=etree.SubElement(item,'{'+nsb+'}Author');a2=etree.SubElement(a,'{'+nsb+'}Author');etree.SubElement(a2,'{'+nsb+'}Corporate').text=ref['author'].rstrip('.')
custom=etree.tostring(root,encoding='UTF-8',xml_declaration=True)
with zipfile.ZipFile(OUTPUT) as z: data={n:z.read(n) for n in z.namelist()}
data['customXml/item1.xml']=custom
nsr='http://schemas.openxmlformats.org/package/2006/relationships'
rels=etree.fromstring(data['word/_rels/document.xml.rels'])
rel=etree.SubElement(rels,'{'+nsr+'}Relationship');rel.set('Id','rIdBibliography');rel.set('Type','http://schemas.openxmlformats.org/officeDocument/2006/relationships/customXml');rel.set('Target','../customXml/item1.xml')
data['word/_rels/document.xml.rels']=etree.tostring(rels,encoding='UTF-8',xml_declaration=True)
with zipfile.ZipFile(OUTPUT,'w',compression=zipfile.ZIP_DEFLATED) as z:
    for n,v in data.items():z.writestr(n,v)
(SRC/'contenido_integrado.json').write_text(json.dumps(blocks,ensure_ascii=False,indent=2),encoding='utf-8')
(QA/'inventario_documento.json').write_text(json.dumps({'blocks':len(blocks),'words':sum(len(b.get('text','').split()) for b in blocks),'figures':counts['Figura'],'tables':counts['Cuadro'],'references':len(refs),'headings':[b['text'] for b in blocks if b['kind']=='heading'],'captions':captions},ensure_ascii=False,indent=2),encoding='utf-8')
print(str(OUTPUT));print(json.dumps({'words':sum(len(b.get('text','').split()) for b in blocks),'figures':counts['Figura'],'tables':counts['Cuadro'],'blocks':len(blocks)},ensure_ascii=False))
