import fs from 'node:fs/promises';
import path from 'node:path';
import { createHash } from 'node:crypto';

export const sha256 = bytes => createHash('sha256').update(bytes).digest('hex');

// Same length-prefixed little-endian float32/int32 stream as AtlasMeshAudit.cs.
export function hashMesh(rig) {
    const positions = rig.positions.map((v, i) => i % 3 === 2 ? 0 : v);
    const fields = [[positions, false], [rig.uv, false], [rig.cropUV, false], [rig.triangles, true]];
    const bytes = Buffer.alloc(fields.reduce((n, [values]) => n + 4 + values.length * 4, 0));
    let offset = 0;
    for (const [values, integer] of fields) {
        bytes.writeInt32LE(values.length, offset); offset += 4;
        for (const value of values) {
            if (!Number.isFinite(value) || (integer && !Number.isInteger(value))) throw new Error('Invalid mesh value');
            if (integer) bytes.writeInt32LE(value, offset);
            else bytes.writeFloatLE(value === 0 ? 0 : value, offset);
            offset += 4;
        }
    }
    return sha256(bytes);
}

function resolveInput(root, relative) {
    if (!relative || relative.includes('\\') || path.isAbsolute(relative) || relative.split('/').includes('..'))
        throw new Error(`Unsafe atlas input: ${relative}`);
    return path.join(root, relative);
}

export async function snapshotFiles(root, paths) {
    if (!paths.length || new Set(paths).size !== paths.length) throw new Error('Empty or duplicate atlas inputs');
    const entries = [];
    for (const relative of [...paths].sort()) {
        entries.push({path: relative, sha256: sha256(await fs.readFile(resolveInput(root, relative)))});
    }
    return entries;
}

export async function assertSnapshot(root, entries) {
    const current = await snapshotFiles(root, entries.map(e => e.path));
    for (const entry of current) {
        if (entry.sha256 !== entries.find(e => e.path === entry.path).sha256)
            throw new Error(`Stale atlas input: ${entry.path}`);
    }
}
