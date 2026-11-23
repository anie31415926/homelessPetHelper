"""Lossless-size extraction of the supplied loading illustration; no generative redraw."""
from collections import deque
from pathlib import Path
import hashlib
import json
import shutil
import sys

import numpy as np
from PIL import Image, ImageDraw, ImageFilter

HERE = Path(__file__).resolve().parent
ROOT = HERE.parent.parent
ART = ROOT / "unity/HomelessPetHelper/Assets/Art/Loading"
SOURCE = HERE / "reference.png"
if not SOURCE.exists():
    shutil.copyfile(sys.argv[1], SOURCE)
source = Image.open(SOURCE).convert("RGB")
pixels = np.asarray(source)
ART.mkdir(parents=True, exist_ok=True)
CHECK = ROOT / "test-results/loading/extraction"
CHECK.mkdir(parents=True, exist_ok=True)

# Every coordinate refers to the unscaled 261 x 451 user attachment.
regions = [
    ("House", "house", (60, 114, 201, 245)),
    ("DoorLeft", "door", (63, 208, 106, 253)),
    ("DoorRight", "door", (145, 204, 199, 258)),
    ("OrbitCat", "orbit", (77, 60, 143, 111)),
    ("OrbitRabbitPair", "orbit", (137, 63, 204, 122)),
    ("OrbitPuppyHead", "orbit", (190, 114, 234, 153)),
    ("OrbitLongDog", "orbit", (211, 145, 261, 212)),
    ("OrbitBlueCat", "orbit", (201, 209, 252, 263)),
    ("OrbitGreyCat", "orbit", (161, 252, 217, 304)),
    ("OrbitWhiteDog", "orbit", (130, 275, 165, 327)),
    ("OrbitGinger", "orbit", (85, 270, 133, 307)),
    ("OrbitTurtle", "orbit", (51, 252, 91, 298)),
    ("OrbitGreyDog", "orbit", (12, 214, 67, 263)),
    ("OrbitPatchDog", "orbit", (0, 174, 51, 220)),
    ("OrbitHappyDog", "orbit", (0, 131, 62, 178)),
    ("OrbitFluffy", "orbit", (33, 89, 88, 139)),
    ("ProgressCat", "progress", (30, 353, 89, 408)),
    ("HeartLeft", "heart", (20, 96, 36, 113)),
    ("HeartUpper", "heart", (37, 78, 54, 96)),
    ("HeartLower", "heart", (47, 291, 66, 311)),
    ("HeartRight", "heart", (235, 124, 250, 140)),
    ("StarLeft", "star", (1, 228, 17, 247)),
    ("StarUpper", "star", (177, 29, 199, 49)),
    ("StarRight", "star", (227, 98, 246, 121)),
    ("StarBottom", "star", (78, 319, 100, 341)),
    ("SparkleLeft", "star", (13, 65, 30, 86)),
    ("SparkleRight", "star", (245, 64, 261, 86)),
    ("FlowerLeft", "heart", (29, 269, 50, 293)),
    ("FlowerRight", "heart", (212, 86, 231, 106)),
]

clips = {
    "OrbitRabbitPair": [(159, 64), (173, 64), (176, 80), (183, 80), (193, 75), (202, 78),
                        (202, 89), (197, 96), (201, 107), (199, 121), (182, 120), (174, 114),
                        (151, 112), (138, 101), (142, 91), (152, 82)],
    "House": [(61, 164), (130, 114), (153, 130), (153, 121), (172, 121), (172, 141),
              (200, 162), (199, 172), (185, 180), (185, 243), (76, 243), (76, 178), (62, 175)],
    "DoorLeft": [(82, 209), (96, 213), (104, 226), (102, 236), (96, 241), (95, 251),
                 (75, 251), (69, 245), (68, 237), (64, 232), (65, 222), (72, 214)],
    "DoorRight": [(162, 206), (179, 206), (189, 213), (197, 226), (196, 239), (188, 251),
                  (181, 256), (165, 254), (155, 247), (148, 235), (147, 219), (154, 211)],
    "ProgressCat": [(49, 354), (61, 362), (69, 354), (77, 356), (82, 374), (86, 384),
                    (85, 394), (76, 403), (61, 407), (45, 402), (32, 391), (34, 374), (39, 360)],
    "OrbitRabbitDark": [(159, 64), (173, 64), (176, 80), (171, 91), (175, 100),
                        (171, 113), (151, 112), (138, 101), (142, 91), (152, 82)],
    "OrbitRabbitWhite": [(184, 79), (193, 75), (202, 78), (202, 89), (197, 96), (201, 107),
                         (199, 121), (182, 120), (173, 113), (174, 98), (180, 90)],
    "OrbitLongDog": [(216, 155), (224, 149), (236, 151), (245, 148), (255, 149), (260, 155),
                     (256, 162), (244, 166), (234, 162), (232, 177), (241, 188), (240, 199),
                     (244, 203), (239, 210), (232, 208), (224, 210), (218, 204), (219, 190)],
    "OrbitBlueCat": [(215, 219), (230, 216), (246, 212), (250, 215), (238, 222), (231, 228),
                     (231, 240), (238, 247), (231, 257), (214, 262), (203, 254), (205, 242), (210, 237)],
    "OrbitGreyCat": [(163, 281), (173, 275), (180, 264), (189, 261), (193, 255), (202, 257),
                     (205, 264), (211, 266), (213, 279), (207, 288), (193, 292), (181, 291),
                     (176, 300), (168, 300), (165, 291)],
    "OrbitWhiteDog": [(132, 279), (140, 277), (146, 280), (152, 278), (161, 279), (164, 287),
                      (161, 294), (161, 304), (158, 320), (152, 325), (140, 322), (136, 311), (136, 299)],
    "OrbitGreyDog": [(17, 243), (25, 233), (24, 226), (28, 219), (40, 217), (51, 222),
                     (52, 231), (46, 236), (51, 239), (62, 243), (64, 250), (58, 257), (47, 258),
                     (37, 254), (30, 261), (20, 260), (16, 254)],
}


def enclosed_alpha(crop, kind):
    rgb = np.asarray(crop)
    if kind in ("star", "heart"):
        # Decorations are colored, unlike the near-neutral surrounding paper.
        corners = np.array([rgb[0, 0], rgb[0, -1], rgb[-1, 0], rgb[-1, -1]], dtype=float)
        color = np.median(corners, axis=0)
        distance = np.linalg.norm(rgb.astype(float) - color, axis=2)
        return Image.fromarray(np.uint8(np.clip((distance - 18) * 14, 0, 255)))
    dark = (rgb[:, :, 0] < 170) & (rgb[:, :, 1] < 155) & (rgb[:, :, 2] < 143)
    outline = Image.fromarray(np.uint8(dark * 255)).filter(ImageFilter.MaxFilter(3))
    wall = np.asarray(largest_component(outline)) > 0
    h, w = wall.shape
    exterior = np.zeros((h, w), dtype=bool)
    queue = deque()
    for y in range(h):
        for x in (0, w - 1):
            if not wall[y, x]:
                exterior[y, x] = True
                queue.append((x, y))
    for x in range(w):
        for y in (0, h - 1):
            if not wall[y, x] and not exterior[y, x]:
                exterior[y, x] = True
                queue.append((x, y))
    while queue:
        x, y = queue.popleft()
        for nx, ny in ((x - 1, y), (x + 1, y), (x, y - 1), (x, y + 1)):
            if 0 <= nx < w and 0 <= ny < h and not wall[ny, nx] and not exterior[ny, nx]:
                exterior[ny, nx] = True
                queue.append((nx, ny))
    alpha = Image.fromarray(np.uint8(~exterior * 255))
    # Retain the original thin pale sticker edge, not the rectangular background.
    return alpha.filter(ImageFilter.MaxFilter(3)).filter(ImageFilter.GaussianBlur(.30))


def largest_component(alpha):
    a = np.asarray(alpha).copy()
    h, w = a.shape
    seen = np.zeros_like(a, dtype=bool)
    largest = []
    for y in range(h):
        for x in range(w):
            if seen[y, x] or a[y, x] < 32:
                continue
            queue = deque([(x, y)])
            component = []
            seen[y, x] = True
            while queue:
                px, py = queue.popleft()
                component.append((px, py))
                for nx, ny in ((px - 1, py), (px + 1, py), (px, py - 1), (px, py + 1)):
                    if 0 <= nx < w and 0 <= ny < h and not seen[ny, nx] and a[ny, nx] >= 32:
                        seen[ny, nx] = True
                        queue.append((nx, ny))
            if len(component) > len(largest):
                largest = component
    mask = Image.new("L", alpha.size)
    for x, y in largest:
        mask.putpixel((x, y), 255)
    return Image.fromarray(np.minimum(a, np.asarray(mask.filter(ImageFilter.MaxFilter(3)))))


sprites = []
for name, kind, box in regions:
    crop = source.crop(box).convert("RGBA")
    alpha = largest_component(enclosed_alpha(crop.convert("RGB"), kind))
    if name in ("House", "OrbitRabbitPair", "DoorLeft", "DoorRight", "ProgressCat"):
        mask = Image.new("L", crop.size)
        ImageDraw.Draw(mask).polygon([(x - box[0], y - box[1]) for x, y in clips[name]], fill=255)
        alpha = mask if name in ("House", "OrbitRabbitPair") else Image.fromarray(
            np.minimum(np.asarray(alpha), np.asarray(mask)))
    crop.putalpha(alpha)
    if name == "House":
        # Door pets overlap the original house. Fill only those occluded wall corners;
        # the actual extracted pets remain separate foreground layers.
        draw = ImageDraw.Draw(crop)
        draw.rectangle((17, 92, 43, 126), fill=(250, 229, 157, 255))
        draw.rectangle((88, 91, 124, 126), fill=(250, 229, 157, 255))
        draw.line((17, 59, 17, 126), fill=(127, 104, 57, 255), width=2)
        draw.line((119, 59, 119, 126), fill=(127, 104, 57, 255), width=2)
        draw.line((18, 126, 124, 126), fill=(127, 104, 57, 255), width=2)
        crop.putalpha(alpha)
    sprites.append({"name": name, "kind": kind, "source": list(box), "image": crop})

# Strip dynamic text and progress out of the frame instead of baking fake progress.
sign = source.crop((94, 341, 177, 365)).convert("RGBA")
sign_alpha = Image.new("L", sign.size)
ImageDraw.Draw(sign_alpha).rounded_rectangle((2, 1, 81, 23), radius=3, fill=255)
sign.putalpha(sign_alpha)
ImageDraw.Draw(sign).rectangle((5, 5, 77, 18), fill=(255, 251, 207, 255))
sprites.append({"name": "LoadingSign", "kind": "progress", "source": [94, 341, 177, 365], "image": sign})
track = source.crop((85, 374, 232, 401)).convert("RGBA")
track.putalpha(enclosed_alpha(track.convert("RGB"), "progress"))
ImageDraw.Draw(track).rounded_rectangle((0, 5, 141, 22), radius=8, fill=(255, 252, 218, 255))
sprites.append({"name": "ProgressTrack", "kind": "progress", "source": [85, 374, 232, 401], "image": track})
fill = source.crop((86, 382, 169, 392)).convert("RGBA")
sprites.append({"name": "ProgressFill", "kind": "progress", "source": [86, 382, 169, 392], "image": fill})

# Reconstruct only the paper, using the original clean edge colors and paper grain.
# This avoids a second, motionless copy of every extracted element underneath.
rng = np.random.default_rng(20261123)
bg = np.empty_like(pixels)
for y in range(source.height):
    top = max(0, y - 8)
    bottom = min(source.height, y + 9)
    samples = np.concatenate((pixels[top:bottom, :7].reshape(-1, 3),
                              pixels[top:bottom, -7:].reshape(-1, 3)))
    valid = samples[(samples[:, 0] > 230) & (samples[:, 1] > 160) & (samples[:, 2] > 90)]
    color = np.median(valid if len(valid) else samples, axis=0)
    grain = rng.normal(0, 1.6, (source.width, 1))
    bg[y] = np.uint8(np.clip(color + grain, 0, 255))
background = Image.fromarray(bg).filter(ImageFilter.GaussianBlur(.4))
# Retain the reference's bottom paper lights; no additional decorative assets are drawn.
background.paste(source.crop((0, 409, 261, 438)), (0, 409))
background.save(ART / "T_Loading_Background.png", optimize=True)

# Shelf packing with a four-pixel gutter. No resampling or invented detail.
atlas = Image.new("RGBA", (512, 512))
x = y = row_height = 4
manifest = {"sourceSize": list(source.size), "sourceSha256": hashlib.sha256(SOURCE.read_bytes()).hexdigest(),
            "atlasSize": [512, 512], "sprites": []}
for sprite in sorted(sprites, key=lambda s: -s["image"].height):
    image = sprite.pop("image")
    if x + image.width + 4 > 512:
        x = 4
        y += row_height + 4
        row_height = 0
    if y + image.height + 4 > 512:
        raise ValueError("Atlas overflow")
    atlas.paste(image, (x, y))
    sprite["rect"] = [x, y, image.width, image.height]
    manifest["sprites"].append(sprite)
    x += image.width + 4
    row_height = max(row_height, image.height)
atlas.save(ART / "T_Loading_Elements.png", optimize=True)
(HERE / "slices.json").write_text(json.dumps(manifest, indent=2) + "\n", encoding="utf-8")
(ART / "LoadingSlices.json").write_text(json.dumps(manifest, indent=2) + "\n", encoding="utf-8")
for color, name in (((54, 65, 80), "dark"), ((255, 246, 218), "light")):
    sheet = Image.new("RGBA", atlas.size, color + (255,))
    sheet.alpha_composite(atlas)
    sheet.convert("RGB").resize((1024, 1024)).save(CHECK / (name + ".png"))
composition = background.convert("RGBA")
for sprite in sorted(manifest["sprites"], key=lambda s: {"house": 0, "orbit": 1, "door": 2}.get(s["kind"], 3)):
    ax, ay, w, h = sprite["rect"]
    sx, sy, _, _ = sprite["source"]
    composition.alpha_composite(atlas.crop((ax, ay, ax + w, ay + h)), (sx, sy))
composition.resize((522, 902)).save(CHECK / "reassembled.png")
print(json.dumps({"sprites": len(sprites), "atlasUsedHeight": y + row_height,
                  "output": str(ART)}, ensure_ascii=False))
