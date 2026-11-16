import React, { memo } from 'react';
import { Pet } from '../Pet.jsx';
import { MAZE, TILE, MAP_X, MAP_Y, NET_LENGTH, cellPoint, netPoint, distance } from './game.js';

function Sprig({ x, y, size = 1, flower = false }) {
  return <g transform={`translate(${x} ${y}) scale(${size})`}>
    <path d="M0 9V-9M0 3Q-15 3-11-6 0-7 0 3M0-2Q13-3 10-11 1-12 0-2" fill="#8eae87" stroke="#6c9075" strokeWidth="1" />
    {flower && <g transform="translate(0 -14)" fill="#fffdf7"><circle cx="-4" cy="0" r="4" /><circle cx="4" cy="0" r="4" /><circle cx="0" cy="-4" r="4" /><circle cx="0" cy="4" r="4" /><circle r="2.5" fill="#efc574" /></g>}
  </g>;
}

function Paw({ x, y, rotate = 0, opacity = .35 }) {
  return <g transform={`translate(${x} ${y}) rotate(${rotate})`} fill="#91a38a" opacity={opacity}>
    <ellipse cy="3" rx="4" ry="3" /><ellipse cx="-5" cy="-2" rx="1.8" ry="2.3" /><ellipse cx="-.8" cy="-4" rx="1.8" ry="2.3" /><ellipse cx="3.5" cy="-3.5" rx="1.8" ry="2.3" /><ellipse cx="6" cy="0" rx="1.6" ry="2" />
  </g>;
}

function Hedge({ x, y, index }) {
  return <g transform={`translate(${x} ${y})`}>
    <rect x="1" y="4" width="29" height="27" rx="8" fill="#77977a" opacity=".2" />
    <path d="M4 2Q9-2 15 1 25-2 28 5L30 20Q29 28 21 28L7 29Q0 27 1 19L0 9Q0 4 4 2Z" fill={index % 3 === 0 ? '#a7c19a' : '#b4cba5'} stroke="#94b18f" strokeWidth=".8" />
    <path d="m7 9 4-2m10 12 3-2M8 22l3 1" stroke="#769b7a" opacity=".5" strokeWidth="1.5" strokeLinecap="round" />
    {index % 11 === 0 && <g fill="#f5dfdf"><circle cx="20" cy="8" r="3" /><circle cx="24" cy="10" r="2.4" /><circle cx="21" cy="12" r="2.8" /><circle cx="21" cy="10" r="1.3" fill="#efc574" /></g>}
  </g>;
}

const MazeMap = memo(function MazeMap() {
  return <g className="maze-map">
    <rect x={MAP_X - 7} y={MAP_Y - 7} width="344" height="344" rx="14" fill="#cad8b9" />
    <rect x={MAP_X} y={MAP_Y} width="330" height="330" rx="8" fill="#f0eddb" />
    {MAZE.flatMap((row, r) => [...row].map((cell, c) => cell === '#' ? <Hedge key={`${r}-${c}`} x={MAP_X + c * TILE} y={MAP_Y + r * TILE} index={r * 11 + c} /> : <g key={`${r}-${c}`} fill="#d9d7c4" opacity=".8"><circle cx={MAP_X + c * TILE + 9} cy={MAP_Y + r * TILE + 20} r="1" /><path d={`M${MAP_X + c * TILE + 23} ${MAP_Y + r * TILE + 7}l2 1`} stroke="#d1d1bb" /></g>))}
  </g>;
});

function Animal({ x, y, kind, caught = false, happy = false, scale = 1 }) {
  return <g className={`scene-animal ${caught ? 'is-caught' : ''}`} transform={`translate(${x} ${y}) scale(${scale})`}>
    <svg x="-43" y="-50" width="86" height="92" overflow="visible"><Pet kind={kind} happy={happy} /></svg>
  </g>;
}

function Rescuer({ x, y, moving, face = 'up' }) {
  return <g className={`rescuer ${moving ? 'walking' : ''}`} transform={`translate(${x} ${y})`}>
    <ellipse cy="11" rx="13" ry="5" fill="#43584d" opacity=".13" />
    <g className="rescuer-body">
      <path d="m-7 4-2 9m15-9 2 9" stroke="#6b756e" strokeWidth="6" strokeLinecap="round" />
      <path d="M-12-10Q0-17 12-10L10 6Q0 11-10 6Z" fill="#d78484" stroke="#ad7473" strokeWidth="1" />
      <rect x="-7" y="-9" width="14" height="15" rx="5" fill="#e6eed9" stroke="#90a28a" />
      <path d="M-3-3h6m-3-3v6" stroke="#799279" strokeWidth="2" strokeLinecap="round" />
      <path d="m-12-7-4 9m28-9 4 9" stroke="#e8c5a9" strokeWidth="5" strokeLinecap="round" />
      <circle cy="-20" r="11" fill="#f4d8b9" stroke="#ba9c82" strokeWidth="1" />
      <path d="M-11-18Q-15-34 0-34 16-34 11-18" fill="#638b73" />
      <ellipse cy="-28" rx="17" ry="4" fill="#8ca78b" stroke="#638b73" strokeWidth="1" />
      <path d="M-3-32h6" stroke="#e7f2e9" strokeWidth="2" strokeLinecap="round" />
      {face !== 'up' && <g fill="#43584d"><circle cx="-4" cy="-19" r="1" /><circle cx="4" cy="-19" r="1" /><path d="M-2-15q2 2 4 0" fill="none" stroke="#a9766b" strokeWidth="1" /></g>}
    </g>
  </g>;
}

function Fence({ y, width = 390 }) {
  return <g stroke="#a8b49c" fill="#e4e9d6" strokeWidth="1">
    <rect x="0" y={y + 10} width={width} height="7" rx="2" /><rect x="0" y={y + 28} width={width} height="6" rx="2" />
    {Array.from({ length: 16 }, (_, i) => <path key={i} d={`M${i * 27 - 7} ${y + 39}v-37l5-6 5 6v37Z`} />)}
  </g>;
}

function Crate({ x, y, scale = 1 }) {
  return <g transform={`translate(${x} ${y}) scale(${scale})`} stroke="#8a9c88" strokeWidth="1.3">
    <path d="M-24-18q24-12 48 0l3 33h-54Z" fill="#b6c9b0" /><path d="M-27 2h54v20h-54Z" fill="#e7e6d4" />
    <rect x="-14" y="-13" width="28" height="32" rx="8" fill="#faf9ed" />
    <path d="M-8-7v20m8-22v24M8-7v20" stroke="#a9b7a1" /><path d="M-8-23q0-12 16 0" fill="none" strokeWidth="4" />
  </g>;
}

function Bowl({ x, y, opacity = 1 }) {
  return <g transform={`translate(${x} ${y})`} opacity={opacity}>
    <ellipse cy="8" rx="15" ry="4" fill="#43584d" opacity=".1" />
    <path d="m-13-1 4 10q9 5 18 0l4-10" fill="#d9a09b" stroke="#ad7975" strokeWidth="1" />
    <ellipse rx="13" ry="5" fill="#fffdf7" stroke="#ad7975" /><ellipse rx="9" ry="3" fill="#c4a16a" />
    <circle cx="-5" r="1.5" fill="#92714c" /><circle cx="3" cy="-1" r="1.5" fill="#92714c" />
  </g>;
}

export default function Scene({ game, onGround, sceneRef, moving }) {
  const maze = game.phase === 'maze';
  const station = game.phase === 'station';
  const success = game.phase === 'success';
  const goalRow = game.entrance === 'bottom' ? 0 : 10;
  const goal = cellPoint(5, goalRow);
  const p = game.player;
  const origin = { x: p.x, y: p.y - 20 };
  const tip = game.net ? netPoint(game.net) : netPoint({ origin, angle: game.angle, length: NET_LENGTH });
  const reachable = distance(origin, game.animal) <= NET_LENGTH + 27;
  return <svg ref={sceneRef} className="game-scene" viewBox="0 0 390 580" role="img" aria-label={maze ? '河畔花园迷宫，救助员正前往动物所在的位置' : station ? '救助站里的小动物' : '救助现场，动物在草地上走动'}
    onPointerDown={onGround} data-phase={game.phase} data-col={p.col} data-row={p.row} data-x={p.x.toFixed(1)} data-y={p.y.toFixed(1)} data-angle={game.angle.toFixed(4)} data-animal-x={game.animal.x.toFixed(1)} data-animal-y={game.animal.y.toFixed(1)} data-net={game.net?.mode || 'idle'} data-eating={game.animal.eating.toFixed(1)}>
    <defs>
      <pattern id="grass-marks" width="51" height="43" patternUnits="userSpaceOnUse"><path d="m9 15 2-4 3 4m25 18 1-3" fill="none" stroke="#b2c8a7" strokeWidth="1" opacity=".5" /></pattern>
      <pattern id="net-mesh" width="9" height="9" patternUnits="userSpaceOnUse" patternTransform="rotate(35)"><path d="M0 0H9V9" fill="none" stroke="#829d92" strokeWidth=".8" opacity=".65" /></pattern>
    </defs>
    <rect width="390" height="580" fill="#dfebd2" /><rect width="390" height="580" fill="url(#grass-marks)" />
    <path d="M-20 5q80 34 135 7T240 8t170 0v-30H-20Z" fill="#b2cca8" />
    {[[19, 84], [366, 135], [16, 363], [373, 480], [28, 559], [344, 554]].map(([x, y], i) => <Sprig key={i} x={x} y={y} size={i % 2 ? .8 : 1.1} flower={i % 2 === 0} />)}
    {maze ? <>
      <Fence y={38} />
      <path d="M125 86q70-10 140 0v37H125Z" fill="#fffdf7" stroke="#c5cdb7" strokeWidth="1.5" />
      <path d="M142 124v14m106-14v14" stroke="#a7b598" strokeWidth="4" />
      <text x="195" y="109" textAnchor="middle" fill="#638b73" fontSize="15" fontWeight="700">河畔花园</text>
      <MazeMap />
      <circle cx={goal.x} cy={goal.y} r="13" fill="#f5dfdf" stroke="#d78484" strokeDasharray="3 3" />
      <Paw x={goal.x} y={goal.y} opacity={.8} />
      <Animal x={195} y={goalRow === 0 ? 147 : 548} kind={game.kind} scale={.64} />
      <g fill="#638b73" fontSize="10" textAnchor="middle"><text x="195" y={goalRow === 0 ? 555 : 155}>{game.entrance === 'bottom' ? '南门' : '北门'}</text></g>
      <Rescuer x={p.x} y={p.y} moving={moving} />
    </> : station ? <>
      <rect y="340" width="390" height="240" fill="#e8e8da" />
      <path d="M55 340V150L195 63l140 87v190Z" fill="#fffdf7" stroke="#b6c3ae" strokeWidth="2" />
      <path d="m38 157 157-98 157 98" fill="none" stroke="#b9cdb8" strokeWidth="15" strokeLinecap="round" />
      <path d="M87 198h62v55H87Zm154 0h62v55h-62Z" fill="#deece9" stroke="#a1b5a5" strokeWidth="2" /><path d="M118 198v55m154-55v55" stroke="#b7c8b7" strokeWidth="2" />
      <text x="195" y="158" fill="#638b73" fontSize="17" textAnchor="middle" fontWeight="700">毛茸茸救助站</text>
      <rect x="166" y="205" width="58" height="135" rx="27" fill="#e2c4b9" />
      <ellipse cx="195" cy="428" rx="116" ry="48" fill="#cbd9c5" /><ellipse cx="195" cy="420" rx="105" ry="41" fill="#e7f2e9" stroke="#b1c7ac" strokeWidth="2" />
      <Animal x={195} y={366} kind={game.kind} scale={1.8} happy={game.petCount > 0} />
      <Crate x={316} y={418} scale={.75} /><Bowl x={85} y={427} />
      {game.effects.map((effect, i) => <g key={`${effect.at}-${i}`} transform={`translate(${effect.x} ${effect.y - (game.clock - effect.at) * 38})`} opacity={1 - (game.clock - effect.at) / 1.2}><path d="M0 3C-20-8-11-21 0-12 11-21 20-8 0 3Z" fill="#d78484" /></g>)}
    </> : <>
      <Fence y={42} />
      <path d="M0 346Q100 329 195 345t195-1v236H0Z" fill="#e9e8d9" />
      <path d="M25 355q166-19 340 0" stroke="#b9c6aa" strokeWidth="3" strokeLinecap="round" fill="none" />
      {[392, 448, 503].map(y => <g key={y}><path d={`M57 ${y}h276`} stroke="#c7ccba" strokeWidth="1" strokeDasharray="4 9" /><Paw x={42} y={y} rotate={-30} opacity={.4} /></g>)}
      <g transform="translate(45 106)"><path d="M0 0h42v21H0Z" fill="#cedbc0" stroke="#a7b99d" /><path d="M5-4q17-24 32 0" fill="#afc398" /><Sprig x={21} y={-6} flower size={1.2} /></g>
      <g transform="translate(320 134)"><ellipse rx="30" ry="17" fill="#cbd7bc" /><path d="M-17 5q-9-31 9-20Q-3-39 8-15 30-26 20 6" fill="#a9bf98" stroke="#99af8b" /></g>
      <path d="m42 300 19-4 9 12-19 4Z" fill="#bdc1b1" stroke="#adb2a2" /><path d="m315 308 18-7 13 9-13 7Z" fill="#bfc4b2" />
      <Paw x={164} y={195} rotate={30} /><Paw x={196} y={179} rotate={30} /><Paw x={235} y={184} rotate={70} />
      {game.bait && <Bowl x={game.bait.x} y={game.bait.y + 25} />}
      {!game.animal.caught && <Animal x={game.animal.x} y={game.animal.y} kind={game.kind} happy={game.animal.eating > 0} />}
      {game.animal.eating > 0 && <g transform={`translate(${game.animal.x + 31} ${game.animal.y - 45})`}><path d="M0 2C-12-5-7-13 0-7 7-13 12-5 0 2Z" fill="#d78484" /></g>}
      {!game.net && !success && <g className="aim-guide" opacity=".7">
        <path d={`M${origin.x} ${origin.y}L${tip.x} ${tip.y}`} stroke={reachable ? '#789f89' : '#a9ad9b'} strokeWidth="1.5" strokeDasharray="4 7" />
        <ellipse cx={tip.x} cy={tip.y} rx="17" ry="11" fill="none" stroke={reachable ? '#789f89' : '#a9ad9b'} strokeWidth="1.5" />
        <path d={`M${origin.x - 12} ${origin.y - 14}Q${origin.x} ${origin.y - 27} ${origin.x + 12} ${origin.y - 14}`} fill="none" stroke="#a9b5a0" strokeWidth="1.5" />
      </g>}
      <Crate x={327} y={548} scale={.63} />
      <Rescuer x={p.x} y={p.y} moving={moving && !game.net} />
      {game.net && <g className="flying-net">
        <path d={`M${game.net.origin.x} ${game.net.origin.y}L${tip.x} ${tip.y}`} fill="none" stroke="#b49f82" strokeWidth="3" />
        {game.net.caught && <Animal x={tip.x} y={tip.y} kind={game.kind} scale={.75} />}
        <g transform={`translate(${tip.x} ${tip.y}) rotate(${game.net.angle * 180 / Math.PI})`}>
          <path d="M-29-8Q-25 40 0 40T29-8Z" fill="#e7f2e9" fillOpacity=".3" stroke="#829d92" strokeWidth="1.5" /><path d="M-29-8Q-25 40 0 40T29-8Z" fill="url(#net-mesh)" />
          <ellipse cy="-8" rx="29" ry="15" fill="none" stroke="#f5f7e8" strokeWidth="6" /><ellipse cy="-8" rx="29" ry="15" fill="none" stroke="#7f9c8d" strokeWidth="2" />
        </g>
      </g>}
      <text x="195" y="567" textAnchor="middle" fill="#929d88" fontSize="10">河畔花园 · 救助点</text>
    </>}
  </svg>;
}
