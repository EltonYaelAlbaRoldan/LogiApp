from pathlib import Path
import zipfile
import xml.etree.ElementTree as ET
import json
import re
from io import BytesIO
from PIL import Image
import posixpath

ROOT = Path(__file__).resolve().parents[3]
OUT = Path(__file__).resolve().parent
TEMP = ROOT / '.tmp' / 'inspeccion_evidencias'
TEMP.mkdir(parents=True, exist_ok=True)
NS = {'a':'http://schemas.openxmlformats.org/drawingml/2006/main',
      'r':'http://schemas.openxmlformats.org/package/2006/relationships'}
result = []
for file in list(ROOT.glob('*.pptx')) + list((ROOT/'Entregables').glob('*.pptx')):
    entry = {'archivo':str(file.relative_to(ROOT)), 'slides':[], 'imagenes':[]}
    with zipfile.ZipFile(file) as z:
        slides = sorted((n for n in z.namelist() if re.fullmatch(r'ppt/slides/slide\d+\.xml',n)),
                        key=lambda n:int(re.search(r'slide(\d+)',n).group(1)))
        image_slides = {}
        for slide in slides:
            number = int(re.search(r'slide(\d+)',slide).group(1))
            text = ' | '.join(t.text or '' for t in ET.fromstring(z.read(slide)).findall('.//a:t',NS))
            entry['slides'].append({'slide':number,'texto':text})
            relpath=posixpath.join(posixpath.dirname(slide),'_rels',posixpath.basename(slide)+'.rels')
            if relpath in z.namelist():
                for r in ET.fromstring(z.read(relpath)):
                    if r.attrib.get('Type','').endswith('/image'):
                        value=r.attrib['Target']
                        target=value.lstrip('/') if value.startswith('/') else posixpath.normpath(posixpath.join(posixpath.dirname(slide),value))
                        image_slides.setdefault(target,[]).append(number)
        for media in [n for n in z.namelist() if n.startswith('ppt/media/')]:
            data=z.read(media)
            target=TEMP/(file.stem+'_'+posixpath.basename(media))
            target.write_bytes(data)
            try:
                im=Image.open(BytesIO(data))
                size=list(im.size)
            except Exception:
                size=None
            entry['imagenes'].append({'media':media, 'slides':image_slides.get(media,[]), 'dimensiones':size, 'archivo_inspeccion':str(target)})
    result.append(entry)
(OUT/'presentaciones_inventario_texto.json').write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps(result,ensure_ascii=False,indent=2))
