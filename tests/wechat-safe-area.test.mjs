import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { runInNewContext } from 'node:vm';
import test from 'node:test';

const plugin = readFileSync(new URL('../unity/HomelessPetHelper/Assets/Scripts/Modules/Rescue/UI/WeChatSafeArea.jslib', import.meta.url), 'utf8');

function readArea(wx) {
    const library = {};
    const heap = new Float32Array(8);
    runInNewContext(plugin, { LibraryManager: { library }, mergeInto: Object.assign, HEAPF32: heap, wx });
    return library.HPHReadWeChatSafeArea(4) ? Array.from(heap.slice(1, 5)) : null;
}

const windowInfo = {
    windowWidth: 400, windowHeight: 800, pixelRatio: 3,
    safeArea: { left: 8, top: 48, right: 392, bottom: 764 }
};

test('reserves the capsule row and gesture inset independently of pixel ratio', () => {
    const edges = readArea({
        getWindowInfo: () => windowInfo,
        getMenuButtonBoundingClientRect: () => ({ left: 310, top: 38, right: 392, bottom: 74 })
    });
    assert.deepEqual(edges, [.02, .045, .98, .9075].map(Math.fround));
});

test('never expands the OS safe area and ignores missing or invalid capsule bounds', () => {
    for (const menu of [undefined, () => ({ left: 0, top: 0, right: 0, bottom: 0 }),
        () => ({ left: 310, top: 12, right: 390, bottom: 44 }), () => { throw new Error(); }]) {
        assert.deepEqual(readArea({ getWindowInfo: () => windowInfo, getMenuButtonBoundingClientRect: menu }),
            [.02, .045, .98, .94].map(Math.fround));
    }
});

test('can still avoid the capsule on devices reporting no OS safe area', () => {
    assert.deepEqual(readArea({
        getWindowInfo: () => ({ ...windowInfo, safeArea: undefined }),
        getMenuButtonBoundingClientRect: () => ({ left: 310, top: 12, right: 390, bottom: 40 })
    }), [0, 0, 1, Math.fround(.95)]);
});

test('leaves layout to Unity outside WeChat or with invalid window dimensions', () => {
    assert.equal(readArea(undefined), null);
    assert.equal(readArea({ getWindowInfo: () => ({ windowWidth: 0, windowHeight: 800 }) }), null);
    assert.equal(readArea({ getWindowInfo: () => { throw new Error(); } }), null);
});
