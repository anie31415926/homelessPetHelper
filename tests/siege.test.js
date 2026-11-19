import test from 'node:test';
import assert from 'node:assert/strict';
import { CATALOG, TEXT, validateCatalog, unitDefinition } from '../src/siege/catalog.js';
import { createSession, advanceSession, setPaused, commitOrder, evaluateOrder } from '../src/siege/session.js';
import { spawnUnit, naturalMerges, hitUnit } from '../src/siege/entities.js';
import { routeLength, pointOnRoute, projectOntoRoute, sweepArea, unitInArea, separation, positionOf } from '../src/siege/geometry.js';
import { fillHand } from '../src/siege/supply.js';
import { animalPeriodMs, actAnimal, animalCanAct, pressureLanes } from '../src/siege/animal.js';
import { advanceCombat } from '../src/siege/combat.js';
import { simulate } from './siege-sim.mjs';

const order = (state, extra = {}) => ({ sessionId: state.sessionId, turn: state.turn, ...extra });
function run(state, ms, catalog = CATALOG) {
    for (let remaining = ms; remaining > 0 && state.phase === 'running'; remaining -= 50) state = advanceSession(state, Math.min(50, remaining), catalog);
    return state;
}
test('catalog rejects broken references, geometry, and prices', () => {
    const bad = structuredClone(CATALOG); bad.units[0].upgradeId = 'missing';
    assert.throws(() => validateCatalog(bad, TEXT), /upgrade/);
    const geometry = structuredClone(CATALOG); geometry.map.lanes[0].points[1] = [...geometry.map.lanes[0].points[0]];
    assert.throws(() => validateCatalog(geometry, TEXT), /zero segment/);
    const duplicate = structuredClone(CATALOG); duplicate.map.pads[1].id = duplicate.map.pads[0].id;
    assert.throws(() => validateCatalog(duplicate, TEXT), /duplicate/);
    const price = structuredClone(CATALOG); price.units[0].cost = Infinity;
    assert.throws(() => validateCatalog(price, TEXT), /cost/);
    const damage = structuredClone(CATALOG); damage.rules.slapDamage = -1;
    assert.throws(() => validateCatalog(damage, TEXT), /slapDamage/);
    const state = structuredClone(CATALOG); state.animalStates[1].stamina = 'missing';
    assert.throws(() => validateCatalog(state, TEXT), /relaxed state/);
    for(const [key,value] of [['sweepRadius',0],['slapPeriodMs',0],['repairAmount',-1],['repairThreshold',1],['animalRepairs',1.5]]) {
        const invalid=structuredClone(CATALOG); invalid.rules[key]=value;
        assert.throws(()=>validateCatalog(invalid,TEXT));
    }
    assert.ok(Object.isFrozen(CATALOG.units[0]));
});
test('seeded bag is reproducible, allows repetition and full hand consumes no randomness', () => {
    assert.deepEqual(createSession({seed:7}), createSession({seed:7}));
    const state = createSession({seed:7});
    const before = structuredClone(state); fillHand(state, CATALOG, 3);
    assert.deepEqual(state, before);
    const cards = state.hand.map(card => card.definitionId);
    for (let i = 0; i < 6; i++) { state.hand = []; fillHand(state, CATALOG, 1); cards.push(state.hand[0].definitionId); }
    assert.deepEqual(Object.fromEntries(['cart','ball','cover'].map(id => [id,cards.filter(item=>item===id).length])), {cart:3,ball:3,cover:3});
    assert.ok(Array.from({length:30},(_,seed)=>createSession({seed})).some(item=>new Set(item.hand.map(card=>card.definitionId)).size<3));
});
test('decision windows and manual pause freeze all battle state', () => {
    const state = createSession(); assert.equal(advanceSession(state,1000),state);
    const running = commitOrder(state,CATALOG,order(state)).state;
    const paused = setPaused(running,true);
    assert.equal(advanceSession(paused,1000),paused);
    assert.equal(evaluateOrder(paused,CATALOG,order(paused)).ok,false);
    assert.equal(run(setPaused(paused,false),4000).elapsedMs,4000);
});
test('turn supply occurs once, held cards persist, player has same-tick priority', () => {
    let state = createSession();
    const kept = state.hand.slice(1);
    state = commitOrder(state,CATALOG,order(state,{deploy:{instanceId:state.hand[0].instanceId,laneId:'mid'}})).state;
    state.animal.charge = 1 - CATALOG.rules.playerPeriodMs / 6000;
    state = run(state,4000);
    assert.equal(state.phase,'decision'); assert.equal(state.turn,2);
    assert.equal(state.energy,4); assert.equal(state.hand.length,3);
    assert.deepEqual(state.hand.slice(0,2),kept);
    assert.equal(state.animal.actions,0);
    assert.equal(advanceSession(state,1000),state);
});
test('rejected and previewed orders cannot mutate state or consume RNG', () => {
    const state = createSession(); const before = structuredClone(state);
    const good = order(state,{deploy:{instanceId:state.hand[0].instanceId,laneId:'mid'}});
    assert.equal(evaluateOrder(state,CATALOG,good).ok,true); assert.deepEqual(state,before);
    assert.equal(commitOrder(state,CATALOG,{...good,turn:0}).ok,false);
    assert.equal(commitOrder(state,CATALOG,{...good,sessionId:'other'}).ok,false);
    assert.equal(commitOrder(state,CATALOG,{...good,deploy:{...good.deploy,laneId:'missing'}}).ok,false);
    assert.equal(commitOrder(state,CATALOG,{...good,merge:{anchorId:99,materialIds:[99,100,101]}}).state,state);
    assert.deepEqual(state,before);
    const committed = commitOrder(state,CATALOG,good).state;
    assert.equal(commitOrder(committed,CATALOG,good).ok,false);
});
test('deploy plus one paid merge is atomic, anchors location and preserves injury ratio', () => {
    const state = createSession(); state.energy = 3;
    state.hand[0].definitionId = 'cart';
    const a = spawnUnit(state,CATALOG,'cart','mid'), b = spawnUnit(state,CATALOG,'cart','top');
    a.distance=150; b.distance=130; a.hp=3.5; b.hp=3.5;
    const input = order(state,{deploy:{instanceId:state.hand[0].instanceId,laneId:'bottom'},merge:{anchorId:a.id,materialIds:[a.id,b.id,'new']}});
    const result = commitOrder(state,CATALOG,input);
    assert.equal(result.ok,true); assert.equal(result.state.energy,0); assert.equal(result.state.units.length,1);
    const merged = result.state.units[0]; assert.equal(merged.definitionId,'ram'); assert.equal(merged.distance,150); assert.equal(merged.laneId,'mid');
    assert.ok(Math.abs(merged.hp-17*2/3)<0.0001);
    const poor = structuredClone(state); poor.energy=2;
    assert.equal(commitOrder(poor,CATALOG,input).state,poor);
});
test('natural merge requires all three physically nearby beyond spawn area', () => {
    const state = createSession();
    for (let i=0;i<3;i++) spawnUnit(state,CATALOG,'cart','mid');
    assert.equal(naturalMerges(state,CATALOG),false);
    state.units.forEach((unit,i)=>unit.distance=100+i*10);
    assert.equal(naturalMerges(state,CATALOG),true);
    assert.equal(state.units.length,1); assert.equal(state.energy,4);
    assert.equal(state.units[0].definitionId,'ram');
});
test('reroute uses a charge, traverses space, not an instant proportional teleport', () => {
    const state = createSession();
    const unit = spawnUnit(state,CATALOG,'runner','mid'); unit.distance=140;
    const point = pointOnRoute(CATALOG.map.lanes[1],140);
    const result=commitOrder(state,CATALOG,order(state,{reroute:{unitId:unit.id,laneId:'top'}}));
    assert.equal(result.ok,true); assert.equal(result.state.units[0].reroutes,0);
    assert.deepEqual(result.state.units[0].transit.from,point);
    const moved=run(result.state,700);
    assert.equal(moved.units[0].transit,null);
});
test('important animal attacks wait for an actual intervening player window', () => {
    let state=createSession({profile:'alert'});
    state=commitOrder(state,CATALOG,order(state)).state;
    state=run(state,4000);
    assert.equal(state.animal.actions,1); assert.equal(state.animal.intent.type,'sweep');
    assert.equal(state.animal.intent.seenTurn,2);
    state=commitOrder(state,CATALOG,order(state)).state;
    state=run(state,2000);
    assert.equal(state.animal.actions,2);
    assert.ok(state.log.some(event=>event.key==='event.sweep'));
});
test('animal state changes preserve action charge and affect only subsequent speed', () => {
    let state=createSession({profile:'alert'}); state.progress=CATALOG.rules.target/2;
    state=commitOrder(state,CATALOG,order(state)).state; state.animal.charge=.5;
    state=advanceSession(state,50);
    assert.equal(state.animal.vigilance,'watchful'); assert.equal(animalPeriodMs(state,CATALOG),6000);
    assert.ok(state.animal.charge>.5 && state.animal.charge<.53);
});
test('delivery scores once then removes unit, success and timeout are terminal', () => {
    let state=createSession(); state.progress=65;
    const unit=spawnUnit(state,CATALOG,'cart','bottom'); unit.distance=routeLength(CATALOG.map.lanes[2])-.1;
    state=commitOrder(state,CATALOG,order(state)).state; state=advanceSession(state,50);
    assert.equal(state.phase,'won'); assert.equal(state.stats.delivered,1); assert.equal(state.units.length,0);
    assert.equal(advanceSession(state,1000),state);
    let timeout=createSession(); timeout.elapsedMs=CATALOG.rules.durationMs-50;
    timeout=commitOrder(timeout,CATALOG,order(timeout)).state; timeout=advanceSession(timeout,50);
    assert.equal(timeout.phase,'timeout'); assert.equal(advanceSession(timeout,1000),timeout);
});
test('tower destruction is real and limited construction cannot resurrect destroyed pads', () => {
    let state=createSession();
    const tower=state.towers.find(item=>item.laneId==='mid'); tower.hp=1;
    const unit=spawnUnit(state,CATALOG,'ram','mid'); unit.distance=tower.distance-10;
    state=commitOrder(state,CATALOG,order(state)).state; state=advanceSession(state,50);
    assert.equal(state.towers.find(item=>item.id===tower.id).status,'destroyed'); assert.equal(state.stats.towers,1);
    for(let i=0;i<15;i++) {
        if(state.phase==='decision') state=commitOrder(state,CATALOG,order(state)).state;
        state=run(state,4000);
    }
    assert.equal(state.animal.buildsLeft,0); assert.equal(state.towers.length,3);
    assert.equal(state.towers.find(item=>item.id===tower.id).status,'destroyed');
});
test('fixed steps are chunk independent and geometry follows actual route length', () => {
    const start=createSession(); const running=commitOrder(start,CATALOG,order(start)).state;
    const a=advanceSession(running,1000);
    const b=Array.from({length:10}).reduce(value=>advanceSession(value,100),running);
    assert.deepEqual(a,b);
    const lane=CATALOG.map.lanes[1]; const point=pointOnRoute(lane,150);
    assert.ok(Math.abs(projectOntoRoute(lane,point)-150)<.0001);
    assert.throws(()=>advanceSession(running,1001),RangeError);
});
test('escort shields absorb a hit, brace blocks one push but not damage', () => {
    let state=createSession();
    state.towers=[];
    const escort=spawnUnit(state,CATALOG,'cover','bottom'), ally=spawnUnit(state,CATALOG,'cart','bottom');
    escort.distance=80; ally.distance=85;
    state=commitOrder(state,CATALOG,order(state)).state;
    state=advanceSession(state,50);
    const protectedAlly=state.units.find(unit=>unit.id===ally.id);
    assert.equal(protectedAlly.shield,1);
    hitUnit(state,CATALOG,protectedAlly,2,10);
    assert.equal(protectedAlly.hp,7); assert.equal(protectedAlly.shield,0);
    const ram=spawnUnit(state,CATALOG,'ram','bottom'); ram.distance=100;
    hitUnit(state,CATALOG,ram,2,20);
    assert.equal(ram.hp,15); assert.equal(ram.distance,100); assert.equal(ram.brace,0);
    hitUnit(state,CATALOG,ram,2,20);
    assert.equal(ram.hp,13); assert.equal(ram.distance,80);
});
test('guard damages later arrivals repeatedly, holds one lane, and expires', () => {
    const state=createSession(); state.towers=[];
    actAnimal(state,CATALOG);
    assert.equal(state.animal.intent.type,'sweep');
    assert.equal(state.units.length,0);
    const unit=spawnUnit(state,CATALOG,'ram','mid');
    unit.distance=routeLength(CATALOG.map.lanes[1])*CATALOG.rules.guardStart;
    const other=spawnUnit(state,CATALOG,'ram','bottom'); other.distance=200;
    state.elapsedMs=50; advanceCombat(state,CATALOG,50);
    assert.equal(unit.hp,17-CATALOG.rules.slapDamage);
    assert.equal(other.hp,17); assert.ok(other.distance>200);
    const first=unit.hp;
    state.elapsedMs=100; advanceCombat(state,CATALOG,50); assert.equal(unit.hp,first);
    state.elapsedMs=50+CATALOG.rules.slapPeriodMs; advanceCombat(state,CATALOG,50);
    assert.equal(unit.hp,first-CATALOG.rules.slapDamage);
    const last=unit.hp;
    state.elapsedMs=state.animal.guardUntilMs; advanceCombat(state,CATALOG,50);
    assert.equal(unit.hp,last);
});
test('sweep hits every unit in the locked space across routes and during reroute, never outside', () => {
    const state=createSession(); state.towers=[];
    const area=sweepArea(CATALOG,'mid');
    const units=['top','mid','bottom'].map(laneId=>{
        const unit=spawnUnit(state,CATALOG,'cart',laneId);
        unit.distance=routeLength(CATALOG.map.lanes.find(lane=>lane.id===laneId))*(laneId==='mid'?.84:.96);
        assert.equal(unitInArea(CATALOG,unit,area),true);
        return unit;
    });
    const transit=spawnUnit(state,CATALOG,'cart','top');
    transit.transit={from:{x:area.x,y:area.y},to:{x:area.x+10,y:area.y},remainingMs:CATALOG.rules.rerouteDurationMs};
    const safe=spawnUnit(state,CATALOG,'cart','mid'); safe.distance=0;
    state.animal.intent={type:'sweep',laneId:'mid',padId:null,seenTurn:2,area};
    const locked=structuredClone(area);
    state.resolvedTurn=1; state.animal.charge=1; assert.equal(animalCanAct(state),false);
    state.resolvedTurn=2; assert.equal(animalCanAct(state),true);
    actAnimal(state,CATALOG);
    assert.ok([...units,transit].every(unit=>unit.hp===7-CATALOG.rules.sweepDamage));
    assert.equal(safe.hp,7);
    assert.deepEqual(state.effects.find(effect=>effect.kind==='sweep').area,locked);
    assert.equal(state.log.find(event=>event.key==='event.sweep').hitIds.length,4);
    assert.equal(state.animal.guardUntilMs,state.elapsedMs);
    assert.equal(animalCanAct(state),false,'the recovery window prevents another immediate action');
    state.elapsedMs=state.animal.recoveryUntilMs; assert.equal(animalCanAct(state),true);
    const boundary={...safe,transit:{from:{x:area.x+area.radius,y:area.y},to:{x:area.x+area.radius,y:area.y},remainingMs:700}};
    assert.equal(unitInArea(CATALOG,boundary,area),true);
    boundary.transit.from.x+=.01; boundary.transit.to.x+=.01;
    assert.equal(unitInArea(CATALOG,boundary,area),false);
});
test('tower starts shooting well before the unit can attack it, outside range is safe', () => {
    const state=createSession(); state.towers=state.towers.filter(tower=>tower.laneId==='mid');
    const tower=state.towers[0], unit=spawnUnit(state,CATALOG,'cart','mid');
    unit.distance=tower.distance-75;
    assert.ok(separation(positionOf(CATALOG,unit),positionOf(CATALOG,tower))>unitDefinition(CATALOG,'cart').reach);
    state.elapsedMs=50; advanceCombat(state,CATALOG,50);
    assert.equal(unit.hp,6); assert.equal(tower.hp,CATALOG.tower.hp);
    assert.ok(state.effects.some(effect=>effect.kind==='towerShot'));
    const outside=createSession(); outside.towers=outside.towers.filter(tower=>tower.laneId==='mid');
    const far=spawnUnit(outside,CATALOG,'cart','mid'); far.distance=outside.towers[0].distance-CATALOG.tower.range-5;
    outside.elapsedMs=50; advanceCombat(outside,CATALOG,50); assert.equal(far.hp,7);
});
test('repair has a response window, spends a finite charge, heals only after completion and caps HP', () => {
    let state=createSession(); const tower=state.towers[0]; tower.hp=CATALOG.tower.hp-2;
    state.animal.intent={type:'repair',laneId:tower.laneId,padId:tower.id,seenTurn:2};
    state.resolvedTurn=1; assert.equal(animalCanAct(state),false);
    state.resolvedTurn=2; assert.equal(animalCanAct(state),true);
    actAnimal(state,CATALOG);
    assert.equal(state.animal.repairsLeft,CATALOG.rules.animalRepairs-1);
    const repairAt=state.animal.repair.readyAtMs;
    state=commitOrder(state,CATALOG,order(state)).state;
    const paused=setPaused(state,true); assert.equal(advanceSession(paused,1000),paused);
    state=run(state,repairAt-50); assert.equal(state.towers[0].hp,CATALOG.tower.hp-2);
    state=run(state,50); assert.equal(state.towers[0].hp,CATALOG.tower.hp); assert.equal(state.stats.repairs,1);
    state.animal.repairsLeft=0; state.animal.intent={type:'repair',padId:tower.id,laneId:tower.laneId};
    state.towers[0].hp=2; state.elapsedMs+=CATALOG.rules.repairCooldownMs;
    actAnimal(state,CATALOG); assert.equal(state.animal.repair,null); assert.equal(state.towers[0].hp,2);
});
test('destroying a repair target interrupts work and never resurrects a demolished tower', () => {
    const state=createSession(); state.towers=state.towers.filter(tower=>tower.laneId==='mid');
    const tower=state.towers[0]; tower.hp=1;
    state.animal.intent={type:'repair',padId:tower.id,laneId:tower.laneId}; actAnimal(state,CATALOG);
    const unit=spawnUnit(state,CATALOG,'ram','mid'); unit.distance=tower.distance-10;
    state.elapsedMs=50; advanceCombat(state,CATALOG,50);
    assert.equal(tower.status,'destroyed'); assert.equal(state.animal.repair,null);
    state.elapsedMs=10000; advanceCombat(state,CATALOG,50);
    assert.equal(tower.status,'destroyed'); assert.equal(state.stats.repairs,0);
    assert.ok(state.log.some(event=>event.key==='event.repairStopped'));
});
test('animal prioritizes a near-terminal threat over a distant crowd without changing a locked sweep', () => {
    const state=createSession();
    for(let i=0;i<8;i++)spawnUnit(state,CATALOG,'cart','top');
    const threat=spawnUnit(state,CATALOG,'cart','bottom'); threat.distance=routeLength(CATALOG.map.lanes[2])*.9;
    assert.equal(pressureLanes(state,CATALOG)[0].lane.id,'bottom');
    actAnimal(state,CATALOG); const area=structuredClone(state.animal.intent.area);
    for(let i=0;i<10;i++)spawnUnit(state,CATALOG,'cart','bottom').distance=400;
    assert.deepEqual(state.animal.intent.area,area); assert.equal(state.animal.intent.laneId,'mid');
});
test('shields and upgraded brace still work against the large sweep without giving invulnerability', () => {
    const state=createSession(), area=sweepArea(CATALOG,'mid');
    const ram=spawnUnit(state,CATALOG,'ram','mid'), protectedUnit=spawnUnit(state,CATALOG,'cart','mid');
    ram.distance=routeLength(CATALOG.map.lanes[1])*.86; protectedUnit.distance=ram.distance;
    protectedUnit.shield=1; const before=ram.distance;
    state.animal.intent={type:'sweep',laneId:'mid',area}; actAnimal(state,CATALOG);
    assert.equal(ram.hp,17-CATALOG.rules.sweepDamage); assert.equal(ram.distance,before); assert.equal(ram.brace,0);
    assert.equal(protectedUnit.hp,7); assert.equal(protectedUnit.shield,0); assert.ok(protectedUnit.distance<before);
});
test('repair restores only its specified tower by the configured amount, cooldown is not a second charge', () => {
    const state=createSession(), tower=state.towers[0], other=state.towers[1]; tower.hp=2; other.hp=3;
    state.animal.intent={type:'repair',padId:tower.id,laneId:tower.laneId}; actAnimal(state,CATALOG);
    const spent=state.animal.repairsLeft;
    state.elapsedMs=CATALOG.rules.repairDurationMs; advanceCombat(state,CATALOG,50);
    assert.equal(tower.hp,2+CATALOG.rules.repairAmount); assert.equal(other.hp,3);
    state.animal.intent={type:'repair',padId:other.id,laneId:other.laneId}; actAnimal(state,CATALOG);
    assert.equal(state.animal.repair,null); assert.equal(state.animal.repairsLeft,spent);
    state.elapsedMs=CATALOG.rules.repairCooldownMs;
    state.animal.intent={type:'repair',padId:other.id,laneId:other.laneId}; actAnimal(state,CATALOG);
    assert.equal(state.animal.repairsLeft,spent-1);
    other.status='destroyed'; other.hp=0; advanceCombat(state,CATALOG,50);
    state.animal.intent={type:'repair',padId:other.id,laneId:other.laneId}; actAnimal(state,CATALOG);
    assert.equal(state.animal.repair,null); assert.equal(other.hp,0);
});
test('tower range is spatial and can cover a nearby different route', () => {
    const state=createSession(); state.towers=state.towers.filter(tower=>tower.laneId==='mid');
    const tower=state.towers[0]; tower.distance=routeLength(CATALOG.map.lanes[1])*.87;
    const unit=spawnUnit(state,CATALOG,'cart','bottom'); unit.distance=routeLength(CATALOG.map.lanes[2])*.96;
    assert.ok(separation(positionOf(CATALOG,tower),positionOf(CATALOG,unit))<CATALOG.tower.range);
    state.elapsedMs=50; advanceCombat(state,CATALOG,50);
    assert.equal(unit.hp,6); assert.equal(tower.hp,CATALOG.tower.hp);
});
test('current trial balance has blind single-lane failures and retains both split and deliberate single-lane solutions', () => {
    const seeds=[1,7,19,41,77,119,314,20261119];
    const blind=['top','mid','bottom'].flatMap(strategy=>seeds.map(seed=>simulate(seed,'calm',strategy)));
    const split=seeds.map(seed=>simulate(seed,'calm','read'));
    const concentrated=seeds.map(seed=>simulate(seed,'calm','single-merge'));
    assert.ok(blind.some(result=>result.phase==='timeout'));
    assert.ok(split.some(result=>result.phase==='won'));
    assert.ok(concentrated.some(result=>result.phase==='won'),'do not mandate splitting lanes');
    assert.ok(split.filter(result=>result.phase==='won').length/split.length>blind.filter(result=>result.phase==='won').length/blind.length);
});
test('discard consumes the window without an immediate draw or deployment', () => {
    const state=createSession(), kept=state.hand.slice(1), id=state.hand[0].instanceId;
    const input=order(state,{discardId:id});
    assert.equal(commitOrder(state,CATALOG,{...input,deploy:{instanceId:id,laneId:'mid'}}).ok,false);
    const result=commitOrder(state,CATALOG,input).state;
    assert.equal(result.phase,'running'); assert.deepEqual(result.hand,kept);
    assert.equal(result.energy,state.energy); assert.equal(result.units.length,0);
    const next=run(result,4000); assert.equal(next.hand.length,3); assert.deepEqual(next.hand.slice(0,2),kept);
});
