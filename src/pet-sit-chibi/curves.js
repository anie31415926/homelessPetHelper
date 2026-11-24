import {KEY_POSES} from './poses.js';

// Shape-preserving Hermite interpolation: shared tangents carry velocity through
// the preparation/descent keys. Endpoints and direction reversals ease to rest.
export function sampleChannel(progress, values, times = KEY_POSES.map(key => key.at)) {
    const slopes = values.slice(1).map((value, i) => (value - values[i]) / (times[i + 1] - times[i]));
    const tangents = values.map((_, i) => {
        if (i === 0 || i === values.length - 1 || slopes[i - 1] * slopes[i] <= 0) return 0;
        const before = times[i] - times[i - 1], after = times[i + 1] - times[i];
        const a = 2 * after + before, b = after + 2 * before;
        return (a + b) / (a / slopes[i - 1] + b / slopes[i]);
    });
    const end = Math.max(1, times.findIndex(at => at >= progress)), start = end - 1;
    const width = times[end] - times[start], t = (progress - times[start]) / width;
    return (2 * t ** 3 - 3 * t ** 2 + 1) * values[start] + (t ** 3 - 2 * t ** 2 + t) * width * tangents[start]
        + (-2 * t ** 3 + 3 * t ** 2) * values[end] + (t ** 3 - t ** 2) * width * tangents[end];
}
