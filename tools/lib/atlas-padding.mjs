// Pixel-preserving alpha crop with a real gutter, including beyond source-image edges.
export async function prepareAtlasImage(sharp, input, {padding = 2, trim = true, edge = 'transparent'} = {}) {
    if (!Number.isInteger(padding) || padding < 0 || padding > 64 ||
        typeof trim !== 'boolean' || !['transparent', 'extrude'].includes(edge)) {
        throw new Error('Invalid atlas padding options');
    }
    const {data, info} = await sharp(input).ensureAlpha().raw().toBuffer({resolveWithObject: true});
    if (info.channels !== 4 || info.depth !== 'uchar') throw new Error('Expected 8-bit RGBA atlas input');
    const bounds = trim ? alphaBounds(data, info.width, info.height)
        : {left: 0, top: 0, right: info.width - 1, bottom: info.height - 1};
    const left = bounds.left - padding, top = bounds.top - padding;
    const width = bounds.right - bounds.left + 1 + padding * 2;
    const height = bounds.bottom - bounds.top + 1 + padding * 2;
    const pixels = Buffer.alloc(width * height * 4);
    for (let y = 0; y < height; y++) {
        for (let x = 0; x < width; x++) {
            let sx = left + x, sy = top + y;
            if (edge === 'extrude') {
                sx = Math.max(bounds.left, Math.min(bounds.right, sx));
                sy = Math.max(bounds.top, Math.min(bounds.bottom, sy));
            }
            if (sx < 0 || sx >= info.width || sy < 0 || sy >= info.height) continue;
            const source = (sy * info.width + sx) * 4;
            data.copy(pixels, (y * width + x) * 4, source, source + 4);
        }
    }
    return {
        sourceWidth: info.width, sourceHeight: info.height, left, top, width, height,
        image: await sharp(pixels, {raw: {width, height, channels: 4}}).png().toBuffer(),
    };
}

function alphaBounds(data, width, height) {
    let left = width, top = height, right = -1, bottom = -1;
    for (let y = 0; y < height; y++) {
        for (let x = 0; x < width; x++) {
            if (!data[(y * width + x) * 4 + 3]) continue;
            left = Math.min(left, x); top = Math.min(top, y);
            right = Math.max(right, x); bottom = Math.max(bottom, y);
        }
    }
    if (right < 0) throw new Error('Cannot trim an empty atlas image');
    return {left, top, right, bottom};
}
