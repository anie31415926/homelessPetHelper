import React from 'react';
import { Pet } from '../Pet.jsx';
import { PROFILES } from './engine.js';

function Plant({ x, y, flower = false }) {
  return <g transform={`translate(${x} ${y})`} stroke="#77967a" strokeWidth="1.1"><path d="M0 14V-14M0 7Q-18 6-12-7 0-5 0 7M0 0Q18-3 12-17 0-15 0 0" fill="#9bb68d" />{flower && <g transform="translate(0 -17)" fill="#fffdf7" stroke="none"><circle cx="-4" r="4" /><circle cx="4" r="4" /><circle cy="-4" r="4" /><circle cy="4" r="4" /><circle r="2.5" fill="#efc574" /></g>}</g>;
}

function Transport({ x, y, open = false }) {
  return <g transform={`translate(${x} ${y})`} stroke="#88a28b" strokeWidth="1.5"><ellipse cy="24" rx="36" ry="7" fill="#43584d" fillOpacity=".1" stroke="none" /><path d="M-29-15q29-18 58 0l5 32h-68Z" fill="#b5cbb2" /><rect x="-34" y="5" width="68" height="23" rx="4" fill="#e6e9d9" /><rect x="-19" y="-15" width="38" height="40" rx="11" fill={open ? '#768d77' : '#fffdf7'} />{!open && <path d="M-10-8v27M0-12v34M10-8v27" stroke="#a9baa5" />}<path d="M-9-27q0-12 18 0" fill="none" strokeWidth="4" />{open && <path d="m19-11 23 11v33L19 24Z" fill="#e2e9d7" />}</g>;
}

function Bowl({ x, y }) {
  return <g transform={`translate(${x} ${y})`}><ellipse cy="9" rx="22" ry="5" fill="#43584d" opacity=".09" /><path d="m-21-1 4 14q17 8 34 0l4-14" fill="#dba29d" stroke="#b3847d" /><ellipse rx="21" ry="7" fill="#fffdf7" stroke="#b3847d" /><ellipse rx="15" ry="4" fill="#c9a678" /><circle cx="-7" r="2" fill="#9c7a56" /><circle cx="4" cy="2" r="2" fill="#9c7a56" /></g>;
}

function Rescuer({ x, y, waiting }) {
  return <g transform={`translate(${x} ${y}) scale(${waiting ? '.9 .85' : '1'})`}>
    <ellipse cy="30" rx="29" ry="9" fill="#43584d" opacity=".1" />
    <path d="m-12 10-4 20m27-20 4 20" stroke="#6c8075" strokeWidth="11" strokeLinecap="round" />
    <path d="M-22-24q22-13 44 0L18 17Q0 24-18 17Z" fill="#d78484" stroke="#b37d77" strokeWidth="1.5" />
    <rect x="-12" y="-22" width="24" height="32" rx="8" fill="#e7eedb" stroke="#91a88c" /><path d="M-5-9H5M0-14v10" stroke="#7e9a7f" strokeWidth="3" />
    <path d={waiting ? 'm-21-17-9 25m51-25 9 25' : 'm-21-17-8 19m50-19 11 15'} stroke="#edc9ac" strokeWidth="9" strokeLinecap="round" />
    <circle cy="-43" r="20" fill="#f4d7b9" stroke="#bea084" strokeWidth="1.4" /><path d="M-20-43q-5-27 20-27 26 0 20 27" fill="#638b73" />
    <ellipse cy="-58" rx="30" ry="7" fill="#94af92" stroke="#6f9278" strokeWidth="1.5" /><path d="M-5-66H5" stroke="#e7f2e9" strokeWidth="3" />
  </g>;
}

export default function EncounterScene({ game, onPet }) {
  const turn = game.turn;
  const station = game.stage === 'station';
  const safe = turn.rescued;
  const inCarrier = safe && !station;
  const mood = safe ? 'safe' : turn.phase === 'react' && turn.effect === 'startled' ? 'wary' : turn.mood;
  const locations = [{ x: 213, y: 340 }, { x: 245, y: 292 }, { x: 265, y: 243 }];
  const pet = station ? { x: 195, y: 313 } : inCarrier ? { x: 213, y: 316 } : locations[turn.distance];
  return <svg className={`encounter-scene mood-${mood} ${turn.phase === 'react' ? `effect-${turn.effect}` : ''}`} viewBox="0 0 390 500" role="img" aria-label={station ? '小动物已经回到救助站，安心坐在软垫上' : `回合救助现场，${PROFILES[game.profile].name}正在${turn.distance === 2 ? '远处' : turn.distance === 1 ? '附近' : '身旁'}`} data-alert={turn.alert} data-distance={turn.distance} data-mood={turn.mood} data-phase={turn.phase} data-round={turn.round} data-seed={game.seed} data-rescued={turn.rescued}>
    <defs><pattern id="outing-grass" width="49" height="43" patternUnits="userSpaceOnUse"><path d="m9 16 2-4 3 4m24 19 2-3" fill="none" stroke="#afc6a6" strokeWidth="1" opacity=".65" /></pattern><pattern id="outing-paving" width="55" height="45" patternUnits="userSpaceOnUse"><path d="M0 0h55v45" fill="none" stroke="#d6dac9" /></pattern></defs>
    <rect width="390" height="500" fill="#dfebd2" /><rect width="390" height="500" fill="url(#outing-grass)" />
    {station ? <>
      <path d="M52 276V119l143-83 143 83v157Z" fill="#fffdf7" stroke="#b4c4ad" strokeWidth="2" /><path d="m38 122 157-91 157 91" fill="none" stroke="#b7cbb4" strokeWidth="14" strokeLinecap="round" />
      <text x="195" y="112" textAnchor="middle" fill="#71916f" fontSize="16" fontWeight="700">毛茸茸救助站</text>
      <rect x="86" y="152" width="55" height="50" fill="#deece9" stroke="#aec3b1" /><rect x="249" y="152" width="55" height="50" fill="#deece9" stroke="#aec3b1" /><path d="M113 152v50m163-50v50" stroke="#aec3b1" />
      <path d="M168 276v-91q27-36 54 0v91" fill="#e0beb4" /><path d="M0 276h390v224H0Z" fill="#e9e8d9" /><ellipse cx="195" cy="366" rx="117" ry="45" fill="#c9d8c4" /><ellipse cx="195" cy="357" rx="106" ry="40" fill="#e7f2e9" stroke="#b4c9ae" strokeWidth="2" />
    </> : <>
      {game.route === 'puzzle' ? <g><rect width="390" height="112" fill="#e9e3d7" /><path d="M0 29h390M0 62h390m-345-33v33m65-33v33m65-33v33m65-33v33m65-33v33" stroke="#cfc6b5" /><rect x="230" y="80" width="112" height="100" rx="7" fill="#9ba996" /><rect x="240" y="90" width="92" height="85" rx="4" fill="#becab2" /><path d="M246 96h80m-80 17h80m-80 17h80" stroke="#9eaf97" /></g> : <g fill="#e6e9d9" stroke="#a9b99d"><path d="M0 68h390v8H0Zm0 23h390v7H0Z" />{Array.from({ length: 16 }, (_, i) => <path key={i} d={`M${i * 27 - 6} 112V48l5-6 5 6v64Z`} />)}</g>}
      <path d="M0 347q103-40 193-1t197-3v157H0Z" fill="#e9e8d9" /><path d="M0 347q103-40 193-1t197-3v157H0Z" fill="url(#outing-paving)" />
      <path d="M225 195q50-17 100 0" stroke="#a8b998" strokeWidth="3" strokeLinecap="round" fill="none" />
      <g transform="translate(326 303)"><path d="M-25 15q-8-34 8-20-2-37 12-15 16-22 17 9 23-15 17 26Z" fill="#a5bb96" stroke="#8da583" /></g>
      {[0, 1, 2].map(index => <ellipse key={index} cx={locations[index].x} cy={locations[index].y + 44} rx={index === 2 ? 46 : 38} ry="12" fill={turn.distance === index ? '#b9cdb0' : 'none'} stroke="#bac8ae" strokeDasharray="3 6" strokeWidth="1" opacity=".5" />)}
      <Rescuer x={106} y={385} waiting={turn.lastAction === 'wait' && turn.phase === 'react'} />
    </>}
    <Plant x={25} y={208} flower /><Plant x={354} y={436} flower /><Plant x={36} y={464} />
    {(turn.mood === 'eating' || turn.lastAction === 'food') && !station && <Bowl x={pet.x - 22} y={pet.y + 49} />}
    {inCarrier && <g transform="translate(213 355) scale(1.8)"><Transport x={0} y={0} open /></g>}
    <g className="encounter-animal" transform={`translate(${pet.x} ${pet.y})`}>
      <g className="animal-pose"><svg x={station ? '-91' : '-77'} y={station ? '-123' : '-105'} width={station ? '182' : '154'} height={station ? '194' : '164'}><Pet kind={PROFILES[game.profile].kind} happy={turn.alert <= 35 || station} /></svg></g>
      {mood === 'wary' && <g stroke="#b6837b" strokeWidth="2" strokeLinecap="round"><path d="m-70-53-10-7m14-2-4-11" /></g>}
      {mood === 'hungry' && <g transform="translate(-63 -92)"><path d="M-12-7h24v19h-24Z" fill="#fffdf7" stroke="#c0cbb3" /><path d="M-6 12v7l6-7" fill="#fffdf7" stroke="#c0cbb3" /><path d="M-5 1q6-8 12 0-6 7-12 0Zm12 0 5-4v8Z" fill="#c7ad7f" /></g>}
      {['curious', 'playing'].includes(mood) && <g transform="translate(-69 27)"><circle r="14" fill="#b9abd0" stroke="#9d8db2" /><path d="m-8-6 14 14m-1-15-12 14" stroke="#eae3ef" strokeWidth="2" /></g>}
      {(turn.alert <= 35 || station) && <path className="affection" d="M52-92C30-106 42-123 52-111 65-124 77-106 52-92Z" fill="#d78484" />}
    </g>
    {inCarrier ? <path d="M152 373h122v28q-61 14-122 0Z" fill="#e6e9d9" stroke="#a0b49c" strokeWidth="1.5" /> : <Transport x={station ? 304 : 94} y={station ? 366 : 447} open={!station} />}
    {station && <g role="button" tabIndex="0" aria-label="轻轻抚摸" className="station-pet-target" onPointerDown={onPet} onKeyDown={event => { if (event.key === 'Enter' || event.key === ' ') { event.preventDefault(); onPet(); } }}><ellipse cx="195" cy="310" rx="90" ry="100" fill="transparent" /></g>}
    {station && game.pats > 0 && <text x="195" y="425" textAnchor="middle" fill="#b5837c" fontSize="12">{game.pats < 3 ? '它轻轻蹭了蹭你的手' : '它在这里，安心下来了'}</text>}
  </svg>;
}
