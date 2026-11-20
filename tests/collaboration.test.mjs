import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs/promises';
import os from 'node:os';
import path from 'node:path';
import { spawnSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';
import { collectContext, acknowledge, checkReceipt } from '../tools/collaboration.mjs';

const task = 'docs/协作任务/20261120-execution-fixture.md';
const peer = 'docs/协作任务/20261120-peer.md';
const ended = 'docs/协作任务/20261120-ended.md';
const receiptDir = 'docs/协作执行记录/20261120-execution-fixture';
const session = 'session-a';
const now = new Date('2026-11-20T08:00:00.000Z');
const claim = () => ({ sessionId: session, writePaths: [task, `${receiptDir}/`, 'src/new-file.js'] });
const ownText = (status = '进行中', scope = claim()) => [
    '# Task', `- 状态：${status}。`, '', '```collaboration', JSON.stringify(scope, null, 2), '```', '',
].join('\n');

async function write(root, name, content) {
    const absolute = path.join(root, name);
    await fs.mkdir(path.dirname(absolute), { recursive: true });
    await fs.writeFile(absolute, content);
}

async function fixture(t) {
    const root = await fs.mkdtemp(path.join(os.tmpdir(), 'hph-collaboration-'));
    t.after(() => fs.rm(root, { recursive: true, force: true }));
    for (const [name, text] of Object.entries({
        'AGENTS.md': '# Project instructions\nRead current collaboration state.\n',
        'docs/当前协作状态.md': '# Current direction\nKeep existing gameplay.\n',
        'docs/协作任务/README.md': '# Task template\n',
        'docs/协作执行记录/README.md': '# Execution-record protocol\n',
        [task]: ownText(),
        [peer]: '# Other agent\n- 状态：进行中。\nLegacy scope: src/peer.js\n',
        [ended]: '# Ended\n- 状态：完成。\nReleased.\n',
    })) await write(root, name, text);
    return root;
}

async function record(root, options = {}, at = now) {
    const context = await collectContext(root, task);
    return acknowledge(root, { task, session, digest: context.contextDigest, phase: 'start', ...options }, at);
}

test('read displays required context, retains legacy tasks and does not create a receipt', async t => {
    const root = await fixture(t);
    const context = await collectContext(root, task);
    assert.match(context.contextDigest, /^[a-f0-9]{64}$/u);
    for (const name of ['AGENTS.md', 'docs/当前协作状态.md', 'docs/协作任务/README.md', 'docs/协作执行记录/README.md', task, peer])
        assert(context.documents.some(file => file.path === name), name);
    assert(!context.documents.some(file => file.path === ended));
    assert(context.warnings.some(warning => warning.includes(peer) && warning.includes('structured scope')));
    await assert.rejects(fs.access(path.join(root, receiptDir)), { code: 'ENOENT' });
});

test('acknowledgements preserve history and the current receipt checks the declared session and scope', async t => {
    const root = await fixture(t);
    const first = await record(root);
    const original = await fs.readFile(path.join(root, first.recordPath), 'utf8');
    const later = new Date(now.getTime() + 1);
    const second = await record(root, { phase: 'resume' }, later);
    assert.notEqual(first.recordPath, second.recordPath);
    assert.equal((await fs.readdir(path.join(root, receiptDir))).length, 2);
    assert.equal(await fs.readFile(path.join(root, first.recordPath), 'utf8'), original);
    const checked = await checkReceipt(root, { task, session }, later);
    assert.equal(checked.recordPath, second.recordPath);
    assert.equal(checked.record.phase, 'resume');
    assert.equal(checked.record.readAt, later.toISOString());
    assert.deepEqual(checked.record.writePaths, claim().writePaths.sort());
    assert(checked.warnings.some(warning => warning.includes(peer)));
});

test('changed instructions, direction, template or own task invalidate checks and reject a stale acknowledgement', async t => {
    for (const name of ['AGENTS.md', 'docs/当前协作状态.md', 'docs/协作任务/README.md', 'docs/协作执行记录/README.md', task]) {
        const root = await fixture(t);
        const previous = await record(root);
        await fs.appendFile(path.join(root, name), '\nChanged after reading.\n');
        await assert.rejects(checkReceipt(root, { task, session }, now), error => error.message.includes('Stale context') && error.message.includes(name));
        await assert.rejects(acknowledge(root, { task, session, phase: 'resume', digest: previous.record.contextDigest }, now), /Stale context/u);
        assert.equal((await fs.readdir(path.join(root, receiptDir))).length, 1, 'rejected ack must not create a record');
    }
});

test('new, edited, finished and removed unfinished tasks require rereading; completed history is excluded', async t => {
    const root = await fixture(t);
    await record(root);
    await fs.appendFile(path.join(root, ended), 'Historical typo fixed.\n');
    await checkReceipt(root, { task, session }, now);
    const added = 'docs/协作任务/20261120-new.md';
    await write(root, added, '# New agent\n- 状态：待协调。\n');
    await assert.rejects(checkReceipt(root, { task, session }, now), /Stale context/u);
    await record(root, { phase: 'resume' }, new Date(now.getTime() + 1));
    await fs.appendFile(path.join(root, peer), 'Additional shared path.\n');
    await assert.rejects(checkReceipt(root, { task, session }, new Date(now.getTime() + 1)), /Stale context/u);
    await record(root, { phase: 'resume' }, new Date(now.getTime() + 2));
    await write(root, added, '# New agent\n- 状态：完成。\nReleased.\n');
    await assert.rejects(checkReceipt(root, { task, session }, new Date(now.getTime() + 2)), /Stale context/u);
    await record(root, { phase: 'resume' }, new Date(now.getTime() + 3));
    await fs.unlink(path.join(root, peer));
    await assert.rejects(checkReceipt(root, { task, session }, new Date(now.getTime() + 3)), /Stale context/u);
});

test('nonstandard or missing peer status stays visible instead of silently releasing that task', async t => {
    const root = await fixture(t);
    for (const text of ['# Peer\n- 状态：已完成工具；渠道未验。\n', '# Peer\nNo status field.\n']) {
        await write(root, peer, text);
        const context = await collectContext(root, task);
        assert(context.tasks.some(entry => entry.path === peer));
        assert(context.warnings.some(warning => warning.includes('retained as unfinished') && warning.includes(peer)));
    }
});

test('scope changes require a new acknowledgement and record the updated paths', async t => {
    const root = await fixture(t);
    await record(root);
    const changed = claim(); changed.writePaths.push('src/another-file.js');
    await write(root, task, ownText('进行中', changed));
    await assert.rejects(checkReceipt(root, { task, session }, now), /writePaths changed/u);
    const later = new Date(now.getTime() + 1);
    await record(root, { phase: 'scope-change' }, later);
    const result = await checkReceipt(root, { task, session }, later);
    assert(result.record.writePaths.includes('src/another-file.js'));
});

test('missing record and wrong session cannot pass or silently create a record', async t => {
    const root = await fixture(t);
    await assert.rejects(checkReceipt(root, { task, session }, now), /Missing receipt/u);
    await assert.rejects(record(root, { session: 'someone-else' }), /Session does not match/u);
    await assert.rejects(fs.access(path.join(root, receiptDir)), { code: 'ENOENT' });
    await record(root);
    await assert.rejects(checkReceipt(root, { task, session: 'someone-else' }, now), /session/u);
});

test('a corrupt latest receipt fails without falling back to an older successful record', async t => {
    const root = await fixture(t);
    const first = await record(root);
    const second = await record(root, { phase: 'resume' }, new Date(now.getTime() + 1));
    await fs.writeFile(path.join(root, second.recordPath), '{broken');
    await assert.rejects(checkReceipt(root, { task, session }, new Date(now.getTime() + 1)), SyntaxError);
    assert.equal(JSON.parse(await fs.readFile(path.join(root, first.recordPath), 'utf8')).phase, 'start');
});

test('simultaneous timestamps are reported as ambiguous instead of choosing a random receipt', async t => {
    const root = await fixture(t);
    await record(root);
    await record(root, { phase: 'resume' });
    await assert.rejects(checkReceipt(root, { task, session }, now), /Ambiguous receipts/u);
    const later = new Date(now.getTime() + 1);
    await record(root, { phase: 'resume' }, later);
    await checkReceipt(root, { task, session }, later);
});

test('invalid metadata, incomplete fingerprints and altered record scope fail validation', async t => {
    const root = await fixture(t);
    const valid = await record(root);
    const variants = [
        { ...valid.record, schemaVersion: 9 },
        { ...valid.record, task: peer },
        { ...valid.record, phase: 'typo' },
        { ...valid.record, files: [] },
        { ...valid.record, files: [valid.record.files[0], valid.record.files[0]] },
        { ...valid.record, contextDigest: '0'.repeat(64) },
        { ...valid.record, readAt: 'not-a-time' },
        { ...valid.record, writePaths: ['src/undeclared.js'] },
    ];
    for (const variant of variants) {
        await fs.writeFile(path.join(root, valid.recordPath), JSON.stringify(variant));
        await assert.rejects(checkReceipt(root, { task, session }, now));
    }
});

test('a receipt expires after 60 minutes without deleting history or releasing any paths', async t => {
    const root = await fixture(t);
    const valid = await record(root);
    await checkReceipt(root, { task, session }, new Date(now.getTime() + 60 * 60 * 1000));
    await assert.rejects(checkReceipt(root, { task, session }, new Date(now.getTime() + 60 * 60 * 1000 + 1)), /Expired receipt/u);
    await assert.rejects(checkReceipt(root, { task, session }, new Date(now.getTime() - 1)), /timestamp/u);
    assert.equal(await fs.readFile(path.join(root, task), 'utf8'), ownText());
    await fs.access(path.join(root, valid.recordPath));
});

test('delivery can record a completed task without allowing it to restart implicitly', async t => {
    const root = await fixture(t);
    await write(root, task, ownText('完成'));
    await assert.rejects(record(root), /Ended tasks/u);
    await record(root, { phase: 'delivery' });
    const result = await checkReceipt(root, { task, session }, now);
    assert.equal(result.record.phase, 'delivery');
});

test('claims require exact paths, an owner, and coverage of their task and record directory', async t => {
    const root = await fixture(t);
    for (const value of ['../outside', '/absolute', 'C:/outside', 'src\\file', 'src//file', 'src/./file', 'src/*.js']) {
        const invalid = claim(); invalid.writePaths.push(value);
        await write(root, task, ownText('进行中', invalid));
        await assert.rejects(collectContext(root, task), /relative path/u);
    }
    const duplicate = claim(); duplicate.writePaths.push(task);
    await write(root, task, ownText('进行中', duplicate));
    await assert.rejects(collectContext(root, task), /Duplicate/u);
    for (const writePaths of [[task], [`${receiptDir}/`]]) {
        await write(root, task, ownText('进行中', { sessionId: session, writePaths }));
        await assert.rejects(record(root), /must cover/u);
    }
    await write(root, task, '# Legacy own task\n- 状态：进行中。\n');
    await assert.rejects(record(root), /collaboration JSON block/u);
    await assert.rejects(collectContext(root, '../outside.md'), /relative path/u);
    await assert.rejects(collectContext(root, 'src/task.md'), /directly inside/u);
    await assert.rejects(fs.access(path.join(root, receiptDir)), { code: 'ENOENT' });
});

test('record output refuses a symlink/junction and does not write outside the repository', async t => {
    const root = await fixture(t);
    const outside = await fs.mkdtemp(path.join(os.tmpdir(), 'hph-collaboration-outside-'));
    t.after(() => fs.rm(outside, { recursive: true, force: true }));
    await fs.symlink(outside, path.join(root, receiptDir), 'junction');
    await assert.rejects(record(root), /symlink/u);
    assert.deepEqual(await fs.readdir(outside), []);
});

test('CLI works from a nested working directory and rejects invalid options with a failure exit code', async t => {
    const root = await fixture(t);
    const script = path.join(root, 'tools/collaboration.mjs');
    await fs.mkdir(path.dirname(script), { recursive: true });
    await fs.copyFile(fileURLToPath(new URL('../tools/collaboration.mjs', import.meta.url)), script);
    const cwd = path.join(root, 'unity/HomelessPetHelper');
    await fs.mkdir(cwd, { recursive: true });
    const run = args => spawnSync(process.execPath, [script, ...args], { cwd, encoding: 'utf8' });
    const read = run(['read', '--task', task]);
    assert.equal(read.status, 0, read.stderr);
    assert.match(read.stdout, /Keep existing gameplay/u);
    assert.match(read.stdout, /Legacy scope: src\/peer.js/u);
    const digest = read.stdout.match(/contextDigest: ([a-f0-9]{64})/u)[1];
    const ack = run(['ack', '--task', task, '--session', session, '--phase', 'start', '--digest', digest]);
    assert.equal(ack.status, 0, ack.stderr);
    const check = run(['check', '--task', task, '--session', session]);
    assert.equal(check.status, 0, check.stderr);
    assert.match(check.stdout, /PASS: current coordination receipt/u);
    for (const args of [['unknown'], ['check'], ['read', '--task', task, '--task', task], ['read', '--unexpected', task]]) {
        const failed = run(args);
        assert.equal(failed.status, 1);
        assert.match(failed.stderr, /Collaboration check failed/u);
    }
    assert.equal(run(['--help']).status, 0);
});
