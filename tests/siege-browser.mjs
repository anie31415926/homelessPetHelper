import assert from 'node:assert/strict';
import fs from 'node:fs/promises';
import { chromium } from '@playwright/test';
import { CATALOG } from '../src/siege/catalog.js';
import { createSession, advanceSession, commitOrder } from '../src/siege/session.js';
import { chooseOrder } from './siege-sim.mjs';

const base = process.env.SIEGE_BASE_URL || 'http://localhost:5198';
const output = 'test-results/siege';
await fs.mkdir(output, { recursive: true });
let browser;
try { browser = await chromium.launch({ headless: true }); }
catch (error) {
    if (!error.message.includes("Executable doesn't exist")) throw error;
    browser = await chromium.launch({ channel: 'msedge', headless: true });
}
const context = await browser.newContext({ hasTouch: true, isMobile: true, reducedMotion: 'reduce' });
const page = await context.newPage();
const errors = [], failures = [], measurements = [], checks = [];
page.on('pageerror', error => errors.push(error.message));
page.on('response', response => { if (response.status() >= 400) failures.push(response.url()); });
const main = page.locator('main');
const attr = async name => Number(await main.getAttribute(`data-${name}`));
const snapshot = () => page.evaluate(() => ({
    time: document.querySelector('main').dataset.time,
    energy: document.querySelector('[data-energy]').textContent,
    units: [...document.querySelectorAll('[data-unit]')].map(el => [el.dataset.unit, el.dataset.distance, el.dataset.definition]),
    towers: [...document.querySelectorAll('[data-tower]')].map(el => [el.dataset.tower, el.dataset.hp, el.dataset.repair]),
    meters: [...document.querySelectorAll('[role=progressbar]')].map(el => el.getAttribute('aria-valuenow'))
}));
async function layout(width, height, suffix = '') {
    await page.setViewportSize({ width, height });
    await page.goto(`${base}/siege.html?seed=20261119`, { waitUntil: 'networkidle' });
    const measurement = await page.evaluate(async () => {
        for (const image of document.images) await image.decode();
        const rect = el => { const r = el.getBoundingClientRect(); return { x:r.x, y:r.y, right:r.right, bottom:r.bottom, width:r.width, height:r.height }; };
        const shell = document.querySelector('main');
        return {
            overflow: document.documentElement.scrollWidth > innerWidth,
            scroll: shell.scrollHeight > shell.clientHeight + 1,
            clipped: [...document.querySelectorAll('button,h1')].filter(el => el.clientWidth && (el.scrollWidth > el.clientWidth + 2 || el.scrollHeight > el.clientHeight + 2)).map(el => el.textContent),
            shell: rect(shell), stage: rect(document.querySelector('.siege-stage')), controls: rect(document.querySelector('.siege-controls')),
            images: [...document.images].map(el => el.naturalWidth)
        };
    });
    assert.equal(measurement.overflow, false, `${width} horizontal overflow`);
    assert.equal(measurement.scroll, false, `${width} shell overflow`);
    assert.deepEqual(measurement.clipped, [], `${width} clipped text`);
    assert.ok(measurement.stage.height >= 160);
    assert.ok(measurement.controls.bottom <= measurement.shell.bottom);
    assert.ok(measurement.images.every(size => size > 0));
    const landscape = height <= 470 && width >= 550;
    assert.equal(measurement.shell.width, Math.min(width, landscape ? 667 : 375));
    assert.equal(measurement.shell.height, Math.min(height, landscape ? 375 : 667));
    measurements.push({ width, height, ...measurement });
    await page.screenshot({ path: `${output}/layout-${width}x${height}${suffix}.png`, fullPage: true });
}
async function advanceToDecision() {
    for (let i=0; i<100 && await main.getAttribute('data-phase') === 'running'; i++) await page.clock.runFor(100);
    assert.notEqual(await main.getAttribute('data-phase'), 'running', 'simulation reaches a decision or result');
}
async function enact(order) {
    if (order.deploy) {
        await page.locator(`[data-card="${order.deploy.instanceId}"]`).click();
        await page.locator(`[data-lane-select="${order.deploy.laneId}"]`).click();
    }
    if (order.merge) {
        await page.getByRole('button', { name:'合体', exact:true }).click();
        for (const id of order.merge.materialIds) {
            if (id === 'new') await page.locator('.merge-selection').getByRole('button', { name:'出兵', exact:true }).last().click();
            else await page.getByLabel('选择材料',{exact:true}).selectOption(String(id));
        }
        await page.locator('.merge-selection').getByRole('button', { name:order.merge.anchorId === 'new' ? '出兵' : `#${order.merge.anchorId}`, exact:true }).first().click();
        for (const [width,height] of [[320,568],[360,500],[667,375]]) {
            await page.setViewportSize({width,height});
            const fits=await page.evaluate(()=>document.querySelector('main').scrollHeight<=document.querySelector('main').clientHeight+1);
            assert.equal(fits,true,'three-material plan still fits the phone');
            await page.screenshot({path:`${output}/plan-${width}x${height}.png`});
        }
        await page.setViewportSize({width:375,height:667});
    }
    assert.equal(await page.locator('[data-execute]').isEnabled(),true,`valid plan enables execution: ${JSON.stringify(order)}`);
    await page.locator('[data-execute]').click();
}
try {
    for (const [w,h] of [[320,568],[360,500],[360,640],[375,667],[390,844],[768,1024],[1440,900],[667,375]]) await layout(w,h);
    await layout(320,568);
    await page.getByRole('button', { name:'合体', exact:true }).click();
    assert.equal(await page.locator('[data-execute]').isDisabled(), true);
    const mergeLayout = await page.evaluate(() => ({ scroll: document.querySelector('main').scrollHeight > document.querySelector('main').clientHeight+1, height:document.querySelector('.siege-stage').clientHeight }));
    assert.equal(mergeLayout.scroll,false); assert.ok(mergeLayout.height >=160);
    await page.screenshot({ path:`${output}/merge-320x568.png` });
    checks.push('eight responsive viewports, images, merge controls');

    await page.setViewportSize({width:375,height:667});
    await page.clock.install();
    await page.goto(`${base}/siege.html?seed=20261119`, {waitUntil:'networkidle'});
    const openingCards = await page.locator('[data-card]').evaluateAll(els=>els.map(el=>el.dataset.definition));
    await page.locator('[data-tower="mid.outer"]').click();
    assert.equal(Number(await page.locator('[data-tower-range="mid.outer"]').getAttribute('r')),CATALOG.tower.range);
    await page.screenshot({path:`${output}/tower-range.png`});
    await page.locator('[data-tower="mid.outer"]').click();
    const frozen = await snapshot(); await page.clock.runFor(20000); assert.deepEqual(await snapshot(),frozen);
    await page.locator('[data-card]').first().click();
    await page.locator('[data-lane-select=bottom]').click();
    await page.locator('[data-execute]').click();
    await page.clock.runFor(1000);
    assert.ok(await attr('time') >= 950); assert.equal(await attr('turn'),1);
    await page.getByRole('button',{name:'暂停与设置',exact:true}).click();
    const manual = await snapshot(); await page.clock.runFor(20000); assert.deepEqual(await snapshot(),manual);
    await page.getByRole('button',{name:'继续',exact:true}).click();
    await advanceToDecision();
    assert.equal(await attr('turn'),2); assert.equal(await attr('time'),4000);
    const turnFrozen = await snapshot(); await page.clock.runFor(20000); assert.deepEqual(await snapshot(),turnFrozen);
    checks.push('decision and manual pause freeze all clocks, units and supply');

    await page.getByRole('button',{name:'暂停与设置',exact:true}).click();
    await page.getByLabel('相遇编号',{exact:true}).fill('-1');
    await page.getByRole('button',{name:'重新开始',exact:true}).click();
    assert.equal(await page.getByRole('alert').count(),1); assert.equal(await attr('time'),4000);
    await page.getByRole('button',{name:'重试本局',exact:true}).click();
    assert.equal(await attr('time'),0); assert.equal(await attr('turn'),1);
    assert.deepEqual(await page.locator('[data-card]').evaluateAll(els=>els.map(el=>el.dataset.definition)),openingCards);
    await page.locator('[data-execute]').click(); await page.clock.runFor(500);
    await page.evaluate(()=>window.dispatchEvent(new Event('blur')));
    await page.getByRole('dialog').waitFor();
    const blur = await snapshot(); await page.clock.runFor(10000); assert.deepEqual(await snapshot(),blur);
    await page.getByRole('button',{name:'继续',exact:true}).click();
    await advanceToDecision();
    checks.push('invalid seed preserves encounter, seeded retry, blur-safe resume');

    // Drive the real controls from the same deterministic command contract, never a debug state hook.
    const seed=4;
    await page.goto(`${base}/siege.html?seed=${seed}&profile=calm`,{waitUntil:'networkidle'});
    let expected=createSession({seed,profile:'calm',sessionId:`${seed}-1`});
    let uiMerges=0, uiReroutes=0, uiSweeps=0, uiRepairs=0, uiShots=0;
    for(let i=0;i<40&&expected.phase==='decision';i++) {
        const order=chooseOrder(expected,'read');
        const runner=expected.units.find(unit=>unit.reroutes>0&&!unit.transit);
        if(runner && !uiReroutes) {
            delete order.merge;
            order.reroute={unitId:runner.id,laneId:runner.laneId==='mid'?'bottom':'mid'};
        }
        assert.equal(await attr('turn'),expected.turn);
        if(order.reroute) {
            await page.locator(`[data-unit="${order.reroute.unitId}"]`).click();
            await page.getByRole('button',{name:'改道',exact:true}).click();
            await page.getByLabel('改道目的地',{exact:true}).selectOption(order.reroute.laneId);
            uiReroutes++;
        }
        if(order.merge)uiMerges++;
        await enact(order);
        if(order.merge)await page.screenshot({path:`${output}/paid-merge.png`});
        expected=commitOrder(expected,CATALOG,order).state;
        while(expected.phase==='running')expected=advanceSession(expected,1000);
        await advanceToDecision();
        if(await page.locator('[data-sweep-area]').count()) {
            const area=expected.animal.intent.area;
            const shown=await page.locator('[data-sweep-area]').evaluate(el=>({x:Number(el.getAttribute('cx')),y:Number(el.getAttribute('cy')),radius:Number(el.getAttribute('r'))}));
            assert.deepEqual(shown,area,'warning uses the exact locked damage footprint');
            uiSweeps++;
            if(uiSweeps===2)await page.screenshot({path:`${output}/sweep-warning.png`});
        }
        if(await page.locator('[data-repair=planned],[data-repair=active]').count()) {
            uiRepairs++;
            if(uiRepairs===1)await page.screenshot({path:`${output}/repair-warning.png`});
        }
        if(await page.locator('.tower-shot').count())uiShots++;
        assert.equal(await main.getAttribute('data-phase'),expected.phase);
        assert.equal(await attr('time'),expected.elapsedMs);
        const actualUnits=await page.locator('[data-unit]').evaluateAll(els=>els.map(el=>({id:Number(el.dataset.unit),definition:el.dataset.definition,lane:el.dataset.lane,distance:Number(el.dataset.distance),hp:Number(el.dataset.hp)})));
        assert.deepEqual(actualUnits,expected.units.map(unit=>({id:unit.id,definition:unit.definitionId,lane:unit.laneId,distance:Number(unit.distance.toFixed(1)),hp:unit.hp})));
        const actualTowers=await page.locator('[data-tower]').evaluateAll(els=>els.map(el=>({id:el.dataset.tower,hp:Number(el.dataset.hp),status:el.dataset.status,repair:el.dataset.repair})));
        assert.deepEqual(actualTowers,CATALOG.map.pads.map(pad=>{
            const tower=expected.towers.find(item=>item.id===pad.id);
            return {id:pad.id,hp:tower?.hp??0,status:tower?.status??'empty',repair:expected.animal.repair?.towerId===pad.id?'active':expected.animal.intent.type==='repair'&&expected.animal.intent.padId===pad.id?'planned':'none'};
        }));
    }
    assert.equal(expected.phase,'won');
    assert.ok(uiMerges>0,'complete a real paid merge');
    assert.ok(uiReroutes>0,'use the upgraded unit reroute');
    assert.ok(uiSweeps>0,'see a real locked area warning');
    assert.ok(uiRepairs>0,'see a real repair target');
    assert.ok(uiShots>0,'tower fires in the real browser battle');
    await page.getByRole('dialog',{name:'防线已化解',exact:true}).waitFor();
    await page.screenshot({path:`${output}/victory.png`});
    checks.push(`real UI victory, ${uiMerges} paid merges, ${uiReroutes} reroute, ${uiSweeps} exact area warnings, ${uiRepairs} repair warnings, ${uiShots} firing snapshots; state agrees with engine`);
    assert.deepEqual(errors,[]); assert.deepEqual(failures,[]);
    await fs.writeFile(`${output}/report.json`,JSON.stringify({checks,measurements,errors,failures},null,2));
    console.log(JSON.stringify({checks,errors,failures},null,2));
} catch(error) {
    await page.screenshot({path:`${output}/failure.png`});
    throw error;
} finally { await browser.close(); }
