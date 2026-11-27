import { Group, Vector3 } from 'three';

export const tailArt = {
    size: 512,
    crop: { x: 320, y: 140, width: 144, height: 148 },
    atlas: { width: 656, height: 512, tailX: 512, rootY: 152 },
};

export const tailHinge = {
    from: { x: 360, y: 231 },
    to: { x: 409, y: 265 },
    restTilt: .41,
    swingLimit: .63,
    liftLimit: .29,
};

const smooth = value => {
    const t = Math.max(0, Math.min(1, value));
    return t * t * (3 - 2 * t);
};

export function deformPoint(x, y, lean, intensity) {
    const head = (1 - smooth((y - .37) / .20)) * (1 - smooth((x - .56) / .09));
    const ears = 1 - smooth((y - .19) / .14);
    const tilt = intensity * lean * head;
    return {
        x: x + (.016 + .012 * ears) * tilt + (x - .385) * .025 * intensity * head,
        y: y - .027 * intensity * head + (x - .385) * .082 * tilt + .019 * ears * intensity * head,
    };
}

export function tailSwing(now, amplitude) {
    return amplitude * Math.sin(now * .014);
}

export function mountTailPlane(plane) {
    const { size, crop } = tailArt;
    const { from, to, restTilt, swingLimit, liftLimit } = tailHinge;
    const root = new Group();
    const axis = new Vector3(to.x - from.x, from.y - to.y, 0).normalize();
    const normal = new Vector3(0, 0, 1);
    root.position.set(to.x / size - .5, .5 - to.y / size, 0);
    const offsetX = (crop.x + crop.width / 2 - to.x) / size;
    const offsetY = (to.y - crop.y - crop.height / 2) / size;
    const position = plane.geometry.getAttribute('position');
    const depthSlope = Math.tan(restTilt);
    // Preserve the painted rest projection while giving the rigid plane a visible near/far side.
    for (let i = 0; i < position.count; i++) {
        const x = position.getX(i) + offsetX;
        const y = position.getY(i) + offsetY;
        position.setXYZ(i, x, y, (axis.x * y - axis.y * x) * depthSlope);
    }
    position.needsUpdate = true;
    root.add(plane);
    return {
        root,
        pose(swing) {
            root.quaternion.setFromAxisAngle(axis, swingLimit * swing);
            // Lift away from the back near either extreme while the outside root stays planted.
            root.rotateOnWorldAxis(normal, -liftLimit * swing * swing);
        },
    };
}
