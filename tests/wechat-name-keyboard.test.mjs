import { readFileSync } from 'node:fs';
import assert from 'node:assert/strict';
import test from 'node:test';
import { runInNewContext } from 'node:vm';

const source = readFileSync(new URL('../unity/HomelessPetHelper/Assets/Scripts/Game/UI/WeChatNameKeyboard.jslib',
  import.meta.url), 'utf8');

function bridge(hasWx = true) {
  const callbacks = { Input: new Set(), Confirm: new Set(), Complete: new Set() };
  const calls = { shows: [], hides: 0, output: '' };
  const wx = {
    showKeyboard(options) { calls.shows.push(options); },
    hideKeyboard() { calls.hides++; }
  };
  for (const [event, handlers] of Object.entries(callbacks)) {
    wx['onKeyboard' + event] = listener => handlers.add(listener);
    wx['offKeyboard' + event] = listener => handlers.delete(listener);
  }
  const context = {
    ...(hasWx ? { wx } : {}),
    LibraryManager: { library: {} },
    mergeInto: Object.assign,
    UTF8ToString: x => x,
    stringToUTF8: (value, _, capacity) => {
      assert.ok(Buffer.byteLength(value) < capacity);
      calls.output = value;
    }
  };
  runInNewContext(source, context);
  const api = context.LibraryManager.library;
  context.HPHNameKeyboard = api.$HPHNameKeyboard;
  const emit = (name, value) => [...callbacks[name]].forEach(fn => fn({ value }));
  const poll = ticket => api.HPHPollNameKeyboard(ticket, 1, 257);
  return { api, callbacks, calls, emit, poll };
}

test('native keyboard seeds the draft, streams Chinese text, and finishes without saving', () => {
  const { api, calls, emit, poll, callbacks } = bridge();
  assert.equal(api.HPHHasNameKeyboard(), 1);
  const ticket = api.HPHOpenNameKeyboard('栗子', 64);
  assert.equal(calls.shows[0].defaultValue, '栗子');
  assert.equal(calls.shows[0].confirmType, 'done');
  assert.equal(poll(ticket), 0);
  emit('Input', '阿橘');
  assert.equal(poll(ticket), 1);
  assert.equal(calls.output, '阿橘');
  emit('Confirm', '柚子');
  emit('Complete', '柚子');
  assert.equal(poll(ticket), 2);
  assert.equal(calls.output, '柚子');
  assert.ok(Object.values(callbacks).every(value => value.size === 0));
});

test('close ignores stale callbacks and does not close a new keyboard', () => {
  const { api, calls, poll, callbacks } = bridge();
  const old = api.HPHOpenNameKeyboard('A', 64);
  const [late] = callbacks.Complete;
  api.HPHCloseNameKeyboard(old);
  const next = api.HPHOpenNameKeyboard('B', 64);
  late({ value: 'stale' });
  api.HPHCloseNameKeyboard(old);
  assert.equal(calls.hides, 1);
  assert.equal(poll(next), 0);
  assert.equal(callbacks.Complete.size, 1);
});

test('show failure detaches all listeners and can be retried', () => {
  const { api, calls, poll, callbacks } = bridge();
  const ticket = api.HPHOpenNameKeyboard('A', 64);
  calls.shows[0].fail();
  assert.equal(poll(ticket), -1);
  assert.ok(Object.values(callbacks).every(value => value.size === 0));
  assert.notEqual(api.HPHOpenNameKeyboard('A', 64), ticket);
});

test('ordinary WebGL does not call wx', () => {
  const { api } = bridge(false);
  assert.equal(api.HPHHasNameKeyboard(), 0);
  assert.equal(api.HPHOpenNameKeyboard('A', 64), 0);
});
