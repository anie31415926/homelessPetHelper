from pathlib import Path
import json
import shutil
from PIL import Image

HERE = Path(__file__).resolve().parent
ROOT = HERE.parent.parent
old = ROOT / 'art-source/loading-20261123'
art = ROOT / 'unity/HomelessPetHelper/Assets/Art/Loading'
preserved = HERE / 'preserved'
preserved.mkdir(parents=True, exist_ok=True)
for file in art.iterdir():
    target = preserved / file.name
    if not target.exists():
        shutil.copyfile(file, target)
manifest = json.loads((old / 'slices.json').read_text())
atlas = Image.open(preserved / 'T_Loading_Elements.png').convert('RGBA')
jobs = {}

def sheet(slug, items, columns, cell, padding=28):
    rows = (len(items) + columns - 1) // columns
    image = Image.new('RGB', (columns * cell[0], rows * cell[1]), 'white')
    slots = []
    for i, item in enumerate(items):
        x, y, w, h = item['rect']
        sprite = atlas.crop((x, y, x+w, y+h))
        sprite.thumbnail((cell[0]-padding*2, cell[1]-padding*2), Image.Resampling.LANCZOS)
        factor = min((cell[0]-padding*2)/w, (cell[1]-padding*2)/h)
        sprite = atlas.crop((x,y,x+w,y+h)).resize((round(w*factor), round(h*factor)), Image.Resampling.LANCZOS)
        left, top = i % columns * cell[0], i // columns * cell[1]
        image.paste(sprite, (left+(cell[0]-sprite.width)//2, top+(cell[1]-sprite.height)//2), sprite)
        slots.append({'name':item['name'], 'kind':item['kind'], 'source':item['source'],
                      'cell':[left,top,cell[0],cell[1]]})
    (HERE/slug).mkdir(exist_ok=True)
    image.save(HERE/slug/'guide.png')
    jobs[slug] = {'slots':slots,'size':list(image.size)}

sheet('animals', [s for s in manifest['sprites'] if s['kind'] in ('orbit','door') or s['name']=='ProgressCat'],
      4,(256,256))
sheet('house', [s for s in manifest['sprites'] if s['name']=='House'],1,(512,512),48)
sheet('controls', [s for s in manifest['sprites'] if s['name'] in ('LoadingSign','ProgressTrack','ProgressFill')],
      1,(768,192),40)
sheet('decorations', [s for s in manifest['sprites'] if s['kind'] in ('heart','star')],4,(128,128),32)
(HERE/'background').mkdir(exist_ok=True)
Image.open(preserved/'T_Loading_Background.png').resize((576,1024),Image.Resampling.LANCZOS).save(
    HERE/'background/guide.png')
jobs['background']={'slots':[],'size':[576,1024]}
(HERE/'plan.json').write_text(json.dumps(jobs,indent=2)+'\n',encoding='utf8')
print({k:v['size'] for k,v in jobs.items()})
