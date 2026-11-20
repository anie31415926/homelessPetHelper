import fs from 'node:fs/promises';
import path from 'node:path';
import { createHash, randomUUID } from 'node:crypto';
import { spawn } from 'node:child_process';
import { fileURLToPath } from 'node:url';
import { checkReceipt } from './collaboration.mjs';

const projectRelative = 'unity/HomelessPetHelper';
const inputFolders = ['Assets', 'Packages', 'ProjectSettings'];
const toolPaths = ['tools/verify-unity.mjs', 'tools/unity.ps1', 'tools/check-unity.mjs', 'tools/collaboration.mjs'];
const baselineRelative = 'test-results/pet-motion/baseline-rig.bundle';
const sha256 = bytes => createHash('sha256').update(bytes).digest('hex');
const ordered = files => [...files].sort((a, b) => a.path < b.path ? -1 : a.path > b.path ? 1 : 0);
export const fingerprint = files => sha256(JSON.stringify(ordered(files)));
const writeJson = (file, value) => fs.writeFile(file, `${JSON.stringify(value, null, 2)}\n`);
const readJson = async file => JSON.parse((await fs.readFile(file, 'utf8')).replace(/^\uFEFF/u, ''));

async function fileInput(root, relative) {
    const absolute = path.join(root, relative);
    if (!(await fs.lstat(absolute)).isFile()) throw new Error(`Expected a regular file: ${absolute}`);
    const bytes = await fs.readFile(absolute);
    return { path: relative, bytes: bytes.length, sha256: sha256(bytes) };
}

export async function projectInputs(project) {
    const files = [];
    async function visit(relative) {
        const absolute = path.join(project, relative);
        const info = await fs.lstat(absolute);
        if (info.isSymbolicLink()) throw new Error(`Snapshot inputs cannot be symlinks: ${absolute}`);
        if (info.isDirectory()) {
            for (const name of (await fs.readdir(absolute)).sort()) await visit(`${relative}/${name}`);
        } else files.push(await fileInput(project, relative));
    }
    for (const folder of inputFolders) await visit(folder);
    if (!files.length) throw new Error('Empty project input manifest.');
    return ordered(files);
}

export function assertSameInputs(expected, actual, label) {
    if (!Array.isArray(actual) || fingerprint(expected) !== fingerprint(actual))
        throw new Error(`${label}: input files differ from the saved source manifest.`);
}

async function optionalBaseline(root) {
    try { return await fileInput(root, baselineRelative); }
    catch (error) { if (error.code === 'ENOENT') return null; throw error; }
}

// Always wait for the actual process exit, even if Unity has already written XML.
export async function runCommand(command, args, { cwd, stdout, stderr }) {
    const output = await fs.open(stdout, 'wx');
    const errors = await fs.open(stderr, 'wx');
    try {
        return await new Promise((resolve, reject) => {
            const child = spawn(command, args, { cwd, shell: false, windowsHide: true,
                stdio: ['ignore', output.fd, errors.fd] });
            child.once('error', reject);
            child.once('close', (code, signal) => resolve({ code, signal }));
        });
    } finally { await output.close(); await errors.close(); }
}

function requireSuccess(result, label) {
    if (result?.code !== 0 || result.signal) throw new Error(`${label}: process failed (${JSON.stringify(result)}).`);
}

export async function validateUnityRun(root, recordPath, expected) {
    const record = await readJson(recordPath);
    const reportPath = path.join(root, 'test-results/unity', `${expected.stage}-isolated-${expected.runId}.xml`);
    const logPath = reportPath.replace(/\.xml$/u, '.log');
    const isolated = path.relative(path.join(root, 'test-results/unity'), record.projectPath ?? '');
    const total = record.tests?.total;
    if (record.schemaVersion !== 1 || record.task !== expected.stage || record.status !== 'passed' ||
        record.isolated !== true || record.graphics !== true || record.testFilter ||
        record.exitCode !== 0 || !record.finishedAt || record.editorVersion !== expected.editorVersion ||
        !record.editorProductVersion?.startsWith(`${expected.editorVersion}_`) || !record.editorFileVersion ||
        !record.powershellVersion || !new RegExp(`^isolated-${expected.stage}-[a-f0-9]{32}$`, 'u').test(isolated) ||
        path.resolve(record.reportPath ?? '') !== reportPath || path.resolve(record.logPath ?? '') !== logPath ||
        !Number.isSafeInteger(total) || total <= 0 || record.tests.result !== 'Passed' ||
        record.tests.passed !== total || record.tests.failed !== 0 || record.tests.skipped !== 0 ||
        record.tests.inconclusive !== 0)
        throw new Error(`${expected.stage}: incomplete, filtered, skipped or failed Unity run record.`);
    assertSameInputs(expected.files, record.inputFiles, `${expected.stage} pre-launch snapshot`);
    assertSameInputs(expected.files, await projectInputs(record.projectPath), `${expected.stage} final snapshot`);
    let baseline = null;
    if (expected.stage === 'PlayMode' && expected.baseline) {
        baseline = await fileInput(record.projectPath, 'PetMotionBaseline/pet-motion');
        if (baseline.sha256 !== expected.baseline.sha256 || baseline.bytes !== expected.baseline.bytes)
            throw new Error('PlayMode: external baseline input differs.');
    }
    for (const [file, hash] of [[reportPath, record.reportSha256], [logPath, record.logSha256]]) {
        const bytes = await fs.readFile(file);
        if (!bytes.length || sha256(bytes) !== hash) throw new Error(`Missing, empty or changed report/log: ${file}`);
    }
    return { ...record, inputFingerprint: fingerprint(record.inputFiles), baseline };
}

export async function verifyUnity(root, options, dependencies = {}) {
    const execute = dependencies.execute ?? runCommand;
    const check = dependencies.check ?? checkReceipt;
    const progress = dependencies.progress ?? (message => console.log(message));
    const runId = `${new Date().toISOString().replace(/[^0-9TZ]/gu, '')}-${randomUUID()}`;
    const directory = path.join(root, 'test-results/unity', `verify-${runId}`);
    await fs.mkdir(directory, { recursive: true });
    const summary = { schemaVersion: 1, runId, startedAt: new Date().toISOString(), status: 'running',
        task: options.task, sessionId: options.session, nodeVersion: process.version,
        platform: process.platform, scope: 'Saved-project, full graphical EditMode and PlayMode; no build/device validation.',
        stages: [] };
    const summaryPath = path.join(directory, 'summary.json');
    await writeJson(summaryPath, summary);
    try {
        if (!options.task || !options.session) throw new Error('Explicit --task and --session are required.');
        summary.receipt = await check(root, options);
        for (const warning of summary.receipt.warnings) progress(`WARNING: ${warning}`);
        const files = await projectInputs(path.join(root, projectRelative));
        const tools = await Promise.all(toolPaths.map(file => fileInput(root, file)));
        const baseline = await optionalBaseline(root);
        const versionFile = await fs.readFile(path.join(root, projectRelative, 'ProjectSettings/ProjectVersion.txt'), 'utf8');
        summary.editorVersion = /^m_EditorVersion: (.+)$/mu.exec(versionFile)?.[1].trim();
        if (!summary.editorVersion) throw new Error('Missing Unity version in ProjectVersion.txt.');
        summary.inputFingerprint = fingerprint(files);
        summary.inputCount = files.length;
        summary.tools = tools;
        summary.baseline = baseline;
        await writeJson(path.join(directory, 'inputs.json'), { files, fingerprint: summary.inputFingerprint, baseline });
        async function checkUnchanged() {
            assertSameInputs(files, await projectInputs(path.join(root, projectRelative)), 'Source project');
            assertSameInputs(tools, await Promise.all(toolPaths.map(file => fileInput(root, file))), 'Verification tools');
            if (JSON.stringify(baseline) !== JSON.stringify(await optionalBaseline(root)))
                throw new Error('External baseline changed during verification.');
            const receipt = await check(root, options);
            if (receipt.record.contextDigest !== summary.receipt.record.contextDigest)
                throw new Error('Coordination context changed during verification.');
        }
        progress(`Verification ${runId}: ${files.length} saved inputs; checking source.`);
        const sourceOutput = path.join(directory, 'source-check.json');
        const sourceProcess = await execute(process.execPath, [path.join(root, 'tools/check-unity.mjs')],
            { cwd: root, stdout: sourceOutput, stderr: path.join(directory, 'source-check.stderr.log') });
        summary.stages.push({ stage: 'source', process: sourceProcess });
        requireSuccess(sourceProcess, 'Source check');
        const source = await readJson(sourceOutput);
        if (!Array.isArray(source.issues) || source.issues.length || !(source.scripts > 0) || !(source.assemblies > 0))
            throw new Error('Missing or failed source-check result.');
        summary.sourceCheck = { report: sourceOutput, sha256: sha256(await fs.readFile(sourceOutput)), ...source };
        for (const stage of ['EditMode', 'PlayMode']) {
            await checkUnchanged();
            progress(`${stage}: running full suite in a new isolated snapshot.`);
            const recordPath = path.join(directory, `${stage}.json`);
            const args = ['-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', path.join(root, 'tools/unity.ps1'),
                '-Task', stage, '-Isolated', '-Graphics', '-ReportSuffix', runId, '-RunRecordPath', recordPath];
            if (options.editor) args.push('-EditorPath', options.editor);
            const processResult = await execute(options.powershell ?? 'powershell.exe', args,
                { cwd: root, stdout: path.join(directory, `${stage}.stdout.log`),
                    stderr: path.join(directory, `${stage}.stderr.log`) });
            const step = { stage, process: processResult, recordPath };
            summary.stages.push(step);
            await writeJson(summaryPath, summary);
            requireSuccess(processResult, stage);
            step.run = await validateUnityRun(root, recordPath,
                { stage, runId, files, baseline, editorVersion: summary.editorVersion });
            await checkUnchanged();
            progress(`${stage}: ${step.run.tests.passed}/${step.run.tests.total}; snapshot and report verified.`);
        }
        await checkUnchanged();
        summary.status = 'passed';
    } catch (error) { summary.status = 'failed'; summary.error = error.message; }
    summary.finishedAt = new Date().toISOString();
    await writeJson(summaryPath, summary);
    progress(`${summary.status.toUpperCase()}: ${summaryPath}`);
    return { summary, summaryPath };
}

async function main(args) {
    if (args.length === 1 && ['--help', '-h'].includes(args[0])) {
        console.log('node tools/verify-unity.mjs --task docs/协作任务/<task>.md --session <id> [--editor <Unity.exe>] [--powershell <powershell.exe>]');
        return;
    }
    const options = {};
    for (let i = 0; i < args.length; i += 2) {
        const key = args[i].slice(2);
        if (!['task', 'session', 'editor', 'powershell'].includes(key) || !args[i].startsWith('--') ||
            !args[i + 1] || options[key]) throw new Error(`Invalid/duplicate option: ${args[i]}`);
        options[key] = args[i + 1];
    }
    const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
    const { summary } = await verifyUnity(root, options);
    if (summary.status !== 'passed') process.exitCode = 1;
}

if (process.argv[1] && path.resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
    main(process.argv.slice(2)).catch(error => { console.error(error.message); process.exitCode = 1; });
}
