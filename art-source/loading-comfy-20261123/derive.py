"""Build the Loading runtime atlas from real ComfyUI output, not the small reference."""
from collections import deque
from pathlib import Path
import hashlib
import json

import numpy as np
from PIL import Image, ImageFilter

HERE = Path(__file__).resolve().parent
ROOT = HERE.parent.parent
OUT = HERE / 'candidate'
OUT.mkdir(exist_ok=True)
CHECK = ROOT / 'test-results/loading-comfy/edges'
CHECK.mkdir(parents=True, exist_ok=True)
plan = json.loads((HERE/'plan.json').read_text())
old = json.loads((HERE/'preserved/LoadingSlices.json').read_text())

def largest_component(binary):
    h,w = binary.shape
    seen = np.zeros_like(binary)
    largest = []
    for y,x in zip(*np.where(binary)):
        if seen[y,x]:
            continue
        queue = deque([(x,y)])
        seen[y,x] = True
        component = []
        while queue:
            x,y = queue.popleft()
            component.append((x,y))
            for nx,ny in ((x-1,y),(x+1,y),(x,y-1),(x,y+1)):
                if 0<=nx<w and 0<=ny<h and binary[ny,nx] and not seen[ny,nx]:
                    seen[ny,nx] = True
                    queue.append((nx,ny))
        if len(component)>len(largest):
            largest = component
    result = np.zeros_like(binary)
    for x,y in largest:
        result[y,x] = True
    return result

def extract(image):
    rgb = np.asarray(image.convert('RGB'))
    # Only the connected white exterior becomes transparent; pale fur/house interiors stay opaque.
    paper = rgb.min(axis=2)>241
    h,w = paper.shape
    exterior = np.zeros_like(paper)
    queue = deque()
    for y in range(h):
        for x in (0,w-1):
            if paper[y,x]:
                exterior[y,x] = True
                queue.append((x,y))
    for x in range(w):
        for y in (0,h-1):
            if paper[y,x] and not exterior[y,x]:
                exterior[y,x] = True
                queue.append((x,y))
    while queue:
        x,y = queue.popleft()
        for nx,ny in ((x-1,y),(x+1,y),(x,y-1),(x,y+1)):
            if 0<=nx<w and 0<=ny<h and paper[ny,nx] and not exterior[ny,nx]:
                exterior[ny,nx] = True
                queue.append((nx,ny))
    body = largest_component(~exterior)
    # Sub-pixel alpha is derived at master size before downsampling, not a thick blur halo.
    matte = Image.fromarray(np.uint8(body*255))
    smooth = matte.filter(ImageFilter.MinFilter(3)).filter(ImageFilter.MaxFilter(3))
    # Decontaminate the white matte at the silhouette boundary, retaining pale opaque interiors.
    boundary = np.asarray(smooth.filter(ImageFilter.MaxFilter(3))) != np.asarray(
        smooth.filter(ImageFilter.MinFilter(3)))
    alpha_values = np.asarray(smooth,dtype=float)/255
    opacity = np.clip((255-rgb.min(axis=2).astype(float))/38,0,1)
    alpha_values[boundary] *= opacity[boundary]
    corrected = np.clip((rgb.astype(float)-(1-alpha_values[:,:,None])*255)/
                        np.maximum(alpha_values[:,:,None],.001),0,255).astype('uint8')
    alpha = Image.fromarray(np.uint8(alpha_values*255))
    box = alpha.getbbox()
    if box is None:
        raise ValueError('Empty generated cell')
    # A restrained pigment contrast grade keeps the thin generated pencil work legible at phone size.
    corrected = np.uint8(np.clip(255-(255-corrected.astype(float))*1.22,0,255))
    result = Image.fromarray(corrected).convert('RGBA')
    result.putalpha(alpha)
    return result.crop(box), list(box)

sprites = []
for slug in ('animals','house','controls','decorations'):
    master = Image.open(HERE/slug/'master.png').convert('RGB')
    target_size = plan[slug]['size']
    if list(master.size) != target_size:
        master = master.resize(target_size,Image.Resampling.LANCZOS)
    for original_slot in plan[slug]['slots']:
        slot = dict(original_slot)
        if slug == 'controls':
            # Prompt explicitly requested label/track/fill top-to-bottom; map those generated rows by meaning.
            slot['name'] = {'ProgressTrack':'LoadingSign','LoadingSign':'ProgressTrack'}.get(slot['name'],slot['name'])
            slot['source'] = next(s['source'] for s in old['sprites'] if s['name']==slot['name'])
        x,y,w,h = slot['cell']
        image,box = extract(master.crop((x,y,x+w,y+h)))
        name,kind = slot['name'],slot['kind']
        if name == 'House': maximum=(288,288)
        elif name == 'ProgressCat': maximum=(224,224)
        elif name == 'ProgressTrack': maximum=(640,96)
        elif name == 'ProgressFill': maximum=(512,64)
        elif name == 'LoadingSign': maximum=(384,96)
        elif kind in ('heart','star'): maximum=(48,48)
        else: maximum=(176,176)
        image.thumbnail(maximum,Image.Resampling.LANCZOS)
        sprites.append({**slot,'image':image,'master':slug,'trim':box})

# Shelf pack the hand-painted layers together; no full-screen animal image is shipped.
width = 1024
x = y = row_height = 4
page = Image.new('RGBA',(width,1024))
manifest = {**old,'atlasSize':[1024,1024],'artSource':'ComfyUI Qwen Image2.1 redraw','sprites':[]}
details = []
for item in sorted(sprites,key=lambda s:-s['image'].height):
    image = item.pop('image')
    if x+image.width+4>width:
        x=4
        y+=row_height+4
        row_height=0
    if y+image.height+4>1024:
        raise ValueError('Atlas overflow')
    page.paste(image,(x,y))
    manifest['sprites'].append({'name':item['name'],'kind':item['kind'],'source':item['source'],
                                'rect':[x,y,image.width,image.height]})
    details.append({**item,'runtimeSize':list(image.size)})
    x+=image.width+4
    row_height=max(row_height,image.height)
height = ((y+row_height+7)//4)*4
page = page.crop((0,0,width,height))
manifest['atlasSize']=[width,height]
runtime_page = page
runtime_page.save(OUT/'T_Loading_Elements.png',optimize=True)
background = Image.open(HERE/'background/master.png').convert('RGB')
background.thumbnail((576,1024),Image.Resampling.LANCZOS)
runtime_background=background.quantize(colors=256,method=Image.Quantize.MEDIANCUT,dither=Image.Dither.NONE)
runtime_background.save(OUT/'T_Loading_Background.png',optimize=True)
(OUT/'LoadingSlices.json').write_text(json.dumps(manifest,indent=2)+'\n',encoding='utf8')
(HERE/'derivation.json').write_text(json.dumps({'method':'Connected exterior-white matte, largest closed component, 1px morphological opening and white-matte decontamination, 1.22x pigment contrast grade, LANCZOS downsample; full RGBA atlas, background-only 256-color PNG with no dithering. Masters remain untouched. Control rows mapped by prompt semantics label/track/fill.',
    'backgroundPaletteRgbMeanAbsoluteError':float(np.abs(np.asarray(background,dtype=float)-np.asarray(runtime_background.convert('RGB'),dtype=float)).mean()),
    'atlasSize':list(page.size),'items':details,'masterSha256':{
    key:hashlib.sha256((HERE/key/'master.png').read_bytes()).hexdigest() for key in plan}},indent=2)+'\n',encoding='utf8')
page=runtime_page.convert('RGBA')
background=runtime_background.convert('RGB')
for color,name in (((49,62,74),'dark'),((255,246,221),'light')):
    check=Image.new('RGBA',page.size,color+(255,))
    check.alpha_composite(page)
    check.convert('RGB').save(CHECK/(name+'.png'))

# Same composition as the scene; typography/progress remain live Unity controls.
preview=background.resize((522,902),Image.Resampling.LANCZOS).convert('RGBA')
for item in sorted(manifest['sprites'],key=lambda s:{'house':0,'orbit':1,'door':2}.get(s['kind'],3)):
    x,y,w,h=item['rect']
    image=page.crop((x,y,x+w,y+h))
    sx,sy,ex,ey=item['source']
    if item['name'] in ('ProgressFill','LoadingSign','ProgressTrack'):
        continue
    image.thumbnail(((ex-sx)*2,(ey-sy)*2),Image.Resampling.LANCZOS)
    px=(sx+ex)-image.width//2
    py=(sy+ey)-image.height//2
    preview.alpha_composite(image,(px,py))
preview.save(CHECK/'composition.png')
print(json.dumps({'sprites':len(sprites),'page':list(page.size),'bytes':sum(f.stat().st_size for f in OUT.glob('*.png'))}))
