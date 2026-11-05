import json, subprocess, sys, re
from pathlib import Path
from pypdf import PdfReader
import pdfplumber
from PIL import Image,ImageDraw,ImageFont

root=Path(__file__).resolve().parents[1]
pdf=root/'LogiApp_Informe_integrado_2026_final.pdf'
qa=root/'revision';pagesdir=qa/'paginas_finales';pagesdir.mkdir(exist_ok=True)
poppler=Path('C:/Users/elton/.cache/codex-runtimes/codex-primary-runtime/dependencies/native/poppler/Library/bin/pdftoppm.exe')
subprocess.run([str(poppler),'-r','110','-png',str(pdf),str(pagesdir/'page')],check=True)
data=[]
with pdfplumber.open(pdf) as doc:
    for i,p in enumerate(doc.pages,1):
        text=p.extract_text() or '';words=p.extract_words()
        data.append({'page':i,'words':len(text.split()),'start':text[:220],'end':text[-150:],'xmin':min([w['x0'] for w in words],default=0),'xmax':max([w['x1'] for w in words],default=0),'top':min([w['top'] for w in words],default=0),'bottom':max([w['bottom'] for w in words],default=0),'images':len(p.images),'bad_glyph':'\ufffd' in text,'field_error':bool(re.search(r'Error!|Error de referencia|Marcador no definido|Actualizar tabla',text,re.I))})
(qa/'paginas_resumen.json').write_text(json.dumps(data,ensure_ascii=False,indent=2),encoding='utf-8')
pngs=sorted(pagesdir.glob('page-*.png'))
font=ImageFont.truetype('C:/Windows/Fonts/arial.ttf',18)
for start in range(0,len(pngs),12):
    batch=pngs[start:start+12];im=Image.new('RGB',(1200,4*430),'#d9d9d9');d=ImageDraw.Draw(im)
    for j,p in enumerate(batch):
        thumb=Image.open(p);thumb.thumbnail((284,390));x=(j%4)*300+8;y=(j//4)*430+30
        im.paste(thumb,(x,y));d.text((x,y-24),p.stem,font=font,fill='black')
    im.save(qa/f'contacto-{start+1:03d}.png')
summary={'pages':len(data),'min_words':min(x['words'] for x in data),'empty':[x['page'] for x in data if x['words']<10],'field_errors':[x['page'] for x in data if x['field_error']],'bad_glyphs':[x['page'] for x in data if x['bad_glyph']],'png_count':len(pngs),'pdf_bytes':pdf.stat().st_size}
(qa/'validacion_pdf.json').write_text(json.dumps(summary,ensure_ascii=False,indent=2),encoding='utf-8');print(json.dumps(summary,ensure_ascii=False))
