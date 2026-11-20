import { createHash } from 'node:crypto';
import { createReadStream } from 'node:fs';
import { mkdir, readFile, readdir, realpath, stat, writeFile } from 'node:fs/promises';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const statuses = ['planned', 'candidate', 'temporary', 'approved', 'retired'];
const timings = ['preview-only', 'first-play', 'on-demand', 'unassigned'];
const roles = ['runtime', 'candidate', 'master', 'workflow', 'generation-record', 'reference'];
const imageExtensions = new Set(['.png', '.jpg', '.jpeg', '.webp', '.tga', '.psd', '.exr', '.tif', '.tiff', '.bmp']);
const repo = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const catalogPath = 'art-source/resource-catalog.json';

function validRelativePath(value) {
    return typeof value === 'string' && value.length > 0 && !value.includes('\\') &&
        !value.includes(':') && !path.posix.isAbsolute(value) &&
        !value.split('/').some(part => part === '..' || part === '.' || part === '');
}

function inside(root, file) {
    const relative = path.relative(root, file);
    return relative !== '..' && !relative.startsWith(`..${path.sep}`) && !path.isAbsolute(relative);
}

function validate(catalog) {
    const errors = [];
    const require = (condition, message) => { if (!condition) errors.push(message); };
    const positiveBytes = value => Number.isSafeInteger(value) && value > 0;
    require(catalog.schemaVersion === 1, 'Expected schemaVersion 1.');
    require(catalog.firstPlayableScope && ['draft', 'confirmed'].includes(catalog.firstPlayableScope.status),
        'firstPlayableScope.status must be draft or confirmed.');
    require(typeof catalog.firstPlayableScope?.notes === 'string', 'firstPlayableScope.notes is required.');
    const budgets = catalog.budgets;
    require(budgets && positiveBytes(budgets.firstPlaySourcePlanningBytes) &&
        positiveBytes(budgets.firstPlayDownloadTargetBytes), 'First-play budgets must be positive integer bytes.');
    const modules = budgets?.moduleSourceReviewBytes;
    require(modules && Object.keys(modules).length > 0 && Object.values(modules).every(positiveBytes),
        'Module source review budgets must be positive integer bytes.');
    require(Array.isArray(catalog.assets), 'assets must be an array.');
    const ids = new Set();
    const ownedImages = new Set();
    for (const asset of Array.isArray(catalog.assets) ? catalog.assets : []) {
        const id = asset.id;
        require(typeof id === 'string' && /^[a-z0-9]+(?:[.-][a-z0-9]+)*$/.test(id) && !ids.has(id),
            `Invalid or duplicate asset id: ${id}`);
        ids.add(id);
        require(typeof asset.name === 'string' && asset.name.length > 0, `${id}: name is required.`);
        require(typeof asset.use === 'string' && asset.use.length > 0, `${id}: use is required.`);
        require(typeof asset.availability === 'string' && asset.availability.length > 0,
            `${id}: availability must describe the content boundary, independently of loading.`);
        require(modules && Object.hasOwn(modules, asset.module), `${id}: unknown module.`);
        require(statuses.includes(asset.status), `${id}: invalid status.`);
        require(timings.includes(asset.loadTiming), `${id}: invalid loadTiming.`);
        require(positiveBytes(asset.plannedBytes), `${id}: plannedBytes must be positive integer bytes.`);
        require(asset.review && ['visual', 'rights', 'target'].every(key =>
            ['pending', 'accepted'].includes(asset.review[key])), `${id}: invalid review fields.`);
        require(Array.isArray(asset.files), `${id}: files must be an array.`);
        const primary = [];
        for (const file of Array.isArray(asset.files) ? asset.files : []) {
            require(roles.includes(file.role), `${id}: invalid file role.`);
            require(validRelativePath(file.path), `${id}: file path must stay repository-relative: ${file.path}`);
            if (['runtime', 'candidate'].includes(file.role)) {
                primary.push(file);
                require(!ownedImages.has(file.path), `${id}: image already owned by another entry: ${file.path}`);
                ownedImages.add(file.path);
            }
            require(file.reviewedSha256 == null || /^[a-f0-9]{64}$/.test(file.reviewedSha256),
                `${id}: invalid reviewedSha256.`);
        }
        require(primary.length <= 1, `${id}: keep only one current runtime/candidate image per entry.`);
        if (!['planned', 'retired'].includes(asset.status)) {
            require(primary.length === 1, `${id}: current image is required.`);
        }
        if (asset.status === 'approved') {
            require(primary[0]?.role === 'runtime' && primary[0]?.reviewedSha256 &&
                ['first-play', 'on-demand'].includes(asset.loadTiming) &&
                ['visual', 'rights', 'target'].every(key => asset.review?.[key] === 'accepted'),
            `${id}: approved requires runtime image, reviewed hash, delivery timing and accepted reviews.`);
        }
    }
    if (errors.length) throw new Error(errors.join('\n'));
}

async function scanImages(root, relative) {
    const directory = path.join(root, relative);
    let entries;
    try { entries = await readdir(directory, { withFileTypes: true }); }
    catch (error) { if (error.code === 'ENOENT') return []; throw error; }
    const files = [];
    for (const entry of entries) {
        const child = `${relative}/${entry.name}`;
        if (entry.isSymbolicLink()) continue;
        if (entry.isDirectory()) files.push(...await scanImages(root, child));
        else if (imageExtensions.has(path.extname(entry.name).toLowerCase())) files.push(child);
    }
    return files.sort();
}

export async function buildCatalogReport(root, catalog) {
    validate(catalog);
    const resolvedRoot = await realpath(root);
    const findings = [];
    const assets = [];
    const registered = new Set();
    const hashes = new Map();
    for (const asset of catalog.assets) {
        const files = [];
        for (const file of asset.files) {
            registered.add(file.path);
            let observed = { ...file, bytes: null, sha256: null };
            try {
                const absolute = await realpath(path.join(root, file.path));
                if (!inside(resolvedRoot, absolute)) throw new Error(`File resolves outside repository: ${file.path}`);
                const info = await stat(absolute);
                if (!info.isFile()) throw new Error(`Not a file: ${file.path}`);
                const hash = createHash('sha256');
                for await (const chunk of createReadStream(absolute)) hash.update(chunk);
                observed = { ...file, bytes: info.size, sha256: hash.digest('hex') };
                if (file.reviewedSha256 && file.reviewedSha256 !== observed.sha256) {
                    findings.push({ level: 'error', code: 'ReviewedFileChanged', id: asset.id, path: file.path });
                }
            } catch (error) {
                if (error.code !== 'ENOENT') throw error;
                findings.push({ level: 'error', code: 'MissingFile', id: asset.id, path: file.path });
            }
            files.push(observed);
        }
        const image = files.find(file => ['runtime', 'candidate'].includes(file.role));
        const plannedOrSourceBytes = image?.bytes ?? asset.plannedBytes;
        if (image?.bytes > asset.plannedBytes && asset.status !== 'retired') {
            findings.push({ level: 'review', code: 'ImageOverPlan', id: asset.id, path: image.path });
        }
        if (asset.status !== 'retired' && image?.sha256) {
            if (hashes.has(image.sha256)) findings.push({ level: 'review', code: 'IdenticalImage',
                id: asset.id, otherId: hashes.get(image.sha256), path: image.path });
            else hashes.set(image.sha256, asset.id);
        }
        if (['first-play', 'on-demand'].includes(asset.loadTiming) && asset.status !== 'retired' &&
            asset.status !== 'approved') findings.push({ level: 'review', code: 'NotApprovedForDelivery', id: asset.id });
        assets.push({ ...asset, files, sourceBytes: image?.bytes ?? null, plannedOrSourceBytes });
    }
    const inventory = [...await scanImages(root, 'art-source'),
        ...await scanImages(root, 'unity/HomelessPetHelper/Assets')];
    for (const file of inventory) {
        if (!registered.has(file)) findings.push({ level: 'review', code: 'UnregisteredImage', path: file });
    }
    const active = assets.filter(asset => asset.status !== 'retired');
    const total = list => list.reduce((sum, asset) => sum + asset.plannedOrSourceBytes, 0);
    const first = active.filter(asset => asset.loadTiming === 'first-play');
    const modules = Object.entries(catalog.budgets.moduleSourceReviewBytes).map(([module, reviewBytes]) => {
        const entries = active.filter(asset => asset.module === module && asset.loadTiming !== 'preview-only');
        const projectedBytes = total(entries);
        if (projectedBytes > reviewBytes) findings.push({ level: 'review', code: 'ModuleOverPlan', module });
        return { module, entries: entries.length, projectedBytes, reviewBytes };
    });
    if (catalog.firstPlayableScope.status !== 'confirmed' || first.length === 0) {
        findings.push({ level: 'review', code: 'FirstPlayableScopeIncomplete' });
    }
    if (active.some(asset => asset.loadTiming === 'unassigned')) {
        findings.push({ level: 'review', code: 'UnassignedDeliveryTiming' });
    }
    if (total(first) > catalog.budgets.firstPlaySourcePlanningBytes) {
        findings.push({ level: 'review', code: 'FirstPlaySourceOverPlan' });
    }
    return { schemaVersion: 1, firstPlayableScope: catalog.firstPlayableScope, budgets: catalog.budgets,
        actualFirstPlayDownloadBytes: null, assets, modules,
        firstPlay: { entries: first.length, plannedOrSourceBytes: total(first) },
        preview: { entries: active.filter(asset => asset.loadTiming === 'preview-only').length,
            imageFiles: active.filter(asset => asset.loadTiming === 'preview-only' && asset.sourceBytes != null).length,
            sourceBytes: active.filter(asset => asset.loadTiming === 'preview-only')
                .reduce((sum, asset) => sum + (asset.sourceBytes ?? 0), 0) },
        findings: findings.sort((a, b) => JSON.stringify(a).localeCompare(JSON.stringify(b), 'en')) };
}

const cell = value => String(value ?? '').replaceAll('|', '\\|').replaceAll('\n', ' ');
const kib = bytes => (bytes / 1024).toFixed(1);

export function toMarkdown(report) {
    const lines = ['# 图片资源台账检查', '',
        '本报告由 art-source/resource-catalog.json 生成，不手改；不是 Unity 依赖、分包或微信下载实测。', '',
        `首次可玩范围：${report.firstPlayableScope.status}。${report.firstPlayableScope.notes}`, '',
        `首批登记 ${report.firstPlay.entries} 项，源文件/计划参考合计 ${kib(report.firstPlay.plannedOrSourceBytes)} KiB。`,
        `源文件规划线 ${kib(report.budgets.firstPlaySourcePlanningBytes)} KiB；` +
            `转换后首次图片下载目标 ${kib(report.budgets.firstPlayDownloadTargetBytes)} KiB。`,
        '**实际首次下载：未测量，不能判达标；首批为 0 或范围未确认也不代表通过。**', '',
        `仅试做/历史计划登记 ${report.preview.entries} 项，其中已有运行/候选图 ${report.preview.imageFiles} 张，` +
            `源文件 ${kib(report.preview.sourceBytes)} KiB；不算正式首批。`, '',
        '## 模块参考量', '', '| 模块 | 交付计划项 | 源文件/计划 KiB | 源文件复核线 KiB |',
        '| --- | --- | --- | --- |'];
    for (const module of report.modules) {
        lines.push(`| ${cell(module.module)} | ${module.entries} | ${kib(module.projectedBytes)} | ${kib(module.reviewBytes)} |`);
    }
    lines.push('', '实际运行/候选图用文件字节，未产出的图用 plannedBytes；母图/工作流不计运行量。',
        '共享文件只登记一个归属，其余记引用；内容哈希相同仅提示复核，不推断实际打包去重。', '',
        '## 资源清单', '', '| ID / 名称 | 模块 | 状态 / 加载时机 | 实际源 KiB | 计划 KiB |',
        '| --- | --- | --- | --- | --- |');
    for (const asset of report.assets) {
        lines.push(`| ${cell(asset.id)} / ${cell(asset.name)} | ${cell(asset.module)} | ` +
            `${cell(asset.status)} / ${cell(asset.loadTiming)} | ` +
            `${asset.sourceBytes == null ? '无当前图' : kib(asset.sourceBytes)} | ${kib(asset.plannedBytes)} |`);
    }
    lines.push('', '## 复核项', '');
    if (!report.findings.length) lines.push('无台账复核项；不等于微信包或美术验收通过。');
    for (const finding of report.findings) {
        lines.push((`- ${finding.level}: ${finding.code} ${finding.id ?? finding.module ?? ''} ` +
            `${finding.path ?? ''}${finding.otherId ? ` (same as ${finding.otherId})` : ''}`).trimEnd());
    }
    return `${lines.join('\n').trimEnd()}\n`;
}

async function main() {
    const catalog = JSON.parse(await readFile(path.join(repo, catalogPath), 'utf8'));
    const report = await buildCatalogReport(repo, catalog);
    const output = path.join(repo, 'test-results/resource-catalog');
    await mkdir(output, { recursive: true });
    await writeFile(path.join(output, 'report.json'), `${JSON.stringify(report, null, 2)}\n`);
    await writeFile(path.join(output, 'report.md'), toMarkdown(report));
    console.log(toMarkdown(report));
    console.log(`Reports: ${output}`);
    if (report.findings.some(finding => finding.level === 'error')) process.exitCode = 1;
}

if (process.argv[1] && path.resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
    main().catch(error => { console.error(error.message); process.exitCode = 1; });
}
