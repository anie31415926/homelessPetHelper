import fs from 'node:fs/promises';
import path from 'node:path';
import sharp from 'sharp';

const base = 'art-source/ui-soft-20261122';
const server = 'http://127.0.0.1:8188';
const [action, slug] = process.argv.slice(2);
const jobs = JSON.parse(await fs.readFile(`${base}/jobs.json`, 'utf8'));
const job = jobs.find(item => item.slug === slug);
if (!job || !['run', 'collect'].includes(action)) throw new Error('Use run|collect with a jobs.json slug.');
const dir = `${base}/${slug}`;
await fs.mkdir(dir, { recursive: true });

async function request(endpoint, options = {}) {
    const response = await fetch(`${server}/${endpoint}`, { ...options, signal: AbortSignal.timeout(30000) });
    if (!response.ok) throw new Error(`${endpoint}: ${response.status} ${await response.text()}`);
    return response;
}

async function upload(file) {
    const form = new FormData();
    form.append('image', new Blob([await fs.readFile(file)]), path.basename(file));
    form.append('subfolder', 'ui-soft-20261122');
    form.append('overwrite', 'false');
    const result = await (await request('upload/image', { method: 'POST', body: form })).json();
    return result.subfolder ? `${result.subfolder}/${result.name}` : result.name;
}

function graphFor(images) {
    const graph = {
        '1': { class_type: 'UNETLoader', inputs: { unet_name: 'qwen_image_2.1_int8_convrot.safetensors', weight_dtype: 'default' } },
        '2': { class_type: 'CLIPLoader', inputs: { clip_name: 'qwen3vl_8b_int8_convrot.safetensors', type: 'qwen_image', device: 'default' } },
        '3': { class_type: 'VAELoader', inputs: { vae_name: 'qwen_image_2.1_vae_bf16.safetensors' } },
        '5': { class_type: 'TextEncodeQwenImage21', inputs: { clip: ['2', 0], vae: ['3', 0], prompt: job.prompt,
            negative_prompt: job.negative, resolution: 0 } },
        '6': { class_type: 'ModelSamplingAuraFlow', inputs: { model: ['1', 0], shift: 3.1 } },
        '7': { class_type: 'KSampler', inputs: { model: ['6', 0], positive: ['5', 0], negative: ['5', 1],
            latent_image: ['5', 2], seed: job.seed, steps: job.steps ?? 24, cfg: 4,
            sampler_name: 'euler', scheduler: 'simple', denoise: 1 } },
        '8': { class_type: 'VAEDecode', inputs: { samples: ['7', 0], vae: ['3', 0] } },
        '9': { class_type: 'SaveImage', inputs: { images: ['8', 0], filename_prefix: `ui-soft-20261122/${slug}` } },
    };
    images.forEach((name, index) => {
        const id = String(20 + index);
        graph[id] = { class_type: 'LoadImage', inputs: { image: name } };
        graph['5'].inputs[`images.image_${index + 1}`] = [id, 0];
    });
    return graph;
}

const exists = file => fs.access(file).then(() => true, () => false);
async function submit() {
    if (await exists(`${dir}/submission.json`)) throw new Error('Submission exists; collect it, do not enqueue a duplicate.');
    const queue = await (await request('queue')).json();
    if (queue.queue_running.length || queue.queue_pending.length) throw new Error('Queue occupied; do not mix unrelated work.');
    const images = [];
    for (const file of job.references) images.push(await upload(file));
    const graph = graphFor(images);
    await fs.writeFile(`${dir}/workflow.json`, JSON.stringify({ job, prompt: graph }, null, 2));
    const result = await (await request('prompt', { method: 'POST', headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ prompt: graph, client_id: 'ui-soft-20261122' }) })).json();
    await fs.writeFile(`${dir}/submission.json`, JSON.stringify(result, null, 2));
    console.log(JSON.stringify({ queued: slug, promptId: result.prompt_id }));
    return result;
}

async function collect(submission) {
    const history = await (await request(`history/${submission.prompt_id}`)).json();
    const result = history[submission.prompt_id];
    if (!result) return false;
    await fs.writeFile(`${dir}/history.json`, JSON.stringify(result, null, 2));
    if (result.status?.status_str === 'error') throw new Error(JSON.stringify(result.status));
    const file = result.outputs?.['9']?.images?.[0];
    if (!file) return false;
    if (!await exists(`${dir}/master.png`)) {
        const response = await request(`view?${new URLSearchParams(file)}`);
        await fs.writeFile(`${dir}/master.png`, Buffer.from(await response.arrayBuffer()));
    }
    if (!await exists(`${dir}/candidate.jpg`)) {
        await sharp(`${dir}/master.png`).flatten({ background: '#faf5e9' })
            .jpeg({ quality: 88, mozjpeg: true }).toFile(`${dir}/candidate.jpg`);
    }
    const catalog = JSON.parse(await fs.readFile('art-source/resource-catalog.json', 'utf8'));
    const asset = catalog.assets.find(item => item.id === job.assetId);
    if (!asset) throw new Error('Asset must be planned before generation.');
    const files = ['master.png', 'workflow.json', 'history.json', 'submission.json'].map((name, i) => ({
        role: i === 0 ? 'master' : i === 1 ? 'workflow' : 'generation-record', path: `${dir}/${name}`,
    }));
    asset.status = 'candidate';
    asset.files = [
        ...asset.files.filter(item => !['candidate', 'runtime'].includes(item.role) && !files.some(f => f.path === item.path)),
        { role: 'candidate', path: `${dir}/candidate.jpg` }, ...files,
    ];
    asset.generation = { tool: '本地ComfyUI Qwen Image 2.1 INT8 ConvRot',
        model: 'qwen_image_2.1_int8_convrot.safetensors', seed: job.seed,
        record: `${base}/README.md`, notes: '完整提示、参考输入与模型参数见本项workflow.json；实际历史及提交ID已保存。临时候选，未导入Unity；视觉、许可、目标交付均待审核。' };
    await fs.writeFile('art-source/resource-catalog.json', `${JSON.stringify(catalog, null, 2)}\n`);
    const metadata = await sharp(`${dir}/master.png`).metadata();
    console.log(JSON.stringify({ saved: slug, width: metadata.width, height: metadata.height,
        promptId: submission.prompt_id, seconds: result.execution_time ?? null }));
    return true;
}

const submission = action === 'run' ? await submit() : JSON.parse(await fs.readFile(`${dir}/submission.json`, 'utf8'));
const deadline = Date.now() + (action === 'run' ? 30 * 60 * 1000 : 0);
do {
    if (await collect(submission)) process.exit(0);
    if (action === 'collect') break;
    console.log(JSON.stringify({ waiting: slug, at: new Date().toISOString() }));
    await new Promise(resolve => setTimeout(resolve, 15000));
} while (Date.now() < deadline);
throw new Error('Not finished; inspect history/queue then collect the existing submission.');
