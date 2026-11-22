"""User-requested local ComfyUI concept batch; no Unity import or model install."""
import json
import shutil
import time
import urllib.request
from pathlib import Path
from PIL import Image

BATCH = Path(__file__).resolve().parent
ROOT = BATCH.parent.parent
INPUT = Path('C:/Users/Admin/ComfyUI-Shared/input/level-design-20261122')
INPUT.mkdir(exist_ok=True, parents=True)
API = 'http://127.0.0.1:8188'

def read_json(route):
    with urllib.request.urlopen(API + route, timeout=20) as response:
        return json.load(response)

def write_json(path, value):
    path.write_text(json.dumps(value, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')

read_json('/system_stats')
source = ROOT / 'test-results/level-visibility/final/rescue-expanded-390x844.png'
Image.open(source).convert('RGB').resize((768, 1664), Image.Resampling.LANCZOS).save(BATCH / 'layout-reference.png')
shutil.copy2(BATCH / 'layout-reference.png', INPUT / 'layout-reference.png')
shutil.copy2(ROOT / 'unity/HomelessPetHelper/Assets/Art/Outing/T_Outing_Yard.jpg', INPUT / 'yard.jpg')
base = json.loads((ROOT / 'art-source/ui-handdrawn-20261122/paper/workflow.json').read_text(encoding='utf-8'))
common = '''Use case: ui-mockup. Create one full-screen 768x1664 portrait mobile game interface concept for a cozy stray-cat-and-dog rescue game. Image 1 is the existing game layout to REDESIGN with a much larger playable scene; image 2 supplies the actual residential-yard scenery, watercolor palette and perspective. Fresh cute hand-drawn colored-pencil outlines, light watercolor, sage green foliage, warm ivory paper, peach accents, soft natural daylight, clean readable shapes. Keep the yard floor and vegetation continuous edge-to-edge behind all UI. No phone frame. The central 78 percent of screen height is unobstructed interactive outdoor scenery. Use actual paths among flowerbeds and garden furniture, never three straight graphic lines. Animals have believable natural body proportions relative to crates and potted plants, no huge heads. At the very top only two compact corner HUD clusters, a tiny pause button left and tiny objective button right; top CENTER contains scenery only, safe for a camera cutout. Do not put a wide opaque header, sidebar, huge dialog, or text panel over the field. All HUD paper has a delicate hand-drawn outline and restrained decoration. '''
prompts = {
    'rescue': common + '''Show an active rescue strategy scene: one small dusty kitten near the upper middle garden bench; three curved natural footpaths from the lower yard entrances converge toward it. Draw a few small enticing objects directly on these paths, fish treat, yarn ball and feather wand, and tiny faint paw footprints indicating direction. Bottom only: one compact horizontal tray of four small illustrated ability cards with tiny circular resource-cost icons and one small confirm paw button at the right; the tray occupies at most 12 percent of screen height. Put a slim translucent hint tag at the left edge of empty grass, clear of kitten, all paths, cards and buttons. Use icons and small numerals only, no lengthy text. The main experience is tapping the scenery itself. All scene props must remain legible at phone size.''',
    'clearing': common + '''Show the SAME yard in the preceding route-clearing stage with three garden entrances. A wooden crate partly blocks a stone path, a small pile of twigs blocks another, and weeds and a shallow muddy patch obstruct the third; other pots and garden furniture are fixed scenery. Show one crate selected with a fine mint outline and a short translucent destination marker on an adjacent free paving tile, keeping the wooden crate fully visible. A small dusty kitten peeks from behind the upper garden bench. Bottom only: a slim tray with three illustrated tools, glove, small rake and brush, occupying at most 10 percent of screen height. One small circular completion icon at bottom right. No large progress panels. Show route clearing through believable physical garden objects, not an abstract grid. Use icons and small numerals only, no long text.'''
}
negative = 'photorealism, 3d render, glossy plastic, bevel buttons, heavy black strokes, brown parchment, huge UI, broad opaque header, sidebar, large speech bubble, long text, straight route lines, abstract grid, giant animals, chibi proportions, phone frame, watermark, logo'
jobs = []
for index, (slug, prompt) in enumerate(prompts.items()):
    folder = BATCH / slug
    folder.mkdir(exist_ok=True)
    workflow = json.loads(json.dumps(base))
    workflow['job'] = dict(slug=slug, seed=2026112291 + index, steps=24,
        references=['art-source/level-design-20261122/layout-reference.png', 'unity/HomelessPetHelper/Assets/Art/Outing/T_Outing_Yard.jpg'],
        prompt=prompt, negative=negative)
    graph = workflow['prompt']
    graph['5']['inputs'].update(prompt=prompt, negative_prompt=negative)
    graph['7']['inputs']['seed'] = 2026112291 + index
    graph['20']['inputs']['image'] = 'level-design-20261122/layout-reference.png'
    graph['21']['inputs']['image'] = 'level-design-20261122/yard.jpg'
    graph['9']['inputs']['filename_prefix'] = 'level-design-20261122/' + slug
    write_json(folder / 'workflow.json', workflow)
    body = dict(prompt=graph, client_id='level-design-20261122')
    request = urllib.request.Request(API + '/prompt', data=json.dumps(body).encode(), headers={'Content-Type': 'application/json'})
    with urllib.request.urlopen(request, timeout=30) as response:
        submitted = json.load(response)
    write_json(folder / 'submission.json', submitted)
    print('Submitted', slug, submitted['prompt_id'], flush=True)
    jobs.append((slug, submitted['prompt_id']))
while jobs:
    for slug, prompt_id in jobs[:]:
        result = read_json('/history/' + prompt_id)
        if prompt_id not in result:
            continue
        entry = result[prompt_id]
        write_json(BATCH / slug / 'history.json', entry)
        if entry.get('status', {}).get('status_str') == 'error':
            raise RuntimeError('ComfyUI failed: ' + slug)
        outputs = entry.get('outputs', {}).get('9', {}).get('images', [])
        if not outputs:
            raise RuntimeError('No saved output: ' + slug)
        item = outputs[0]
        source = Path('C:/Users/Admin/ComfyUI-Shared/output') / item['subfolder'] / item['filename']
        shutil.copy2(source, BATCH / slug / 'master.png')
        Image.open(source).convert('RGB').save(BATCH / slug / 'concept.jpg', quality=93)
        print('Completed', slug, Image.open(source).size, flush=True)
        jobs.remove((slug, prompt_id))
    if jobs:
        time.sleep(8)
