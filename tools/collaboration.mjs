import fs from 'node:fs/promises';
import path from 'node:path';
import { createHash, randomUUID } from 'node:crypto';
import { fileURLToPath } from 'node:url';
import { TextDecoder } from 'node:util';

const taskDirectory = 'docs/协作任务';
const recordDirectory = 'docs/协作执行记录';
const requiredFiles = ['AGENTS.md', 'docs/当前协作状态.md', `${taskDirectory}/README.md`, `${recordDirectory}/README.md`];
const statuses = ['进行中', '待验证', '阻塞', '待协调', '交接中', '完成', '已取消'];
const endedStatuses = new Set(['完成', '已取消']);
const phases = ['start', 'resume', 'scope-change', 'delivery'];
const maxAgeMs = 60 * 60 * 1000;
const sha256 = value => createHash('sha256').update(value).digest('hex');
const digestFiles = files => sha256(JSON.stringify(files));

function relativePath(value, directoryAllowed = false) {
    if (typeof value !== 'string' || !value || value !== value.trim() ||
        /[\\:*?"<>|{}\u0000-\u001f]/u.test(value) || value.startsWith('/'))
        throw new Error(`Invalid repository-relative path: ${value}`);
    const parts = (directoryAllowed && value.endsWith('/') ? value.slice(0, -1) : value).split('/');
    if (parts.some(part => !part || part === '.' || part === '..'))
        throw new Error(`Invalid repository-relative path: ${value}`);
    return value;
}

function taskPath(value) {
    relativePath(value);
    if (path.posix.dirname(value) !== taskDirectory || !value.endsWith('.md') || value.endsWith('/README.md'))
        throw new Error(`Task must be a Markdown file directly inside ${taskDirectory}: ${value}`);
    return value;
}

function sessionId(value) {
    if (typeof value !== 'string' || !/^[A-Za-z0-9][A-Za-z0-9._:-]{0,199}$/u.test(value))
        throw new Error('A valid explicit session ID is required');
    return value;
}

async function readWithin(root, relative) {
    const absolute = await fs.realpath(path.join(root, relativePath(relative)));
    const fromRoot = path.relative(root, absolute);
    if (fromRoot.startsWith(`..${path.sep}`) || fromRoot === '..' || path.isAbsolute(fromRoot))
        throw new Error(`Path resolves outside repository: ${relative}`);
    const bytes = await fs.readFile(absolute);
    return { path: relative, sha256: sha256(bytes), text: new TextDecoder('utf-8', { fatal: true }).decode(bytes) };
}

function statusOf(text) {
    const matches = [...text.matchAll(/^- 状态：[ \t]*(.+)$/gmu)];
    if (matches.length > 1) throw new Error('Task has multiple status fields');
    return matches[0]?.[1].trim().replace(/[。.]$/u, '') ?? null;
}

function claimOf(text, required = false) {
    const blocks = [...text.matchAll(/^```collaboration[ \t]*\r?\n([\s\S]*?)^```[ \t]*\r?$/gmu)];
    if (!blocks.length && !required) return null;
    if (blocks.length !== 1) throw new Error('Task needs exactly one collaboration JSON block; see execution-record README');
    const claim = JSON.parse(blocks[0][1]);
    sessionId(claim.sessionId);
    if (!Array.isArray(claim.writePaths) || !claim.writePaths.length)
        throw new Error('collaboration.writePaths must contain the claimed repository-relative paths');
    const writePaths = claim.writePaths.map(value => relativePath(value, true)).sort();
    if (new Set(writePaths).size !== writePaths.length) throw new Error('Duplicate writePaths in task claim');
    return { sessionId: claim.sessionId, writePaths };
}

function receiptDirectory(task) {
    return `${recordDirectory}/${path.posix.basename(task, '.md')}`;
}

function assertOwner(context, session, phase) {
    sessionId(session);
    if (!phases.includes(phase)) throw new Error(`Invalid phase; choose ${phases.join(', ')}`);
    const own = context.tasks.find(task => task.path === context.task);
    if (!statuses.includes(own.status)) throw new Error('Own task needs a standard status from the task template');
    if (endedStatuses.has(own.status) && phase !== 'delivery')
        throw new Error('Ended tasks can only acknowledge the delivery phase; do not restart them implicitly');
    const claim = claimOf(own.text, true);
    if (claim.sessionId !== session) throw new Error('Session does not match the task claim');
    const covers = target => claim.writePaths.some(p => p === target || (p.endsWith('/') && target.startsWith(p)));
    if (!covers(context.task) || !covers(`${receiptDirectory(context.task)}/`))
        throw new Error('writePaths must cover the task file and its execution-record directory');
    return claim;
}

export async function collectContext(root, task) {
    root = await fs.realpath(root);
    taskPath(task);
    const names = (await fs.readdir(path.join(root, taskDirectory))).filter(name => name.endsWith('.md') && name !== 'README.md').sort();
    const tasks = await Promise.all(names.map(async name => {
        const file = await readWithin(root, `${taskDirectory}/${name}`);
        return { ...file, status: statusOf(file.text) };
    }));
    if (!tasks.some(entry => entry.path === task)) throw new Error(`Task not found: ${task}`);
    const selected = tasks.filter(entry => entry.path === task || !endedStatuses.has(entry.status));
    const base = await Promise.all(requiredFiles.map(file => readWithin(root, file)));
    const documents = [...base, ...selected].sort((a, b) => a.path < b.path ? -1 : a.path > b.path ? 1 : 0);
    const files = documents.map(file => ({ path: file.path, sha256: file.sha256 }));
    const warnings = [];
    for (const entry of selected) {
        if (!statuses.includes(entry.status)) warnings.push(`Nonstandard/missing status; retained as unfinished: ${entry.path}`);
        try {
            if (!claimOf(entry.text)) warnings.push(`No structured scope; inspect the task manually: ${entry.path}`);
        } catch (error) {
            throw new Error(`${entry.path}: ${error.message}`);
        }
    }
    return { root, task, documents, tasks: selected, files, contextDigest: digestFiles(files), warnings };
}

async function ensureDirectory(root, relative) {
    let current = root;
    for (const part of relativePath(relative).split('/')) {
        current = path.join(current, part);
        try { await fs.mkdir(current); }
        catch (error) { if (error.code !== 'EEXIST') throw error; }
        const info = await fs.lstat(current);
        if (info.isSymbolicLink() || !info.isDirectory()) throw new Error(`Record directory must not be a symlink or file: ${current}`);
    }
}

export async function acknowledge(root, { task, session, digest, phase }, now = new Date()) {
    if (!/^[a-f0-9]{64}$/u.test(digest ?? '')) throw new Error('Use the contextDigest printed by read');
    const first = await collectContext(root, task);
    assertOwner(first, session, phase);
    if (first.contextDigest !== digest) throw new Error('Stale context: run read again before acknowledging');
    const current = await collectContext(root, task);
    const claim = assertOwner(current, session, phase);
    if (current.contextDigest !== digest) throw new Error('Context changed during acknowledgement; run read again');
    const record = {
        schemaVersion: 1, task, sessionId: session, phase, readAt: now.toISOString(),
        writePaths: claim.writePaths, contextDigest: digest, files: current.files,
    };
    const directory = receiptDirectory(task);
    await ensureDirectory(current.root, directory);
    const recordPath = `${directory}/${record.readAt.replaceAll(':', '-')}-${randomUUID()}.json`;
    await fs.writeFile(path.join(current.root, recordPath), `${JSON.stringify(record, null, 2)}\n`, { flag: 'wx' });
    return { record, recordPath, warnings: current.warnings };
}

function validateRecord(record, task, session, now) {
    if (record?.schemaVersion !== 1 || record.task !== task || record.sessionId !== session || !phases.includes(record.phase))
        throw new Error('Invalid receipt schema, task, session or phase');
    const readAt = Date.parse(record.readAt);
    if (!Number.isFinite(readAt) || new Date(readAt).toISOString() !== record.readAt || readAt > now.getTime())
        throw new Error('Invalid receipt readAt timestamp');
    if (now.getTime() - readAt > maxAgeMs) throw new Error('Expired receipt (over 60 minutes); read and acknowledge again');
    if (!Array.isArray(record.files) || !record.files.length || !Array.isArray(record.writePaths))
        throw new Error('Invalid receipt files or writePaths');
    const paths = record.files.map(file => relativePath(file.path));
    if (new Set(paths).size !== paths.length || record.files.some(file => !/^[a-f0-9]{64}$/u.test(file.sha256 ?? '')) ||
        digestFiles(record.files) !== record.contextDigest)
        throw new Error('Invalid receipt file fingerprints or contextDigest');
}

function changedFiles(previous, current) {
    const before = new Map(previous.map(file => [file.path, file.sha256]));
    const after = new Map(current.map(file => [file.path, file.sha256]));
    return [...new Set([...before.keys(), ...after.keys()])].sort().filter(file => before.get(file) !== after.get(file));
}

export async function checkReceipt(root, { task, session }, now = new Date()) {
    const current = await collectContext(root, task);
    sessionId(session);
    const directory = receiptDirectory(task);
    let names;
    try { names = await fs.readdir(path.join(current.root, directory)); }
    catch (error) {
        if (error.code === 'ENOENT') throw new Error(`Missing receipt for ${task}; run read and ack`);
        throw error;
    }
    const latest = names.filter(name => name.endsWith('.json')).sort().at(-1);
    if (!latest) throw new Error(`Missing receipt for ${task}; run read and ack`);
    if (names.filter(name => name.endsWith('.json') && name.slice(0, 24) === latest.slice(0, 24)).length > 1)
        throw new Error('Ambiguous receipts with the same timestamp; read and acknowledge again');
    const recordPath = `${directory}/${latest}`;
    const record = JSON.parse((await readWithin(current.root, recordPath)).text);
    validateRecord(record, task, session, now);
    const claim = assertOwner(current, session, record.phase);
    if (JSON.stringify(claim.writePaths) !== JSON.stringify(record.writePaths))
        throw new Error('Task writePaths changed; read and acknowledge again');
    if (current.contextDigest !== record.contextDigest)
        throw new Error(`Stale context; changed files/tasks: ${changedFiles(record.files, current.files).join(', ')}`);
    const final = await collectContext(current.root, task);
    if (final.contextDigest !== current.contextDigest) throw new Error('Context changed during check; read and acknowledge again');
    return { record, recordPath, warnings: final.warnings };
}

const usage = `Usage (from repository root; Node >=20.9):
  node tools/collaboration.mjs read --task docs/协作任务/<task>.md
  node tools/collaboration.mjs ack --task docs/协作任务/<task>.md --session <id> --phase start|resume|scope-change|delivery --digest <contextDigest>
  node tools/collaboration.mjs check --task docs/协作任务/<task>.md --session <id>
Read/ack/check only inspect coordination context and declared scope; they do not prove understanding or lock files.
See docs/协作执行记录/README.md.`;

async function main(args) {
    if (!args.length || (args.length === 1 && ['--help', '-h'].includes(args[0]))) {
        console.log(usage);
        return;
    }
    const command = args.shift();
    const allowed = { read: ['task'], ack: ['task', 'session', 'phase', 'digest'], check: ['task', 'session'] }[command];
    if (!allowed) throw new Error(`Unknown command: ${command}\n${usage}`);
    const options = {};
    while (args.length) {
        const key = args.shift();
        const value = args.shift();
        if (!key?.startsWith('--') || !allowed.includes(key.slice(2)) || key.slice(2) in options || !value || value.startsWith('--'))
            throw new Error(`Invalid, missing or duplicate option: ${key}`);
        options[key.slice(2)] = value;
    }
    for (const key of allowed) if (!options[key]) throw new Error(`Missing --${key}`);
    const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
    if (command === 'read') {
        const context = await collectContext(root, options.task);
        for (const warning of context.warnings) console.log(`WARNING: ${warning}`);
        for (const file of context.documents) console.log(`\n### ${file.path} (sha256 ${file.sha256})\n${file.text}`);
        console.log(`\ncontextDigest: ${context.contextDigest}\nReview this context, then acknowledge with --digest ${context.contextDigest}`);
        return;
    }
    const result = command === 'ack' ? await acknowledge(root, options) : await checkReceipt(root, options);
    for (const warning of result.warnings) console.log(`WARNING: ${warning}`);
    console.log(`${command === 'ack' ? 'Recorded' : 'PASS: current coordination receipt'}: ${result.recordPath}`);
    console.log(`Session: ${result.record.sessionId}; phase: ${result.record.phase}; readAt: ${result.record.readAt}`);
    console.log('Scope overlap and authorization still require human/agent review.');
}

if (process.argv[1] && path.resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
    main(process.argv.slice(2)).catch(error => {
        console.error(`Collaboration check failed: ${error.message}`);
        process.exitCode = 1;
    });
}
