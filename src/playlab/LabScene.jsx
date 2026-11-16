import React from 'react';
import { Pet } from '../Pet.jsx';
import { BOARD_X, BOARD_Y, BRIDGES, CELL, COLS, HOME, ROWS, canClean, canRescue, center, matrixFor } from './engine.js';

function Plant({ x, y, flower = false, large = false }) {
  return <g transform={`translate(${x} ${y}) scale(${large ? 1.5 : 1})`} stroke="#70937a" strokeWidth="1.1">
    <path d="M0 14V-12M0 7Q-18 4-12-6 0-5 0 7M0 0Q18-4 12-14 0-13 0 0" fill="#9ab58d" />
    {flower && <g transform="translate(0 -15)" stroke="none" fill="#fffdf7"><circle cx="-5" r="5" /><circle cx="5" r="5" /><circle cy="-5" r="5" /><circle cy="5" r="5" /><circle r="3" fill="#efc574" /></g>}
  </g>;
}

function Fence() {
  return <g fill="#e5e8d7" stroke="#aab99d" strokeWidth="1"><path d="M0 46h390v7H0Zm0 20h390v7H0Z" />{Array.from({ length: 16 }, (_, i) => <path key={i} d={`M${i * 27 - 6} 83V39l5-6 5 6v44Z`} />)}</g>;
}

function Crate({ x, y, home = false, selected = false }) {
  return <g transform={`translate(${x} ${y})`}>
    <ellipse cy="18" rx="23" ry="6" fill="#43584d" opacity=".1" />
    {home ? <g stroke="#88a28b" strokeWidth="1.5"><path d="M-24-10q24-18 48 0l4 28h-56Z" fill="#b5cbb2" /><rect x="-28" y="4" width="56" height="19" rx="4" fill="#e6e9d9" /><rect x="-15" y="-12" width="30" height="32" rx="10" fill="#fffdf7" /><path d="M-8-6v20M0-9v26M8-6v20" stroke="#a9baa5" /><path d="M-8-22q0-10 16 0" fill="none" strokeWidth="4" /></g> : <g stroke={selected ? '#a97571' : '#a99272'} strokeWidth="1.5"><path d="M-20-16h40v34h-40Z" fill={selected ? '#e8b8ac' : '#dfc69e'} /><path d="M-20-16-13-22h39l-6 6m0 0 6-6v34l-6 6" fill="#c9b18a" /><path d="M-20-5h40M-20 7h40m-30-22v33m20-33v33" /><path d="m-5-15 9 6 3-6" fill="#efe5c7" stroke="none" /></g>}
  </g>;
}

export function AnimalArt({ value, kind = 'cat', scale = 1, happy = false, hidden = false }) {
  return <g transform={`translate(${value.x} ${value.y})`} className={value.path?.length ? 'lab-animal walking-pet' : 'lab-animal'} opacity={hidden ? .48 : 1}>
    <g transform={`scale(${scale})`}><svg x="-36" y="-49" width="72" height="77"><Pet kind={kind} happy={happy} /></svg></g>
  </g>;
}

function Bowl({ x, y }) {
  return <g transform={`translate(${x} ${y})`}><ellipse cy="8" rx="15" ry="4" fill="#43584d" opacity=".1" /><path d="m-13-1 3 11q10 5 20 0l3-11" fill="#d78484" stroke="#a77674" /><ellipse rx="13" ry="5" fill="#fffdf7" stroke="#a77674" /><ellipse rx="9" ry="3" fill="#cda66c" /><g fill="#947b59"><circle cx="-4" r="1.5" /><circle cx="3" cy="1" r="1.5" /></g></g>;
}

function Rescuer({ value, toy }) {
  return <g transform={`translate(${value.x} ${value.y})`} className={value.path.length ? 'lab-person walking-person' : 'lab-person'}>
    <ellipse cy="15" rx="15" ry="5" fill="#43584d" opacity=".12" /><g className="person-body">
      <path d="m-7 6-2 9m15-9 2 9" stroke="#6c7f75" strokeWidth="6" strokeLinecap="round" />
      <path d="M-12-10Q0-17 12-10L10 8Q0 12-10 8Z" fill="#d78484" stroke="#b17d78" /><rect x="-7" y="-9" width="14" height="17" rx="5" fill="#dfebd2" stroke="#90a48b" />
      <path d="M-3-2h6m-3-3v6" stroke="#799279" strokeWidth="2" /><path d="m-12-6-4 10m28-10 4 10" stroke="#ecc7a8" strokeWidth="5" strokeLinecap="round" />
      <circle cy="-21" r="11" fill="#f2d6b9" stroke="#bea184" /><path d="M-11-19Q-14-35 0-35 15-35 11-19" fill="#638b73" /><ellipse cy="-29" rx="17" ry="4" fill="#91ac90" stroke="#638b73" />
      {toy && <g transform="translate(23 -2)"><circle r="8" fill="#b7aacd" stroke="#8d7f9e" /><path d="M-4-4 4 4m0-8-8 8" stroke="#e7e3ee" /></g>}
    </g>
  </g>;
}

function Junk({ item }) {
  const point = center(item.col, item.row);
  const fraction = item.hp / item.maxHp;
  const variant = (item.col * 11 + item.row * 7) % 4;
  return <g transform={`translate(${point.x + (variant - 1.5) * 2} ${point.y + (variant % 2 ? -2 : 2)}) rotate(${(variant - 1.5) * 7}) scale(${(.65 + fraction * .35) * (variant === 2 ? .9 : 1)})`} opacity={.3 + fraction * .7} className={`junk junk-${item.type}`}>
    {item.type === 'litter' ? <>
      <ellipse cy="13" rx="22" ry="7" fill="#7e8c71" opacity=".15" /><g transform="rotate(-24)"><path d="M-14-14h9v5l4 3v19q-10 6-17 0V-6l4-3Z" fill="#aecdd0" stroke="#809fa2" /><path d="M-18 1h17v9h-17Z" fill="#e8eee0" /><path d="M-14-17h9v4h-9Z" fill="#d8a19b" stroke="#a77d76" /></g>
      <path d="m4-7 16 1 3 18-17 1Z" fill={variant % 2 ? '#e6cdaa' : '#d9dfd7'} stroke="#b39d7d" /><path d={variant % 2 ? 'm5-7 7 5 8-4m-6 5 2 8' : 'm8-2 10 1m-9 5 10 1m-9 4 7 1'} fill="none" stroke="#b39d7d" />
    </> : item.type === 'weeds' ? <g stroke="#75916f" strokeWidth="1.3" fill="#96ad7f">
      <path d="M-23 18q-2-31 10-9-7-44 7-20Q-5-42 4-7q11-33 12-4 20-20 10 13l-4 16Z" /><path d="M-7 13q-7-22-9-24M0 15V-15m9 29 7-26" fill="none" /><path d="m-11-8-8-4m20-2 7-8m8 13 10-7" /><circle cx="-7" cy="-14" r="3" fill="#d3c0d8" stroke="none" /><circle cx="15" cy="-8" r="2.5" fill="#efc574" stroke="none" />
    </g> : <g fill="#9b8c6f"><path d="M-23 3q-4-12 12-11 7-15 19-4 18-4 13 7 17 12-3 16-3 10-19 5-24 8-27-4Z" opacity=".5" /><path d="M-12-4q-5 12 4 14m15-13 6 6" stroke="#857b62" strokeWidth="2" opacity=".3" fill="none" /><circle cx="-22" cy="-12" r="2" opacity=".4" /><circle cx="25" cy="13" r="2" opacity=".5" /></g>}
  </g>;
}

function Bench({ col, row, width = 3 }) {
  const p = center(col, row);
  return <g transform={`translate(${p.x - 18} ${p.y - 14})`} stroke="#9a9f87" strokeWidth="1.2"><path d={`M5 31v8m${width * CELL - 20}-8v8`} strokeWidth="4" /><rect x="0" y="-8" width={width * CELL - 8} height="32" rx="4" fill="#b7c4a2" /><path d={`M0 2h${width * CELL - 8}M0 14h${width * CELL - 8}`} /><rect y="24" width={width * CELL - 8} height="8" rx="2" fill="#cdd5ba" /></g>;
}

export default function LabScene({ game, svgRef, handlers, onCell, onBox, onJunk, onPet }) {
  const puzzle = game.mode === 'puzzle';
  const cleanup = game.mode === 'cleanup';
  const matrix = matrixFor(game);
  const home = center(HOME.col, HOME.row);
  return <svg className={`lab-scene scene-${game.mode}`} viewBox="0 0 390 540" role="group" aria-label={`${game.mode}救助场景`} ref={svgRef} {...handlers}
    data-mode={game.mode} data-done={game.done} data-actions={game.actions} data-cleaned={game.cleaned ?? ''} data-safe={game.pets?.filter(pet => pet.safe).length ?? ''}
    data-player-x={game.player?.x.toFixed(1) ?? ''} data-player-y={game.player?.y.toFixed(1) ?? ''} data-animal-x={game.animal?.x.toFixed(1) ?? ''} data-animal-y={game.animal?.y.toFixed(1) ?? ''}
    data-attention={game.attention ?? ''} data-trust={game.trust?.toFixed(3) ?? ''} data-clock={game.clock.toFixed(2)}>
    <defs>
      <pattern id="lab-grass" width="39" height="43" patternUnits="userSpaceOnUse"><path d="m8 19 2-5 3 4m19 17 2-4" fill="none" stroke="#b3c7a9" strokeWidth="1.2" opacity=".5" /></pattern>
      <pattern id="lab-paving" width="44" height="44" patternUnits="userSpaceOnUse" x={BOARD_X} y={BOARD_Y}><path d="M0 0h44v44H0Z" fill="none" stroke="#d5d7c6" strokeWidth="1" /><path d="m6 36 2 1m27-30 2 1" stroke="#c8ccbb" strokeWidth="1" /></pattern>
      <pattern id="lab-water" width="32" height="18" patternUnits="userSpaceOnUse"><path d="M0 7q8-5 16 0t16 0" fill="none" stroke="#f0f7ed" strokeWidth="1.5" opacity=".8" /></pattern>
    </defs>
    <rect width="390" height="540" fill={puzzle ? '#e2e8d8' : '#dfebd2'} /><rect width="390" height="540" fill="url(#lab-grass)" />
    {puzzle ? <g><rect y="0" width="390" height="70" fill="#eee5da" /><path d="M0 18h390M0 44h390m-350-26v26m60-26v26m60-26v26m60-26v26m60-26v26m60-26v26" stroke="#d5cbbd" /><path d="M0 67h390" stroke="#c4b7a6" strokeWidth="5" /><path d="M125 26h140v36H125Z" fill="#fffdf7" stroke="#c1b7a6" /><text x="195" y="49" textAnchor="middle" fontSize="13" fill="#87927d">河畔旧仓库</text></g> : <Fence />}
    <Plant x={19} y={117} flower /><Plant x={372} y={205} large /><Plant x={19} y={370} /><Plant x={360} y={478} flower /><Plant x={41} y={490} flower />
    <path d="M149 540v-69q46-23 92 0v69" fill="#e9e8d9" /><path d="M158 510q37-8 74 0" fill="none" stroke="#c5ccb9" strokeWidth="1" strokeDasharray="3 6" />
    <rect x={BOARD_X} y={BOARD_Y} width={CELL * COLS} height={CELL * ROWS} rx="8" fill={cleanup ? '#d9dfc6' : '#eeeddf'} />
    {!cleanup && <rect x={BOARD_X} y={BOARD_Y} width={CELL * COLS} height={CELL * ROWS} rx="8" fill="url(#lab-paving)" />}
    {Array.from({ length: ROWS * COLS }, (_, index) => {
      const col = index % COLS;
      const row = Math.floor(index / COLS);
      const p = center(col, row);
      const wall = puzzle && matrix[row][col] && row !== 4 && !game.boxes.some(box => box.col === col && box.row === row);
      if (wall) return <g key={index}><rect x={p.x - 22} y={p.y - 22} width="44" height="44" rx="3" fill="#b6c6a7" stroke="#a5b69a" /><path d={`m${p.x - 10} ${p.y - 4} 4-3m12 19 4-2`} stroke="#8fa584" opacity=".6" /></g>;
      if (cleanup || (!puzzle && (col === 0 || col === 6))) return null;
      return <g key={index} role="button" tabIndex={game.done ? -1 : 0} aria-label={`地面 ${col + 1}-${row + 1}`} className="ground-target" data-cell={`${col},${row}`} onKeyDown={event => { if (event.key === 'Enter' || event.key === ' ') { event.preventDefault(); onCell(col, row); } }}>
        <rect x={p.x - 22} y={p.y - 22} width="44" height="44" fill="transparent" />
      </g>;
    })}
    {puzzle ? <>
      <rect x={BOARD_X} y={BOARD_Y + CELL * 4 + 4} width={CELL * COLS} height="36" fill="#b7d5d0" /><rect x={BOARD_X} y={BOARD_Y + CELL * 4 + 4} width={CELL * COLS} height="36" fill="url(#lab-water)" />
      {BRIDGES.map(col => { const p = center(col, 4); const built = game.bridge === col; return <g key={col} className="bridge-slot" data-bridge={col} role="button" tabIndex={game.done ? -1 : 0} aria-label={`${col === 1 ? '左' : '右'}侧搭桥位置`} onKeyDown={event => { if (event.key === 'Enter' || event.key === ' ') { event.preventDefault(); onCell(col, 4); } }}>
        <rect x={p.x - 16} y={p.y - 24} width="32" height="48" rx="3" stroke={built ? '#9d8f74' : '#88b1a9'} strokeWidth="2" strokeDasharray={built ? undefined : '4 4'} fill={built ? '#dcc49d' : '#e7f2e9'} fillOpacity={built ? 1 : .35} />
        {built && <path d={`M${p.x - 15} ${p.y - 12}h30m-30 12h30m-30 12h30`} stroke="#ac997a" strokeWidth="1.4" />}
      </g>; })}
      {game.boxes.map(box => { const p = center(box.col, box.row); return <g key={box.id} data-box={box.id} data-col={box.col} data-row={box.row} role="button" tabIndex={game.done ? -1 : 0} aria-pressed={game.selected === box.id} aria-label={`搬动木箱 ${box.id.toUpperCase()}`} className="box-target"
        onKeyDown={event => { const directions = { ArrowLeft: [-1, 0], ArrowRight: [1, 0], ArrowUp: [0, -1], ArrowDown: [0, 1] }; if (directions[event.key]) { event.preventDefault(); event.stopPropagation(); onBox(box.id, box.col + directions[event.key][0], box.row + directions[event.key][1]); } else if (event.key === 'Enter' || event.key === ' ') { event.preventDefault(); onBox(box.id); } }}>
        <rect x={p.x - 22} y={p.y - 22} width="44" height="44" fill="transparent" /><Crate x={p.x} y={p.y} selected={game.selected === box.id} />
        <text x={p.x} y={p.y + 5} textAnchor="middle" fill="#8d7b61" fontSize="10" pointerEvents="none">{box.id.toUpperCase()}</text>
      </g>; })}
      {game.drag && <g opacity=".5" pointerEvents="none"><Crate x={game.drag.x} y={game.drag.y} selected /></g>}
      {game.bait && <Bowl {...center(game.bait.col, game.bait.row)} />}
      {!game.done && <AnimalArt value={game.animal} kind={game.kind || 'cat'} happy={Boolean(game.animal.path.length)} />}
    </> : cleanup ? <>
      {game.pets.filter(pet => !pet.safe).map(pet => <g key={pet.id} data-pet={pet.id} role="button" tabIndex={game.done ? -1 : 0} aria-label={`接回${pet.kind === 'cat' ? '小猫' : '小狗'}`} onKeyDown={event => { if (event.key === 'Enter' || event.key === ' ') { event.preventDefault(); onPet(pet.id); } }}>
        <circle cx={pet.x} cy={pet.y} r="23" fill={canRescue(game, pet) ? '#f5dfdf' : '#c7d3b8'} stroke={canRescue(game, pet) ? '#d78484' : '#aabca0'} strokeDasharray="3 4" />
        <AnimalArt value={pet} kind={pet.kind} happy={pet.following || canRescue(game, pet)} scale={.88} />
      </g>)}
      {game.items.filter(item => item.hp > 0).sort((a, b) => a.row - b.row).map(item => <g key={item.id} data-junk={item.id} data-type={item.type} data-hp={item.hp.toFixed(2)} data-accessible={canClean(game, item)} role="button" tabIndex={game.done ? -1 : 0} aria-label={`${item.type === 'litter' ? '捡拾杂物' : item.type === 'weeds' ? '修剪杂草' : '冲洗泥斑'} ${item.col + 1}-${item.row + 1}`} onKeyDown={event => { if (event.key === 'Enter' || event.key === ' ') { event.preventDefault(); onJunk(item); } }}>
        <rect x={center(item.col, item.row).x - 22} y={center(item.col, item.row).y - 22} width="44" height="44" fill="transparent" /><Junk item={item} />
      </g>)}
      {game.effects.map((effect, index) => <g key={`${effect.at}-${index}`} transform={`translate(${effect.x} ${effect.y - (game.clock - effect.at) * 25})`} opacity={1 - (game.clock - effect.at) / .8} stroke={effect.kind === 'mud' ? '#91b7b7' : '#fffdf7'} strokeWidth="2"><path d="M-9-4h8m-4-4v8m12 3h8m-4-4v8" /><circle cx="2" cy="12" r="2" fill="#fffdf7" stroke="none" /></g>)}
      {game.brush && <g transform={`translate(${game.brush.x} ${game.brush.y})`} pointerEvents="none"><circle r="29" fill={game.tool === 'mud' ? '#d6edf0' : '#fffdf7'} fillOpacity=".22" stroke="#fffdf7" strokeWidth="2" strokeDasharray="4 3" />{game.tool === 'mud' && <g fill="#f3faf5" opacity=".7"><circle cx="-9" cy="-9" r="5" /><circle cx="10" cy="3" r="7" /><circle cx="-5" cy="12" r="4" /></g>}</g>}
    </> : <>
      <path d="M85 149q220-20 220 63t-220 43q-39 0-14 59t201 67q52 6 14 36t-90 20" fill="none" stroke="#dfe4cf" strokeWidth="29" />
      <Bench col={2} row={3} /><Plant {...center(1, 5)} large flower />
      <g transform={`translate(${center(5, 4).x} ${center(5, 4).y})`}><ellipse cy="12" rx="12" ry="4" fill="#6c836b" opacity=".15" /><circle r="12" fill="#e7bd77" stroke="#b69a68" /><path d="M-7-10q16 6 12 21m-16-10q11-2 19-5" fill="none" stroke="#fff6d9" strokeWidth="2" /></g>
      {game.player.path.length > 0 && <circle cx={game.player.path.at(-1).x} cy={game.player.path.at(-1).y} r="13" fill="none" stroke="#91ad93" strokeDasharray="3 4" />}
      {game.bait && <Bowl x={game.bait.x} y={game.bait.y} />}
      <circle cx={game.animal.x} cy={game.animal.y + 15} r="27" fill="none" stroke={game.attention === '有点紧张' ? '#d78484' : '#88ac91'} strokeWidth="2" strokeDasharray={`${game.trust * 170} 170`} transform={`rotate(-90 ${game.animal.x} ${game.animal.y + 15})`} />
      {game.player.y <= game.animal.y && <Rescuer value={game.player} toy={game.toy} />}
      <AnimalArt value={game.animal} kind="dog" happy={game.trust > .5} />
      {game.player.y > game.animal.y && <Rescuer value={game.player} toy={game.toy} />}
    </>}
    <g data-home="true" pointerEvents="none"><Crate x={home.x} y={home.y + 6} home /><text x={home.x} y={home.y + 41} textAnchor="middle" fill="#7c947a" fontSize="10">{game.goalLabel || '安全回家'}</text></g>
    {cleanup && game.pets.some(pet => pet.safe) && <g transform={`translate(${home.x + 27} ${home.y - 17})`}><circle r="10" fill="#d78484" /><text y="4" fontSize="11" textAnchor="middle" fill="white">{game.pets.filter(pet => pet.safe).length}</text></g>}
    <text x="195" y="520" textAnchor="middle" fill="#8ea187" fontSize="10">{puzzle ? '雨后仓库 · 一条安全的路' : cleanup ? '旧院子 · 藏起来的小家伙' : '河畔小路 · 一起慢慢走'}</text>
  </svg>;
}
