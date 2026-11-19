import fs from 'node:fs';
import { CATALOG } from '../src/siege/catalog.js';
import { createSession, advanceSession, commitOrder } from '../src/siege/session.js';
import { chooseOrder } from '../tests/siege-sim.mjs';

const file = 'unity/HomelessPetHelper/Assets/Tests/Siege/Fixtures/SiegeParity.json';
const cases = [];
for (const seed of [1,19,20261119]) for (const profile of ['calm','alert','tired']) for (const policy of ['read','cycle-merge']) {
    let s = createSession({seed,profile,sessionId:'parity'});
    const steps = [];
    while (['decision','running'].includes(s.phase)) {
        let order = null;
        if (s.phase === 'decision') {
            const input = chooseOrder(s, policy);
            order = { turn: input.turn, deployId: input.deploy?.instanceId || 0, lane: input.deploy?.laneId || '',
                ids: input.merge?.materialIds.map(x => x === 'new' ? -1 : x) || [], anchor: input.merge?.anchorId === 'new' ? -1 : input.merge?.anchorId || 0,
                rerouteId:input.reroute?.unitId || 0, destination:input.reroute?.laneId || '' };
            const result = commitOrder(s,CATALOG,input);
            if (!result.ok) throw new Error(result.reason);
            s = result.state;
        }
        s = advanceSession(s,1000);
        steps.push({order, expected:{phase:s.phase,elapsed:s.elapsedMs,turn:s.turn,energy:s.energy,progress:s.progress,
            deployed:s.stats.deployed,merges:s.stats.merges,lost:s.stats.lost,delivered:s.stats.delivered,repairs:s.stats.repairs,
            hand:s.hand.map(x=>({id:x.instanceId,definition:x.definitionId})),
            units:s.units.map(x=>({id:x.id,definition:x.definitionId,lane:x.laneId,distance:x.distance,hp:x.hp,shield:x.shield,brace:x.brace,reroutes:x.reroutes,transit:x.transit?.remainingMs || 0})),
            towers:s.towers.map(x=>({id:x.id,status:x.status,hp:x.hp,ready:x.readyAtMs})),
            animal:{charge:s.animal.charge,actions:s.animal.actions,intent:s.animal.intent.type,lane:s.animal.intent.laneId,pad:s.animal.intent.padId || '',seen:s.animal.intent.seenTurn,
                guard:s.animal.guardLane || '',until:s.animal.guardUntilMs,recovery:s.animal.recoveryUntilMs,builds:s.animal.buildsLeft,repairs:s.animal.repairsLeft,
                repairId:s.animal.repair?.towerId || '',repairReady:s.animal.repair?.readyAtMs || 0,
                area:s.animal.intent.area || {x:0,y:0,radius:0}}}});
    }
    cases.push({seed,profile,policy,steps});
}
const content=JSON.stringify({cases})+'\n';
if (process.argv.includes('--check')) {
    if (fs.readFileSync(file,'utf8')!==content) throw new Error('Stale Unity parity fixture.');
} else {fs.mkdirSync(new URL('../unity/HomelessPetHelper/Assets/Tests/Siege/Fixtures/',import.meta.url),{recursive:true});fs.writeFileSync(file,content);}
console.log(`HTML parity fixtures: ${cases.length} battles / ${cases.reduce((n,c)=>n+c.steps.length,0)} checkpoints.`);
