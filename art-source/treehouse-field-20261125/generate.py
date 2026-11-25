"""Generate treehouse field candidates through the local Windows ComfyUI API."""
import json
import sys
import time
import uuid
from pathlib import Path
from urllib.parse import urlencode
from urllib.request import Request, urlopen

from PIL import Image

ROOT = Path(__file__).resolve().parent
ART = ROOT.parent
URL = 'http://127.0.0.1:8197'
REFERENCE = next((Path.home() / 'Documents' / 'xwechat_files').glob(
    'wxid_60unbizb1hdy22_1938/msg/file/2026-11/*/*/精灵树屋'))
JOBS = {
    'room': (2026112501, (640, 1152), 30, '30',
             'Make a NEW original cozy fantasy elf treehouse interior for a portrait mobile game. The attached room provides only the warm woodland watercolor palette, round window and living timber architecture. '
             'Completely redesign the composition as a playable game board: elevated three-quarter dollhouse camera tilted down 55 degrees, looking from the open near threshold toward the rear wall. '
             'One structurally plausible room inside a giant tree trunk. Visible wood walls and load-bearing curves at the top and sides only. '
             'Round clear window in the rear upper quarter, with soft daylight, and small built-in empty shelving flush in the rear wall. Small green vines and creamy flowers on rear window trim, small peripheral accents. '
             'The bottom 72 percent of this portrait picture is one wide uninterrupted flat wooden plank floor, highly visible from above, with uniform subdued honey-cream color, low-contrast seams, minimal shadows, and ample usable width from top to bottom. '
             'The vast empty floor can accommodate THREE parallel paths side by side, each freely usable from the near threshold to the rear at the same elevation. '
             'The lower edge is a broad, unobstructed entrance across the room. Keep the width of the painted floor at least 85 percent of canvas except at the very top, no pinched foreground. '
             'No props or staging on the floor: no carpet, seating, shelves, table, stool, chest, books, plants, stairs, ladder or trees on the floor. No figures, no HUD, no drawn lanes. '
             'Soft hand-painted watercolor game art, matte gouache edge details, simple chunky timber shapes. Low contrast foreground so colored toy units can stand out. Clean original illustration without any writing, signature, logo, symbols or watermark.'),
    'room-stairs': (2026112521, (640, 1152), 30, '29',
                    'Paint a NEW portrait cozy elf treehouse interior for a mobile game, as a hand painted watercolor picture-book set viewed from the open lower doorway, looking slightly down. '
                    'This is ONE real room built inside a giant hollow tree with TWO clearly connected levels, not a landscape, not a cutaway exterior. Use the style reference for the warm honey wood, round windows, vines, flower details and curved beams, and design a fresh composition. '
                    'The defining feature is a fully visible wooden staircase on the LEFT half running diagonally from the back-left edge of the ground floor up to a small built-in wooden mezzanine in the upper third. '
                    'Show the entire continuous staircase, its wide individual closed treads, one handrail, the bottom step seated on the same lower floor and the top stair flush with the upper deck. Upper deck has floorboards, balcony rail on the outside edge and an open stair landing. A believable timber beam supports it. No other staircases or ladders. '
                    'On the right a large round window and softly lit organic timber walls. The lower half is a broad irregular room entrance and unoccupied warm low contrast wooden floor, naturally laid staggered planks across the room, NO marked game routes or repeated floor lanes. Keep the main floor clear enough to place removable furniture and units later. '
                    'Cozy handcrafted matte watercolor and gouache for a family mobile game. Simplified forms, soft colored edges and warm textured wood, natural lighting. Built-in rear shelving only, no furniture, rugs, baskets, books, humans, animals, labels, signatures, watermarks or letters.'),
    'props': (2026112502, (768, 768), 26, '30',
              'Draw a fresh matching game sprite sheet of four distinct indoor treehouse cleanup obstacles, on perfectly solid pure white (#ffffff), no cast shadows, no contact shadow. '
              'Exactly four separated objects, one per quadrant, with wide plain white gutters and no panel divisions. Slightly elevated three-quarter view, consistent 55-degree downward view, fronts facing the viewer. '
              'TOP LEFT: a short leaning stack of three closed muted sage and peach hardback books, pale page edges. '
              'TOP RIGHT: a tipped-over low arched wooden milking stool, warm honey brown, simple solid silhouette. '
              'BOTTOM LEFT: a shallow oval wicker basket filled and fully covered by a draped muted pale blue blanket, with enough volume to conceal a small kitten, no animal visible. '
              'BOTTOM RIGHT: a compact pile of two pale rolled scrolls with a dropped tan fabric pouch and crumpled paper. '
              'Match cozy warm hand-painted woodland watercolor from reference, simplify shapes, keep low contrast and medium saturation so active units are easier to see. All silhouettes complete and fully inside cells. No floor, border, text, animal, watermark, labels or signature.'),
    'units': (2026112503, (768, 768), 26, None,
              'Redesign these three existing toy combat units as a bold mobile game sprite sheet. Polished cute chibi collectible toy props, hand-painted watercolor with matte material and soft rounded shapes, strongly distinct silhouettes and colors readable at 40px. '
              'Exactly one object per quadrant, pure solid white background, wide white gutters, no cast shadows, no frame. Each object appears from elevated three-quarter view at its natural horizontal ground orientation, aligned to the same floor plane. '
              'TOP LEFT: chunky orange dried fish toy, large rounded body, little fan tail, tiny stitched dark eye. TOP RIGHT: round rose pink yarn ball, large overlapping yarn curves and just one short loose end. '
              'BOTTOM LEFT: wide mint teal feather toy, puffy round feather lobes and a short pale gold handle, slight diagonal, simplified rounded silhouette. '
              'BOTTOM RIGHT: one heavyweight handcrafted low wooden rescue cart with very wide rounded cream base, muted lilac hood and two oversized plum wheels. '
              'Make all objects feel stout, wide, cuddly and weighty, fill their cells evenly, entirely in frame. Clear color separation so units stand out strongly on honey-brown wooden floor. No people, animals, faces on yarn or cart, typography, signatures or logos. Outside each cutout only pure flat white.'),
    'units-clean': (2026112506, (768, 768), 28, 'units',
                    'Production edit of this four-object 2x2 sprite sheet. Completely DELETE all four brown wooden discs, patches, ground shadows and floorboards beneath the toys. Replace the wooden surfaces with perfectly pure solid white. '
                    'Retain all four toys in their original quadrants and the same colors: orange fish, pink yarn ball, mint feather, lilac and cream cart. Preserve their entire shapes including underside and all visible wheels. '
                    'Each toy is an isolated floating cutout in featureless uniform white space; leave no wooden pixels under, beside, or behind any of the toys, including between the wheels. No platforms, no cast shadows, no gray shadows, no wood grain anywhere, no labels. Keep the Q-shaped rounded silhouettes.'),
    'support': (2026112504, (768, 768), 26, 'units',
                'Create exactly four NEW cute hand-painted chibi mobile game sprites in the same gouache watercolor rendering as the attached style example. '
                'All on a pure flat white background, wide white gutters and four quadrants, no ground or shadows. Broad 55-degree downward three-quarter game camera. '
                'TOP LEFT: mint wooden spring clip, round chunky jaws holding a small lavender button, punchy green silhouette. '
                'TOP RIGHT: rolled coral and cream knitted scarf with a generous broad loop and short fringe, rounded warm support item. '
                'BOTTOM LEFT: a stout treehouse defense mechanism, pale warm orange cardboard pop-up box with a rounded upright hinged flap and a blue paper ball, apparent wide anchored base. '
                'BOTTOM RIGHT: a stout treehouse defense mechanism, low dark sage wooden thread frame with two rounded upright posts and a plum ball of yarn suspended between them, apparent wide anchored base. '
                'Strong distinct clean silhouettes readable on a small phone, no text, letters, dirt, signatures, logo, faces, limbs, platforms, speckles, or extra pieces.'),

    'mechanisms': (2026112505, (768, 768), 26, 'units',
                   'Four NEW chibi toy combat sprites in cozy hand-painted gouache watercolor, same 55-degree downward three-quarter game camera as the style reference. '
                   'Exactly four distinct objects on solid white, one entirely within each quadrant, wide empty gutters, no cast shadows. '
                   'TOP LEFT: sturdy short tilted ochre scratching post on a thick flat honey wooden base, a large rounded pink paw cushion at the end. '
                   'TOP RIGHT: a round warm rose lion-shaped yarn toy, fluffy circular yarn mane and one simple cute cream stitched lion face. '
                   'BOTTOM LEFT: three stubby orange dried fish toys tied with one muted coral ribbon, a silhouette noticeably wider than a single fish. '
                   'BOTTOM RIGHT: stout teal feather pinwheel made of three round feather lobes and a small golden central button. '
                   'Gently rounded chunky shapes, simple clean edges, read at 40 pixels. No clutter, scenery, people, living animals, writings, letters, logos or signatures.'),

}


def request(route, data=None, headers=None):
    with urlopen(Request(f'{URL}/{route}', data, headers or {}), timeout=30) as response:
        return response.read()


def write_json(file, data):
    file.write_text(json.dumps(data, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')


def upload(file):
    boundary = uuid.uuid4().hex
    body = (f'--{boundary}\r\nContent-Disposition: form-data; name="image"; filename="{file.name}"'
            f'\r\nContent-Type: image/png\r\n\r\n').encode() + file.read_bytes() + f'\r\n--{boundary}--\r\n'.encode()
    return json.loads(request('upload/image', body, {'Content-Type': f'multipart/form-data; boundary={boundary}'}))['name']


def generate(slug):
    seed, size, steps, ref_no, text = JOBS[slug]
    folder = ROOT / slug
    folder.mkdir(exist_ok=True)
    if (folder / 'master.png').exists():
        return
    submission_path = folder / 'submission.json'
    if submission_path.exists():
        submission = json.loads(submission_path.read_text(encoding='utf-8'))
    else:
        source = next(REFERENCE.glob(f'*({ref_no}).png')) if ref_no and ref_no.isdecimal() else (
            ROOT / 'units/master.png' if ref_no == 'units' else ART / 'field-props-20261122/cat-toys/master.png')
        reference = folder / f'treehouse-{slug}-reference.png'
        with Image.open(source) as image:
            if ref_no and ref_no.isdecimal():
                # Avoid copying footer lettering into the conditioning input.
                image = image.crop((0, 0, image.width, int(image.height * .935)))
            if slug == 'room-stairs':
                image.thumbnail((480, 640), Image.Resampling.LANCZOS)
                guide = Image.new('RGB', size, image.convert('RGB').getpixel((0, 0)))
                guide.paste(image.convert('RGB'), ((size[0] - image.width) // 2, (size[1] - image.height) // 2))
                guide.save(reference)
            else:
                image.convert('RGB').resize(size, Image.Resampling.LANCZOS).save(reference)
        queue = json.loads(request('queue'))
        if queue['queue_running'] or queue['queue_pending']:
            raise RuntimeError('Local queue busy')
        template = ART / 'station-room-20261123/background/workflow.json'
        graph = json.loads(template.read_text(encoding='utf-8'))['prompt']
        graph['4']['inputs']['image'] = upload(reference)
        graph['5']['inputs'].update(prompt=text, negative_prompt=(
            'watermark, signature, caption, lettering, logo, UI, noisy details, photorealism, heavy black line, glossy plastic' +
            (', staircase, ladder, chairs, table, rug, props on playable floor, ground at different heights' if slug == 'room' else ', broken or floating steps, watermarks, floor lane markings, rugs' if slug == 'room-stairs' else ', ground texture, colored background, cropped cutout, floorboards, wooden discs, backing floor, shadows')))
        graph['7']['inputs'].update(seed=seed, steps=steps, cfg=4)
        graph['9']['inputs']['filename_prefix'] = f'treehouse-field/{slug}'
        write_json(folder / 'workflow.json', {'source': source.name, 'localReference': reference.name,
                                              'size': size, 'prompt': graph})
        submission = json.loads(request('prompt', json.dumps({'prompt': graph, 'client_id': 'treehouse-field-20261125'}).encode(),
                                        {'Content-Type': 'application/json'}))
        write_json(submission_path, submission)
    prompt_id = submission['prompt_id']
    print(f'{slug} submitted: {prompt_id}', flush=True)
    deadline = time.monotonic() + 1200
    while time.monotonic() < deadline:
        result = json.loads(request(f'history/{prompt_id}')).get(prompt_id)
        if result:
            write_json(folder / 'history.json', result)
            output = result.get('outputs', {}).get('9', {}).get('images', [])
            if not output:
                raise RuntimeError(result['status'])
            (folder / 'master.png').write_bytes(request(f'view?{urlencode(output[0])}'))
            with Image.open(folder / 'master.png') as image:
                image.convert('RGB').save(folder / 'candidate.jpg', quality=91, optimize=True)
            print(f'{slug} complete', flush=True)
            return
        time.sleep(3)
    raise TimeoutError(f'{prompt_id}: rerun to collect, do not resubmit')


if __name__ == '__main__':
    for job in sys.argv[1:] or JOBS:
        generate(job)
