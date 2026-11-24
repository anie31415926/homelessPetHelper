import assert from 'node:assert/strict';
import { mkdir, readFile, writeFile } from 'node:fs/promises';
import path from 'node:path';
import sharp from 'sharp';

const [reference, work, variant = 'all'] = process.argv.slice(2);
const host = 'http://127.0.0.1:8198';
const input = path.join(work, 'comfy/input');
const records = path.join(work, 'generation');
await mkdir(records, { recursive: true });
await sharp(reference).extract({ left: 22, top: 14, width: 383, height: 582 })
    .resize(736, 1120, { fit: 'fill' }).png().toFile(path.join(input, 'storybook-reference.png'));
if (variant === 'refined') {
    await sharp(path.join(work, 'comfy/output/storybook/storybook_00001_.png'))
        .png().toFile(path.join(input, 'storybook-first-pass.png'));
}

const base = 'Redraw the reference as a single continuous portrait game world map. Match its exquisite cute chibi '
    + 'storybook illustration: rounded bushy trees, warm spring lime grass, pastel teal river on the left, '
    + 'miniature toy-like landmarks, softly hand-painted cel shading, fine clean warm contour lines, small '
    + 'colorful flowers and buttery cream winding paths. Same playful proportions, readable silhouettes and '
    + 'oblique top-down viewpoint throughout. Preserve the left river and vertically meandering land paths. '
    + 'Every location is built into the landscape: grass and flowers overlap foundations, soft contact shadows, '
    + 'paths meet doorways and entrances, the pier meets water. No large empty top area, no mountain horizon. '
    + 'Art reaches all edges, no card border, no UI, no letters, no writing, no numbers or watermark. ';
const locations = 'From top to bottom, draw: a tiny rainbow cloud amusement park by the upper left river and a '
    + 'toy ferry dock, one violet crystal mine in the upper right with a visible little mine cart and short rail, '
    + 'one single leafy oak treehouse beside the right-hand bend, one small red-roof vegetable garden courtyard '
    + 'on the middle trail, a small pink flower garden on the left lower bank, a turquoise bubbling pebble pool '
    + 'and a blue moon pond on the lower trail, one cream-and-blue miniature candy factory with a blank fascia '
    + 'on the lower right, a pastel rainbow footbridge across a small channel or trail near the bottom, and a '
    + 'small coral heart-roof animal shelter on the lower-left land near the bridge. No duplicate trees with '
    + 'doors, no second cave, no floating buildings except the cloud park. Leave clear grass near each landmark.';
const jobs = [
    { name: 'storybook', seed: 2026112472, prompt: base + locations },
    { name: 'miniature', seed: 2026112481, prompt: base + 'Keep the reference map composition and decorative scales. '
        + 'Prioritize delicate tiny details, curving bank vegetation and consistent whimsical toy scale. ' + locations },
    { name: 'refined', seed: 2026112485, input: 'storybook-first-pass.png', prompt:
        'Keep this entire cute illustrated map precisely as it is, same river, port, paths, palette, positions, '
        + 'and small drawings. Only three edits: behind the upper-right mine cart paint a clear dark opening among '
        + 'the purple crystals, as a real miniature mine tunnel cut into the existing rock. Place one very small '
        + 'red-roof cottage on the upper edge of the vegetable garden, connected to the gate. The tiny wind-bell '
        + 'garden at the path bend between the mine and the one treehouse should read as a cute park, with one small '
        + 'windmill and a short curved stone edge following the path. Leave all other landmarks, grass, rainbow '
        + 'bridge, boat and heart-roof shelter intact. No words, labels, signs, added scenery, or duplicate landmarks.' },
].filter(job => variant === 'all' || job.name === variant);
assert(jobs.length);
const template = JSON.parse(await readFile('art-source/station-room-20261123/empty/workflow.json', 'utf8')).prompt;
const get = async (endpoint, init) => {
    const response = await fetch(host + endpoint, init);
    if (!response.ok) throw new Error(await response.text());
    return response.json();
};
const queue = await get('/queue');
assert(!queue.queue_pending.length && !queue.queue_running.length);
for (const { name, prompt, seed, input: referenceFile = 'storybook-reference.png' } of jobs) {
    const graph = structuredClone(template);
    graph['4'].inputs.image = referenceFile;
    graph['5'].inputs.prompt = prompt;
    graph['5'].inputs.negative_prompt = 'decals, white sticker outline, cut-out bases, pale fringing, detached shadows, '
        + 'photo, harsh 3D, landscape horizon, rectangular island frames, text, signs with writing, watermark, '
        + 'double cave, duplicate treehouse, disconnected paths, large solid outlines, manufactured gloss';
    Object.assign(graph['7'].inputs, { seed, steps: 24, denoise: 1 });
    graph['9'].inputs.filename_prefix = 'storybook/' + name;
    await writeFile(path.join(records, name + '-workflow.json'), JSON.stringify({ prompt: graph }, null, 2) + '\n');
    const res = await get('/prompt', { method: 'POST', headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ prompt: graph }) });
    await writeFile(path.join(records, name + '-submission.json'), JSON.stringify(res, null, 2) + '\n');
    console.log(name, res.prompt_id);
}
