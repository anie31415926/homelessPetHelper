import {mkdir, readFile, writeFile} from 'node:fs/promises';
import {createHash} from 'node:crypto';
import sharp from 'sharp';

const server = 'http://127.0.0.1:8196';
const work = 'test-results/pet-sit-accepted/standard-stand';
const base = 'test-results/pet-roster-20261124';
const standard = 'public/pet-mesh/cat-side.png';
const shibaStand = 'art-source/pet-roster-20261124/standard-stand/shiba-neutral/';
const roster = JSON.parse(await readFile(base + '/manifest.json', 'utf8'));
const variants = {
    tuxedo: {id: 'cat.tuxedo', file: 'identityFile', seed: 2026112471,
        identity: 'the exact original kitten: white muzzle and triangular blaze, asymmetrical black cap, large brown eyes, two original black saddle patches, black tail with its white band, pink scarf with its original knot, very short chunky white legs and oversized head'},
    shiba: {id: 'dog.shiba', file: 'turnaroundFile', seed: 2026112472, extract: true,
        identity: 'the exact Shiba Inu dog: pointed small ears, amber red watercolor fur, white cream muzzle and thick chest, canine head and shoulder profile, broad rump, and original tightly curled tail carried over its own back'},
    'shiba-neutral': {id: 'dog.shiba', file: 'turnaroundFile', seed: 2026112551, extract: true, neutral: true},
    'shiba-pose-only': {id: 'dog.shiba', file: 'turnaroundFile', seed: 2026112601, extract: true, poseOnly: true},
    'shiba-pose-prompt': {id: 'dog.shiba', file: 'turnaroundFile', seed: 2026112611, extract: true, poseOnly: true, promptStudy: true},
    'shiba-simple-stand': {id: 'dog.shiba', file: 'turnaroundFile', seed: 2026112611, extract: true, simpleStand: true},
    'shiba-simple-stand-locked': {id: 'dog.shiba', file: 'turnaroundFile', seed: 2026112611, extract: true, poseOnly: true, simpleStand: true},
    'shiba-style-en': {id: 'dog.shiba', file: 'turnaroundFile', seed: 2026112611, extract: true, simpleStand: true, styleLanguage: 'en'},
    'shiba-style-zh': {id: 'dog.shiba', file: 'turnaroundFile', seed: 2026112611, extract: true, simpleStand: true, styleLanguage: 'zh'},
    'shiba-style-first': {id: 'dog.shiba', file: 'turnaroundFile', seed: 2026112611, extract: true, simpleStand: true, styleLanguage: 'zh', styleFirst: true},
    'shiba-qwen-text': {id: 'dog.shiba', seed: 2026112611, freshStudy: true, textOnly: true},
    'shiba-qwen-ref-vae': {id: 'dog.shiba', file: 'turnaroundFile', seed: 2026112611, extract: true, simpleStand: true, freshStudy: true},
    'shiba-redraw': {id: 'dog.shiba', file: 'turnaroundFile', seed: 2026112561, extract: true, redraw: true},
    'shiba-storybook': {id: 'dog.shiba', file: 'turnaroundFile', seed: 2026112581, extract: true, storybook: true},
    'shiba-seat': {id: 'dog.shiba', file: 'turnaroundFile', seed: 2026112505, extract: true, seated: true},
    'shiba-seat-legs': {id: 'dog.shiba', file: 'turnaroundFile', seed: 2026112541, extract: true, seated: true, legStudy: true}
};
const refine = process.argv.includes('--refine');
const shibaStyleChinese = [
    '\u5c06\u539f\u56fe\u4e2d\u7684\u67f4\u72ac\u5e7c\u72ac\u6539\u6210\u81ea\u7136\u653e\u677e\u7684\u6807\u51c6\u7ad9\u59ff\uff0c\u56db\u722a\u7740\u5730\uff0c\u4fdd\u6301\u539f\u6765\u7684\u4fa7\u9762\u89c6\u89d2\u3002',
    '\u53ea\u6539\u59ff\u52bf\uff0c\u4e0d\u6539\u53d8\u89d2\u8272\u8bbe\u8ba1\uff1a\u4fdd\u7559\u539f\u56fe\u7684\u5927\u5706\u5934\u3001\u77ed\u543b\u3001\u77ed\u817f\u3001\u7d27\u51d1\u8eab\u5f62\u3001\u5976\u6cb9\u80f8\u6bdb\u548c\u5377\u5c3e\u3002',
    '\u6cbf\u7528\u539f\u56fe\u7684\u6696\u6843\u8272\u5757\u3001\u624b\u7ed8\u6c34\u5f69\u7b14\u89e6\u548c\u7ec6\u8910\u7ebf\u3002',
    '\u4e0d\u8981\u753b\u6210\u5199\u5b9e\u6210\u5e74\u72ac\u3002',
];
const names = process.argv.slice(2).filter(arg => arg !== '--refine');
if (names.length === 0 || names.some(name => !Object.hasOwn(variants, name))) throw Error('Supply a variant: ' + Object.keys(variants).join(', '));

const api = async (path, init) => {
    const response = await fetch(server + path, init);
    if (!response.ok) throw Error(await response.text());
    return response;
};
const json = value => ({method: 'POST', headers: {'Content-Type': 'application/json'}, body: JSON.stringify(value)});
const sha = value => createHash('sha256').update(value).digest('hex');
async function upload(pixels) {
    const form = new FormData();
    form.append('image', new Blob([pixels]), 'roster-stand-' + sha(pixels).slice(0, 16) + '.png');
    form.append('overwrite', 'false');
    return (await (await api('/upload/image', {method: 'POST', body: form})).json()).name;
}

async function standingSilhouette(file) {
    const {data, info: {width, height}} = await sharp(file).removeAlpha().raw().toBuffer({resolveWithObject: true});
    const gray = Buffer.alloc(width * height, 119);
    const queue = new Uint32Array(gray.length);
    let end = 0;
    const background = i => {
        if (gray[i] === 249 || data[i * 3 + 2] < 231 || data[i * 3 + 1] < 244) return;
        gray[i] = 249;
        queue[end++] = i;
    };
    // Flood only edge-connected paper so cream markings inside the figure do not become holes.
    for (let x = 0; x < width; x++) { background(x); background(gray.length - width + x); }
    for (let y = 0; y < height; y++) { background(y * width); background(y * width + width - 1); }
    for (let cursor = 0; cursor < end; cursor++) {
        const i = queue[cursor], x = i % width;
        if (x) background(i - 1);
        if (x + 1 < width) background(i + 1);
        if (i >= width) background(i - width);
        if (i + width < gray.length) background(i + width);
    }
    return sharp(gray, {raw: {width, height, channels: 1}}).median(5).png().toBuffer();
}

for (const name of names) {
    const v = variants[name];
    const animal = roster.animals.find(a => a.id === v.id);
    const file = base + '/' + animal[v.file];
    const raw = v.textOnly ? null : v.extract ? await sharp(await sharp(file).resize({width: 1000}).png().toBuffer())
        .extract({left: 320, top: 87, width: 370, height: 390}).png().toBuffer() : await readFile(file);
    const neutralSize = {width: 728, height: 768};
    const identity = raw ? await sharp(raw).resize(v.neutral || v.storybook || v.poseOnly || v.simpleStand
        ? neutralSize : {width: 768, withoutEnlargement: true}).png().toBuffer() : null;
    const poseFile = v.poseOnly || v.simpleStand || v.textOnly ? null : v.storybook ? shibaStand + 'standing-redraw.png' : v.redraw ? shibaStand + 'standing.png'
        : v.neutral ? null : v.legStudy ? work + '/shiba-seat/master.png'
        : v.seated ? (refine ? base + '/' + animal.identityFile : null) : standard;
    const pose = v.storybook ? await standingSilhouette(poseFile) : poseFile ? await sharp(poseFile).resize({width: 768, withoutEnlargement: true})
        .flatten({background: '#f4f5ef'}).png().toBuffer() : null;
    const path = work + '/' + name + (refine ? '-refine' : '');
    await mkdir(path, {recursive: true});
    const a = identity ? await upload(identity) : null, b = pose ? await upload(pose) : null;
    const legMask = v.poseOnly ? await sharp(Buffer.from(`<svg xmlns="http://www.w3.org/2000/svg" width="728" height="768" viewBox="320 87 370 390">
        <rect x="320" y="87" width="370" height="390" fill="black"/>
        <path fill="white" d="M 408,383 Q 437,378 ${refine || v.promptStudy || v.simpleStand ? '477,350 Q 490,356 491,377' : '474,365 Q 482,367 483,384'} Q 486,405 496,429 L 492,477 L 378,477 L 378,454 Q 400,427 408,383 Z"/>
        </svg>`)).blur(6).png().toBuffer()
        : v.neutral && refine ? await sharp(Buffer.from(`<svg xmlns="http://www.w3.org/2000/svg" width="728" height="768">
        <defs><linearGradient id="m" x2="0" y2="1"><stop offset=".59" stop-color="black"/>
        <stop offset=".70" stop-color="white"/></linearGradient></defs><rect width="728" height="768" fill="url(#m)"/></svg>`)).png().toBuffer() : null;
    const maskImage = legMask ? await upload(legMask) : null;
    const prompt = v.freshStudy
        ? '\u6e29\u6696\u7684\u513f\u7ae5\u7ed8\u672c\u624b\u7ed8\u6c34\u5f69\u63d2\u753b\uff0c\u7c73\u767d\u7eb8\u5e95\u3002\u67d4\u548c\u7684\u871c\u6843\u6a58\u548c\u5976\u6cb9\u767d\u5927\u8272\u5757\uff0c\u8910\u8272\u7ec6\u94c5\u7b14\u8f6e\u5ed3\uff0c\u6e05\u695a\u7684\u7b14\u5237\u8272\u6591\uff0c\u6bdb\u53d1\u753b\u6210\u5c42\u53e0\u7684\u5706\u5c16\u6bdb\u675f\u800c\u4e0d\u662f\u7ec6\u6bdb\u4e1d\u3002\u4e00\u4e2a\u5927\u5934\u77ed\u817f\u7684\u67f4\u72ac\u5e7c\u72ac\uff0c\u5706\u9f13\u9f13\u7684\u5934\u51e0\u4e4e\u548c\u8eaf\u5e72\u4e00\u6837\u9ad8\uff0c\u5706\u4eae\u68d5\u773c\u3001\u77ed\u949d\u5976\u6cb9\u8272\u543b\u90e8\u3001\u5fae\u7b11\u5c0f\u5634\u3001\u5c0f\u4e09\u89d2\u7acb\u8033\u3001\u84ec\u677e\u5976\u6cb9\u767d\u80f8\u6bdb\u3001\u7d27\u51d1\u5706\u8eab\u548c\u642d\u5728\u80cc\u4e0a\u7684\u591a\u5c42\u87ba\u65cb\u5377\u5c3e\u3002Q\u7248\u6bd4\u4f8b\uff0c\u4e0d\u662f\u6210\u5e74\u72ac\u3002\u5168\u8eab\uff0c\u5411\u5de6\u7684\u6b63\u4fa7\u89c6\u56fe\uff0c\u81ea\u7136\u653e\u677e\u7684\u6807\u51c6\u7ad9\u59ff\uff0c\u56db\u53ea\u77ed\u722a\u7740\u5730\u3002'
        : v.styleLanguage === 'zh'
        ? (v.styleFirst ? [2, 1, 3, 0].map(i => shibaStyleChinese[i]) : shibaStyleChinese).join('')
        : v.styleLanguage === 'en'
        ? 'Change the Shiba puppy in the original image to a natural relaxed standard standing pose, with all four paws planted and the same side view. Change only the pose, not the character design: keep the large round head, short muzzle, short legs, compact body, cream chest ruff and curled tail. Keep the original warm peach color blocks, hand-painted watercolor strokes and fine brown outlines. Do not make it a realistic adult dog.'
        : v.simpleStand
        ? 'Change the Shiba in the original image to a natural four-legged standing pose, viewed from the same side. Keep the original character and watercolor style unchanged.'
        : v.promptStudy
        ? (refine
            ? 'Make a small paint correction to the forefeet of this exact Shiba illustration. The puppy has finished stepping and is now standing still with its weight evenly on all four paws. Move the reaching paw back underneath its own shoulder. Draw one uninterrupted, short, thick foreleg from the orange shoulder down to the cream toes, like a soft stout column; the two sides of the forearm run nearly parallel, without a notch, pinched wrist or separate cuff. The near forepaw sits flat at the same height as the near rear paw. Tuck the far front leg directly behind the near one: only a small toe crescent and a slim outer edge are visible just to its left, not an entire forward leg. Replace the vacated step with the existing paper and shadow. Keep the original watercolor brush shapes and all other features exactly as shown.'
            : 'Replace the front legs only. Final image: exact original side-view watercolor Shiba, relaxed square stand. One near foreleg descends almost vertically from beneath the fluffy chest; the thick short forearm transitions smoothly into a broad rounded paw without a narrow wrist. Place the base of this paw on the existing floor, level with the visible near rear paw. The two forelegs almost overlap in profile, with the far toe tips peeking out directly behind the near paw. Neither front paw reaches ahead of the chest. Keep the original shoulder and chest outline above the upper foreleg, and leave all other fur, anatomy, colors, head, hind legs and tail unchanged. Match the original small wash shapes and brown pencil line. Clean away the former reaching paw without leaving a duplicate outline.')
        : v.poseOnly
        ? 'Edit ONLY the two front legs below the cream neck ruff in this original cartoon watercolor Shiba. Replace the forward-reaching step with a relaxed neutral standing pose: the near front leg stands almost vertically under the chest, its short flat paw at the same floor height as the existing near rear paw. Show the far front paw offset to the left of and behind the near one, with a small distinct toe outline. The near front leg upper edge continues into the original orange shoulder, with the same color and tapered fur shapes; no seam, gap or divided patch at the chest. Erase the old extended paw and paint the existing paper behind it. The original head, coat, chest ruff, back, hind legs, paws and curled tail remain unchanged. Same short sturdy paws and soft warm watercolor.'
        : v.storybook
        ? 'Draw the SAME cartoon Shiba puppy from color Image 1, as another pose drawn by the same artist for the same children\'s storybook. Image 1 is the exact style and character reference, not a generic dog: warm peach and apricot watercolor washes, warm brown delicate contour lines, angular scalloped groups of cream chest fluff, broad brush-shaped orange side patches, warm blush on the cheek, large round dark brown eye with a small soft highlight, tiny smile and the same layered brush shapes in the spiral tail. Keep the original huge round head, very short round muzzle and very short chunky paws. Copy the hand-painted color blocks and visible watercolor strokes of Image 1, NOT realistic hair. Image 2 is a plain gray silhouette used ONLY for the neutral four-foot stance, not a style guide. Draw a complete new continuous illustration, facing directly left with only one visible eye. Front paws stand beneath the chest without striding; near rump flows unbroken into the round thigh, bent hock and planted rear paw, one contour with no transverse line. Four distinct paws. Bare warm ivory background, no ground line, cast shadow or text.'
            + (refine ? ' Keep visibly broad warm-orange painted shape accents on the back and ear rims and bold light-versus-peach fur masses as in Image 1. Tail swirl has chunky layered tufts, not hair strands. Matte hand-painted picture-book puppy, not a rendered animal.' : '')
        : v.redraw
        ? 'Paint one new uninterrupted watercolor illustration from scratch, a production base sprite of the exact super-deformed Shiba puppy in Image 1, at rest on four paws in a strict side profile looking directly left. Image 1 is the identity: very big round head, single large round glossy brown eye, tiny stubby muzzle, soft peach honey and cream fur, layered fluffy chest and a thick tight spiral tail over the back. Image 2 is for the placement of the four feet ONLY; discard its disconnected hind-leg junction. Match source baby proportions, short low body and very short legs. The near rump is a rounded solid mass continuous with a full rounded upper thigh. Paint the warm coat and fur flow from the back across the hip and into the upper thigh without a line, gap, horizontal break, white band or sudden color change. Follow one uninterrupted outer contour down the rump, the slightly angulated hock and the short hind paw. The belly curves naturally into the front edge of the thigh. Keep the near hind thigh in front of the far leg, not hidden. Near front leg upright and directly under the shoulder, equal level near front and rear paw soles, far paws slightly offset but readable. Level back, standing still, not walking. Crisp soft pencil edge and translucent warm watercolor. Simple untextured ivory background, no ground shadow, no text.'
            + (refine ? ' Keep a small arc of soft shading around the front of the broad outer thigh, not a closed outline. A round puppy with a disproportionately large head, never an adult dog.' : '')
        : v.neutral
        ? 'Precisely edit the attached hand-painted chibi Shiba puppy sprite. Change ONLY the stance of its four legs from a walking stride to a relaxed square stand. Keep the exact original big round head, large round brown eye, tiny blunt muzzle, ear shapes, cream scalloped chest ruff, pale peach honey watercolor pigment, short torso, and thick spiral tail over the back. No redraw of the head or coat. The dog still faces directly LEFT with only one eye visible. Put the near front paw directly under the shoulder instead of reaching forward; its short shin is almost vertical. Keep the near hind leg connected from the round hip into a clearly bent hock and a short planted paw, almost under the hip. Show two far paws offset a little behind the near paws, not merged or extra. Four very short sturdy legs on one horizontal ground plane, flat paws, no step, no crouch. Keep the existing level back and compact baby proportions, same soft pencil edge and watercolor brush texture. Plain pale paper background, no drawn baseline, no shadow, no text. One complete character.'
        : v.legStudy
        ? 'Edit Image 1, the pale soft chibi watercolor puppy, changing only its posture to the side-on sit shown in Image 2. Use Image 2 ONLY for placement of legs and pelvis, NEVER copy its taller adult proportions, dark orange coat or face. Keep the very large round head, little round brown eye, extremely short blunt smiling muzzle, pale honey fur, cream chest, ears and thick curled tail of Image 1. In the new pose the chest is upright, both short front legs descend vertically and smoothly from below the chest, and their paws lie flat pointing left. Do not draw a diagonal foreleg, dog bow or straight line from throat to front toe. One broad, visibly rounded near rear thigh sits beside the belly, NOT hidden by the torso. Its narrow folded lower leg and short rear paw extend out in front of the haunches, behind the front paws. Show the entire near rear paw and its joint contour, with a clear gap between the front and rear paws. Rump, front paws and rear paw are on one ground plane; the spiral tail stays on top of the rump. Fully seated strict left profile, single character, full paws in view, same hand-painted soft storybook illustration as Image 1. Plain pale background, no shadow, no text.'
        + (refine ? ' Preserve the pale colors and compact round silhouette of Image 1. There is exactly ONE visible round eye. Show the near foreleg entirely from its rounded shoulder to its planted toes, and the curved haunch to the visible planted rear paw.' : '')
        : v.seated ? 'Change ONLY the pose of this exact watercolor Shiba puppy from standing to a relaxed fully seated left-facing side view. Keep the original head size, round eye, small short muzzle, cheek and ear silhouettes, chest ruff, reddish and cream fur colors, short thick legs and spiral tail. Do not make it realistic or adult. The dog is in a real canine sit, not a crouch: both front paws support on one floor with near foreleg mostly upright under the chest; the rounded rump actually rests on the SAME floor; the hind thigh points forward, the hock is tucked underneath, and one small rear paw peeks out ahead of the rump. The tail curls at the back with its root attached and does not prop up the body. Keep the muzzle pointing strictly LEFT. One continuous side-on illustration, full paws and ears in frame, neutral solid pale background, no floor shadow, no props, no letters.'
        + (refine ? ' Image 2 is the original character design, not a camera guide. Match its very round oversized baby head and round glossy dark eye, its tiny blunt mouth and compact paws, its pale soft colors. Preserve body scale from Image 1 and make the visible head tall and round, not a realistic adult dog. Match the strict side view of Image 1. Short neck, broad fluffy chest, compact pear-shaped body when seated; rump and paws on the same floor.' : '')
        : 'Redraw Image 1 as ONE completed production sprite standing naturally on all four paws in strict LEFT-facing SIDE PROFILE. Preserve ' + v.identity
        + '. Image 2 controls view, level standing ground, short low stance, and composition ONLY. Do not borrow face, breed, color, coat markings, scarf or tail from Image 2. '
        + 'Paint one continuous watercolor character. Keep the near hind thigh-to-heel-to-paw outline connected with a single light pencil edge and no cut seams or duplicate lines. '
        + 'The far paws overlap naturally behind the near ones. Neutral head height, hips raised like the pose guide, no walking stride and no crouch. '
        + 'Exactly one subject, whole body fits, blank solid pale neutral background, no cast shadow, no text, no sample annotations.'
        + (refine && name === 'tuxedo'
            ? ' Absolute side view like an orthographic game sprite. Exactly ONE eye is visible and the whole far cheek is hidden. Nose points directly LEFT. The kitten must be baby-round with a tiny low body, not an adult or realistic long-bodied cat. Exactly two separated dark patches on the back as in Image 1, not one merged saddle. Keep the original scarf shape without dangling large ribbons.'
            : refine ? ' Keep the head and eyes round and cute like the first reference. Make the visible white forepaw-and-shin section very short and thick. A compact juvenile Shiba, not an adult show dog, with a head as tall as the chest, a short back and low ground clearance. Do not lengthen the muzzle.' : '');
    const graph = {
        '1': {class_type: 'UNETLoader', inputs: {unet_name: 'qwen_image_2.1_int8_convrot.safetensors', weight_dtype: 'default'}},
        '2': {class_type: 'CLIPLoader', inputs: {clip_name: 'qwen3vl_8b_int8_convrot.safetensors', type: 'qwen_image', device: 'default'}},
        '3': {class_type: 'VAELoader', inputs: {vae_name: 'qwen_image_2.1_vae_bf16.safetensors'}},
        ...(a ? {'4': {class_type: 'LoadImage', inputs: {image: a}}} : {}),
        ...(b ? {'10': {class_type: 'LoadImage', inputs: {image: b}}} : {}),
        '5': {class_type: 'TextEncodeQwenImage21', inputs: {clip: ['2', 0], prompt,
            negative_prompt: v.freshStudy ? '\u5199\u5b9e\u6210\u5e74\u72ac\uff0c\u957f\u817f\uff0c\u957f\u543b\uff0c\u5c0f\u5934\uff0c\u5199\u5b9e\u6bdb\u4e1d\uff0c3D\u6e32\u67d3'
                : v.simpleStand ? '' : 'three-quarter, frontal, biped, ' + (v.seated ? 'standing, crouching, lying, raised haunches, '
                : 'sitting, walking, ') + (v.legStudy ? 'adult, narrow head, realistic long muzzle, diagonal forearm, missing hind paw, buried thigh, merged feet, ' : '')
                + (v.neutral || v.redraw ? 'adult dog, long muzzle, narrow eye, saturated orange coat, long legs, striding, lifting paw, merged feet, ' : '')
                + (v.redraw ? 'disconnected thigh, white stripe across hip, collage seam, ' : '')
                + (v.storybook ? 'individual hairs, fur strands, airbrush gradients, realistic fur, adult, long muzzle, reflective glass eye, washed-out tan, disconnected thigh, gray animal, ' : '')
                + (v.poseOnly ? 'long legs, one merged front paw, diagonal front leg, interrupted shoulder contour, new chest markings, silhouette tear, ' : '')
                + 'floating paws, broken joints, extra legs, thick outlines, photoreal, 3d, cast shadow, background elements, text, watermark',
            resolution: 768, ...(a ? {'images.image_1': ['4', 0]} : {}),
            ...(v.freshStudy && a ? {vae: ['3', 0]} : {}), ...(b ? {'images.image_2': ['10', 0]} : {})}},
        ...(v.freshStudy ? {'16': {class_type: 'QwenImage21Cache', inputs: {model: ['1', 0], device: 'off', dtype: 'default'}}} : {}),
        '6': {class_type: 'ModelSamplingAuraFlow', inputs: {model: [v.freshStudy ? '16' : '1', 0], shift: 3.1}},
        '7': {class_type: 'KSampler', inputs: {model: ['6', 0], positive: ['5', 0], negative: ['5', 1], latent_image: legMask ? ['14', 0] : ['5', 2],
            seed: v.seed + (refine && !v.promptStudy ? 10 : 0), steps: 24, cfg: 4, sampler_name: 'euler', scheduler: 'simple', denoise: 1}},
        '8': {class_type: 'VAEDecode', inputs: {samples: ['7', 0], vae: ['3', 0]}},
        '9': {class_type: 'SaveImage', inputs: {images: legMask ? ['15', 0] : ['8', 0], filename_prefix: 'roster-standard-' + name}},
        ...(legMask ? {
            '11': {class_type: 'LoadImage', inputs: {image: maskImage}},
            '12': {class_type: 'ImageToMask', inputs: {image: ['11', 0], channel: 'red'}},
            '13': {class_type: 'VAEEncode', inputs: {pixels: ['4', 0], vae: ['3', 0]}},
            '14': {class_type: 'SetLatentNoiseMask', inputs: {samples: ['13', 0], mask: ['12', 0]}},
            '15': {class_type: 'ImageCompositeMasked', inputs: {destination: ['4', 0], source: ['8', 0], x: 0, y: 0,
                resize_source: false, mask: ['12', 0]}}
        } : {})
    };
    await writeFile(path + '/workflow.json', JSON.stringify({identity: identity ? file : null,
        ...(v.textOnly ? {referenceMode: 'none'} : v.freshStudy ? {referenceMode: 'vision-plus-vae', cropAtWidth1000: [320, 87, 370, 390], neutralSize}
            : v.poseOnly ? {cropAtWidth1000: [320, 87, 370, 390], neutralSize, legMaskSha256: sha(legMask), maskScope: 'front-pair'}
            : v.simpleStand ? {cropAtWidth1000: [320, 87, 370, 390], neutralSize, maskScope: 'none'}
            : v.storybook ? {cropAtWidth1000: [320, 87, 370, 390], neutralSize, poseReference: poseFile, poseProcessing: 'paper-flood-silhouette'}
            : v.redraw ? {cropAtWidth1000: [320, 87, 370, 390], poseReference: poseFile}
            : v.neutral ? {cropAtWidth1000: [320, 87, 370, 390], neutralSize, legMaskSha256: legMask ? sha(legMask) : null}
            : v.seated ? {cropAtWidth1000: [320, 87, 370, 390], identityDetail: poseFile} : {standard}),
        inputSha256: [identity, pose].filter(Boolean).map(sha), graph}, null, 2));
    const {prompt_id: id} = await (await api('/prompt', json({prompt: graph}))).json();
    await writeFile(path + '/submission.json', JSON.stringify({id}));
    console.log('Queued', name, id);
    for (let i = 0; i < 150; i++) {
        await new Promise(resolve => setTimeout(resolve, 4000));
        const job = (await (await api('/history/' + id)).json())[id];
        if (!job) continue;
        await writeFile(path + '/history.json', JSON.stringify(job, null, 2));
        const image = job.outputs?.['9']?.images?.[0];
        if (!image) throw Error(JSON.stringify(job.status));
        await writeFile(path + '/master.png', Buffer.from(await (await api('/view?' + new URLSearchParams(image))).arrayBuffer()));
        console.log('Saved', name);
        break;
    }
    await readFile(path + '/master.png');
}
