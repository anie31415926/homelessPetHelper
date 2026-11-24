import {mkdir, readFile, writeFile} from 'node:fs/promises';
import {createHash} from 'node:crypto';
import sharp from 'sharp';

const server = 'http://127.0.0.1:8196';
const work = 'test-results/pet-sit-accepted/standard-stand';
const base = 'test-results/pet-roster-20261124';
const standard = 'public/pet-mesh/cat-side.png';
const roster = JSON.parse(await readFile(base + '/manifest.json', 'utf8'));
const variants = {
    tuxedo: {id: 'cat.tuxedo', file: 'identityFile', seed: 2026112471,
        identity: 'the exact original kitten: white muzzle and triangular blaze, asymmetrical black cap, large brown eyes, two original black saddle patches, black tail with its white band, pink scarf with its original knot, very short chunky white legs and oversized head'},
    shiba: {id: 'dog.shiba', file: 'turnaroundFile', seed: 2026112472, extract: true,
        identity: 'the exact Shiba Inu dog: pointed small ears, amber red watercolor fur, white cream muzzle and thick chest, canine head and shoulder profile, broad rump, and original tightly curled tail carried over its own back'}
};
const refine = process.argv.includes('--refine');
const names = process.argv.slice(2).filter(arg => arg !== '--refine');
if (names.length === 0 || names.some(name => !Object.hasOwn(variants, name))) throw Error('Supply tuxedo or shiba');

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

for (const name of names) {
    const v = variants[name];
    const file = base + '/' + roster.animals.find(a => a.id === v.id)[v.file];
    const raw = v.extract ? await sharp(await sharp(file).resize({width: 1000}).png().toBuffer())
        .extract({left: 320, top: 87, width: 370, height: 390}).png().toBuffer() : await readFile(file);
    const identity = await sharp(raw).resize({width: 768, withoutEnlargement: true}).png().toBuffer();
    const pose = await sharp(standard).flatten({background: '#f4f5ef'}).png().toBuffer();
    const path = work + '/' + name + (refine ? '-refine' : '');
    await mkdir(path, {recursive: true});
    const a = await upload(identity), b = await upload(pose);
    const prompt = 'Redraw Image 1 as ONE completed production sprite standing naturally on all four paws in strict LEFT-facing SIDE PROFILE. Preserve ' + v.identity
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
        '4': {class_type: 'LoadImage', inputs: {image: a}},
        '10': {class_type: 'LoadImage', inputs: {image: b}},
        '5': {class_type: 'TextEncodeQwenImage21', inputs: {clip: ['2', 0], prompt,
            negative_prompt: 'three-quarter, frontal, biped, sitting, walking, floating paws, broken joints, extra legs, thick outlines, photoreal, 3d, cast shadow, background elements, text, watermark',
            resolution: 768, 'images.image_1': ['4', 0], 'images.image_2': ['10', 0]}},
        '6': {class_type: 'ModelSamplingAuraFlow', inputs: {model: ['1', 0], shift: 3.1}},
        '7': {class_type: 'KSampler', inputs: {model: ['6', 0], positive: ['5', 0], negative: ['5', 1], latent_image: ['5', 2],
            seed: v.seed + (refine ? 10 : 0), steps: 24, cfg: 4, sampler_name: 'euler', scheduler: 'simple', denoise: 1}},
        '8': {class_type: 'VAEDecode', inputs: {samples: ['7', 0], vae: ['3', 0]}},
        '9': {class_type: 'SaveImage', inputs: {images: ['8', 0], filename_prefix: 'roster-standard-' + name}}
    };
    await writeFile(path + '/workflow.json', JSON.stringify({identity: file, standard, inputSha256: [sha(identity), sha(pose)], graph}, null, 2));
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
