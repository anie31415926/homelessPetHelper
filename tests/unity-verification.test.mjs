import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs/promises';
import path from 'node:path';
import os from 'node:os';
import { createHash, randomUUID } from 'node:crypto';
import { verifyUnity, projectInputs, fingerprint, runCommand } from '../tools/verify-unity.mjs';
import { collectContext, acknowledge } from '../tools/collaboration.mjs';

const projectRelative = 'unity/HomelessPetHelper';
const tools = ['verify-unity.mjs', 'unity.ps1', 'check-unity.mjs', 'collaboration.mjs'];
const hash = bytes => createHash('sha256').update(bytes).digest('hex');
const json = (file, data) => fs.writeFile(file, JSON.stringify(data));

async function fixture(t) {
    const root = await fs.mkdtemp(path.join(os.tmpdir(), 'unity-verification-'));
    t.after(() => fs.rm(root, { recursive: true, force: true }));
    async function write(relative, value) {
        const file = path.join(root, relative);
        await fs.mkdir(path.dirname(file), { recursive: true });
        await fs.writeFile(file, value);
    }
    for (const folder of ['Assets', 'Packages', 'ProjectSettings'])
        await write(`${projectRelative}/${folder}/input.txt`, `saved-${folder}\r\n`);
    await write(`${projectRelative}/ProjectSettings/ProjectVersion.txt`, 'm_EditorVersion: fixture\n');
    for (const tool of tools) await write(`tools/${tool}`, `tool-${tool}`);
    const commands = [];
    const check = async () => ({ record: { contextDigest: 'current-context' }, warnings: [] });
    async function execute(command, args, io) {
        commands.push({ command, args });
        if (command === process.execPath) {
            await json(io.stdout, { tool: 'fixture', scripts: 1, assemblies: 1, issues: [] });
        } else {
            const stage = args[args.indexOf('-Task') + 1];
            const runId = args[args.indexOf('-ReportSuffix') + 1];
            const recordPath = args[args.indexOf('-RunRecordPath') + 1];
            const projectPath = path.join(root, 'test-results/unity', `isolated-${stage}-${randomUUID().replaceAll('-', '')}`);
            await fs.cp(path.join(root, projectRelative), projectPath, { recursive: true });
            const baseline = path.join(root, 'test-results/pet-motion/baseline-rig.bundle');
            if (stage === 'PlayMode') {
                try {
                    await fs.mkdir(path.join(projectPath, 'PetMotionBaseline'));
                    await fs.copyFile(baseline, path.join(projectPath, 'PetMotionBaseline/pet-motion'));
                } catch (error) { if (error.code !== 'ENOENT') throw error; }
            }
            const reportPath = path.join(root, 'test-results/unity', `${stage}-isolated-${runId}.xml`);
            const logPath = reportPath.replace(/\.xml$/u, '.log');
            await fs.writeFile(reportPath, '<test-run result="Passed" total="1" passed="1" failed="0"/>');
            await fs.writeFile(logPath, 'fixture process completed');
            await json(recordPath, { schemaVersion: 1, task: stage, status: 'passed',
                isolated: true, graphics: true, testFilter: '', exitCode: 0, finishedAt: new Date().toISOString(),
                editorVersion: 'fixture', editorFileVersion: 'fixture', powershellVersion: 'fixture',
                editorProductVersion: 'fixture_revision',
                projectPath, reportPath, logPath, inputFiles: await projectInputs(projectPath),
                tests: { result: 'Passed', total: 1, passed: 1, failed: 0, skipped: 0, inconclusive: 0 },
                reportSha256: hash(await fs.readFile(reportPath)), logSha256: hash(await fs.readFile(logPath)) });
        }
        return { code: 0, signal: null };
    }
    async function run(overrides = {}) {
        return verifyUnity(root, { task: 'task', session: 'session' },
            { execute, check, progress() {}, ...overrides });
    }
    return { root, write, execute, check, commands, run };
}

async function alterRecord(args, mutation) {
    const file = args[args.indexOf('-RunRecordPath') + 1];
    const data = JSON.parse(await fs.readFile(file, 'utf8'));
    await mutation(data);
    await json(file, data);
}

test('full pipeline has distinct runs, records exact bytes and executes both suites without filters', async t => {
    const f = await fixture(t);
    await f.write('test-results/pet-motion/baseline-rig.bundle', 'optional-baseline');
    const first = await f.run();
    const next = await f.run();
    assert.equal(first.summary.status, 'passed', first.summary.error);
    assert.equal(next.summary.status, 'passed', next.summary.error);
    assert.notEqual(first.summary.runId, next.summary.runId);
    assert.equal(first.summary.inputCount, 4);
    assert.deepEqual(first.summary.stages.map(stage => stage.stage), ['source', 'EditMode', 'PlayMode']);
    assert.equal(first.summary.stages[2].run.baseline.sha256, hash('optional-baseline'));
    assert.equal(first.summary.stages[1].run.inputFingerprint, first.summary.inputFingerprint);
    assert(f.commands.every(call => !call.args.includes('-TestFilter')));
    const inputs = await projectInputs(path.join(f.root, projectRelative));
    await f.write(`${projectRelative}/Assets/input.txt`, 'saved-Assets\n');
    assert.notEqual(fingerprint(inputs), fingerprint(await projectInputs(path.join(f.root, projectRelative))));
});

for (const reason of ['missing receipt', 'stale context', 'expired receipt', 'wrong session']) {
    test(`coordination ${reason} blocks all source/Unity execution without acknowledging`, async t => {
        const f = await fixture(t);
        const result = await f.run({ check: async () => { throw new Error(reason); } });
        assert.equal(result.summary.status, 'failed');
        assert.match(result.summary.error, new RegExp(reason));
        assert.equal(f.commands.length, 0);
    });
}

for (const failure of ['nonzero', 'signal', 'empty', 'issues', 'missing']) {
    test(`source ${failure} blocks Unity`, async t => {
        const f = await fixture(t);
        const result = await f.run({ execute: async (command, args, io) => {
            if (failure === 'nonzero') return { code: 7, signal: null };
            if (failure === 'signal') return { code: null, signal: 'SIGTERM' };
            if (failure === 'empty') await fs.writeFile(io.stdout, '');
            if (failure === 'issues') await json(io.stdout, { scripts: 1, assemblies: 1, issues: ['bad'] });
            return { code: 0, signal: null };
        } });
        assert.equal(result.summary.status, 'failed');
        assert.equal(result.summary.stages.length, 1);
    });
}

const mutations = {
    'wrong Unity version': record => { record.editorProductVersion = 'different_revision'; },
    'nonzero Unity exit': record => { record.exitCode = 2; },
    'filtered suite': record => { record.testFilter = 'one-test'; },
    'zero tests': record => { record.tests.total = 0; },
    'failed tests': record => { record.tests.failed = 1; },
    'skipped tests': record => { record.tests.skipped = 1; },
    'incomplete process': record => { record.status = 'running'; },
    'missing step': record => { record.task = 'PlayMode'; },
    'stale report path': record => { record.reportPath = record.reportPath.replace('.xml', '-old.xml'); },
    'missing input': record => { record.inputFiles.pop(); },
    'changed pre-launch input': record => { record.inputFiles[0].sha256 = '0'.repeat(64); },
    'changed snapshot': async record => { await fs.appendFile(path.join(record.projectPath, 'Assets/input.txt'), 'new'); },
    'missing report': async record => { await fs.unlink(record.reportPath); },
    'empty report': async record => { await fs.writeFile(record.reportPath, ''); },
    'changed report': async record => { await fs.appendFile(record.reportPath, 'changed'); },
    'changed log': async record => { await fs.appendFile(record.logPath, 'changed'); },
};
for (const [name, mutate] of Object.entries(mutations)) {
    test(`${name} cannot produce a passing verification`, async t => {
        const f = await fixture(t);
        const result = await f.run({ execute: async (command, args, io) => {
            const exit = await f.execute(command, args, io);
            if (command !== process.execPath) await alterRecord(args, mutate);
            return exit;
        } });
        assert.equal(result.summary.status, 'failed');
        assert.equal(result.summary.stages.length, 2, result.summary.error);
    });
}

for (const target of ['source', 'tools', 'baseline', 'context', 'missing record', 'process exit']) {
    test(`${target} changing during a run fails before PlayMode`, async t => {
        const f = await fixture(t);
        const result = await f.run({ execute: async (command, args, io) => {
            const exit = await f.execute(command, args, io);
            if (command === process.execPath) return exit;
            if (target === 'source') await f.write(`${projectRelative}/Assets/new.txt`, 'concurrent');
            if (target === 'tools') await f.write('tools/unity.ps1', 'concurrent');
            if (target === 'baseline') await f.write('test-results/pet-motion/baseline-rig.bundle', 'new');
            if (target === 'context') throw new Error('Coordination context changed');
            if (target === 'missing record') await fs.unlink(args[args.indexOf('-RunRecordPath') + 1]);
            return target === 'process exit' ? { code: 3, signal: null } : exit;
        } });
        assert.equal(result.summary.status, 'failed');
        assert(f.commands.length <= 2);
    });
}

test('context change at final receipt check rejects otherwise successful suites', async t => {
    const f = await fixture(t);
    let reads = 0;
    const result = await f.run({ check: async () => {
        reads++;
        if (reads === 6) throw new Error('stale context at delivery');
        return f.check();
    } });
    assert.equal(result.summary.status, 'failed');
    assert.equal(result.summary.stages.length, 3);
});

test('command runner waits for real exit and retains nonzero exit and stderr', async t => {
    const f = await fixture(t);
    const stdout = path.join(f.root, 'stdout.log');
    const stderr = path.join(f.root, 'stderr.log');
    const result = await runCommand(process.execPath,
        ['-e', 'process.stdout.write("done"); process.stderr.write("failed"); process.exitCode = 7;'],
        { cwd: f.root, stdout, stderr });
    assert.equal(result.code, 7);
    assert.equal(await fs.readFile(stdout, 'utf8'), 'done');
    assert.equal(await fs.readFile(stderr, 'utf8'), 'failed');
});

test('real collaboration receipts are required, never auto-created, and rechecked during verification', async t => {
    const f = await fixture(t);
    const task = 'docs/协作任务/verification-fixture.md';
    const receiptDirectory = 'docs/协作执行记录/verification-fixture/';
    for (const file of ['AGENTS.md', 'docs/当前协作状态.md', 'docs/协作任务/README.md',
        'docs/协作执行记录/README.md']) await f.write(file, '# Fixture\n');
    await f.write(task, ['# Task', '- 状态：进行中。', '', '```collaboration',
        JSON.stringify({ sessionId: 'session', writePaths: [task, receiptDirectory] }), '```', ''].join('\n'));
    const options = { task, session: 'session' };
    const dependencies = { execute: f.execute, progress() {} };
    const missing = await verifyUnity(f.root, options, dependencies);
    assert.equal(missing.summary.status, 'failed');
    assert.match(missing.summary.error, /Missing receipt/u);
    assert.equal(f.commands.length, 0);
    await assert.rejects(fs.access(path.join(f.root, receiptDirectory)), { code: 'ENOENT' });
    const context = await collectContext(f.root, task);
    const receipt = await acknowledge(f.root, { ...options, digest: context.contextDigest, phase: 'start' });
    const valid = await verifyUnity(f.root, options, dependencies);
    assert.equal(valid.summary.status, 'passed', valid.summary.error);
    const changed = await verifyUnity(f.root, options, { ...dependencies, execute: async (command, args, io) => {
        const exit = await f.execute(command, args, io);
        if (command !== process.execPath) await f.write('AGENTS.md', '# New direction\n');
        return exit;
    } });
    assert.equal(changed.summary.status, 'failed');
    assert.match(changed.summary.error, /Stale context/u);
    assert.deepEqual(await fs.readdir(path.join(f.root, receiptDirectory)), [path.basename(receipt.recordPath)]);
});
