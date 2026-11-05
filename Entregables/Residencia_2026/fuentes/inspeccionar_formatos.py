from pathlib import Path
import json, zipfile, datetime
from pypdf import PdfReader
from openpyxl import load_workbook

OUT=Path(__file__).resolve().parent
PDF=Path(r'C:\Users\elton\Downloads\Informe Tecnico de Residencia Profesional__.pdf')
XLSX=Path(r'C:\Users\elton\Documents\Copia_2026\Modelo Dual\Anexos_Dual\Elton_TLB_2 Formatos Modelo Dual 2026_27_04_26.xlsx')
reader=PdfReader(PDF)
pages=[{'pagina':i+1,'texto':p.extract_text() or ''} for i,p in enumerate(reader.pages)]
(OUT/'pdf_paginas.json').write_text(json.dumps(pages,ensure_ascii=False,indent=2),encoding='utf-8')
(OUT/'informe_tecnico_referencia.txt').write_text('\n\n'.join(f'=== PÁGINA {p["pagina"]} ===\n{p["texto"]}' for p in pages),encoding='utf-8')

def safe(v):
    if isinstance(v,(datetime.date,datetime.datetime)): return v.isoformat()
    return v
wb=load_workbook(XLSX,data_only=False,read_only=False)
cached=load_workbook(XLSX,data_only=True,read_only=True)
summary=[]
all_cells=[]
for ws in wb.worksheets:
    cells=[]
    for row in ws:
        for c in row:
            if c.value is not None:
                cells.append({'celda':c.coordinate,'valor':safe(c.value),'cache':safe(cached[ws.title][c.coordinate].value),'tipo':c.data_type,'formato':c.number_format,'fuente':c.font.name,'tamano_fuente':c.font.sz,'wrap':c.alignment.wrap_text,'horizontal':c.alignment.horizontal,'vertical':c.alignment.vertical,'ancho_columna':ws.column_dimensions[c.column_letter].width,'alto_fila':ws.row_dimensions[c.row].height})
    images=[]
    for i,img in enumerate(ws._images):
        a=img.anchor
        info={'indice':i,'formato':img.format,'ancho':img.width,'alto':img.height}
        if hasattr(a,'_from'):info['ancla']={'fila':a._from.row+1,'columna':a._from.col+1}
        images.append(info)
    info={'hoja':ws.title,'filas':ws.max_row,'columnas':ws.max_column,'celdas_con_valor':len(cells),'rangos_combinados':list(map(str,ws.merged_cells.ranges)),'print_area':str(ws.print_area),'print_title_rows':ws.print_title_rows,'print_title_cols':ws.print_title_cols,'page_setup':str(ws.page_setup),'page_margins':str(ws.page_margins),'imagenes':images,'row_breaks':[b.id for b in ws.row_breaks.brk],'col_breaks':[b.id for b in ws.col_breaks.brk]}
    summary.append(info)
    all_cells.append({'hoja':ws.title,'celdas':cells})
    if 'VIII' in ws.title.upper() or 'Vlll' in ws.title:
        (OUT/'anexo_viii_celdas.json').write_text(json.dumps({'estructura':info,'celdas':cells},ensure_ascii=False,indent=2),encoding='utf-8')
(OUT/'libro_estructura.json').write_text(json.dumps(summary,ensure_ascii=False,indent=2),encoding='utf-8')
(OUT/'libro_celdas.json').write_text(json.dumps(all_cells,ensure_ascii=False,indent=2),encoding='utf-8')
(OUT/'libro_celdas.txt').write_text('\n\n'.join('=== '+s['hoja']+' ===\n'+'\n'.join(f'{c["celda"]}: {c["valor"]}' for c in s['celdas']) for s in all_cells),encoding='utf-8')
with zipfile.ZipFile(XLSX) as z:
    media=[n for n in z.namelist() if n.startswith('xl/media/')]
    media_dir=OUT/'logos_libro';media_dir.mkdir(exist_ok=True)
    for n in media:(media_dir/Path(n).name).write_bytes(z.read(n))
    (OUT/'libro_media.json').write_text(json.dumps(media,indent=2),encoding='utf-8')
print(json.dumps({'pdf_paginas':len(pages),'hojas':[{'hoja':s['hoja'],'celdas':s['celdas_con_valor'],'imagenes':len(s['imagenes'])} for s in summary],'media':media},ensure_ascii=False,indent=2))
