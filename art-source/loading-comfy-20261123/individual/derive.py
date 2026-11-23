"""Prepare review sheets; build only from explicitly reviewed, unchanged masters."""
from collections import deque
from pathlib import Path
import hashlib
import json
import sys

import numpy as np
from PIL import Image, ImageDraw, ImageFilter, ImageFont

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[2]
ART = ROOT / 'unity/HomelessPetHelper/Assets/Art/Loading'
PLAN = json.loads((HERE / 'plan.json').read_text(encoding='utf8'))
OUT = HERE / 'candidate'
OUT.mkdir(exist_ok=True)


def digest(file):
    return hashlib.sha256(file.read_bytes()).hexdigest()


def components(mask):
    height, width = mask.shape
    visited = np.zeros_like(mask)
    result = []
    for y, x in zip(*np.where(mask)):
        if visited[y, x]:
            continue
        queue = deque([(x, y)])
        visited[y, x] = True
        part = []
        while queue:
            px, py = queue.popleft()
            part.append((px, py))
            for nx, ny in ((px-1, py), (px+1, py), (px, py-1), (px, py+1)):
                if 0 <= nx < width and 0 <= ny < height and mask[ny, nx] and not visited[ny, nx]:
                    visited[ny, nx] = True
                    queue.append((nx, ny))
        result.append(part)
    return sorted(result, key=len, reverse=True)


def extract(image, count):
    rgb = np.asarray(image.convert('RGB'))
    # Flood only the exterior white, retaining pale closed fur and negative spaces between feet.
    paper = Image.fromarray(np.uint8((rgb.min(axis=2) > 241) * 255)).copy()
    ImageDraw.floodfill(paper, (0, 0), 128)
    body = np.asarray(paper) != 128
    parts = components(body)
    selected = np.zeros_like(body)
    for part in parts[:count]:
        for x, y in part:
            selected[y, x] = True
    matte = Image.fromarray(np.uint8(selected * 255))
    matte = matte.filter(ImageFilter.MinFilter(3)).filter(ImageFilter.MaxFilter(3))
    edge = np.asarray(matte.filter(ImageFilter.MaxFilter(3))) != np.asarray(
        matte.filter(ImageFilter.MinFilter(3)))
    alpha = np.asarray(matte, dtype=float) / 255
    alpha[edge] *= np.clip((255-rgb.min(axis=2).astype(float))[edge]/38, 0, 1)
    clean = np.clip((rgb.astype(float)-(1-alpha[:, :, None])*255) /
                    np.maximum(alpha[:, :, None], .001), 0, 255)
    clean = np.uint8(np.clip(255-(255-clean)*1.22, 0, 255))
    result = Image.fromarray(clean).convert('RGBA')
    result.putalpha(Image.fromarray(np.uint8(alpha*255)))
    bounds = result.getbbox()
    if not bounds:
        raise ValueError('Empty cutout')
    return result.crop(bounds), list(bounds), [len(part) for part in parts[:6]]


action = sys.argv[1] if len(sys.argv) > 1 else 'prepare'
selection_path = HERE / 'selection.json'
selection = json.loads(selection_path.read_text()) if selection_path.exists() else {}
font = ImageFont.truetype('C:/Windows/Fonts/arial.ttf', 15)
sheet = Image.new('RGB', (1024, 1152), 'white')
edges = Image.new('RGB', (1024, 1152), 'white')
records = []
images = {}
for index, job in enumerate(PLAN['jobs']):
    source = HERE / selection.get(job['id'], f"v2/{job['id']}/master.png")
    if not source.exists():
        continue
    raw = Image.open(source).convert('RGB')
    cutout, bounds, areas = extract(raw, 2 if job['id'] == 'rabbits' else 1)
    cutout.save(OUT / f"{job['id']}.png", optimize=True)
    images[job['sprite']] = cutout
    x, y = (index % 4)*256, (index // 4)*288
    sheet.paste(raw.resize((256, 256), Image.Resampling.LANCZOS), (x, y))
    ImageDraw.Draw(sheet).text((x+8, y+261), f"{index+1:02} {job['id']}", font=font, fill='#343b3d')
    for dx, color in ((0, '#fff2d3'), (128, '#344651')):
        tile = Image.new('RGBA', (128, 256), color)
        small = cutout.copy()
        small.thumbnail((116, 204), Image.Resampling.LANCZOS)
        tile.alpha_composite(small, ((128-small.width)//2, (256-small.height)//2))
        edges.paste(tile.convert('RGB'), (x+dx, y))
    ImageDraw.Draw(edges).text((x+8, y+261), f"{index+1:02} {job['id']}", font=font, fill='#343b3d')
    records.append({'id': job['id'], 'sprite': job['sprite'], 'master': source.relative_to(HERE).as_posix(),
                    'sha256': digest(source), 'trim': bounds, 'componentAreas': areas,
                    'cutoutSha256': digest(OUT/f"{job['id']}.png")})
sheet.save(HERE/'review-sheet.png', optimize=True)
edges.save(HERE/'edge-review.png', optimize=True)
report = {'method': 'Connected exterior white removal, largest closed component (two for rabbit pair), '
          'one-pixel opening, white matte decontamination and 1.22x pigment grade. No anatomical edits.',
          'items': records}
(HERE/'derivation.json').write_text(json.dumps(report, indent=2)+'\n', encoding='utf8')
if action == 'prepare':
    print(f'Prepared {len(records)}/16; no runtime files modified.')
    sys.exit(0)
if action != 'build':
    raise ValueError('Use prepare or build')
review = json.loads((HERE/'review.json').read_text(encoding='utf8'))
if len(records) != 16 or len(review['items']) != 16:
    raise ValueError('All 16 sprites require explicit visual review')
for record in records:
    accepted = next(item for item in review['items'] if item['id'] == record['id'])
    if accepted['status'] != 'accepted' or accepted['sha256'] != record['sha256']:
        raise ValueError(f"Unreviewed or changed master: {record['id']}")

# Non-animal pixels and their source coordinates are preserved from the prior runtime atlas.
prior = HERE/'prior-runtime'
prior.mkdir(exist_ok=True)
for file in ART.iterdir():
    if file.is_file() and not (prior/file.name).exists():
        (prior/file.name).write_bytes(file.read_bytes())
manifest = json.loads((prior/'LoadingSlices.json').read_text(encoding='utf8'))
old_page = Image.open(prior/'T_Loading_Elements.png').convert('RGBA')
sprites = []
for sprite in manifest['sprites']:
    item = dict(sprite)
    if sprite['name'] in images:
        image = images[sprite['name']].copy()
        rotations = {'OrbitLongDog': 90, 'OrbitRabbitPair': -12, 'OrbitBlueCat': -25,
                     'OrbitGreyCat': -135, 'OrbitWhiteDog': 180, 'OrbitFluffy': 25,
                     'OrbitGreyDog': 110, 'OrbitHappyDog': 65, 'OrbitTurtle': -60,
                     'OrbitPatchDog': 90, 'OrbitGinger': -15}
        image = image.rotate(rotations.get(sprite['name'], 0), Image.Resampling.BICUBIC, expand=True)
        image.thumbnail((224, 224) if sprite['name'] == 'ProgressCat' else (176, 176), Image.Resampling.LANCZOS)
    else:
        x, y, w, h = sprite['rect']
        image = old_page.crop((x, y, x+w, y+h))
    sprites.append((item, image))
page = Image.new('RGBA', (1024, 1024))
x = y = row_height = 4
packed = []
for sprite, image in sorted(sprites, key=lambda item: -item[1].height):
    if x+image.width+4 > 1024:
        x = 4
        y += row_height+4
        row_height = 0
    if y+image.height+4 > 1024:
        raise ValueError('Atlas overflow')
    page.paste(image, (x, y))
    sprite['rect'] = [x, y, image.width, image.height]
    packed.append(sprite)
    x += image.width+4
    row_height = max(row_height, image.height)
page = page.crop((0, 0, 1024, ((y+row_height+7)//4)*4))
manifest['sprites'] = packed
manifest['atlasSize'] = list(page.size)
manifest['artSource'] = 'ComfyUI individual animal redraw; visual anatomy/pose review in individual/review.json'
manifest['animalCompositionRotations'] = rotations
page.save(OUT/'T_Loading_Elements.png', optimize=True)
(OUT/'LoadingSlices.json').write_text(json.dumps(manifest, indent=2)+'\n', encoding='utf8')
print(json.dumps({'sprites': len(packed), 'atlasSize': list(page.size),
                  'bytes': (OUT/'T_Loading_Elements.png').stat().st_size,
                  'reviewSha256': digest(HERE/'review.json')}))
