import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs/promises';
import os from 'node:os';
import path from 'node:path';
import { hashMesh, snapshotFiles, assertSnapshot } from '../tools/lib/atlas-manifest.mjs';

test('mesh fingerprints detect stale UV, clipping UV, geometry and topology, using float32 precision', () => {
    const rig = {positions: [0, 1, 2], uv: [.25, .5], cropUV: [0, 1], triangles: [0, 0, 0]};
    const hash = hashMesh(rig);
    assert.equal(hash.length, 64);
    assert.equal(hash, hashMesh({...rig, positions: [-0, 1, 999]}), 'Unity deliberately writes z=0');
    for (const field of ['positions', 'uv', 'cropUV', 'triangles']) {
        const edited = structuredClone(rig); edited[field][0] += 1;
        assert.notEqual(hashMesh(edited), hash, field);
    }
    assert.throws(() => hashMesh({...rig, uv: [NaN, 0]}));
});

test('generation snapshot rejects changed or missing sources and unsafe/duplicate paths', async () => {
    const root = await fs.mkdtemp(path.join(os.tmpdir(), 'atlas-manifest-'));
    try {
        await fs.writeFile(path.join(root, 'input'), 'original');
        const entries = await snapshotFiles(root, ['input']);
        await assertSnapshot(root, entries);
        await fs.writeFile(path.join(root, 'input'), 'edited');
        await assert.rejects(assertSnapshot(root, entries), /Stale/);
        await fs.unlink(path.join(root, 'input'));
        await assert.rejects(assertSnapshot(root, entries), /ENOENT/);
        for (const paths of [[], ['x', 'x'], ['../x'], ['/x'], ['a\\x']])
            await assert.rejects(snapshotFiles(root, paths));
    } finally { await fs.rm(root, {recursive: true, force: true}); }
});
