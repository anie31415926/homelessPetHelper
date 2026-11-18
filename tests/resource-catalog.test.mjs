import assert from 'node:assert/strict';
import { createHash } from 'node:crypto';
import { mkdtemp, mkdir, readFile, readdir, rm, writeFile } from 'node:fs/promises';
import os from 'node:os';
import path from 'node:path';
import test from 'node:test';
import { buildCatalogReport, toMarkdown } from '../tools/resource-catalog.mjs';

const imagePath = 'unity/HomelessPetHelper/Assets/Art/cat.png';

function entry(overrides = {}) {
    return {
        id: 'animal.cat', name: 'Cat', module: 'Station', status: 'temporary', loadTiming: 'preview-only',
        availability: 'Editor sample only', use: 'Animal image', plannedBytes: 100,
        files: [{ role: 'runtime', path: imagePath }],
        review: { visual: 'pending', rights: 'pending', target: 'pending' }, ...overrides
    };
}

function catalog(assets = [entry()]) {
    return {
        schemaVersion: 1, firstPlayableScope: { status: 'draft', notes: 'Not confirmed' },
        budgets: { firstPlaySourcePlanningBytes: 150, firstPlayDownloadTargetBytes: 200,
            moduleSourceReviewBytes: { Station: 120 } }, assets
    };
}

async function fixture(t, files = { [imagePath]: 'image' }) {
    const root = await mkdtemp(path.join(os.tmpdir(), 'hph-resource-catalog-'));
    t.after(async () => { await rm(root, { recursive: true, force: true }); });
    for (const [relative, content] of Object.entries(files)) {
        const absolute = path.join(root, relative);
        await mkdir(path.dirname(absolute), { recursive: true });
        await writeFile(absolute, content);
    }
    return root;
}

const codes = report => report.findings.map(finding => finding.code);

test('uses source bytes and hashes, excludes masters from runtime and preview from delivery', async t => {
    const root = await fixture(t, { [imagePath]: 'small', 'art-source/cat-master.png': 'much bigger master' });
    const data = catalog([entry({ files: [
        { role: 'runtime', path: imagePath }, { role: 'master', path: 'art-source/cat-master.png' }
    ] })]);
    const report = await buildCatalogReport(root, data);
    assert.equal(report.assets[0].sourceBytes, 5);
    assert.equal(report.assets[0].files[0].sha256, createHash('sha256').update('small').digest('hex'));
    assert.equal(report.preview.sourceBytes, 5);
    assert.equal(report.firstPlay.plannedOrSourceBytes, 0);
    assert.equal(report.modules[0].projectedBytes, 0);
    assert.equal(report.actualFirstPlayDownloadBytes, null);
    assert(!codes(report).includes('UnregisteredImage'));
});

test('planned entries use estimates, not fake actual bytes or download success', async t => {
    const root = await fixture(t, {});
    const report = await buildCatalogReport(root, catalog([entry({ status: 'planned',
        loadTiming: 'first-play', files: [], plannedBytes: 110 })]));
    assert.equal(report.assets[0].sourceBytes, null);
    assert.equal(report.firstPlay.plannedOrSourceBytes, 110);
    assert.equal(report.modules[0].projectedBytes, 110);
    assert(codes(report).includes('FirstPlayableScopeIncomplete'));
    assert(codes(report).includes('NotApprovedForDelivery'));
    assert.match(toMarkdown(report), /实际首次下载：未测量/);
});

test('reports missing registered file instead of treating its estimate as observed bytes', async t => {
    const root = await fixture(t, {});
    const report = await buildCatalogReport(root, catalog());
    assert(codes(report).includes('MissingFile'));
    assert.equal(report.assets[0].sourceBytes, null);
    assert.equal(report.preview.sourceBytes, 0);
});

test('reviewed hash detects replacement even when file size stays equal', async t => {
    const root = await fixture(t);
    const data = catalog([entry({ files: [{ role: 'runtime', path: imagePath,
        reviewedSha256: createHash('sha256').update('other').digest('hex') }] })]);
    const report = await buildCatalogReport(root, data);
    assert(codes(report).includes('ReviewedFileChanged'));
});

test('reports unregistered Unity and source images without adding them to the catalog', async t => {
    const root = await fixture(t, { [imagePath]: 'cat', 'art-source/new.png': 'candidate',
        'unity/HomelessPetHelper/Assets/Art/new.jpg': 'new', 'art-source/workflow.json': '{}' });
    const data = catalog();
    const snapshot = JSON.stringify(data);
    const report = await buildCatalogReport(root, data);
    assert.equal(report.findings.filter(finding => finding.code === 'UnregisteredImage').length, 2);
    assert.equal(JSON.stringify(data), snapshot);
});

test('identical image content is a review candidate, not assumed exported deduplication', async t => {
    const secondPath = 'art-source/second.png';
    const root = await fixture(t, { [imagePath]: 'same', [secondPath]: 'same' });
    const report = await buildCatalogReport(root, catalog([entry(), entry({ id: 'animal.second',
        files: [{ role: 'candidate', path: secondPath }] })]));
    assert(codes(report).includes('IdenticalImage'));
    assert.equal(report.preview.sourceBytes, 8);
});

test('enforces one canonical image owner and one current image per entry', async t => {
    const root = await fixture(t);
    await assert.rejects(buildCatalogReport(root, catalog([entry(), entry({ id: 'animal.other' })])),
        /image already owned/);
    await assert.rejects(buildCatalogReport(root, catalog([entry({ files: [
        { role: 'runtime', path: imagePath }, { role: 'candidate', path: 'art-source/other.png' }
    ] })])), /one current/);
});

test('rejects ambiguous enums, invalid bytes, ids and modules', async t => {
    const root = await fixture(t);
    for (const changes of [{ status: 'final' }, { loadTiming: 'early' }, { plannedBytes: -1 },
        { plannedBytes: 0.5 }, { id: '../cat' }, { module: 'Unknown' }]) {
        await assert.rejects(buildCatalogReport(root, catalog([entry(changes)])));
    }
});

test('rejects absolute paths and traversal rather than reading outside the repository', async t => {
    const root = await fixture(t);
    for (const file of ['../cat.png', '/tmp/cat.png', 'C:/cat.png', 'a/../../cat.png', 'a\\cat.png']) {
        await assert.rejects(buildCatalogReport(root,
            catalog([entry({ files: [{ role: 'runtime', path: file }] })])), /repository-relative/);
    }
});

test('approval requires accepted reviews, a pinned runtime image and delivery timing', async t => {
    const root = await fixture(t);
    await assert.rejects(buildCatalogReport(root, catalog([entry({ status: 'approved' })])), /approved requires/);
    const report = await buildCatalogReport(root, catalog([entry({ status: 'approved', loadTiming: 'first-play',
        files: [{ role: 'runtime', path: imagePath,
            reviewedSha256: createHash('sha256').update('image').digest('hex') }],
        review: { visual: 'accepted', rights: 'accepted', target: 'accepted' } })]));
    assert(!codes(report).includes('NotApprovedForDelivery'));
    assert(!codes(report).includes('ReviewedFileChanged'));
});

test('warns at source and module planning lines but not by comparing source to download target', async t => {
    const root = await fixture(t, { [imagePath]: Buffer.alloc(160) });
    const report = await buildCatalogReport(root, catalog([entry({ loadTiming: 'first-play' })]));
    assert(codes(report).includes('ImageOverPlan'));
    assert(codes(report).includes('FirstPlaySourceOverPlan'));
    assert(codes(report).includes('ModuleOverPlan'));
    const data = catalog([entry({ loadTiming: 'first-play', plannedBytes: 200 })]);
    data.budgets.firstPlaySourcePlanningBytes = 500;
    data.budgets.moduleSourceReviewBytes.Station = 500;
    data.budgets.firstPlayDownloadTargetBytes = 1;
    assert(!codes(await buildCatalogReport(root, data)).includes('FirstPlaySourceOverPlan'));
});

test('retired content is excluded from active budgets, and an empty confirmed scope still warns', async t => {
    const root = await fixture(t);
    const data = catalog([entry({ status: 'retired', loadTiming: 'first-play' })]);
    data.firstPlayableScope.status = 'confirmed';
    const report = await buildCatalogReport(root, data);
    assert.equal(report.firstPlay.entries, 0);
    assert.equal(report.modules[0].projectedBytes, 0);
    assert(codes(report).includes('FirstPlayableScopeIncomplete'));
});

test('scan is read-only and report is deterministic', async t => {
    const root = await fixture(t);
    const before = await readFile(path.join(root, imagePath));
    const first = await buildCatalogReport(root, catalog());
    assert.deepEqual(await buildCatalogReport(root, catalog()), first);
    assert.deepEqual(await readFile(path.join(root, imagePath)), before);
    assert.deepEqual((await readdir(root)).sort(), ['unity']);
});
