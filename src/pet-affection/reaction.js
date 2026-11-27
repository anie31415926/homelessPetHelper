const CAP = 36;
const SOFT_PX = 38;
const SOFT_MS = 160;
const HAPPY_PX = 100;
const HAPPY_MS = 540;
const HOLD_MS = 260;
const PAUSE_MS = 420;
const IDLE_MS = 780;
const SMILE_LINGER_MS = 1800;
const PULSE_MS = 650;
const PULSE_PX = 56;
const PREVIEW_MS = 360;
const PREVIEW_HOLD_MS = 650;

const HEAD = { calm: 0, soft: 0.45, happy: 1 };
const TAIL = { calm: [0, 0, 0], soft: [0.3, 160, 480], happy: [1, 120, 640] };

const time = (v) => (typeof v === 'number' && Number.isFinite(v) && v >= 0 ? v : null);
const round = (v) => (Number.isFinite(v) ? Math.round(v * 1e6) / 1e6 : 0);

function decay(peak, since, hold, fall) {
  if (since <= hold) return peak;
  const k = 1 - Math.min(1, (since - hold) / fall);
  return peak * k * k;
}

export function createReaction() {
  let clock = 0;
  let active = false;
  let softAt = null;
  let softUntil = -Infinity;
  let happy = false;
  let cycleAt = null;
  let cyclePx = 0;
  let pulsePx = 0;
  let moveAt = null;
  let pulseAt = null;
  let tailAt = null;
  let releaseWag = 0;
  let pStart = null;
  let pHappy = null;
  let pEnd = null;

  function faceAt(t) {
    if (pEnd !== null && t >= pStart && t < pEnd) return t >= pHappy ? 'happy' : 'soft';
    if (active && happy && moveAt !== null && t - moveAt <= IDLE_MS) return 'happy';
    return t < softUntil ? 'soft' : 'calm';
  }

  function wagAt(t, face) {
    if (face === 'calm') return 0;
    if (pEnd !== null) {
      if (t >= pEnd || t < pStart) return 0;
      const spec = t >= pHappy ? TAIL.happy : TAIL.soft;
      const mark = t >= pHappy ? pHappy : pStart;
      return decay(spec[0], t - mark, spec[1], spec[2]);
    }
    if (tailAt === null) return 0;
    if (!active) return decay(releaseWag, Math.max(0, t - tailAt), 0, 560);
    const spec = TAIL[face];
    return decay(spec[0], Math.max(0, t - tailAt), spec[1], spec[2]);
  }

  function touch(now) {
    const t = time(now);
    if (t === null || t < clock) return;
    clock = t;
    if (active) return;
    if (pEnd !== null && t < pEnd) softUntil = t + SMILE_LINGER_MS;
    pStart = pHappy = pEnd = null;
    active = true;
    happy = false;
    cycleAt = null;
    cyclePx = 0;
    pulsePx = 0;
    moveAt = null;
    pulseAt = null;
    tailAt = null;
    releaseWag = 0;
  }

  function stroke(now, pixels) {
    const t = time(now);
    if (t === null || t < clock) return false;
    clock = t;
    if (!active) return false;
    if (typeof pixels !== 'number' || !Number.isFinite(pixels) || pixels <= 0) return false;
    const step = pixels > CAP ? CAP : pixels;

    if (happy && moveAt !== null && t - moveAt > IDLE_MS) {
      happy = false;
      pulseAt = null;
      pulsePx = 0;
      cycleAt = null;
      cyclePx = 0;
    }
    if (moveAt !== null && t - moveAt > PAUSE_MS) {
      cycleAt = null;
      cyclePx = 0;
      pulsePx = 0;
    }
    if (cycleAt === null) cycleAt = t;
    cyclePx += step;
    pulsePx += step;
    moveAt = t;
    tailAt = t;

    if (t >= softUntil) softAt = null;
    if (softAt === null && cyclePx >= SOFT_PX && t - cycleAt >= SOFT_MS) {
      softAt = t;
    }
    if (softAt !== null) softUntil = t + SMILE_LINGER_MS + (happy ? IDLE_MS : 0);
    if (happy) {
      if (pulseAt !== null && t - pulseAt >= PULSE_MS && pulsePx >= PULSE_PX) {
        pulseAt = t;
        pulsePx = 0;
        return true;
      }
      return false;
    }
    if (cyclePx >= HAPPY_PX && t - cycleAt >= HAPPY_MS && softAt !== null && t - softAt >= HOLD_MS) {
      happy = true;
      softUntil = t + IDLE_MS + SMILE_LINGER_MS;
      pulseAt = t;
      pulsePx = 0;
      return true;
    }
    return false;
  }

  function release(now) {
    const t = time(now);
    if (t === null || t < clock) return;
    clock = t;
    const face = faceAt(t);
    if (!active) {
      // Interrupted button previews may keep their smile, but must not finish in the background.
      if (pEnd !== null && t < pEnd) softUntil = t + SMILE_LINGER_MS;
      pStart = pHappy = pEnd = null;
      return;
    }
    releaseWag = wagAt(t, face);
    if (face !== 'calm' && moveAt !== null) softUntil = t + SMILE_LINGER_MS;
    active = false;
    happy = false;
    cycleAt = null;
    cyclePx = 0;
    pulsePx = 0;
    moveAt = null;
    pulseAt = null;
    tailAt = t;
  }

  function reset() {
    clock = 0;
    active = false;
    softAt = null;
    softUntil = -Infinity;
    happy = false;
    cycleAt = null;
    cyclePx = 0;
    pulsePx = 0;
    moveAt = null;
    pulseAt = null;
    tailAt = null;
    releaseWag = 0;
    pStart = null;
    pHappy = null;
    pEnd = null;
  }

  function preview(now) {
    const t = time(now);
    if (t === null || t < clock) return false;
    clock = t;
    if (active) return false;
    if (pEnd !== null && t < pEnd) return false;
    pStart = t;
    pHappy = t + PREVIEW_MS;
    pEnd = t + PREVIEW_MS + PREVIEW_HOLD_MS;
    softAt = t;
    softUntil = pEnd + SMILE_LINGER_MS;
    moveAt = null;
    tailAt = t;
    return true;
  }

  function sample(now) {
    const t = time(now);
    const at = t === null ? clock : t;
    const face = faceAt(at);
    return { face, active, intensity: HEAD[face], wag: round(wagAt(at, face)) };
  }

  return { touch, stroke, release, reset, sample, preview };
}
