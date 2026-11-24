import {ramp} from './join.js';

const floor = .004;
const heelDown = 40 * Math.PI / 180;

export function pawContact(progress, points) {
    const angle = heelDown * ramp(.53, 1, progress);
    const sin = Math.sin(angle), cos = Math.cos(angle);
    const lowest = Math.min(...points.map(([x, y]) => x * sin + y * cos));
    return {angle, height: floor - lowest};
}

export function createPawContact(skin, manifest) {
    const paw = skin.skeleton.bones.find(bone => bone.name === 'nearPaw');
    const origin = skin.rig.profile.source.origin;
    const scale = skin.rig.profile.source.pixelsPerUnit;
    const toe = skin.rig.profile.landmarks.toe;
    const layer = manifest.layers.find(layer => layer.id === 'nearHindPaw');
    // Include the raised rear edge in the support search, not just the old sole.
    const rear = [[324, 488], [330, 483], [335, 474], [340, 467], [345, 459]];
    const points = [...layer.support, ...rear]
        .map(([x, y]) => [(toe[0] - x) / scale, (toe[1] - y) / scale]);
    return (enabled, progress) => {
        if (enabled && progress > .53) {
            const {angle, height} = pawContact(progress, points);
            paw.rotation.z = angle;
            // Preserve the original toe anchor's height at the start of the roll.
            const restHeight = (origin[1] - toe[1]) / scale;
            paw.position.y = restHeight + (height - restHeight) * ramp(.53, .73, progress);
        } else {
            paw.rotation.z = 0;
            paw.position.copy(skin.rig.world('nearToe'));
        }
        skin.rig.group.updateMatrixWorld(true);
        skin.skeleton.update();
    };
}
