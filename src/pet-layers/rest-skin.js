import { BufferAttribute, PlaneGeometry } from 'three';
import { bindSkin, GRID, SIZE } from '../pet-mesh/skin.js';

export function createRestSkin(material, spec, weightsAt) {
    const geometry = new PlaneGeometry(SIZE.width, SIZE.height, GRID.columns, GRID.rows);
    const uv = geometry.getAttribute('uv'), indices = [], weights = [];
    for (let i = 0; i < uv.count; i++) {
        const influences = weightsAt(uv.getX(i) * 452, (1 - uv.getY(i)) * 512);
        for (let j = 0; j < 4; j++) { indices.push(influences[j]?.index || 0); weights.push(influences[j]?.weight || 0); }
    }
    geometry.setAttribute('skinIndex', new BufferAttribute(new Uint16Array(indices), 4));
    geometry.setAttribute('skinWeight', new BufferAttribute(new Float32Array(weights), 4));
    return bindSkin(material, geometry, spec);
}
