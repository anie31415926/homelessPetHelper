import { access, copyFile, mkdir, readFile, writeFile } from 'node:fs/promises';

const server = process.env.COMFY_URL || 'http://127.0.0.1:8188';
if (!['127.0.0.1', 'localhost', '[::1]'].includes(new URL(server).hostname)) throw new Error('Local ComfyUI only');
const folder = 'art-source/pet-limbs';
const { source, specs } = JSON.parse(await readFile(`${folder}/spec.json`, 'utf8'));
const upload = async (path, name) => {
    const form = new FormData(); form.append('image', new Blob([await readFile(path)]), name); form.append('overwrite', 'false');
    const response = await fetch(`${server}/upload/image`, { method: 'POST', body: form });
    if (!response.ok) throw new Error(await response.text()); return (await response.json()).name;
};
const reference = await upload(source, 'pet-complete-limbs-original.png');
for (const [kind, spec] of Object.entries(specs)) {
    const output = `${folder}/${kind}`; await mkdir(output, { recursive: true });
    if (await access(`${output}/master.png`).then(() => true, () => false)) {
        // Recover the exact uploaded guide, not a subsequently refined runtime mask.
        if (!await access(`${output}/input-guide.png`).then(() => true, () => false)) {
            const workflow = JSON.parse(await readFile(`${output}/workflow.json`, 'utf8'));
            const imageResponse = await fetch(`${server}/view?${new URLSearchParams({ filename: workflow.prompt['4'].inputs.image, type: 'input' })}`);
            if (!imageResponse.ok) throw new Error('Original generation guide unavailable');
            await writeFile(`${output}/input-guide.png`, Buffer.from(await imageResponse.arrayBuffer()));
        }
        console.log('preserved existing', kind); continue;
    }
    await copyFile(`${folder}/${kind}-guide.png`, `${output}/input-guide.png`);
    const guide = await upload(`${folder}/${kind}-guide.png`, `pet-complete-${kind}-guide.png`);
    const prompt = `Image 1 is an EDIT TARGET for the SAME kitten as image 2. Image 2 is identity, anatomy, proportion and paint-style REFERENCE ONLY. ${spec.anatomy} The grey silhouette marks the entire missing/occluded surface. Existing ivory paw and narrow visible slivers belong to the original kitten: keep their scale and placement. Paint the grey area to complete this ONE connected entire limb, including the hidden side of the paw, without changing its silhouette or the first image canvas. Soft pale apricot upper fur, ivory lower fur, delicate peach outline, subtle watercolor-like shading and light orange stripes like the reference. Complete rounded shoulder or hip root with overlap margin; no cut flat stump, no exposed cross-section. Preserve EXACT image 1 footprint, root location, joint bends and paw placement. Keep the solid pale green background. Do not add a torso, another leg, head, scarf, tail, shadow, text or labels. No realistic fur or thick black contours. This must be a complete anatomical limb, not a foot with a small upper patch.`;
    const graph = {
        '1': { class_type: 'UNETLoader', inputs: { unet_name: 'qwen_image_2.1_int8_convrot.safetensors', weight_dtype: 'default' } },
        '2': { class_type: 'CLIPLoader', inputs: { clip_name: 'qwen3vl_8b_int8_convrot.safetensors', type: 'qwen_image', device: 'default' } },
        '3': { class_type: 'VAELoader', inputs: { vae_name: 'qwen_image_2.1_vae_bf16.safetensors' } },
        '4': { class_type: 'LoadImage', inputs: { image: guide } },
        '10': { class_type: 'LoadImage', inputs: { image: reference } },
        '5': { class_type: 'TextEncodeQwenImage21', inputs: { clip: ['2', 0], vae: ['3', 0], prompt,
            negative_prompt: 'torso, head, scarf, tail, extra legs, extra paw, missing shoulder, amputated stump, short ankle patch, grey unfinished area, text, watermark, checkerboard, black outline, realistic anatomy rendering',
            resolution: 768, 'images.image_1': ['4', 0], 'images.image_2': ['10', 0] } },
        '6': { class_type: 'ModelSamplingAuraFlow', inputs: { model: ['1', 0], shift: 3.1 } },
        '7': { class_type: 'KSampler', inputs: { model: ['6', 0], positive: ['5', 0], negative: ['5', 1], latent_image: ['5', 2], seed: spec.seed, steps: 24, cfg: 4, sampler_name: 'euler', scheduler: 'simple', denoise: 1 } },
        '8': { class_type: 'VAEDecode', inputs: { samples: ['7', 0], vae: ['3', 0] } },
        '9': { class_type: 'SaveImage', inputs: { images: ['8', 0], filename_prefix: `pet-limbs/complete-${kind}` } },
    };
    await writeFile(`${output}/workflow.json`, JSON.stringify({ kind, source, crop: spec.crop, prompt: graph,
        method: 'Local two-reference complete-occluded-limb edit. Visible original cores are locked during postprocessing, not by diffusion-level masking.' }, null, 2));
    const response = await fetch(`${server}/prompt`, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ prompt: graph }) });
    const submitted = await response.json(); if (!response.ok) throw new Error(JSON.stringify(submitted));
    console.log('queued complete', kind, submitted.prompt_id);
    let result;
    for (let attempt = 0; attempt < 450; attempt++) {
        await new Promise(resolve => setTimeout(resolve, 4000));
        result = (await (await fetch(`${server}/history/${submitted.prompt_id}`)).json())[submitted.prompt_id];
        if (result?.status?.status_str === 'error') throw new Error(JSON.stringify(result.status));
        if (result?.outputs?.['9']?.images?.length) break;
    }
    const image = result?.outputs?.['9']?.images?.[0]; if (!image) throw new Error(`${kind} generation timeout`);
    const imageResponse = await fetch(`${server}/view?${new URLSearchParams(image)}`); if (!imageResponse.ok) throw new Error(await imageResponse.text());
    await writeFile(`${output}/master.png`, Buffer.from(await imageResponse.arrayBuffer()));
    await writeFile(`${output}/history.json`, JSON.stringify(result, null, 2));
    console.log('saved complete', kind);
}
