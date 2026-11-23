"""Export previews from actual Unity capture frames, with pixel-preservation checks."""
from pathlib import Path
import hashlib
import json
import sys

import numpy as np
from PIL import Image, ImageDraw

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[2]
ART = ROOT / 'unity/HomelessPetHelper/Assets/Art/Loading'
CAPTURE = ROOT / sys.argv[1] / 'LoadingEvidence'
OUT = ROOT / 'test-results/loading-comfy/individual'
OUT.mkdir(parents=True, exist_ok=True)


def sha(file):
    return hashlib.sha256(file.read_bytes()).hexdigest()


manifest = json.loads((ART/'LoadingSlices.json').read_text(encoding='utf8'))
old = json.loads((HERE/'prior-runtime/LoadingSlices.json').read_text(encoding='utf8'))
page = Image.open(ART/'T_Loading_Elements.png').convert('RGBA')
prior = Image.open(HERE/'prior-runtime/T_Loading_Elements.png').convert('RGBA')
animals = {item['sprite'] for item in json.loads((HERE/'plan.json').read_text())['jobs']}
unchanged = []
assert {item['name'] for item in old['sprites']} == {item['name'] for item in manifest['sprites']}
for item in manifest['sprites']:
    if item['name'] in animals:
        continue
    before = next(sprite for sprite in old['sprites'] if sprite['name'] == item['name'])
    def crop(source, sprite):
        x, y, width, height = sprite['rect']
        return np.asarray(source.crop((x, y, x+width, y+height)))
    assert np.array_equal(crop(prior, before), crop(page, item)), item['name']
    unchanged.append(item['name'])
assert len(unchanged) == 16
assert sha(ART/'T_Loading_Background.png') == sha(HERE/'prior-runtime/T_Loading_Background.png')
for folder in ('', 'v2', 'v3', 'v4'):
    for job in (HERE/folder).glob('*/submission.json'):
        assert (job.parent/'history.json').exists() and (job.parent/'master.png').exists(), str(job)

frames = sorted((CAPTURE/'animation').glob('frame-*.png'))
assert len(frames) == 48
images = [Image.open(file).convert('RGB') for file in frames]
assert np.abs(np.asarray(images[0], dtype=float)-np.asarray(images[-1], dtype=float)).mean() > 1
durations = [80 if i % 3 != 2 else 90 for i in range(48)]
images[0].save(OUT/'loading-preview.gif', save_all=True, append_images=images[1:],
               duration=durations, loop=0, optimize=True, disposal=2)
assert sum(durations) == 4000
with Image.open(OUT/'loading-preview.gif') as gif:
    total = 0
    for i in range(gif.n_frames):
        gif.seek(i)
        total += gif.info['duration']
    assert total == 4000

sizes = [(320,569), (360,640), (390,780), (390,845), (360,800), (768,1024)]
contact = Image.new('RGB', (1080, 640), 'white')
for index, (width, height) in enumerate(sizes):
    file = CAPTURE/f'loading-{width}x{height}-1.png'
    screenshot = Image.open(file).convert('RGB')
    assert screenshot.size == (width, height)
    screenshot.thumbnail((176, 600), Image.Resampling.LANCZOS)
    contact.paste(screenshot, (index*180+(180-screenshot.width)//2, 24))
    ImageDraw.Draw(contact).text((index*180+10, 4), f'{width}x{height}', fill='#343b3d')
contact.save(OUT/'responsive-contact.png', optimize=True)
Image.open(CAPTURE/'loading-360x640-1.png').save(OUT/'loading-static.png', optimize=True)
report = {'captureProject': CAPTURE.parent.relative_to(ROOT).as_posix(),
          'sprites': len(manifest['sprites']), 'unchangedNonAnimalPixels': unchanged,
          'backgroundUnchanged': True, 'animationFrames': len(frames), 'animationMilliseconds': 4000,
          'reviewSha256': sha(HERE/'review.json'), 'runtime': {
              file.name: {'sha256': sha(file), 'bytes': file.stat().st_size}
              for file in ART.glob('*.png')},
          'frames': {file.name: sha(file) for file in frames},
          'screenshots': {file.name: sha(file) for file in CAPTURE.glob('loading-*.png')}}
(OUT/'render-provenance.json').write_text(json.dumps(report, indent=2)+'\n', encoding='utf8')
print(json.dumps({'frames': len(frames), 'unchangedSprites': len(unchanged), 'output': str(OUT)}))
