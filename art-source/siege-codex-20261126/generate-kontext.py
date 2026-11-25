"""Fallback: generate the missing siege sprites with Flux Kontext on the running ComfyUI Desktop (8188).

The Desktop instance has no Qwen Image 2.1 weights, so this script builds a minimal Flux Kontext graph
(UNETLoader + DualCLIPLoader + ReferenceLatent) instead of reusing the Qwen workflow.
"""
import json
import time
import uuid
from pathlib import Path
from urllib.error import HTTPError
from urllib.parse import urlencode
from urllib.request import Request, urlopen

from PIL import Image

ROOT = Path(__file__).resolve().parent
ART = ROOT.parent
URL = 'http://127.0.0.1:8188'

STYLE = ('Four NEW chibi toy props for a cozy mobile game, FLAT 2D hand-painted watercolor storybook illustration, '
         'visible soft paper grain, watercolor wash edges and thin warm brown outlines, matte flat colors, absolutely no 3D render, '
         'no plastic shine, no gradients, no drop shadow, no floor, no frame, pure solid white background. '
         'Exactly four objects, each fully inside its own quadrant with wide white margins, centered, nothing crossing the middle. '
         'Slightly elevated 55-degree downward three-quarter view, fronts facing the viewer. ')

JOBS = {
    'kcx-dog-basics': (2026112601, 32, 'units',
                       STYLE +
                       'TOP LEFT: a chunky cream dog bone toy with two thick rounded knobs at each end and one soft coral stitched seam. '
                       'TOP RIGHT: a flat mint-teal flying disc with a thick rounded rim, tilted slightly so the saucer shape reads clearly. '
                       'BOTTOM LEFT: a knotted rope toy, three thick cream loops tied into one knot with short tan ends. '
                       'BOTTOM RIGHT: a heavy bone windmill toy, three cream bones arranged like a pinwheel around a small golden center button. '
                       'No people, animals, faces, text, letters, logos, signatures or watermark.'),
    'kcx-dog-support': (2026112602, 32, 'units',
                        STYLE +
                        'TOP LEFT: a clover flying disc toy, three mint-teal discs overlapping like a three-leaf clover with one small golden center button. '
                        'TOP RIGHT: a rope knot octopus toy, a chunky cream rope knot with several short thick loops radiating out and small plum tips. '
                        'BOTTOM LEFT: a stout cardboard paper-ball turret, pale warm orange pop-up box with a rounded upright hinged flap and one blue paper ball on top. '
                        'BOTTOM RIGHT: a low dark sage wooden thread frame with two rounded upright posts and a plum ball of yarn suspended between them. '
                        'No people, animals, faces, text, letters, logos, signatures or watermark.'),
    'kcx-dog-mechanisms': (2026112603, 32, 'mechanisms',
                           STYLE +
                           'TOP LEFT: a rounded cream doghouse-shaped ball launcher with a dark arched opening and one pink ball at the opening. '
                           'TOP RIGHT: a shallow cream ceramic water bowl filled with water, three pale blue soap bubbles rising above the surface. '
                           'BOTTOM LEFT: a low wooden slipper seesaw, one wide plank on a rounded pivot with a cream house slipper resting on the raised end. '
                           'BOTTOM RIGHT: an alternate cardboard paper-ball turret variant, pale sage pop-up box with a rounded flap and one warm pink paper ball. '
                           'All mechanisms look anchored and weighty, no faces, no animals, text.'),
}


def request(route, data=None, headers=None):
    try:
        with urlopen(Request(f'{URL}/{route}', data, headers or {}), timeout=60) as response:
            return response.read()
    except HTTPError as error:
        print(f'HTTP {error.code} {route}: {error.read().decode("utf-8", "replace")[:2000]}', flush=True)
        raise


def upload(file):
    boundary = uuid.uuid4().hex
    body = (f'--{boundary}\r\nContent-Disposition: form-data; name="image"; filename="{file.name}"'
            f'\r\nContent-Type: image/png\r\n\r\n').encode() + file.read_bytes() + f'\r\n--{boundary}--\r\n'.encode()
    return json.loads(request('upload/image', body, {'Content-Type': f'multipart/form-data; boundary={boundary}'}))['name']


def graph(image_name, prompt, seed, steps, size):
    width, height = size
    return {
        '1': {'class_type': 'UNETLoader', 'inputs': {'unet_name': 'flux1-dev-kontext_fp8_scaled.safetensors', 'weight_dtype': 'default'}},
        '2': {'class_type': 'DualCLIPLoader', 'inputs': {'clip_name1': 't5xxl_fp16.safetensors',
                                                        'clip_name2': 'clip_l.safetensors', 'type': 'flux'}},
        '3': {'class_type': 'VAELoader', 'inputs': {'vae_name': 'ae.safetensors'}},
        '4': {'class_type': 'LoadImage', 'inputs': {'image': image_name}},
        '5': {'class_type': 'VAEEncode', 'inputs': {'pixels': ['4', 0], 'vae': ['3', 0]}},
        '6': {'class_type': 'CLIPTextEncode', 'inputs': {'clip': ['2', 0], 'text': prompt}},
        '7': {'class_type': 'ReferenceLatent', 'inputs': {'conditioning': ['6', 0], 'latent': ['5', 0]}},
        '8': {'class_type': 'FluxGuidance', 'inputs': {'conditioning': ['7', 0], 'guidance': 3.2}},
        '9': {'class_type': 'ConditioningZeroOut', 'inputs': {'conditioning': ['6', 0]}},
        '10': {'class_type': 'EmptySD3LatentImage', 'inputs': {'width': width, 'height': height, 'batch_size': 1}},
        '11': {'class_type': 'KSampler', 'inputs': {'model': ['1', 0], 'positive': ['8', 0], 'negative': ['9', 0],
                                                   'latent_image': ['10', 0], 'seed': seed, 'steps': steps, 'cfg': 1,
                                                   'sampler_name': 'euler', 'scheduler': 'simple', 'denoise': 1}},
        '12': {'class_type': 'VAEDecode', 'inputs': {'samples': ['11', 0], 'vae': ['3', 0]}},
        '13': {'class_type': 'SaveImage', 'inputs': {'images': ['12', 0], 'filename_prefix': 'siege-codex/kcx'}},
    }


def generate(slug):
    seed, steps, ref, text = JOBS[slug]
    folder = ROOT / slug
    folder.mkdir(exist_ok=True)
    if (folder / 'master.png').exists():
        return
    source = ART / 'treehouse-field-20261125' / ref / 'master.png'
    reference = folder / 'reference.png'
    with Image.open(source) as image:
        image.convert('RGB').resize((768, 768), Image.Resampling.LANCZOS).save(reference)
    queue = json.loads(request('queue'))
    if queue.get('queue_running') or queue.get('queue_pending'):
        raise RuntimeError('queue busy')
    prompt = graph(upload(reference), text, seed, steps, (768, 768))
    (folder / 'workflow.json').write_text(json.dumps({'reference': str(source), 'graph': prompt}, ensure_ascii=False, indent=2), encoding='utf-8')
    submission = json.loads(request('prompt', json.dumps({'prompt': prompt, 'client_id': 'siege-codex-kontext'}).encode(),
                                    {'Content-Type': 'application/json'}))
    (folder / 'submission.json').write_text(json.dumps(submission, ensure_ascii=False, indent=2), encoding='utf-8')
    prompt_id = submission['prompt_id']
    print(f'{slug} submitted {prompt_id}', flush=True)
    deadline = time.monotonic() + 900
    while time.monotonic() < deadline:
        result = json.loads(request(f'history/{prompt_id}')).get(prompt_id)
        if result:
            (folder / 'history.json').write_text(json.dumps(result, ensure_ascii=False, indent=2), encoding='utf-8')
            images = result.get('outputs', {}).get('13', {}).get('images', [])
            if not images:
                raise RuntimeError(str(result.get('status')))
            (folder / 'master.png').write_bytes(request(f'view?{urlencode(images[0])}'))
            print(f'{slug} complete', flush=True)
            return
        time.sleep(3)
    raise TimeoutError(prompt_id)


if __name__ == '__main__':
    import sys
    for slug in sys.argv[1:] or JOBS:
        generate(slug)
