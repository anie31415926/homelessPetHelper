export const PARTS = ['head', 'body', 'tail', 'earL', 'earR', 'frontL', 'frontR', 'backL', 'backR'];
export const ACTIONS = ['idle', 'walk', 'happy', 'listen', 'crouch'];
export const PROPORTIONS = {
    standard: { body: 1, leg: 1, head: 1 },
    short: { body: 1.08, leg: 0.75, head: 1.04 },
    long: { body: 1.18, leg: 1.12, head: 0.94 },
};
export const JOINT_LIMITS = { head: 12, ears: 22, tail: 35, legs: 22 };
export const clamp = (value, min, max) => Math.max(min, Math.min(max, Number.isFinite(value) ? value : 0));

export function poseAt(time, action = 'idle', manual = {}) {
    if (!ACTIONS.includes(action)) throw new Error(`Unknown action: ${action}`);
    const t = Number.isFinite(time) ? Math.max(0, time) : 0;
    const walk = action === 'walk', happy = action === 'happy', listen = action === 'listen';
    const wave = Math.sin(t * (happy ? 7 : 3.2));
    const gait = t * 5.4;
    const pose = {
        bodyY: action === 'crouch' ? 26 : happy ? -Math.max(0, Math.sin(t * 4)) * 16 : Math.sin(t * 2.4) * 1.3,
        bodyScaleY: action === 'crouch' ? 0.88 : 1 + Math.sin(t * 2.4) * 0.006,
        head: listen ? Math.sin(t * 1.5) * 7 : Math.sin(t * 1.7) * 1.6,
        earL: listen ? -15 + wave * 4 : wave * 3,
        earR: listen ? 13 - wave * 3 : Math.sin(t * 3.2 + 0.8) * 3,
        tail: wave * (happy ? 24 : 8),
        frontL: walk ? Math.sin(gait) * 17 : happy ? -7 + wave * 5 : 0,
        frontR: walk ? -Math.sin(gait) * 17 : happy ? 7 - wave * 5 : 0,
        backL: walk ? -Math.sin(gait) * 15 : 0,
        backR: walk ? Math.sin(gait) * 15 : 0,
        lift: walk ? Math.max(0, Math.sin(gait)) * 9 : 0,
        oppositeLift: walk ? Math.max(0, -Math.sin(gait)) * 9 : 0,
    };
    pose.head += clamp(manual.head, -12, 12);
    pose.earL += clamp(manual.ears, -22, 22);
    pose.earR -= clamp(manual.ears, -22, 22);
    pose.tail += clamp(manual.tail, -35, 35);
    pose.frontL += clamp(manual.legs, -22, 22);
    pose.frontR -= clamp(manual.legs, -22, 22);
    return pose;
}

export function createRig(view, proportion = 'standard') {
    if (!['front', 'side'].includes(view)) throw new Error(`Unknown view: ${view}`);
    const p = PROPORTIONS[proportion];
    if (!p) throw new Error(`Unknown proportions: ${proportion}`);
    const side = view === 'side';
    const node = (id, parent, x, y, width, height, ox, oy) => ({ id, parent, x, y, width, height, ox, oy });
    const b = p.body, h = p.head, leg = p.leg;
    return [
        node('tail', 'root', side ? 105 * b : 62, side ? -10 : 39, 150, 76, .10, .24),
        node('backL', 'root', side ? 91 * b : -59, 56, side ? 57 : 43, 101 * leg, .5, .12),
        node('backR', 'root', side ? 104 * b : 56, 58, side ? 60 : 43, 102 * leg, .5, .12),
        node('frontL', 'root', side ? -82 * b : -35, 56, 53, 105 * leg, .5, .12),
        node('frontR', 'root', side ? -72 * b : 35, 57, 53, 106 * leg, .5, .12),
        node('body', 'root', 0, side ? 0 : -12, side ? 265 * b : 150 * b, side ? 128 : 145, .5, side ? .26 : .24),
        node('head', 'root', side ? -117 * b : 0, side ? -40 : -51, side ? 161 * h : 218 * h, 143 * h, side ? .55 : .5, .85),
        node('earL', 'head', side ? -2 * h : -75 * h, side ? -104 * h : -90 * h, side ? 40 * h : 66 * h, side ? 57 * h : 94 * h, .55, .88),
        node('earR', 'head', side ? 44 * h : 76 * h, side ? -96 * h : -89 * h, side ? 55 * h : 66 * h, side ? 75 * h : 94 * h, side ? .55 : .45, .88),
    ];
}

export function transformPoint(matrix, x, y) {
    return { x: matrix.a * x + matrix.c * y + matrix.e, y: matrix.b * x + matrix.d * y + matrix.f };
}
