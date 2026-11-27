// Each row is a non-repeating ink contour. Long, shallow strokes gather into
// uneven tapered tips; the quieter spans are part of the drawing.
const contours = [
    [[0, .16], [.12, .20], [.25, .31], [.318, .65], [.351, .93], [.387, .36], [.47, .21],
        [.54, .28], [.65, .43], [.707, .99], [.739, .41], [.80, .25], [.94, .19], [1, .17]],
    [[0, .15], [.12, .20], [.244, .39], [.289, .71], [.318, .33], [.42, .18],
        [.49, .29], [.571, .53], [.622, .95], [.655, .40], [.72, .24], [.83, .32], [.90, .64], [.936, .26], [1, .16]],
    [[0, .18], [.11, .23], [.177, .48], [.211, .77], [.252, .31], [.34, .24], [.47, .25],
        [.53, .37], [.604, .64], [.642, .98], [.680, .44], [.74, .34], [.82, .46], [.85, .66], [.886, .32], [1, .19]],
    [[0, .20], [.12, .22], [.24, .31], [.339, .58], [.372, .91], [.411, .39], [.46, .28],
        [.49, .30], [.57, .45], [.608, .78], [.647, .34], [.73, .20], [.85, .26], [.91, .50], [.943, .23], [1, .15]],
];
const accents = [[.336, .692], [.277, .607], [.199, .628], [.356, .595]];

export const edgeWidth = 256;
export const edgeRows = contours.length;

export function buildEdgeTexture() {
    const pixels = new Uint8Array(edgeWidth * edgeRows * 2);
    for (const [row, points] of contours.entries()) {
        let segment = 0;
        for (let x = 0; x < edgeWidth; x++) {
            const u = x / (edgeWidth - 1);
            while (segment + 2 < points.length && u > points[segment + 1][0]) segment++;
            const [left, low] = points[segment];
            const [right, high] = points[segment + 1];
            const t = (u - left) / (right - left);
            const curve = high > low ? t * t * (2 - t) : 1 - (1 - t) ** 2;
            const index = (row * edgeWidth + x) * 2;
            pixels[index] = Math.round(255 * (low + (high - low) * curve));
            const stroke = Math.max(...accents[row].map(center => Math.max(0, 1 - Math.abs(center - u) / .013)));
            pixels[index + 1] = Math.round(stroke * 255);
        }
    }
    return pixels;
}
