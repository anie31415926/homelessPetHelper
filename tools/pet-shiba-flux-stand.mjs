import assert from 'node:assert/strict';
import {createHash} from 'node:crypto';
import {mkdir, readFile, writeFile} from 'node:fs/promises';
import sharp from 'sharp';

const origin = 'http://127.0.0.1:8196';
const work = 'test-results/shiba-flux-comfy';
const source = 'test-results/pet-roster-20261124';
const {animals} = JSON.parse(await readFile(source + '/manifest.json', 'utf8'));
const {turnaroundFile} = animals.find(a => a.id === 'dog.shiba');
const width = 720, height = 768;
const raw = {width, height, channels: 3};
const sha = bytes => createHash('sha256').update(bytes).digest('hex');
const readRgb = png => sharp(png).removeAlpha().toColourspace('srgb').raw().toBuffer();
const svg = content => Buffer.from(`<svg xmlns="http://www.w3.org/2000/svg" width="${width}" height="${height}" viewBox="0 0 728 768">${content}</svg>`);
const modes = process.argv.slice(2);
assert.ok(modes.length > 0 && modes.every(mode => ['text', 'guide', 'shape'].includes(mode)), 'Use text, guide and/or shape');

await mkdir(work, {recursive: true});
const original = await sharp(await sharp(source + '/' + turnaroundFile).resize({width: 1000}).png().toBuffer())
    .extract({left: 320, top: 87, width: 370, height: 390}).resize(width, height, {fit: 'fill'}).removeAlpha().png().toBuffer();
const mask = await sharp(svg('<rect width="728" height="768" fill="black"/>'
    + '<path fill="white" d="M 139,562 Q 205,549 305,507 L 338,524 Q 338,590 371,637 L 355,762 H 109 L 108,722 Z"/>'))
    .blur(6).extractChannel('red').raw().toBuffer();
const guide = await sharp(svg('<rect width="728" height="768" fill="#f4f4f4"/>'
    + '<path d="M 175,563 Q 226,576 286,554 L 292,707 Q 270,731 235,722 Q 218,717 228,704 L 245,694 Q 244,633 229,601 L 198,590 Z" fill="#bcbcbc"/>'
    + '<path d="M 258,559 Q 294,539 347,543 Q 348,583 333,608 Q 324,637 324,694 Q 349,713 334,729 Q 316,738 288,732 Q 270,728 274,714 Q 275,703 289,696 Q 287,652 284,615 Z" fill="#606060"/>'
    + '<path d="M 244 736 H 359" stroke="#aaaaaa" stroke-width="2"/>')).png().toBuffer();
await writeFile(work + '/identity.png', original);
await writeFile(work + '/guide.png', guide);

async function api(path, options) {
    const res = await fetch(origin + path, options);
    if (!res.ok) throw Error(path + ': ' + await res.text());
    return res;
}

async function upload(bytes) {
    const body = new FormData();
    body.append('image', new Blob([bytes], {type: 'image/png'}), 'shiba-flux-' + sha(bytes).slice(0, 16) + '.png');
    return (await (await api('/upload/image', {method: 'POST', body})).json()).name;
}

function workflow(mode, image, guideImage) {
    const prompt = mode === 'shape'
        ? 'Clean up the forelegs of the watercolor Shiba in image 1. Keep the forepaw placements in image 1: the near paw stays back under its shoulder in a standing pose. Image 2 is the original character and watercolor texture only, do not copy its forward-reaching paw. Smooth the kink in the left outline of the near shin into a gentle near-vertical line, and round the short cream paw so it rests flat. The far forepaw is only a few pale toes behind the near one. No new outline across the chest, no stump, no color block between shoulder and shin. Preserve all painted fur, proportions and cropping of image 1. Change the least possible area.'
        : 'Edit image 1, keeping its exact warm watercolor character, scale and crop. Only the front legs change. '
        + 'A relaxed four-paw stand, not walking. One short, stout near foreleg descends straight from the chest/shoulder with a continuous warm-orange outer contour and no skinny wrist. '
        + 'Its small cream paw is flat and level with the near rear paw. The far foreleg is mostly behind the near one, with just a small toe crescent visible to its left. '
        + 'Erase the old forward-reaching leg and preserve the warm paper. Leave the painted face, chest ruff, belly, two hind legs, rump, colors and spiral tail untouched.'
        + (guideImage ? ' Image 2 is a gray placement stencil in the same canvas. Dark gray is the near leg and light gray is the partly hidden far leg. Use it only for placement and overlap; render both legs in the watercolor of image 1, never gray.' : '');
    return {
        '1': {class_type: 'UNETLoader', inputs: {unet_name: 'flux-2-klein-4b.safetensors', weight_dtype: 'default'}},
        '2': {class_type: 'CLIPLoader', inputs: {clip_name: 'qwen_3_4b_fp4_flux2.safetensors', type: 'flux2', device: 'default'}},
        '3': {class_type: 'VAELoader', inputs: {vae_name: 'flux2-vae.safetensors'}},
        '4': {class_type: 'LoadImage', inputs: {image}},
        '5': {class_type: 'VAEEncode', inputs: {pixels: ['4', 0], vae: ['3', 0]}},
        '6': {class_type: 'CLIPTextEncode', inputs: {clip: ['2', 0], text: prompt}},
        '7': {class_type: 'ReferenceLatent', inputs: {conditioning: ['6', 0], latent: ['5', 0]}},
        '8': {class_type: 'BasicGuider', inputs: {model: ['1', 0], conditioning: [guideImage ? '18' : '7', 0]}},
        '9': {class_type: 'KSamplerSelect', inputs: {sampler_name: 'euler'}},
        '10': {class_type: 'Flux2Scheduler', inputs: {steps: 4, width, height}},
        '11': {class_type: 'EmptyFlux2LatentImage', inputs: {width, height, batch_size: 1}},
        '12': {class_type: 'RandomNoise', inputs: {noise_seed: 2026112611}},
        '13': {class_type: 'SamplerCustomAdvanced', inputs: {noise: ['12', 0], guider: ['8', 0],
            sampler: ['9', 0], sigmas: ['10', 0], latent_image: ['11', 0]}},
        '14': {class_type: 'VAEDecode', inputs: {samples: ['13', 0], vae: ['3', 0]}},
        '15': {class_type: 'SaveImage', inputs: {images: ['14', 0], filename_prefix: 'shiba-flux-' + mode}},
        ...(guideImage ? {
            '16': {class_type: 'LoadImage', inputs: {image: guideImage}},
            '17': {class_type: 'VAEEncode', inputs: {pixels: ['16', 0], vae: ['3', 0]}},
            '18': {class_type: 'ReferenceLatent', inputs: {conditioning: ['7', 0], latent: ['17', 0]}}
        } : {})
    };
}

const sourceName = await upload(original);
const guideName = modes.includes('guide') ? await upload(guide) : null;
for (const mode of modes) {
    const path = work + '/' + mode;
    await mkdir(path, {recursive: true});
    const shapeFile = 'test-results/shiba-pose-only-comfy/standing-geometry-preview.png';
    const shape = mode === 'shape' ? await sharp(shapeFile).resize(width, height, {fit: 'fill'}).png().toBuffer() : null;
    const graph = workflow(mode, shape ? await upload(shape) : sourceName, shape ? sourceName : mode === 'guide' ? guideName : null);
    await writeFile(path + '/workflow.json', JSON.stringify({
        identity: turnaroundFile, identitySha256: sha(original), cropAtWidth1000: [320, 87, 370, 390],
        canvas: [width, height], guideSha256: mode === 'guide' ? sha(guide) : null,
        ...(shape ? {shapeFile, shapeSha256: sha(shape)} : {}), postMaskSha256: sha(mask), graph
    }, null, 2) + '\n');
    const {prompt_id: id} = await (await api('/prompt', {method: 'POST', headers: {'Content-Type': 'application/json'},
        body: JSON.stringify({prompt: graph})})).json();
    console.log('Queued', mode, id);
    let rendered;
    for (let i = 0; i < 100; i++) {
        await new Promise(resolve => setTimeout(resolve, 3000));
        const job = (await (await api('/history/' + id)).json())[id];
        if (!job) continue;
        await writeFile(path + '/history.json', JSON.stringify(job, null, 2) + '\n');
        const file = job.outputs?.['15']?.images?.[0];
        if (!file) throw Error(JSON.stringify(job.status));
        rendered = Buffer.from(await (await api('/view?' + new URLSearchParams(file))).arrayBuffer());
        break;
    }
    assert.ok(rendered, 'ComfyUI job did not finish: ' + id);
    await writeFile(path + '/raw.png', rendered);
    const [a, b] = await Promise.all([readRgb(original), readRgb(rendered)]);
    assert.equal(a.length, b.length);
    assert.equal(a.length, mask.length * 3);
    const edited = Buffer.from(a);
    for (let i = 0; i < mask.length; i++) {
        if (!mask[i]) continue;
        const alpha = mask[i] / 255;
        for (let c = 0; c < 3; c++) edited[i * 3 + c] = Math.round(a[i * 3 + c] * (1 - alpha) + b[i * 3 + c] * alpha);
    }
    await sharp(edited, {raw}).png().toFile(path + '/locked.png');
    const saved = await readRgb(await readFile(path + '/locked.png'));
    for (let i = 0; i < mask.length; i++) {
        if (!mask[i]) assert.equal(saved.compare(a, i * 3, i * 3 + 3, i * 3, i * 3 + 3), 0);
    }
    console.log('Saved raw and mask-locked preview', mode);
}
