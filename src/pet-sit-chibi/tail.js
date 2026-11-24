import {MathUtils, Quaternion, Vector3} from 'three';

import {DEFAULT_APPEARANCE} from './profile.js';

export const TAIL_MOTION = DEFAULT_APPEARANCE.tail;
export function applyTailExpression(tail, joinLocal, raise = 0, swish = 0, seconds = 0, motion = TAIL_MOTION) {
    if (raise === 0 && swish === 0) return;
    const pivot = joinLocal.clone().applyQuaternion(tail.quaternion).add(tail.position);
    tail.quaternion.slerp(new Quaternion().setFromAxisAngle(new Vector3(0, 0, 1), MathUtils.degToRad(motion.raisedDegrees)), raise);
    const angle = -swish * (motion.swishLiftDegrees + motion.swishDegrees * Math.sin(seconds * Math.PI * 2 / motion.periodSeconds));
    tail.quaternion.multiply(new Quaternion().setFromAxisAngle(new Vector3(0, 0, 1), MathUtils.degToRad(angle)));
    tail.position.copy(pivot).sub(joinLocal.clone().applyQuaternion(tail.quaternion));
}
