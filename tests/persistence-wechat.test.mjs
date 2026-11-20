import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import test from 'node:test';
import vm from 'node:vm';

const source = await readFile(new URL(
  '../unity/HomelessPetHelper/Assets/Scripts/Framework/Persistence/WeChat/Plugins/WeChatSave.jslib',
  import.meta.url), 'utf8');

function bridge(wx) {
  const functions = {};
  const heap = new Map();
  let nextPointer = 1;
  const runtime = {
    LibraryManager: { library: functions },
    mergeInto: Object.assign,
    UTF8ToString: pointer => heap.get(pointer),
    lengthBytesUTF8: text => Buffer.byteLength(text, 'utf8'),
    _malloc: () => nextPointer++,
    stringToUTF8: (text, pointer) => heap.set(pointer, text),
  };
  if (wx !== undefined) runtime.wx = wx;
  vm.runInNewContext(source, runtime, { filename: 'WeChatSave.jslib' });
  runtime.HPHSaveResponse = functions.$HPHSaveResponse;
  function pointer(text) {
    const address = nextPointer++;
    heap.set(address, text);
    return address;
  }
  return {
    read: key => JSON.parse(heap.get(functions.HPHSaveRead(pointer(key)))),
    write: (key, value) => JSON.parse(heap.get(functions.HPHSaveWrite(pointer(key), pointer(value)))),
  };
}

function storage(values = new Map()) {
  return {
    getStorageInfoSync: () => ({ keys: [...values.keys()] }),
    getStorageSync: key => values.get(key),
    setStorageSync: (key, value) => values.set(key, value),
  };
}

test('absent runtime is an error, never a fresh profile', () => {
  assert.deepEqual(bridge().read('save.slot0'), { ok: false, code: 'unsupported_runtime' });
  assert.deepEqual(bridge().write('save.slot0', '{}'), { ok: false, code: 'unsupported_runtime' });
});

test('genuine missing key is distinct from a stored empty or damaged string', () => {
  const api = bridge(storage(new Map([['empty', ''], ['bad', 'truncated']])));
  assert.deepEqual(api.read('absent'), { ok: true, value: null });
  assert.deepEqual(api.read('empty'), { ok: true, value: '' });
  assert.deepEqual(api.read('bad'), { ok: true, value: 'truncated' });
});

test('the real bridge preserves complete Unicode save text and slot isolation', () => {
  const values = new Map([['save.slot0', 'old']]);
  const api = bridge(storage(values));
  const checkpoint = JSON.stringify({ name: '\u5c0f\u732b', note: 'quoted " text \\ newline\n' });
  assert.deepEqual(api.write('save.slot1', checkpoint), { ok: true });
  assert.deepEqual(api.read('save.slot1'), { ok: true, value: checkpoint });
  assert.equal(values.get('save.slot0'), 'old');
});

test('storage-info exceptions do not become missing keys', () => {
  const wx = storage();
  wx.getStorageInfoSync = () => { throw new Error('access unavailable'); };
  assert.deepEqual(bridge(wx).read('save'), { ok: false, code: 'read_failed' });
});

test('invalid storage-info results are errors', () => {
  const wx = storage();
  wx.getStorageInfoSync = () => ({});
  assert.deepEqual(bridge(wx).read('save'), { ok: false, code: 'invalid_storage_info' });
});

test('existing-key read failures and wrong value types are not fresh profiles', () => {
  const wx = storage(new Map([['save', 7]]));
  assert.deepEqual(bridge(wx).read('save'), { ok: false, code: 'invalid_value_type' });
  wx.getStorageSync = () => { throw new Error('read failed'); };
  assert.deepEqual(bridge(wx).read('save'), { ok: false, code: 'read_failed' });
});

test('quota and other write errors are reported, not silently accepted', () => {
  const wx = storage();
  wx.setStorageSync = () => { throw new Error('quota exceeded'); };
  assert.deepEqual(bridge(wx).write('save', '{}'), { ok: false, code: 'write_failed' });
});
