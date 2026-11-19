import assert from 'node:assert/strict';
import fs from 'node:fs/promises';
import { CATALOG, unitDefinition } from '../src/siege/catalog.js';
import { createSession, advanceSession, commitOrder, evaluateOrder } from '../src/siege/session.js';
import { routeLength, projectOntoRoute, positionOf, unitInArea } from '../src/siege/geometry.js';

export function chooseOrder(state, strategy = 'adaptive', catalog = CATALOG) {
    const input={sessionId:state.sessionId,turn:state.turn};
    const merging=['adaptive','read','single-merge','cycle-merge'].includes(strategy);
    const counts=Object.fromEntries(catalog.units.filter(unit=>unit.bag).map(unit=>[unit.id,state.units.filter(item=>item.definitionId===unit.id&&!item.transit)]));
    const card=[...state.hand].sort((a,b)=>merging?counts[b.definitionId].length-counts[a.definitionId].length:0)
        .find(item=>unitDefinition(catalog,item.definitionId).cost<=state.energy);
    const lanes=catalog.map.lanes.filter(item=>item.open);
    let lane=['top','mid','bottom'].includes(strategy)?strategy:strategy==='single-merge'?'mid':strategy.startsWith('cycle')?lanes[(state.turn-1)%lanes.length].id:lanes.map(item=>({id:item.id,cost:state.towers.filter(tower=>tower.laneId===item.id&&tower.status!=='destroyed').reduce((sum,tower)=>sum+tower.hp,0)+(item.id==='mid'?0:3)+state.units.filter(unit=>unit.laneId===item.id).length*1.5}))
        .sort((a,b)=>a.cost-b.cost)[0].id;
    if(strategy.startsWith('read')) {
        lane=lanes.map(item=>({id:item.id,cost:state.towers.filter(tower=>tower.laneId===item.id&&tower.status!=='destroyed').reduce((sum,tower)=>sum+tower.hp*.4,0)
            +state.units.filter(unit=>unit.laneId===item.id).length*4
            +(state.animal.guardLane===item.id&&state.animal.guardUntilMs>state.elapsedMs?8:0)
            +(state.animal.intent.type==='guard'&&state.animal.intent.laneId===item.id?5:0)
            +routeLength(item)*.006})).sort((a,b)=>a.cost-b.cost)[0].id;
    }
    if(card) input.deploy={instanceId:card.instanceId,laneId:lane};
    const available=Object.values(counts).find(items=>items.length>=3 || card&&items[0]?.definitionId===card.definitionId&&items.length>=2);
    if(merging&&available&&state.energy>=(card?unitDefinition(catalog,card.definitionId).cost:0)+catalog.rules.mergeCost) {
        const sorted=[...available].sort((a,b)=>b.distance-a.distance);
        input.merge={anchorId:sorted[0].id,materialIds:sorted.slice(0,3).map(unit=>unit.id)};
        if(input.merge.materialIds.length===2) input.merge.materialIds.push('new');
    }
    if(strategy==='read'&&!input.merge) {
        const runner=state.units.find(unit=>unit.reroutes>0&&!unit.transit&&(state.animal.guardLane===unit.laneId&&state.animal.guardUntilMs>state.elapsedMs||state.animal.intent.area&&unitInArea(catalog,unit,state.animal.intent.area)));
        if(runner) {
            const target=lanes.filter(item=>item.id!==runner.laneId).map(item=>{
                const distance=Math.min(routeLength(item)*.97,projectOntoRoute(item,positionOf(catalog,runner)));
                return {id:item.id,risk:(state.animal.guardLane===item.id?10:0)+(state.animal.intent.area&&unitInArea(catalog,{laneId:item.id,distance},state.animal.intent.area)?20:0)};
            }).sort((a,b)=>a.risk-b.risk)[0];
            if(target)input.reroute={unitId:runner.id,laneId:target.id};
        }
    }
    const evaluated=evaluateOrder(state,catalog,input);
    return evaluated.ok?input:{sessionId:state.sessionId,turn:state.turn};
}
export function simulate(seed,profile,strategy='adaptive',catalog=CATALOG) {
    let state=createSession({seed,profile},catalog);
    const events=[];
    while(['decision','running'].includes(state.phase)) {
        if(state.phase==='decision') {
            const input=chooseOrder(state,strategy,catalog),result=commitOrder(state,catalog,input);
            assert.equal(result.ok,true);
            if(events.length<5)events.push(input);
            state=result.state;
        }
        state=advanceSession(state,1000,catalog);
        assert.ok(Number.isFinite(state.energy)&&state.energy>=0&&state.energy<=catalog.rules.energyCap);
        assert.ok(state.units.every(unit=>Number.isFinite(unit.hp)&&Number.isFinite(unit.distance)));
        assert.ok(state.hand.length<=catalog.rules.handSize);
    }
    return {seed,profile,strategy,phase:state.phase,time:state.elapsedMs/1000,progress:state.progress,...state.stats};
}
if(process.argv[1]?.endsWith('siege-sim.mjs')) {
    const results=[];
    const strategies=['top','mid','bottom','adaptive','cycle','cycle-merge','read-unmerged','read','single-merge'];
    for(const profile of ['calm','alert','tired'])for(const seed of [1,7,19,41,77,119,314,20261119])for(const strategy of strategies) results.push(simulate(seed,profile,strategy));
    const summary=Object.fromEntries(['calm','alert','tired'].flatMap(profile=>strategies.map(strategy=> {
        const rows=results.filter(row=>row.profile===profile&&row.strategy===strategy);
        return [`${profile}-${strategy}`,{wins:rows.filter(row=>row.phase==='won').length,total:rows.length,seconds:Math.round(rows.reduce((sum,row)=>sum+row.time,0)/rows.length),merges:rows.reduce((sum,row)=>sum+row.merges,0),repairs:rows.reduce((sum,row)=>sum+row.repairs,0)}];
    })));
    await fs.mkdir('test-results/siege',{recursive:true});
    await fs.writeFile('test-results/siege/balance.json',JSON.stringify({summary,results},null,2));
    console.log(JSON.stringify({cases:results.length,summary},null,2));
}
