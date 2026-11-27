export const maxPatches = 72;

// Field ranges are shared by the authoring UI and the profile validator.
export const regionFields = [
    ['x', '横向位置', 0, 4096, 1, 'px'], ['y', '纵向位置', 0, 4096, 1, 'px'],
    ['width', '铺毛宽度', 10, 240, 1, 'px'], ['depth', '层间总距', 0, 140, 1, 'px'],
    ['angle', '毛流方向', -180, 180, 1, '°'], ['spread', '毛流散开', 0, 120, 1, '°'],
    ['columns', '每层片数', 1, 9, 1], ['layers', '层数', 1, 5, 1],
    ['stagger', '错层', 0, 1, .01], ['arc', '层弧度', -45, 45, 1, 'px'], ['jitter', '长短与排布变化', 0, .45, .01],
    ['patchWidth', '单片宽度', 8, 100, 1, 'px'], ['length', '单片长度', 8, 100, 1, 'px'],
    ['teeth', '毛尖疏密', 2, 16, 1], ['toothDepth', '毛尖深度', .02, .45, .005],
    ['lift', '初始翘起', 0, 1, .01], ['curl', '卷度', 0, 1, .01],
    ['undercoat', '底层毛相对长度', .25, 1, .01],
];

export const lightFields = [['angle', '投影方向', -180, 180, 1, '°'], ['shadow', '阴影强度', 0, 1, .01]];

export function patchCount(regions) {
    return regions.reduce((total, region) => total + region.columns * region.layers, 0);
}

function validWarmthRange(range) {
    return range === undefined || Array.isArray(range) && range.length === 2 &&
        range.every(value => Number.isFinite(value) && value >= -1.6 && value <= 1.6) && range[0] < range[1];
}

function validFields(settings, fields) {
    return fields.every(([key, , min, max, step]) => {
        const value = settings?.[key];
        return Number.isFinite(value) && value >= min && value <= max && (step !== 1 || Number.isInteger(value));
    });
}

export function validateProfile(profile) {
    const portrait = profile?.portrait;
    const crop = portrait?.crop;
    if (profile?.version !== 1 || typeof profile.id !== 'string' || !/^[a-z0-9][a-z0-9-]*$/.test(profile.id) ||
        typeof profile.name !== 'string' || !profile.name.trim()) throw new Error('Invalid profile version or identity');
    if (typeof portrait?.image !== 'string' || !portrait.image || !crop ||
        ![portrait.width, portrait.height, crop.width, crop.height].every(value => Number.isInteger(value) && value > 0 && value <= 4096) ||
        ![crop.x, crop.y].every(value => Number.isInteger(value) && value >= 0) ||
        crop.x + crop.width > portrait.width || crop.y + crop.height > portrait.height) throw new Error('Invalid portrait dimensions');
    if (!validFields(profile.light, lightFields) || !Array.isArray(profile.regions) || !profile.regions.length) throw new Error('Invalid light or regions');
    const ids = new Set();
    for (const region of profile.regions) {
        if (typeof region?.id !== 'string' || !region.id.trim() || ids.has(region.id) ||
            typeof region.label !== 'string' || !region.label.trim() || !validFields(region, regionFields) ||
            region.x > portrait.width || region.y > portrait.height || !validWarmthRange(region.warmthRange)) throw new Error('Invalid region parameters');
        ids.add(region.id);
    }
    if (patchCount(profile.regions) > maxPatches) throw new Error('Patch budget exceeded');
    return profile;
}

export function axis(angle) {
    const radians = angle * Math.PI / 180;
    return [Math.cos(radians), Math.sin(radians)];
}

function variation(id, row, column) {
    let hash = 2166136261;
    for (let i = 0; i < id.length; i++) hash = Math.imul(hash ^ id.charCodeAt(i), 16777619);
    hash = Math.imul(hash ^ (row + 1), 2246822519);
    hash = Math.imul(hash ^ (column + 1), 3266489917);
    hash ^= hash >>> 15;
    return (hash >>> 0) / 4294967296;
}

export function regionOutline(region) {
    const corners = distribute([region]).flatMap(patch => [-.5, .5].flatMap(side => [0, 1].map(tip => [
        patch.x + patch.gy * side * patch.width + patch.gx * tip * patch.length,
        patch.y - patch.gx * side * patch.width + patch.gy * tip * patch.length,
    ])));
    corners.sort(([ax, ay], [bx, by]) => ax - bx || ay - by);
    function half(points) {
        const edge = [];
        for (const p of points) {
            while (edge.length >= 2) {
                const a = edge[edge.length - 2], b = edge[edge.length - 1];
                if ((b[0] - a[0]) * (p[1] - a[1]) - (b[1] - a[1]) * (p[0] - a[0]) > 0) break;
                edge.pop();
            }
            edge.push(p);
        }
        return edge.slice(0, -1);
    }
    return half(corners).concat(half(corners.reverse()));
}

export function distribute(regions) {
    const patches = [];
    for (const region of regions) {
        const [flowX, flowY] = axis(region.angle);
        for (let row = region.layers - 1; row >= 0; row--) {
            const depth = region.layers === 1 ? 0 : row / (region.layers - 1);
            const lengthFactor = 1 - (1 - region.undercoat) * depth;
            const rowShift = (row % 2 ? .48 : -.16) + (variation(region.id, row, 12) - .5) * .36;
            for (let col = 0; col < region.columns; col++) {
                const seed = variation(region.id, row, col);
                const noise = (seed * 2 - 1) * region.jitter;
                const placement = (variation(region.id, row, col + 20) * 2 - 1) * region.jitter;
                const offset = rowShift * region.stagger;
                const u = ((col + .5 + offset + placement * .67) / region.columns) - .5;
                const across = u * region.width;
                const along = depth * region.depth + 4 * u * u * region.arc + placement * region.length * .65;
                const [gx, gy] = axis(region.angle - u * region.spread + noise * 24);
                patches.push({
                    id: `${region.id}:${row}:${col}`, region: region.id,
                    x: region.x + flowY * across + flowX * along,
                    y: region.y - flowX * across + flowY * along,
                    gx, gy, width: region.patchWidth * (1 + noise * .48), length: region.length * lengthFactor * (1 + noise),
                    initial: region.lift * (.63 + .37 * lengthFactor) * (1 - .38 * seed),
                    roll: region.curl * (1 - .42 * seed), teeth: region.teeth, toothDepth: region.toothDepth,
                    seed, warmthRange: region.warmthRange,
                });
            }
        }
    }
    return patches;
}
