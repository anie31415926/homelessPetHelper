import data from './appearances/cat-orange-side.json' with {type: 'json'};

const requiredLandmarks = ['hip', 'knee', 'hock', 'toe', 'shoulder', 'elbow', 'wrist', 'frontToe', 'skull'];
const roles = ['head', 'torso', 'tail', 'nearHindThigh', 'nearHindPaw', 'hindLower', 'shaft', 'nearFront'];
const fail = field => {throw new Error(`Invalid animal appearance: ${field}`);};
const finite = (value, field) => {if (!Number.isFinite(value)) fail(field);};
const positive = (value, field) => {finite(value, field); if (value <= 0) fail(field);};
const vector = (value, size, field) => {if (!Array.isArray(value) || value.length !== size) fail(field); value.forEach(x => finite(x, field));};
const repoPath = (value, field) => {if (typeof value !== 'string' || !/^[A-Za-z0-9_./-]+$/.test(value) || value.startsWith('/') || value.split('/').some(x => !x || x === '..' || x === '.')) fail(field);};
function numericTree(value, field) {
    if (typeof value === 'number') finite(value, field);
    else if (value && typeof value === 'object') for (const [key, child] of Object.entries(value)) numericTree(child, `${field}.${key}`);
}
function freeze(value) {
    if (value && typeof value === 'object' && !Object.isFrozen(value)) {Object.values(value).forEach(freeze); Object.freeze(value);}
    return value;
}

// Validate before creating geometry or writing assets; one immutable profile per appearance.
export function defineAppearance(input) {
    const profile = structuredClone(input);
    if (profile.schemaVersion !== 1 || profile.template !== 'side-quadruped-v1') fail('schemaVersion/template');
    if (typeof profile.id !== 'string' || !/^[a-z][a-z0-9.-]+$/.test(profile.id)) fail('id');
    numericTree(profile, profile.id);
    vector(profile.source?.origin, 2, 'source.origin'); positive(profile.source?.pixelsPerUnit, 'source.pixelsPerUnit');
    for (const name of requiredLandmarks) vector(profile.landmarks?.[name], 2, `landmarks.${name}`);
    validateMotion(profile);
    validateSkin(profile.skin);
    for (const [key, value] of Object.entries(profile.art ?? {})) repoPath(value, `art.${key}`);
    for (const key of ['atlasManifest', 'atlasTexture', 'sourceImage']) repoPath(profile.art?.[key], `art.${key}`);
    for (const key of ['assetFolder', 'actorPath', 'templateActorPath', 'atlasPath', 'shaderPath']) {
        repoPath(profile.unity?.[key], `unity.${key}`);
        if (!profile.unity[key].startsWith('Assets/')) fail(`unity.${key}`);
    }
    if (!/^[A-Za-z][A-Za-z0-9_]*$/.test(profile.unity.assetPrefix)) fail('unity.assetPrefix');
    for (const key of ['bakePath', 'fixturePath']) {
        repoPath(profile.export?.[key], `export.${key}`);
        if (!profile.export[key].endsWith('.json')) fail(`export.${key} extension`);
    }
    if (!/^(art-source|test-results)\//.test(profile.export.bakePath) ||
        !/^(unity\/HomelessPetHelper\/Assets\/Tests|test-results)\//.test(profile.export.fixturePath) ||
        profile.export.bakePath === profile.export.fixturePath) fail('export destinations');
    positive(profile.framing?.orthoSize, 'framing.orthoSize'); vector(profile.framing?.cameraPosition, 3, 'framing.cameraPosition');
    return freeze(profile);
}

function validateMotion(p) {
    for (const key of ['frontBend', 'hindBend', 'base']) {
        const limits = p.rig?.jointLimits?.[key]; vector(limits, 2, `rig.jointLimits.${key}`);
        if (limits[0] >= limits[1]) fail(`rig.jointLimits.${key}`);
    }
    positive(p.rig?.spineLength, 'rig.spineLength'); positive(p.rig?.sideDepth, 'rig.sideDepth');
    for (const key of ['farHindOffset', 'farFrontOffset', 'pelvisDegrees']) vector(p.rig?.[key], 2, `rig.${key}`);
    for (const key of ['neckDropDegrees', 'skullRaiseDegrees', 'foldHipRadians', 'foldKneeRadians']) finite(p.rig?.[key], `rig.${key}`);
    const standingHip = p.landmarks.hip.map((value, i) => (p.source.origin[i] - value) / p.source.pixelsPerUnit);
    if (!p.sit?.keys?.[0]?.hip || standingHip.some((v, i) => Math.abs(v - p.sit.keys[0].hip[i]) > 1e-8)) fail('sit standing hip must match landmarks');
    positive(p.sit?.seconds, 'sit.seconds'); vector(p.sit?.shoulderShift, 2, 'sit.shoulderShift');
    const keys = p.sit?.keys;
    if (!Array.isArray(keys) || keys.length < 2 || keys[0].at !== 0 || keys.at(-1).at !== 1) fail('sit.keys endpoints');
    keys.forEach((key, i) => {
        finite(key.at, 'sit.keys.at'); if (i && key.at <= keys[i - 1].at) fail('sit.keys order');
        vector(key.hip, 2, 'sit.keys.hip'); vector(key.spine, 6, 'sit.keys.spine');
        if (i === 0) {if (key.hockDegrees !== null) fail('standing hock must derive from landmarks');}
        else finite(key.hockDegrees, 'sit.keys.hockDegrees');
        if (typeof key.name !== 'string' || !key.name.trim()) fail('sit.keys.name');
    });
    for (const key of ['distance', 'lift', 'duration', 'farDelay']) positive(p.sit.frontStep?.[key], `sit.frontStep.${key}`);
    for (const key of ['seconds', 'stride', 'lift', 'bodyBob']) positive(p.walk?.[key], `walk.${key}`);
    for (const key of ['bodyFloor', 'tailFloor', 'coatFloor', 'bodyBlendDuration', 'tailCorrectionDegrees']) positive(p.contact?.[key], `contact.${key}`);
    finite(p.contact?.bodyBlendStart, 'contact.bodyBlendStart');
    if (p.contact.bodyBlendStart < 0 || p.contact.bodyBlendStart + p.contact.bodyBlendDuration > 1) fail('contact blend interval');
    for (const key of ['raisedDegrees', 'swishLiftDegrees', 'swishDegrees']) finite(p.tail?.[key], `tail.${key}`);
    positive(p.tail?.periodSeconds, 'tail.periodSeconds');
    if (p.tail.swishDegrees < 0 || p.tail.swishLiftDegrees < p.tail.swishDegrees) fail('tail ground-safe swish range');
}

function validateSkin(s) {
    for (const role of roles) if (typeof s?.layers?.[role] !== 'string' || !s.layers[role]) fail(`skin.layers.${role}`);
    positive(s.meshSpacingPixels, 'skin.meshSpacingPixels');
    for (const key of ['hipBlendPixels', 'elbowBlendPixels', 'wristBlendPixels', 'tibiaWidths', 'metatarsalWidths']) {
        vector(s[key], 2, `skin.${key}`); positive(s[key][1], `skin.${key}[1]`);
    }
    for (const key of ['tailBindOffset', 'tailJoinPixels', 'pelvisDegrees']) vector(s[key], 2, `skin.${key}`);
    finite(s.tailSitDegrees, 'skin.tailSitDegrees'); finite(s.pelvisLift, 'skin.pelvisLift');
    for (const key of ['farTint', 'nearTint']) if (!/^#[a-fA-F0-9]{6}$/.test(s[key])) fail(`skin.${key}`);
    for (const key of ['far', 'near', 'farForeleg', 'nearForeleg', 'torso', 'tail', 'head', 'thighOffset', 'pawOffset', 'lowerCoatOffset']) finite(s.orders?.[key], `skin.orders.${key}`);
    for (const key of ['centerX', 'startY', 'endY']) finite(s.shaftPixels?.[key], `skin.shaftPixels.${key}`);
    for (const key of ['width', 'capLength']) positive(s.shaftPixels?.[key], `skin.shaftPixels.${key}`);
    if (s.shaftPixels.endY <= s.shaftPixels.startY) fail('skin.shaftPixels interval');
}

export function validateAtlas(profile, manifest) {
    if (manifest?.schemaVersion !== 1) fail('atlas schemaVersion');
    positive(manifest.width, 'atlas.width'); positive(manifest.height, 'atlas.height');
    if (!Array.isArray(manifest.layers)) fail('atlas.layers');
    const ids = new Set();
    for (const layer of manifest.layers) {
        if (!layer.id || ids.has(layer.id)) fail('atlas duplicate layer'); ids.add(layer.id);
        for (const key of ['width', 'height']) positive(layer[key], `atlas.${layer.id}.${key}`);
        for (const key of ['left', 'top', 'atlasX', 'atlasY']) finite(layer[key], `atlas.${layer.id}.${key}`);
        if (layer.atlasX < 0 || layer.atlasY < 0 || layer.atlasX + layer.width > manifest.width || layer.atlasY + layer.height > manifest.height) fail(`atlas.${layer.id} bounds`);
    }
    for (const role of roles) if (!ids.has(profile.skin.layers[role])) fail(`atlas missing role ${role}`);
    for (const role of ['torso', 'tail', 'nearHindThigh']) {
        const layer = manifest.layers.find(l => l.id === profile.skin.layers[role]);
        if (!Array.isArray(layer.support) || layer.support.length === 0) fail(`atlas.${role}.support`);
        layer.support.forEach(point => vector(point, 2, `atlas.${role}.support`));
    }
}

export const DEFAULT_APPEARANCE = defineAppearance(data);
