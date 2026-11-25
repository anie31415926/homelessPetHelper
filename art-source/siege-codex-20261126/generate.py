"""Generate the missing siege codex sprites through the running local ComfyUI (Desktop, port 8188)."""
import json
import time
import uuid
from pathlib import Path
from urllib.parse import urlencode
from urllib.error import HTTPError
from urllib.request import Request, urlopen

from PIL import Image

ROOT = Path(__file__).resolve().parent
ART = ROOT.parent
URL = 'http://127.0.0.1:8188'
TEMPLATE = ART / 'station-room-20261123/background/workflow.json'

STYLE = ('Four NEW chibi toy combat props for a cozy mobile game, hand-painted gouache watercolor, matte surfaces, '
         'rounded chunky silhouettes readable at 40px, all on pure solid white (#ffffff), no cast shadows, no floor, no frame. '
         'Exactly four objects, one entirely inside each quadrant, wide plain white gutters between them. '
         'Slightly elevated 55-degree downward three-quarter game camera, fronts facing the viewer.')

NEG = ('watermark, signature, caption, lettering, logo, UI, noisy details, photorealism, heavy black line, glossy plastic, '
       'floor, ground texture, wooden disc, colored background, cropped cutout, shadows, extra objects')

JOBS = {
    'dog-basics': (2026112601, (768, 768), 28, 'units',
                   STYLE + ' '
                   'TOP LEFT: a chunky cream dog bone toy with two thick rounded knobs at each end and one soft coral stitched seam. '
                   'TOP RIGHT: a flat mint-teal flying disc with a thick rounded rim, tilted slightly so the saucer shape reads clearly. '
                   'BOTTOM LEFT: a knotted rope toy, three thick cream loops tied into one knot with short tan ends. '
                   'BOTTOM RIGHT: a heavy bone windmill toy, three cream bones arranged like a pinwheel around a small golden center button, '
                   'noticeably wider and stronger than a single bone. '
                   'Colors clearly separate from honey wood. No people, animals, faces, text, letters, logos, signatures or watermark.'),
    'dog-support': (2026112602, (768, 768), 28, 'units',
                    STYLE + ' '
                    'TOP LEFT: a clover flying disc toy, three mint-teal discs overlapping like a three-leaf clover with one small golden center button. '
                    'TOP RIGHT: a rope knot octopus toy, a chunky cream rope knot with several short thick loops radiating out and small plum tips. '
                    'BOTTOM LEFT: a stout cardboard paper-ball turret, pale warm orange pop-up box with a rounded upright hinged flap and one blue paper ball on top. '
                    'BOTTOM RIGHT: a low dark sage wooden thread frame with two rounded upright posts and a plum ball of yarn suspended between them. '
                    'Gently rounded shapes, simple clean edges, no clutter, no scenery, no labels.'),
    'dog-mechanisms': (2026112603, (768, 768), 28, 'mechanisms',
                       STYLE + ' '
                       'TOP LEFT: a rounded cream doghouse-shaped ball launcher with a dark arched opening and one pink ball at the opening. '
                       'TOP RIGHT: a shallow cream ceramic water bowl filled with water, three pale blue soap bubbles rising above the surface. '
                       'BOTTOM LEFT: a low wooden slipper seesaw, one wide plank on a rounded pivot with a cream house slipper resting on the raised end. '
                       'BOTTOM RIGHT: an alternate cardboard paper-ball turret variant, pale sage pop-up box with a rounded flap and one warm pink paper ball. '
                       'All mechanisms look anchored and weighty, no faces, no animals, no text.'),
}


def request(route, data=None, headers=None):
    try:
        with urlopen(Request(f'{URL}/{route}', data, headers or {}), timeout=60) as response:
            return response.read()
    except HTTPError as error:
        body = error.read().decode('utf-8', 'replace')
        print(f'HTTP {error.code} {route}: {body[:3000]}', flush=True)
        raise


def upload(file):
    boundary = uuid.uuid4().hex
    body = (f'--{boundary}\r\nContent-Disposition: form-data; name="image"; filename="{file.name}"'
            f'\r\nContent-Type: image/png\r\n\r\n').encode() + file.read_bytes() + f'\r\n--{boundary}--\r\n'.encode()
    return json.loads(request('upload/image', body, {'Content-Type': f'multipart/form-data; boundary={boundary}'}))['name']


def generate(slug):
    seed, size, steps, ref, text = JOBS[slug]
    folder = ROOT / slug
    folder.mkdir(exist_ok=True)
    if (folder / 'master.png').exists():
        return
    source = ART / 'treehouse-field-20261125' / ref / 'master.png'
    reference = folder / 'reference.png'
    with Image.open(source) as image:
        image.convert('RGB').resize(size, Image.Resampling.LANCZOS).save(reference)
    queue = json.loads(request('queue'))
    if queue.get('queue_running') or queue.get('queue_pending'):
        raise RuntimeError('queue busy')
    graph = json.loads(TEMPLATE.read_text(encoding='utf-8'))['prompt']
    graph['4']['inputs']['image'] = upload(reference)
    graph['5']['inputs'].update(prompt=text, negative_prompt=NEG)
    graph['7']['inputs'].update(seed=seed, steps=steps, cfg=4)
    graph['9']['inputs']['filename_prefix'] = f'siege-codex/{slug}'
    (folder / 'submit.json').write_text(json.dumps({'graph': graph, 'reference': str(source)}, ensure_ascii=False, indent=2), encoding='utf-8')
    submission = json.loads(request('prompt', json.dumps({'prompt': graph, 'client_id': 'siege-codex-20261126'}).encode(),
                                    {'Content-Type': 'application/json'}))
    (folder / 'submission.json').write_text(json.dumps(submission, ensure_ascii=False, indent=2), encoding='utf-8')
    prompt_id = submission['prompt_id']
    print(f'{slug} submitted {prompt_id}', flush=True)
    deadline = time.monotonic() + 900
    while time.monotonic() < deadline:
        result = json.loads(request(f'history/{prompt_id}')).get(prompt_id)
        if result:
            (folder / 'history.json').write_text(json.dumps(result, ensure_ascii=False, indent=2), encoding='utf-8')
            images = result.get('outputs', {}).get('9', {}).get('images', [])
            if not images:
                raise RuntimeError(str(result.get('status')))
            (folder / 'master.png').write_bytes(request(f'view?{urlencode(images[0])}'))
            print(f'{slug} complete', flush=True)
            return
        time.sleep(3)
    raise TimeoutError(prompt_id)


def alpha_cut(image):
    """Flood-fill near-white from the border, keep interior highlights, soften one pixel."""
    rgb = image.convert('RGB')
    width, height = rgb.size
    pixels = rgb.load()
    near = lambda p: p[0] > 236 and p[1] > 236 and p[2] > 236
    stack = [(x, 0) for x in range(width)] + [(x, height - 1) for x in range(width)] + \
            [(0, y) for y in range(height)] + [(width - 1, y) for y in range(height)]
    seen = bytearray(width * height)
    cut = bytearray(width * height)
    while stack:
        x, y = stack.pop()
        index = y * width + x
        if seen[index]:
            continue
        seen[index] = 1
        if not near(pixels[x, y]):
            continue
        cut[index] = 255
        if x + 1 < width:
            stack.append((x + 1, y))
        if x:
            stack.append((x - 1, y))
        if y + 1 < height:
            stack.append((x, y + 1))
        if y:
            stack.append((x, y - 1))
    out = image.convert('RGBA')
    data = out.load()
    for y in range(height):
        for x in range(width):
            if cut[y * width + x]:
                r, g, b, _ = data[x, y]
                data[x, y] = (r, g, b, 0)
    return out


def slice_sheet(slug, cells):
    folder = ROOT / slug
    sprites = ROOT / 'sprites'
    sprites.mkdir(exist_ok=True)
    with Image.open(folder / 'master.png') as sheet:
        width, height = sheet.size
        cell_w, cell_h = width // 2, height // 2
        for index, name in enumerate(cells):
            box = ((index % 2) * cell_w, (index // 2) * cell_h, (index % 2 + 1) * cell_w, (index // 2 + 1) * cell_h)
            cell = alpha_cut(sheet.crop(box))
            bbox = cell.getbbox()
            if bbox:
                cell = cell.crop(bbox)
            cell.save(sprites / f'{name}.png')
    print(f'{slug} sliced {len(cells)}', flush=True)


CELLS = {
    'dog-basics': ['bone', 'disc', 'rope', 'bone-wheel'],
    'dog-support': ['tri-disc', 'rope-octopus', 'tower-paper', 'tower-thread'],
    'dog-mechanisms': ['tower-kennel', 'tower-bubbles', 'tower-slippers', 'tower-paper-alt'],
}

if __name__ == '__main__':
    import sys
    for slug in sys.argv[1:] or JOBS:
        generate(slug)
        slice_sheet(slug, CELLS[slug])
