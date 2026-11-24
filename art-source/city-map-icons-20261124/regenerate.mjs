import { mkdir, readFile, writeFile } from 'node:fs/promises';
import path from 'node:path';
import sharp from 'sharp';

const [source, work, round = 'initial'] = process.argv.slice(2);
const url = 'http://127.0.0.1:8197';
const input = path.join(work, 'comfy/input');
const evidence = path.join(work, 'generation');
await mkdir(evidence, { recursive: true });
const file = id => path.join(source, '地图小图标', `游戏界面地图图标设计 (${id}).png`);
async function prepareSheet(name, ids) {
    const sheet = await Promise.all(ids.map(async (id, index) => {
        const icon = sharp(id ? file(id) : path.join(source, '地图小图标', '游戏界面地图图标设计.png'));
        const { width, height } = await icon.metadata();
        return { input: await icon.extract({
            left: 0, top: 0, width: id === 13 ? Math.floor(width * .92) : width, height: Math.floor(height * .923),
        }).resize(352, 352, { fit: 'contain', background: '#f7eede' }).png().toBuffer(),
        left: index % 2 * 384 + 16, top: Math.floor(index / 2) * 384 + 16 };
    }));
    await sharp({ create: { width: 768, height: 768, channels: 3, background: '#f7eede' } })
        .composite(sheet).png().toFile(path.join(input, name));
}
if (round === 'initial') {
    await sharp(path.join(source, '游戏地图背景.png')).resize(704, 1040, { fit: 'fill' })
        .png().toFile(path.join(input, 'valley-original.png'));
    await prepareSheet('valley-sheet.png', [6, 12, 19, 72]);
} else {
    await prepareSheet('valley-props.png', [15, 8, 16, 18]);
    await prepareSheet('valley-fantasy.png', [1, 13, 0, 2]);
}

const template = JSON.parse(await readFile('art-source/station-room-20261123/empty/workflow.json', 'utf8')).prompt;
const jobs = round === 'initial' ? [
    {
        name: 'valley', input: 'valley-original.png', steps: 20, denoise: .92,
        prompt: 'Faithfully reproduce this exact portrait watercolor game-map painting with visibly crisp foliage, '
            + 'mountain ridges and ripple lines. Remove ONLY the small overlaid white stamp in the bottom right, '
            + 'paint continuous matching little pine trees there. Keep the identical original composition, elevation, '
            + 'shoreline outline, river winding left and center, exact winding dirt paths, cloud positions, perspective, '
            + 'colors, and scale. No buildings, icons, people, letters, frame, labels or new objects.',
    },
    {
        name: 'icons', input: 'valley-sheet.png', steps: 18, denoise: 1,
        prompt: 'Faithful cleanup of a 2 by 2 hand-painted isometric game icon sheet, same designs and colors. '
            + 'Upper left one oak tree house, upper right one dark crystal mine with a clearly visible small wooden '
            + 'mine cart and rail at the entrance, lower left one small pink heart-roof shelter, lower right one red-roof '
            + 'vegetable farm. Keep exactly four separate icons and their quadrant centers. Preserve original fine '
            + 'illustration and outlines, pure untextured white isolated background, no shadow beyond the foot of each '
            + 'object, no frames, text, marks, or new icon. Leave generous white space between quadrants.',
    },
] : [
    {
        name: 'props', input: 'valley-props.png', steps: 18, denoise: 1,
        prompt: 'Faithfully recreate the exact four isometric watercolor game icons in this 2 by 2 sheet. '
            + 'Upper-left bubbly turquoise pool; upper-right enchanted moon pond; lower-left small rainbow bridge '
            + 'with clouds; lower-right red mushroom and tiny mushrooms. Do not add new objects. Same designs, '
            + 'same silhouettes and colors, clean edges, pure white background, no lettering, no watermark.',
    },
    {
        name: 'fantasy', input: 'valley-fantasy.png', steps: 18, denoise: 1,
        prompt: 'Faithfully recreate the exact four separate watercolor game icons in this 2 by 2 sheet. '
            + 'Upper left ancient doorway with treasure, upper right violet crystal portal, bottom left floating '
            + 'sky islands and rainbow, bottom right rounded-square valley scene. Preserve original designs, '
            + 'distinct silhouettes and colors. Clean white backdrop. No lettering or watermarks.',
    },
];
const request = async (endpoint, options) => {
    const response = await fetch(url + endpoint, options);
    if (!response.ok) throw new Error(`${response.status}: ${await response.text()}`);
    return response.json();
};
const queue = await request('/queue');
if (queue.queue_running.length || queue.queue_pending.length) throw new Error('ComfyUI queue is not empty');
const submissions = [];
for (const [index, job] of jobs.entries()) {
    const node = structuredClone(template);
    node['4'].inputs.image = job.input;
    node['5'].inputs.prompt = job.prompt;
    node['5'].inputs.negative_prompt = 'text, typography, watermark, loss of original composition, blur, smudge, '
        + 'edge artifacts, changed shoreline, duplicate objects, different colors, checkerboard background';
    Object.assign(node['7'].inputs, { seed: 2026112430 + index + (round === 'initial' ? 0 : 2),
        steps: job.steps, denoise: job.denoise });
    node['9'].inputs.filename_prefix = 'map-valley/' + job.name;
    await writeFile(path.join(evidence, job.name + '-workflow.json'), JSON.stringify({ prompt: node }, null, 2));
    const submitted = await request('/prompt', {
        method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ prompt: node }),
    });
    submissions.push({ ...job, id: submitted.prompt_id });
    console.log(job.name, submitted.prompt_id);
}
await writeFile(path.join(evidence, round + '-submissions.json'), JSON.stringify(submissions, null, 2));
