import React from 'react';
import { CircleDot, Truck, Umbrella, Shield, Route, Hammer, Crosshair, PawPrint, Wrench } from 'lucide-react';
import { CATALOG, t, unitDefinition, laneDefinition } from './catalog.js';
import { pointOnRoute, routeLength, positionOf } from './geometry.js';

export const UNIT_ICONS = { truck: Truck, 'circle-dot': CircleDot, umbrella: Umbrella, shield: Shield, route: Route };
const coordStyle = point => ({ left: `${point.x / CATALOG.map.width * 100}%`, top: `${point.y / CATALOG.map.height * 100}%` });
const path = points => points.map(point => point.join(',')).join(' ');

export function Battlefield({ state, selectedLane, selectedUnit, selectedTower, materials, mergeAnchor, onLane, onUnit, onTower }) {
    const intent = state.animal.intent;
    const guarding = state.animal.guardUntilMs > state.elapsedMs;
    const editable = state.phase === 'decision' && !state.paused;
    const towerFocus = state.towers.find(tower=>tower.id===selectedTower&&tower.status==='active');
    const towerPoint = towerFocus && positionOf(CATALOG,towerFocus);
    const guardPoint = guarding && pointOnRoute(laneDefinition(CATALOG,state.animal.guardLane),routeLength(laneDefinition(CATALOG,state.animal.guardLane))*CATALOG.rules.guardStart);
    const guardPlan = intent.type==='guard'&&!guarding && pointOnRoute(laneDefinition(CATALOG,intent.laneId),routeLength(laneDefinition(CATALOG,intent.laneId))*CATALOG.rules.guardStart);
    const materialPosition = id => {
        if(id==='new') return pointOnRoute(laneDefinition(CATALOG,selectedLane),0);
        const unit=state.units.find(item=>item.id===id);
        return unit ? positionOf(CATALOG,unit) : null;
    };
    const landing = mergeAnchor != null ? materialPosition(mergeAnchor) : null;
    const spawn = pointOnRoute(CATALOG.map.lanes.find(lane=>lane.open),0);
    return <div className={`siege-field ${intent.type === 'sweep' ? 'has-warning' : ''}`} aria-label={t('title')}>
        <svg className="siege-ground" viewBox={`0 0 ${CATALOG.map.width} ${CATALOG.map.height}`} preserveAspectRatio="none" aria-hidden="true">
            <path className="garden-bed" d="M75 163L99 103Q107 91 124 94L207 84L210 108L131 173Z" />
            <path className="garden-bed" d="M139 283L224 202L280 159L279 247L224 278Z" />
            <path className="bench" d="M115 115L155 99L160 113L120 129Z M123 134L163 118" />
            <path className="flower-bed" d="M233 231Q252 217 266 226L258 250L234 259Z" />
            {CATALOG.map.lanes.map(lane => <React.Fragment key={lane.id}>
                <polyline className="road-base" points={path(lane.points)} />
                <polyline className={`road-line ${lane.id === selectedLane ? 'selected' : ''}`} points={path(lane.points)} />
            </React.Fragment>)}
            {towerPoint && <circle data-tower-range={selectedTower} className="tower-range" cx={towerPoint.x} cy={towerPoint.y} r={CATALOG.tower.range}/>}
            {intent.type === 'sweep' && <circle data-sweep-area className="sweep-warning" cx={intent.area.x} cy={intent.area.y} r={intent.area.radius}/>}
            {state.effects.filter(effect=>effect.kind==='sweep').map((effect,index)=><circle key={`sweep-${index}`} className="sweep-impact" cx={effect.area.x} cy={effect.area.y} r={effect.area.radius} opacity={(effect.untilMs-state.elapsedMs)/650}/>)}
            {guardPoint && <circle className="guard-zone" cx={guardPoint.x} cy={guardPoint.y} r="18"/>}
            {guardPlan && <circle className="guard-planned-zone" cx={guardPlan.x} cy={guardPlan.y} r="18"/>}
            {state.effects.filter(effect=>effect.from).map((effect,index)=><line key={`${effect.kind}-${index}`} className={effect.kind==='towerShot'?'tower-shot':'unit-shot'} x1={effect.from.x} y1={effect.from.y} x2={effect.to.x} y2={effect.to.y} />)}
            {state.effects.filter(effect=>effect.point).map((effect,index)=> {
                const duration=effect.kind==='merge'||effect.kind==='repair'?650:350, progress=1-(effect.untilMs-state.elapsedMs)/duration;
                return <circle key={`${effect.kind}-${index}`} className={`impact-ring ${effect.kind}`} cx={effect.point.x} cy={effect.point.y} r={10+progress*24} opacity={1-progress}/>;
            })}
            {materials.length > 0 && mergeAnchor != null && materials.map(id=> {
                const a=materialPosition(id), b=landing;
                if(!a || !b || id===mergeAnchor) return null;
                return <line className="merge-link" key={id} x1={a.x} y1={a.y} x2={b.x} y2={b.y}/>;
            })}
            {landing&&<circle className="merge-landing" cx={landing.x} cy={landing.y} r="21"/>}
            <circle className="spawn-zone" cx={spawn.x} cy={spawn.y} r="22" />
            <path className="spawn-arrow" d="M-8 7L8 -9M-5 -9H8V4" transform={`translate(${spawn.x} ${spawn.y})`} />
        </svg>
        <img className={`siege-cat ${guarding?'cat-guard':''} ${state.effects.some(effect=>effect.kind==='sweep')?'cat-sweep':''}`} src={CATALOG.map.animalImage} alt={t('animal')} draggable="false" />
        <span className="cat-ground-shadow" aria-hidden="true" />
        {CATALOG.map.pads.map(pad=> {
            const tower=state.towers.find(item=>item.id===pad.id);
            const point=pointOnRoute(laneDefinition(CATALOG,pad.laneId),routeLength(laneDefinition(CATALOG,pad.laneId))*pad.at);
            const stage=tower?.status;
            const marked=intent.type==='build' && intent.padId===pad.id;
            const repairing=state.animal.repair?.towerId===pad.id, repairPlanned=intent.type==='repair'&&intent.padId===pad.id;
            return <button type="button" key={pad.id} className={`siege-tower ${stage||'empty'} ${marked?'planned':''} ${repairing||repairPlanned?'repairing':''}`} style={coordStyle(point)}
                data-tower={pad.id} data-status={stage||'empty'} data-hp={tower?.hp??0} data-repair={repairing?'active':repairPlanned?'planned':'none'} aria-pressed={selectedTower===pad.id} aria-label={`${t(laneDefinition(CATALOG,pad.laneId).nameKey)} ${t(stage==='destroyed'?'destroyed':stage==='building'?'building':stage==='active'?'tower':'emptyPad')}`}
                onClick={()=>onTower(pad.id)}>
                {repairing?<Wrench size={17}/>:stage==='active'?<Crosshair size={18}/>:stage==='building'||marked?<Hammer size={17}/>:<span className="pad-mark"/>}
                {tower && stage!=='destroyed' && <span className="entity-hp"><span style={{width:`${Math.max(0,tower.hp/CATALOG.tower.hp)*100}%`}}/></span>}
            </button>;
        })}
        {state.units.map(unit=> {
            const definition=unitDefinition(CATALOG,unit.definitionId), Icon=UNIT_ICONS[definition.icon];
            const selected=unit.id===selectedUnit, material=materials.includes(unit.id);
            return <button type="button" key={unit.id} data-unit={unit.id} data-definition={unit.definitionId} data-lane={unit.laneId} data-distance={unit.distance.toFixed(1)}
                data-hp={unit.hp} className={`siege-unit kind-${definition.icon} ${!definition.bag?'upgraded':''} ${selected?'selected':''} ${material?'material':''} ${unit.id===mergeAnchor?'merge-anchor':''} ${unit.transit?'in-transit':''}`}
                style={coordStyle(positionOf(CATALOG,unit))} onClick={()=>onUnit(unit.id)} aria-pressed={selected||material} title={t(definition.detailKey)}
                aria-label={`${t(definition.nameKey)} ${t(laneDefinition(CATALOG,unit.laneId).nameKey)} ${Math.ceil(unit.hp)}/${definition.hp}`}>
                <Icon size={definition.bag?17:21}/>
                {!definition.bag && <span className="upgrade-mark" aria-hidden="true">Ⅱ</span>}
                {unit.shield>0 && <span className="entity-shield" aria-label={t('shield')}/>}
                <span className="entity-hp"><span style={{width:`${Math.max(0,unit.hp/definition.hp)*100}%`}}/></span>
            </button>;
        })}
        <div className="map-route-labels">
            {CATALOG.map.lanes.map(lane=> {
                const point=pointOnRoute(lane,routeLength(lane)*.13);
                return <button type="button" key={lane.id} className={`map-lane ${selectedLane===lane.id?'selected':''}`} style={coordStyle(point)}
                    data-map-lane={lane.id} aria-pressed={selectedLane===lane.id} disabled={!editable||!lane.open} onClick={()=>onLane(lane.id)}>{t(lane.nameKey)}</button>;
            })}
        </div>
        <div className="animal-action-tag" data-intent={intent.type}>
            {guarding&&<span><PawPrint size={13}/>{t('guard')} · {t(laneDefinition(CATALOG,state.animal.guardLane).nameKey)}</span>}
            {state.elapsedMs<state.animal.recoveryUntilMs&&<span>{t('recovery')}</span>}
            <span>{intent.type==='sweep'?<><Route size={13}/>{t('sweep')}</>:intent.type==='repair'?<><Wrench size={13}/>{t('repairPlan')}</>:intent.type==='build'?<><Hammer size={13}/>{t('building')}</>:!guarding?<><PawPrint size={13}/>{t('guardPlan')} · {t(laneDefinition(CATALOG,intent.laneId).nameKey)}</>:null}</span>
        </div>
        {towerFocus&&<span className="tower-range-caption">{t(laneDefinition(CATALOG,towerFocus.laneId).nameKey)} · {towerFocus.hp}/{CATALOG.tower.hp} · {t('range')} {CATALOG.tower.range}</span>}
    </div>;
}
