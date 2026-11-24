import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
const data = JSON.parse(fs.readFileSync('art-source/pet-sit-chibi-20261123/unity-export.json', 'utf8'));

test('station bake keeps all poses on the same source skin with normalized weights and native curves', () => {
    assert.equal(data.schema, 2);
    assert.equal(data.appearanceId, 'cat.orange.side');
    assert.equal(data.bones.length, 36);
    assert.equal(new Set(data.bones.map(b => b.id)).size, 36);
    assert.deepEqual(data.takes.map(t => t.id), ['SitDown', 'Tread']);
    for (const mesh of data.meshes) {
        assert.equal(mesh.positions.length / 3, mesh.weights.length / 4);
        for (let i = 0; i < mesh.weights.length; i += 4) assert.ok(Math.abs(mesh.weights.slice(i, i + 4).reduce((a,b) => a+b,0) - 1) < .000001);
        for (const index of mesh.triangles) assert.ok(index < mesh.positions.length / 3);
    }
    for (const take of data.takes) for (const curve of take.curves) {
        assert.equal(curve.times[0], 0); assert.equal(curve.times.at(-1), take.duration);
        assert.ok(!/Scale|Active|Opacity/.test(curve.property));
        assert.ok(curve.values.every(Number.isFinite));
    }
    const tread = data.takes[1];
    for (const curve of tread.curves) assert.ok(Math.abs(curve.values[0] - curve.values.at(-1)) < .000002);
});
