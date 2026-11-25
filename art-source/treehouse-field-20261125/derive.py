"""Extract transparent sprites and stage a noninteractive, common-scale preview."""
import hashlib
import json
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageFilter
from scipy import ndimage

ROOT = Path(__file__).resolve().parent
GROUPS = {
    'props': ('books', 'stool', 'blanket-basket', 'scrolls'),
    'units-clean': ('fish', 'yarn', 'feather', 'heavy-cart'),
    'support': ('clip', 'scarf', 'tower-paper', 'tower-thread'),
    'mechanisms': ('tower-scratch', 'yarn-lion', 'fish-bundle', 'feather-wheel'),
}


def cutout(cell):
    rgb = np.asarray(cell.convert('RGB'), dtype=np.float32)
    ink = 255 - rgb.min(axis=2)
    # Remove flat near-white backing, including the open space between stool legs.
    mask = ink > 24
    labels, count = ndimage.label(mask)
    if not count:
        raise ValueError('No object in cell')
    areas = np.bincount(labels.ravel())
    keep = np.where(areas >= 45)[0]
    solid = np.isin(labels, keep[keep != 0])
    # Fill tiny specks in the object, not the visibly open holes in its silhouette.
    holes, _ = ndimage.label(ndimage.binary_fill_holes(solid) & ~solid)
    area = np.bincount(holes.ravel())
    solid |= (holes != 0) & (area[holes] < 145)
    interior = ndimage.binary_erosion(solid, iterations=2)
    near = ndimage.binary_dilation(solid, iterations=1)
    # Estimate the matte by projecting edge pixels onto the line from white to nearby ink.
    if not interior.any():
        raise ValueError('No opaque interior in cell')
    nearest = ndimage.distance_transform_edt(~interior, return_distances=False, return_indices=True)
    foreground = rgb[tuple(nearest)]
    delta = 255 - foreground
    coverage = ((255 - rgb) * delta).sum(axis=2) / np.maximum((delta ** 2).sum(axis=2), 1)
    alpha = np.where(interior, 1, np.where(near & (ink > 10), np.clip(coverage, 0, 1), 0))
    unmatted = np.where(interior[..., None], rgb, foreground)
    rgba = np.dstack((unmatted, alpha * 255)).round().astype('uint8')
    sprite = Image.fromarray(rgba, 'RGBA')
    bounds = sprite.getbbox()
    if not bounds or min(bounds[0], bounds[1], cell.width - bounds[2], cell.height - bounds[3]) < 3:
        raise ValueError(f'Object clipped by cell edge: {bounds}')
    sprite = sprite.crop(bounds)
    sprite.thumbnail((116, 116), Image.Resampling.LANCZOS)
    padded = Image.new('RGBA', (sprite.width + 8, sprite.height + 8))
    padded.alpha_composite(sprite, (4, 4))
    return padded, bounds


def scene(room, sprites, placements):
    canvas = room.copy()
    for sprite_id, x, foot, width in sorted(placements, key=lambda item: item[2]):
        sprite = sprites[sprite_id]
        sprite = sprite.resize((width, round(sprite.height * width / sprite.width)), Image.Resampling.LANCZOS)
        left, top = round(x - width / 2), foot - sprite.height
        shadow = Image.new('RGBA', (width + 16, 30))
        ImageDraw.Draw(shadow).ellipse((10, 8, width + 6, 21), fill=(89, 59, 33, 43))
        canvas.alpha_composite(shadow.filter(ImageFilter.GaussianBlur(4)), (left - 8, foot - 18))
        canvas.alpha_composite(sprite, (left, top))
    return canvas


def pack(sprites):
    atlas = Image.new('RGBA', (512, 512))
    rects = {}
    for i, (sprite_id, sprite) in enumerate(sprites.items()):
        # A fixed 128px tile puts at least 4px of alpha on all sides of the artwork.
        x = i % 4 * 128 + (128 - sprite.width) // 2
        y = i // 4 * 128 + (128 - sprite.height) // 2
        atlas.alpha_composite(sprite, (x, y))
        rects[sprite_id] = [x, 512 - y - sprite.height, sprite.width, sprite.height]
    # Existing palette renderers sample the bottom-left texel for solid quads.
    ImageDraw.Draw(atlas).rectangle((0, 508, 3, 511), fill=(255, 255, 255, 255))
    atlas.save(ROOT / 'treehouse-atlas.png', optimize=True)
    return rects


def render_stair_preview(sprites):
    folder = ROOT / 'room-stairs'
    source = folder / 'master.png'
    if not source.is_file():
        return
    with Image.open(source) as master:
        room = master.convert('RGBA')
    # All feet sit on the lower floor; the landing is visible without implying traversal.
    cleanup = [('blanket-basket', 404, 726, 119), ('books', 503, 805, 64),
               ('stool', 286, 846, 75), ('scrolls', 349, 941, 66), ('books', 179, 920, 62)]
    battle = [('books', 507, 819, 63), ('fish', 397, 831, 95), ('yarn', 221, 904, 86),
              ('feather', 453, 929, 96), ('heavy-cart', 326, 1000, 128),
              ('tower-paper', 428, 734, 86), ('tower-thread', 324, 779, 90)]
    preview = Image.new('RGB', (768, 691))
    for x, placements in ((0, cleanup), (384, battle)):
        shot = scene(room, sprites, placements)
        preview.paste(shot.convert('RGB').resize((384, 691), Image.Resampling.LANCZOS), (x, 0))
    preview.save(folder / 'field-preview.jpg', quality=91, optimize=True)
    record = {'source': 'master.png', 'sha256': hashlib.sha256(source.read_bytes()).hexdigest(),
              'cleanup': cleanup, 'battle': battle}
    (folder / 'preview-layout.json').write_text(json.dumps(record, indent=2) + '\n', encoding='utf-8')


def main():
    sprites = {}
    records = []
    for group, ids in GROUPS.items():
        source = ROOT / group / 'master.png'
        if not source.exists():
            continue
        digest = hashlib.sha256(source.read_bytes()).hexdigest()
        with Image.open(source) as master:
            if master.size != (768, 768):
                raise ValueError(f'Unexpected size {master.size} for {group}')
            for i, sprite_id in enumerate(ids):
                split_x = 414 if group == 'mechanisms' and i < 2 else 384
                split_y = 357 if group == 'support' else 384
                left, right = (0, split_x) if i % 2 == 0 else (split_x, 768)
                top, bottom = (0, split_y) if i < 2 else (split_y, 768)
                cell = (left, top, right, bottom)
                try:
                    sprite, bounds = cutout(master.crop(cell))
                except ValueError as error:
                    raise ValueError(f'{group}/{sprite_id}: {error}') from error
                output = ROOT / group / f'{sprite_id}.png'
                sprite.save(output, optimize=True)
                sprites[sprite_id] = sprite
                records.append({'id': sprite_id, 'source': f'{group}/master.png', 'sha256': digest,
                                'cellLTRB': cell, 'alphaBoundsLTRB': bounds,
                                'output': f'{group}/{output.name}', 'size': sprite.size,
                                'pivot': [0.5, 0.0]})
    room = Image.open(ROOT / 'room/master.png').convert('RGBA')
    cleanup = [('blanket-basket', 322, 463, 134), ('books', 157, 578, 77),
               ('stool', 474, 641, 86), ('books', 330, 722, 74), ('scrolls', 215, 863, 87),
               ('scrolls', 464, 809, 78), ('books', 473, 520, 71)]
    battle = [('books', 528, 778, 64), ('scrolls', 97, 760, 64), ('fish', 155, 754, 107),
              ('yarn', 314, 815, 102), ('feather', 474, 863, 105), ('heavy-cart', 318, 979, 148),
              ('tower-paper', 242, 586, 111), ('tower-thread', 383, 662, 109),
              ('tower-scratch', 500, 540, 100)]
    preview = Image.new('RGB', (768, 692), '#eadabc')
    for x, placements in ((0, cleanup), (384, battle)):
        shot = scene(room, sprites, [p for p in placements if p[0] in sprites])
        preview.paste(shot.convert('RGB').resize((384, 691), Image.Resampling.LANCZOS), (x, 0))
    preview.save(ROOT / 'field-preview.jpg', quality=92, optimize=True)
    if len(sprites) == 16:
        render_stair_preview(sprites)
    rects = pack(sprites) if len(sprites) == 16 else {}
    (ROOT / 'derivation.json').write_text(json.dumps({'cutouts': records, 'atlasRectsXYWHBottomLeft': rects,
                                                     'preview': {'cleanup': cleanup, 'battle': battle}},
                                                     indent=2) + '\n', encoding='utf-8')


if __name__ == '__main__':
    main()
