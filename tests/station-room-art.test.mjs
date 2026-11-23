import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs/promises';
import crypto from 'node:crypto';
import sharp from 'sharp';

const base = 'art-source/station-room-20261123';
const json = async file => JSON.parse(await fs.readFile(`${base}/${file}`, 'utf8'));
const hash = b => crypto.createHash('sha256').update(b).digest('hex');

test('watermark repair leaves all pixels outside the declared local region unchanged', async () => {
  const original = await sharp(`${base}/reference.png`).extract({ left: 8, top: 9, width: 336, height: 600 })
    .ensureAlpha().raw().toBuffer();
  const cleaned = await sharp(`${base}/cleaned.png`).ensureAlpha().raw().toBuffer();
  let changed = 0;
  for (let y = 0; y < 600; y++) for (let x = 0; x < 336; x++) {
    const i = (y * 336 + x) * 4;
    if (x < 260 || y < 565) assert.deepEqual(cleaned.subarray(i, i + 4), original.subarray(i, i + 4));
    else if (!cleaned.subarray(i, i + 4).equals(original.subarray(i, i + 4))) changed++;
  }
  assert.ok(changed > 100);
});

test('five complete transparent furniture slices retain exact RGBA in the shared atlas', async () => {
  const manifest = await json('unity-import.json');
  const derivation = await json('derivation.json');
  assert.deepEqual(manifest.slices.map(r => r.id).sort(), ['bed', 'cabinet', 'pillow', 'plant', 'tree']);
  for (const slice of manifest.slices) {
    const piece = await sharp(`${base}/derived/${slice.id}.png`).ensureAlpha().raw().toBuffer();
    const extracted = await sharp(`${base}/T_StationRoom_Furniture.png`).extract({ left: slice.x,
      top: manifest.atlasHeight - slice.y - slice.height, width: slice.width, height: slice.height })
      .ensureAlpha().raw().toBuffer();
    assert.deepEqual(piece, extracted, slice.id);
    const alpha = piece.filter((_, index) => index % 4 === 3);
    assert.ok(alpha.includes(0) && alpha.includes(255));
    assert.ok(alpha.filter(a => a > 0).length > 500);
  }
  assert.equal(hash(await fs.readFile(`${base}/reference.png`)), derivation.sourceSha256);
  assert.equal(hash(await fs.readFile(`${base}/T_StationRoom_Furniture.png`)), derivation.atlas.sha256);
  assert.ok(derivation.atlas.bytes <= 153600);
  assert.ok(derivation.background.bytes <= 204800);
});

test('runtime imports match approved temporary derivations and native aspect', async () => {
  for (const file of ['T_StationRoom_Furniture.png', 'T_StationRoom_Background.jpg'])
    assert.deepEqual(await fs.readFile(`unity/HomelessPetHelper/Assets/Art/StationRoom/${file}`),
      await fs.readFile(`${base}/${file}`));
  const metadata = await sharp(`${base}/assembled.png`).metadata();
  assert.equal(metadata.width, 336);
  assert.equal(metadata.height, 600);
});

test('all eight actual ComfyUI jobs retain successful history, seed and model provenance', async () => {
  for (const slug of ['clean', 'background', 'bed', 'tree', 'rear', 'plant', 'empty', 'plant-local']) {
    const workflow = await json(`${slug}/workflow.json`);
    const submission = await json(`${slug}/submission.json`);
    const history = await json(`${slug}/history.json`);
    assert.equal(history.status.status_str, 'success', slug);
    assert.equal(history.prompt[1], submission.prompt_id);
    assert.equal(workflow.prompt['7'].inputs.steps, 24);
    assert.equal(workflow.prompt['1'].inputs.unet_name, 'qwen_image_2.1_int8_convrot.safetensors');
    assert.ok((await fs.stat(`${base}/${slug}/master.png`)).size > 1000);
  }
});
