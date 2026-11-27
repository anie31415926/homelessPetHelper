import test from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import { createReaction } from '../src/pet-affection/reaction.js';
import { Mesh, PlaneGeometry, Vector3 } from 'three';
import sharp from 'sharp';
import { deformPoint, mountTailPlane, tailArt, tailHinge, tailSwing } from '../src/pet-affection/rig.js';

test('calm until a valid move arrives', () => {
  const r = createReaction();
  r.touch(0);
  assert.deepEqual(r.sample(0), { face: 'calm', active: true, intensity: 0, wag: 0 });
  assert.equal(r.stroke(0, 0), false);
  assert.equal(r.stroke(0, -12), false);
  assert.equal(r.stroke(0, NaN), false);
  assert.equal(r.stroke(0, Infinity), false);
  assert.deepEqual(r.sample(900), { face: 'calm', active: true, intensity: 0, wag: 0 });
  assert.equal(r.stroke(900, 1), false);
  assert.equal(r.sample(900).face, 'calm');
});

test('soft needs 38px and 160ms together', () => {
  const r = createReaction();
  r.touch(0);
  assert.equal(r.stroke(0, 36), false);
  assert.equal(r.stroke(120, 36), false);
  assert.equal(r.sample(120).face, 'calm');
  assert.equal(r.stroke(160, 2), false);
  assert.equal(r.sample(160).face, 'soft');
  assert.equal(r.sample(160).intensity, 0.45);
  assert.equal(r.stroke(160, 8), false);
  assert.equal(r.stroke(170, 12), false);
  assert.equal(r.stroke(180, 12), false);
  assert.equal(r.sample(180).face, 'soft');
  let pulsed = false;
  for (let t = 200; t <= 700; t += 20) {
    if (r.stroke(t, 4)) { pulsed = true; break; }
  }
  assert.equal(pulsed, true);
  assert.equal(r.sample(540).face, 'happy');
});

test('caps each event and cannot skip straight to happy', () => {
  const r = createReaction();
  r.touch(0);
  assert.equal(r.stroke(0, 500), false);
  assert.equal(r.stroke(10, 500), false);
  assert.equal(r.stroke(20, 500), false);
  assert.equal(r.sample(20).face, 'calm');
  assert.equal(r.stroke(160, 1), false);
  assert.equal(r.sample(160).face, 'soft');
  assert.equal(r.stroke(400, 1), false);
  assert.equal(r.sample(400).face, 'soft');
  assert.equal(r.stroke(540, 1), true);
});

test('soft must be visible 260ms before happy', () => {
  const r = createReaction();
  r.touch(0);
  for (let t = 0; t <= 370; t += 10) r.stroke(t, 1);
  assert.equal(r.sample(370).face, 'soft');
  assert.equal(r.stroke(400, 36), false);
  assert.equal(r.stroke(430, 36), false);
  assert.equal(r.stroke(540, 36), false);
  assert.equal(r.sample(540).face, 'soft');
  assert.equal(r.stroke(600, 36), false);
  assert.equal(r.stroke(630, 36), true);
});

test('long pauses wipe partial progress and eventually clear the smile', () => {
  const r = createReaction();
  r.touch(0);
  r.stroke(0, 36);
  r.stroke(100, 36);
  assert.equal(r.sample(100).face, 'calm');
  r.stroke(600, 30);
  assert.equal(r.sample(600).face, 'calm');
  assert.equal(r.stroke(660, 30), false);
  assert.equal(r.stroke(700, 30), false);
  assert.equal(r.sample(700).face, 'calm');
  assert.equal(r.stroke(760, 30), false);
  assert.equal(r.sample(760).face, 'soft');
  assert.equal(r.stroke(5000, 20), false);
  assert.equal(r.sample(5000).face, 'calm');
  assert.equal(r.stroke(5160, 30), false);
  assert.equal(r.sample(5160).face, 'soft');
});

test('release stops input and drops happy to soft', () => {
  const r = createReaction();
  r.touch(0);
  for (let t = 0; t <= 120; t += 40) r.stroke(t, 36);
  assert.equal(r.stroke(160, 36), false);
  assert.equal(r.stroke(540, 36), true);
  assert.equal(r.sample(540).face, 'happy');
  assert.equal(r.sample(540).wag, 1);
  r.release(540);
  assert.deepEqual(r.sample(540), { face: 'soft', active: false, intensity: 0.45, wag: 1 });
  assert.ok(r.sample(840).wag > 0);
  assert.ok(r.sample(840).wag < 1);
  assert.equal(r.sample(1390).wag, 0);
  assert.equal(r.sample(1390).face, 'soft');
  assert.equal(r.sample(2339).face, 'soft');
  assert.equal(r.sample(2340).face, 'calm');
  assert.equal(r.stroke(1400, 36), false);
  assert.equal(r.sample(1400).active, false);
});

test('releasing an already-resting tail does not start motion; release cancels a preview', () => {
  const r = createReaction();
  r.touch(0);
  r.stroke(0, 36);
  r.stroke(160, 36);
  r.release(1200);
  assert.equal(r.sample(1200).wag, 0);
  assert.equal(r.preview(1300), true);
  r.release(1400);
  assert.equal(r.sample(1800).face, 'soft');
  assert.equal(r.sample(1800).wag, 0);
});

test('head deformation leaves the feet and rump anchored', () => {
  for (const [x, y] of [[.66, .67], [.72, .93], [.78, .47]]) {
    assert.deepEqual(deformPoint(x, y, 1, 1), { x, y });
  }
  assert.equal(Math.abs(tailSwing(150, 0)), 0);
  assert.notEqual(tailSwing(75, 1), tailSwing(250, 1));
});

test('the curl lifts from the back and the distal tip moves farther than the near-root edge', () => {
  const { size, crop } = tailArt;
  const { from, to } = tailHinge;
  const geometry = new PlaneGeometry(crop.width / size, crop.height / size);
  const plane = new Mesh(geometry);
  try {
    const rig = mountTailPlane(plane);
    const positions = geometry.getAttribute('position');
    const corner = new Vector3().fromBufferAttribute(positions, 1);
    const axis = new Vector3(to.x - from.x, from.y - to.y, 0).normalize();
    const artPoint = (x, y) => {
      const px = (x - to.x) / size, py = (to.y - y) / size;
      return new Vector3(px, py, (axis.x * py - axis.y * px) * Math.tan(tailHinge.restTilt));
    };
    const lip = artPoint(350, 229);
    const nearRoot = artPoint(405, 260);
    const rest = plane.localToWorld(corner.clone());
    const restLip = plane.localToWorld(lip.clone());
    const restNearRoot = plane.localToWorld(nearRoot.clone());
    const anchor = new Vector3(to.x / size - .5, .5 - to.y / size, 0);
    assert.ok(Math.abs(rest.x - ((crop.x + crop.width) / size - .5)) < 1e-7);
    assert.ok(Math.abs(rest.y - (.5 - crop.y / size)) < 1e-7);
    for (const swing of [-1, 1]) {
      rig.pose(swing);
      const at = plane.localToWorld(corner.clone());
      const lifted = plane.localToWorld(lip.clone());
      const near = plane.localToWorld(nearRoot.clone());
      assert.ok((at.z - rest.z) * swing > .06);
      assert.ok(lifted.y - restLip.y > 12 / size, 'the lip rises off the back');
      assert.ok(at.distanceTo(rest) > 4 * near.distanceTo(restNearRoot));
      assert.ok(plane.localToWorld(new Vector3()).distanceTo(anchor) < 1e-8);
      assert.ok(Math.abs(at.distanceTo(anchor) - rest.distanceTo(anchor)) < 1e-8);
    }
  } finally {
    plane.material.dispose();
    geometry.dispose();
  }
});

test('split texture removes the old curl, fills the rump and leaves paws byte-identical', async () => {
  const decode = async file => sharp(await readFile(new URL(`../public/pet-affection/${file}`, import.meta.url)))
    .ensureAlpha().raw().toBuffer();
  const original = await decode('shiba.webp');
  const atlas = await decode('shiba-tail-layers.webp');
  const { crop, atlas: layout, size } = tailArt;
  const alpha = (pixels, stride, x, y) => pixels[(y * stride + x) * 4 + 3];
  assert.equal(alpha(atlas, layout.width, 383, 190), 0, 'old curl does not persist on the body');
  assert.equal(alpha(atlas, layout.width, layout.tailX + 383 - crop.x, 190 - crop.y), 255);
  assert.equal(alpha(atlas, layout.width, layout.tailX + 383 - crop.x, layout.rootY + 190 - crop.y), 0,
    'the root overlay does not paint a stationary copy of the curl');
  assert.equal(alpha(atlas, layout.width, layout.tailX + 409 - crop.x, layout.rootY + 268 - crop.y), 255,
    'fixed fur covers the actual joint');
  assert.equal(alpha(atlas, layout.width, layout.tailX + 371 - crop.x, layout.rootY + 239 - crop.y), 0,
    'fixed fur no longer extends under the curl lip');
  assert.equal(alpha(atlas, layout.width, layout.tailX + 387 - crop.x, 257 - crop.y), 0,
    'the lower fringe is not in the rotating layer');
  assert.equal(alpha(atlas, layout.width, 391, 244), 255, 'covered rump is opaque');
  for (let y = 330; y < size; y++) {
    for (let x = 0; x < size; x++) {
      const a = alpha(atlas, layout.width, x, y);
      assert.equal(a, alpha(original, size, x, y));
      if (!a) continue; // WebP may store different RGB under fully transparent pixels.
      const packedAt = (y * layout.width + x) * 4;
      const sourceAt = (y * size + x) * 4;
      assert.ok(atlas.subarray(packedAt, packedAt + 3).equals(original.subarray(sourceAt, sourceAt + 3)));
    }
  }
});

test('a stationary retouch does not restart the resting smile timer', () => {
  const r = createReaction();
  r.touch(0);
  r.stroke(0, 36);
  r.stroke(160, 36);
  r.release(200);
  r.touch(1900);
  assert.equal(r.sample(1999).face, 'soft');
  assert.equal(r.sample(2000).face, 'calm');
  r.release(2001);
  assert.equal(r.sample(2001).face, 'calm');
});

test('stroking from soft again earns happy from scratch', () => {
  const r = createReaction();
  r.touch(0);
  for (let t = 0; t <= 120; t += 40) r.stroke(t, 36);
  assert.equal(r.stroke(160, 36), false);
  assert.equal(r.stroke(540, 36), true);
  r.release(540);
  assert.equal(r.sample(540).face, 'soft');
  r.touch(1540);
  assert.equal(r.sample(1540).face, 'soft');
  assert.equal(r.stroke(1540, 36), false);
  assert.equal(r.stroke(1640, 36), false);
  assert.equal(r.stroke(1700, 36), false);
  assert.equal(r.sample(1700).face, 'soft');
  assert.equal(r.stroke(2080, 36), true);
});

test('happy pulses repeat with spacing and fresh distance', () => {
  const r = createReaction();
  r.touch(0);
  for (let t = 0; t <= 120; t += 40) r.stroke(t, 36);
  assert.equal(r.stroke(160, 36), false);
  assert.equal(r.stroke(540, 36), true);
  assert.equal(r.stroke(640, 36), false);
  assert.equal(r.stroke(740, 36), false);
  assert.equal(r.stroke(840, 10), false);
  assert.equal(r.stroke(940, 10), false);
  assert.equal(r.stroke(1040, 10), false);
  assert.equal(r.stroke(1140, 10), false);
  assert.equal(r.stroke(1190, 10), true);
  assert.equal(r.stroke(1890, 20), false);
  assert.equal(r.stroke(1920, 20), false);
  assert.equal(r.stroke(1960, 20), true);
});

test('happy idles back to soft after 780ms', () => {
  const r = createReaction();
  r.touch(0);
  for (let t = 0; t <= 120; t += 40) r.stroke(t, 36);
  assert.equal(r.stroke(160, 36), false);
  assert.equal(r.stroke(540, 36), true);
  assert.equal(r.sample(1240).face, 'happy');
  assert.equal(r.sample(1340).face, 'soft');
  assert.equal(r.sample(1340).active, true);
  assert.equal(r.sample(1340).intensity, 0.45);
  assert.equal(r.sample(1340).wag, 0);
  assert.equal(r.stroke(1400, 36), false);
  assert.equal(r.sample(1400).face, 'soft');
  assert.equal(r.stroke(1500, 36), false);
  assert.equal(r.stroke(1600, 36), false);
  assert.equal(r.stroke(1940, 36), true);
});

test('preview smiles, cheers, then settles soft', () => {
  const r = createReaction();
  assert.equal(r.preview(0), true);
  assert.deepEqual(r.sample(0), { face: 'soft', active: false, intensity: 0.45, wag: 0.3 });
  assert.equal(r.preview(100), false);
  assert.equal(r.sample(300).face, 'soft');
  assert.equal(r.sample(400).face, 'happy');
  assert.equal(r.sample(400).intensity, 1);
  assert.equal(r.sample(400).wag, 1);
  assert.equal(r.sample(1010).face, 'soft');
  assert.equal(r.sample(1010).wag, 0);
  assert.equal(r.sample(2809).face, 'soft');
  assert.equal(r.sample(2810).face, 'calm');
  assert.equal(r.sample(3000).active, false);
  assert.equal(r.preview(3000), true);
  assert.equal(r.preview(3100), false);
  r.touch(4100);
  assert.equal(r.sample(4100).face, 'soft');
  for (let t = 4100; t <= 4300; t += 40) r.stroke(t, 36);
  assert.equal(r.stroke(4640, 36), true);
  const busy = createReaction();
  busy.touch(500);
  assert.equal(busy.preview(600), false);
});

test('bad input is ignored and output stays finite', () => {
  const r = createReaction();
  assert.deepEqual(r.sample(0), { face: 'calm', active: false, intensity: 0, wag: 0 });
  r.touch(100);
  r.touch(NaN);
  r.touch(-5);
  assert.equal(r.sample(100).active, true);
  assert.equal(r.stroke(50, 36), false);
  assert.equal(r.stroke(200, 36), false);
  assert.equal(r.sample(200).face, 'calm');
  const s = r.sample(NaN);
  assert.equal(s.face, 'calm');
  assert.equal(Number.isFinite(s.wag), true);
  assert.equal(Number.isFinite(s.intensity), true);
  assert.equal(r.stroke(210, 36), false);
  assert.equal(r.stroke(260, 36), false);
  assert.equal(r.stroke(320, 36), false);
  assert.equal(r.stroke(360, 36), false);
  assert.equal(r.sample(360).face, 'soft');
  assert.equal(Number.isFinite(r.sample(NaN).wag), true);
});

test('reset clears everything', () => {
  const r = createReaction();
  r.touch(0);
  for (let t = 0; t <= 120; t += 40) r.stroke(t, 36);
  assert.equal(r.stroke(160, 36), false);
  assert.equal(r.stroke(540, 36), true);
  r.reset();
  assert.deepEqual(r.sample(0), { face: 'calm', active: false, intensity: 0, wag: 0 });
  assert.deepEqual(r.sample(9000), { face: 'calm', active: false, intensity: 0, wag: 0 });
  r.touch(0);
  assert.equal(r.stroke(0, 36), false);
  assert.equal(r.sample(100).face, 'calm');
  assert.equal(r.stroke(120, 36), false);
  assert.equal(r.stroke(160, 4), false);
  assert.equal(r.sample(160).face, 'soft');
});
