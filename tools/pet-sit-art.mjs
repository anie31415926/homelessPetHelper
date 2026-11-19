import { access, mkdir, readFile, writeFile } from 'node:fs/promises';

const base = 'art-source/pet-motion/sit-rest', reference = 'public/pet-mesh/cat-side.png';
const server = process.env.COMFY_URL || 'http://127.0.0.1:8188';
if (!['127.0.0.1', 'localhost', '[::1]'].includes(new URL(server).hostname)) throw new Error('Local ComfyUI only');
await mkdir(base, { recursive: true });
if (await access(`${base}/master.png`).then(() => true, () => false)) throw new Error('Refusing overwrite');
const graph = JSON.parse(await readFile('art-source/pet-motion/side-rest/v2/workflow.json', 'utf8')).prompt;
const prompt = 'Change ONLY the pose of this exact same kitten from standing to SITTING. Keep the exact LEFT-FACING SIDE PROFILE camera, face, pale apricot orange and ivory coat, pink scarf, large warm brown eye, tiny pink nose, very large round chibi head and tiny short plump body, delicate warm peach outline and simple soft healing watercolor storybook shading. One complete kitten sitting upright on its haunches: rounded rump on the floor, naturally folded hind legs with full hind paws visible, both front legs straight down with large paws on the floor. Head held upright in left-facing side profile, ears relaxed upright, eye open. Full long striped tail rests on the floor extending to the RIGHT with gently lifted tip, not wrapped across paws. Head without ears about as wide as shoulder-to-rump length. Do NOT make the head smaller or elongate the torso. Keep the same simple sparse coat markings and smooth soft contour, not detailed realistic fur. Single connected finished illustration, not sprite sheet, not separated body parts. Entire ears, all paws, rump and tail visible with generous blank margins. Flat solid pale cyan background RGB180 225 235, no shadows, no floor line, no scene, no text, no watermark. Ivory white fur stays white, not cyan. Do not turn toward viewer or change breed.';
const form = new FormData();
form.append('image', new Blob([await readFile(reference)]), 'pet-sit-rest-reference.png'); form.append('overwrite', 'false');
const uploadResponse = await fetch(`${server}/upload/image`, { method: 'POST', body: form });
if (!uploadResponse.ok) throw new Error(await uploadResponse.text());
graph['4'].inputs.image = (await uploadResponse.json()).name;
graph['5'].inputs.prompt = prompt;
graph['5'].inputs.negative_prompt = 'standing, lying, crouching, front view, three-quarter view, realistic proportions, small head, long thin legs, extra limbs, detached parts, text, watermark, checkerboard, floor shadow, background scene, photorealism, 3d, thick black outlines';
graph['7'].inputs.seed = 2026111941; graph['9'].inputs.filename_prefix = 'pet-motion/sit-rest';
await writeFile(`${base}/workflow.json`, JSON.stringify({ reference, template: 'art-source/pet-motion/side-rest/v2/workflow.json', prompt: graph }, null, 2));
const response = await fetch(`${server}/prompt`, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ prompt: graph }) });
const submitted = await response.json();
if (!response.ok) throw new Error(JSON.stringify(submitted));
await writeFile(`${base}/submission.json`, JSON.stringify(submitted, null, 2)); console.log('queued', submitted.prompt_id);
for (let attempt = 0; attempt < 450; attempt++) {
    await new Promise(resolve => setTimeout(resolve, 4000));
    const result = (await (await fetch(`${server}/history/${submitted.prompt_id}`)).json())[submitted.prompt_id];
    if (result?.status?.status_str === 'error') throw new Error(JSON.stringify(result.status));
    const file = result?.outputs?.['9']?.images?.[0]; if (!file) continue;
    const image = await fetch(`${server}/view?${new URLSearchParams(file)}`); if (!image.ok) throw new Error(await image.text());
    await writeFile(`${base}/master.png`, Buffer.from(await image.arrayBuffer()));
    await writeFile(`${base}/history.json`, JSON.stringify(result, null, 2)); console.log('saved', `${base}/master.png`); process.exit(0);
}
throw new Error('Generation timed out; inspect the saved submission before retry');
