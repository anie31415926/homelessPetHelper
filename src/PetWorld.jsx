import React, { useEffect, useMemo, useRef, useState } from 'react';
import { Canvas, useFrame, useThree } from '@react-three/fiber';
import { RoundedBox } from '@react-three/drei';
import * as THREE from 'three';

const FUR = '#fff4df';
const COCOA = '#a18c80';
const GOLD = '#dca56c';
const DARK = '#34332f';
const eyeMaterial = { color: DARK, roughness: .15, metalness: .05 };
const silk = { roughness: .87, metalness: 0 };

function Ball({ position = [0, 0, 0], scale = 1, color = FUR, material = silk, ...props }) {
  return <mesh position={position} scale={scale} castShadow receiveShadow {...props}><sphereGeometry args={[1, 28, 20]} /><meshStandardMaterial color={color} {...material} /></mesh>;
}
function Box({ position = [0, 0, 0], args, color, radius = .06, ...props }) {
  return <RoundedBox position={position} args={args} radius={radius} smoothness={3} castShadow receiveShadow {...props}><meshStandardMaterial color={color} roughness={.85} /></RoundedBox>;
}
function Tube({ points, color = DARK, radius = .012, ...props }) {
  const curve = useMemo(() => new THREE.CatmullRomCurve3(points.map(point => new THREE.Vector3(...point))), [points]);
  return <mesh {...props}><tubeGeometry args={[curve, 24, radius, 6, false]} /><meshStandardMaterial color={color} roughness={.85} /></mesh>;
}
function Ring({ position, radius, tube, color, rotation = [Math.PI / 2, 0, 0], scale = 1 }) {
  return <mesh position={position} rotation={rotation} scale={scale} castShadow><torusGeometry args={[radius, tube, 10, 48]} /><meshStandardMaterial color={color} roughness={.78} /></mesh>;
}
function Ear({ side, cat }) {
  const shape = useMemo(() => {
    const s = new THREE.Shape();
    s.moveTo(-.25, -.12); s.quadraticCurveTo(-.3, -.02, -.21, .27);
    s.quadraticCurveTo(-.12, .64, -.04, .55); s.quadraticCurveTo(.19, .32, .24, .02);
    s.quadraticCurveTo(.27, -.16, -.25, -.12);
    return s;
  }, []);
  if (!cat) return <group position={[side * .64, 1.88, -.01]} rotation={[0, 0, side * .11]}>
    <Ball scale={[.3, .67, .26]} color="#ba7d4c" />
    <Ball position={[0, -.03, .2]} scale={[.22, .52, .1]} color="#ce9965" />
    {[-1, 0, 1].map(i => <Ball key={i} position={[i * .12, -.48 + Math.abs(i) * .05, .1]} scale={[.12, .23, .16]} color="#bf8250" />)}
  </group>;
  return <group position={[side * .48, 2.28, -.06]} rotation={[0, side * -.13, side * -.2]}>
    <mesh castShadow><extrudeGeometry args={[shape, { depth: .15, bevelEnabled: true, bevelSegments: 4, steps: 1, bevelSize: .07, bevelThickness: .08 }]} /><meshStandardMaterial color={COCOA} roughness={.95} /></mesh>
    <mesh position={[0, .07, .245]} scale={[.62, .67, .1]}><shapeGeometry args={[shape, 16]} /><meshStandardMaterial color="#e4b4ad" side={THREE.DoubleSide} /></mesh>
  </group>;
}
function Bow({ position, color = '#d38e98', scale = 1, rotation = [0, 0, 0] }) {
  return <group position={position} scale={scale} rotation={rotation}>
    <Ball position={[-.13, 0, 0]} scale={[.17, .13, .085]} color={color} rotation={[0, 0, .4]} />
    <Ball position={[.13, 0, 0]} scale={[.17, .13, .085]} color={color} rotation={[0, 0, -.4]} />
    <Ball scale={[.075, .09, .09]} color="#e9abb3" />
  </group>;
}

const MUD = [
  [-.46, 2.01, .51, .15, .11], [.43, 1.66, .59, .13, .09], [-.32, .3, .57, .13, .075],
  [.31, .55, .44, .16, .1], [.42, 2.16, .44, .08, .06], [-.18, 1.1, .44, .1, .07],
  [.12, .8, .48, .07, .05], [-.52, 1.7, .41, .07, .06],
];
function MudSpot({ spot, index, kind }) {
  const geometry = useMemo(() => {
    const [cx, cy, , sx, sy] = spot;
    const surfaces = [
      [0, 1.93, 0, .77, .66, .6], [0, .76, -.05, .57, .7, .45],
      [0, .83, .335, .37, .48, .135], [-.28, .46, .29, .19, .42, .2], [.28, .46, .29, .19, .42, .2],
      [-.28, .16, .41, .24, .16, .29], [.28, .16, .41, .24, .16, .29],
      ...(kind === 'cat' ? [[-.37, 2.01, .35, .285, .38, .21], [.37, 2.01, .35, .285, .38, .21]] : [[0, 1.99, .48, .27, .53, .17]]),
    ];
    const surfaceZ = (x, y) => Math.max(...surfaces.map(([ox, oy, oz, ax, ay, az]) => {
      const q = 1 - ((x - ox) / ax) ** 2 - ((y - oy) / ay) ** 2;
      return q > 0 ? oz + az * Math.sqrt(q) : -5;
    }));
    // Bend each irregular mud patch over the model instead of floating it in front.
    const vertices = [cx, cy, surfaceZ(cx, cy) + .012]; const indices = [];
    const segments = 32; const rings = 4;
    for (let ring = 1; ring <= rings; ring++) {
      for (let i = 0; i <= segments; i++) {
        const angle = i / segments * Math.PI * 2;
        const r = ring / rings * (1 + Math.sin(angle * 3 + index) * .15 + Math.cos(angle * 5) * .1);
        const x = cx + Math.cos(angle) * sx * r; const y = cy + Math.sin(angle) * sy * r;
        vertices.push(x, y, surfaceZ(x, y) + .012);
        if (i < segments) {
          const current = 1 + (ring - 1) * (segments + 1) + i;
          if (ring === 1) indices.push(0, current, current + 1);
          else { const previous = current - segments - 1; indices.push(previous, current, current + 1, previous, current + 1, previous + 1); }
        }
      }
    }
    const g = new THREE.BufferGeometry(); g.setAttribute('position', new THREE.Float32BufferAttribute(vertices, 3)); g.setIndex(indices); g.computeVertexNormals();
    return g;
  }, [spot, index, kind]);
  useEffect(() => () => geometry.dispose(), [geometry]);
  return <mesh geometry={geometry}><meshStandardMaterial color="#8f7153" roughness={1} side={THREE.DoubleSide} polygonOffset polygonOffsetFactor={-1} /></mesh>;
}
function Mud({ dirty, cleanedSpots, kind }) {
  if (dirty < .1) return null;
  return <group name="mud">{MUD.map((spot, index) => index >= cleanedSpots && <MudSpot key={index} spot={spot} index={index} kind={kind} />)}</group>;
}
function Tufts({ kind, messy }) {
  const tufts = [[-.52, 1.62, .01], [.54, 1.64, .01], [-.48, .88, .08], [.48, .92, .06], [-.14, 2.41, .11], [.13, 2.42, .08]];
  return <group name="fur-tufts">{tufts.map(([x, y, z], i) => <Ball key={i} position={[x, y, z]} rotation={[0, 0, (x < 0 ? -.75 : .75) + (messy ? .4 : 0)]} scale={messy ? [.13, .29, .15] : [.12, .15, .12]} color={kind === 'cat' ? FUR : GOLD} />)}</group>;
}

function PetModel({ kind, stage, dirty, cleanedSpots, messy, accessory, happy, session, burst, hitRef, animate }) {
  const root = useRef(); const body = useRef(); const head = useRef(); const tail = useRef(); const eyes = useRef(); const lastBurst = useRef(null);
  const reaction = useRef(0); const clock = useRef(0); const cat = kind === 'cat';
  const size = [.84, .94, 1.02][stage];
  useEffect(() => { if (burst && burst !== lastBurst.current) { reaction.current = 1; lastBurst.current = burst; } }, [burst]);
  useFrame((_, dt) => {
    const delta = Math.min(dt, .1); clock.current += delta;
    const t = clock.current;
    if (!root.current) return;
    reaction.current = Math.max(0, reaction.current - delta * 1.8);
    const r = animate ? reaction.current : 0;
    root.current.rotation.y = -.16 + (animate ? Math.sin(t * .7) * .045 : 0);
    root.current.position.y = animate ? Math.sin(r * Math.PI) * .12 : 0;
    body.current.scale.y = animate ? 1 + Math.sin(t * 2) * .012 : 1;
    head.current.rotation.z = animate ? Math.sin(t * .8) * .035 + Math.sin(r * Math.PI) * .05 : 0;
    head.current.rotation.x = burst === 'feed' && r > 0 ? .13 + Math.sin(t * 14) * .04 : 0;
    tail.current.rotation.z = animate ? Math.sin(t * (happy ? 9 : 3)) * (happy ? .25 : .12) : 0;
    eyes.current.scale.y = animate && Math.sin(t * .9) > .997 ? .12 : 1;
  });
  const handleClick = event => { event.stopPropagation(); if (Date.now() - hitRef.current.last > 100) { hitRef.current.last = Date.now(); hitRef.current.hit(); } };
  const handleMove = event => { if (session && event.buttons === 1 && Date.now() - hitRef.current.last > 190) { event.stopPropagation(); hitRef.current.last = Date.now(); hitRef.current.hit(); } };
  return <group ref={root} name="pet" scale={size} onClick={handleClick} onPointerMove={handleMove} onPointerOver={() => { document.body.style.cursor = 'pointer'; }} onPointerOut={() => { document.body.style.cursor = ''; }}>
    <group ref={body}>
      <group ref={tail} position={[.45, .41, -.18]}>
        <Tube points={cat ? [[0, 0, 0], [.36, .05, -.1], [.65, .33, -.08], [.68, .61, .02]] : [[0, 0, 0], [.35, .17, -.08], [.46, .4, -.02]]} radius={cat ? .16 : .14} color={cat ? COCOA : '#bd824f'} castShadow />
        <Ball position={cat ? [.68, .61, .02] : [.46, .4, -.02]} scale={cat ? .158 : .14} color={cat ? COCOA : '#bd824f'} />
      </group>
      <Ball position={[0, .76, -.05]} scale={[.57, .7, .45]} color={cat ? FUR : GOLD} />
      <Ball position={[0, .83, .335]} scale={[.37, .48, .135]} color={cat ? '#fff9ec' : '#ffebcb'} />
      {[-1, 1].map(side => <group key={side}>
        <Ball position={[side * .43, .36, -.02]} scale={[.3, .34, .35]} color={cat ? FUR : GOLD} />
        <Ball position={[side * .28, .46, .29]} scale={[.19, .42, .2]} color={cat ? FUR : GOLD} />
        <Ball position={[side * .28, .16, .41]} scale={[.24, .16, .29]} color={cat ? FUR : '#ffe6bf'} />
        {[-1, 1].map(i => <Tube key={i} points={[[side * .28 + i * .054, .15, .678], [side * .28 + i * .054, .21, .663], [side * .28 + i * .054, .25, .63]]} color="#c8b7a2" radius={.006} />)}
      </group>)}
      <Ring position={[0, 1.27, .02]} radius={.35} tube={.062} color={accessory === 'scarf' ? '#b4a5c9' : '#8daa93'} scale={[1.16, 1, 1]} />
      <Ball position={[0, 1.2, .41]} scale={[.09, .1, .035]} color="#e9c669" material={{ roughness: .4, metalness: .28 }} />
      <Tube points={[[-.025, 1.2, .451], [.025, 1.2, .451]]} color="#b89547" radius={.007} />
      {accessory === 'scarf' && <Box position={[.24, 1.07, .4]} args={[.2, .34, .065]} rotation={[0, -.1, .25]} color="#b4a5c9" radius={.03} />}
    </group>
    <group ref={head}>
      <Ear side={-1} cat={cat} /><Ear side={1} cat={cat} />
      <Ball position={[0, 1.93, 0]} scale={[.77, .66, .6]} color={cat ? FUR : GOLD} />
      {cat ? [-1, 1].map(side => <Ball key={side} position={[side * .37, 2.01, .35]} rotation={[0, side * .32, side * -.2]} scale={[.285, .38, .21]} color={COCOA} />) : <Ball position={[0, 1.99, .48]} scale={[.27, .53, .17]} color="#ffefd4" />}
      <Tufts kind={kind} messy={messy} />
      <group ref={eyes} position={[0, 1.96, .545]}>
        {[-1, 1].map(side => <group key={side} position={[side * .285, 0, 0]} rotation={[0, side * .13, 0]}>
          <Ball scale={[.153, .185, .09]} color={cat ? '#a0c8cd' : '#b28b5d'} material={{ roughness: .36 }} />
          <Ball position={[0, .005, .061]} scale={[.105, .146, .053]} material={eyeMaterial} />
          <Ball position={[-.036, .068, .106]} scale={[.037, .045, .016]} color="white" material={{ roughness: .1 }} />
          <Ball position={[.035, -.046, .107]} scale={[.018, .021, .01]} color="#fff9ed" />
        </group>)}
      </group>
      {[-1, 1].map(side => <group key={side}>
        <Ball position={[side * .145, 1.69, .54]} scale={[.255, .18, .145]} color={cat ? '#fff8e8' : '#fff0d5'} />
        <Ball position={[side * .48, 1.73, .46]} rotation={[0, side * .4, 0]} scale={[.13, .058, .027]} color="#e5b0a8" />
        {cat && [0, 1].map(index => <Tube key={index} points={[[side * .35, 1.69 - index * .06, .61], [side * .57, 1.69 - index * .09, .62], [side * .81, 1.73 - index * .13, .57]]} color="#a99583" radius={.006} />)}
      </group>)}
      <Ball position={[0, 1.77, .701]} scale={cat ? [.095, .062, .045] : [.115, .079, .067]} color={cat ? '#c68e8d' : '#57473e'} material={{ roughness: .5 }} />
      <Tube points={[[0, 1.73, .696], [0, 1.64, .698], [-.08, 1.6, .682], [-.13, 1.64, .666]]} color="#796759" radius={.01} />
      <Tube points={[[0, 1.64, .698], [.07, 1.6, .682], [.13, 1.64, .666]]} color="#796759" radius={.01} />
      {happy && <Ball position={[.01, 1.575, .68]} scale={[.045, .069, .018]} color="#e8a0a4" />}
      {accessory === 'bow' && <Bow position={[.58, 2.37, .34]} rotation={[0, 0, -.22]} />}
    </group>
    <Mud dirty={dirty} cleanedSpots={cleanedSpots} kind={kind} />
    {session?.type === 'bath' && session.step < 2 && <Foam session={session} />}
  </group>;
}

function Foam({ session }) {
  const amount = session.step === 0 ? session.hits * 3 + 1 : (5 - session.hits) * 3;
  return <group name="soap-bubbles">{Array.from({ length: Math.min(16, amount) }, (_, i) => {
    const angle = i * 2.4;
    return <Ball key={i} position={[Math.sin(angle) * (.3 + i % 3 * .12), .55 + i % 6 * .24, .38 + Math.cos(angle) * .15]} scale={.09 + i % 3 * .036} color="#f4fffa" material={{ roughness: .2, transparent: true, opacity: .87 }} />;
  })}</group>;
}

function Plant({ position, scale = 1, color = '#ccb2c5' }) {
  return <group position={position} scale={scale}>
    <mesh position={[0, .24, 0]} castShadow><cylinderGeometry args={[.26, .19, .45, 24]} /><meshStandardMaterial color={color} roughness={.9} /></mesh>
    <Ring position={[0, .45, 0]} radius={.245} tube={.035} color={color} />
    <Ball position={[0, .455, 0]} scale={[.21, .025, .21]} color="#8b765d" />
    {[-1, 0, 1].map((i, index) => <group key={i}><Tube points={[[0, .4, 0], [i * .1, .8, 0], [i * .16, 1.05 + index * .12, 0]]} color="#809c76" radius={.018} />
      <Ball position={[i * .19 + .06, 1.05 + index * .11, 0]} rotation={[.2, 0, -.6 + index * .55]} scale={[.12, .26, .055]} color={index % 2 ? '#94b18c' : '#769a78'} />
    </group>)}
  </group>;
}
function Room({ location }) {
  const bath = location === 'bathroom'; const clinic = location === 'clinic';
  const wall = bath ? '#d7e8e1' : clinic ? '#e4e5ed' : '#d6e4c9';
  return <group name={location}>
    <Box position={[0, -.13, 0]} args={[12, .22, 12]} radius={.03} color={bath ? '#bdcfc4' : '#dfc5a1'} />
    <Box position={[0, 2.4, -2.45]} args={[12, 5.2, .2]} radius={.025} color={wall} />
    <Box position={[-3.22, 2.4, -.5]} args={[.18, 5.2, 4.1]} radius={.02} color={wall} />
    <Box position={[0, .1, -2.29]} args={[7, .25, .13]} radius={.025} color={bath ? '#b3c9bc' : '#b7c5a4'} />
    {bath ? <>
      {Array.from({ length: 7 }, (_, i) => <Box key={i} position={[-3 + i, 2.4, -2.333]} args={[.016, 5, .016]} radius={.006} color="#b7d1c3" />)}
      {Array.from({ length: 6 }, (_, i) => <Box key={i} position={[0, i * .8, -2.328]} args={[7, .016, .018]} radius={.006} color="#b7d1c3" />)}
      <Bathtub />
      <Tube points={[[1.72, .2, -1.3], [1.72, 2.7, -1.3], [1.5, 3.05, -1.3], [1.02, 3.05, -1.3], [.95, 2.88, -1.3]]} radius={.045} color="#9baea4" />
      <Ball position={[.95, 2.85, -1.3]} scale={[.22, .07, .22]} color="#a9bdb0" material={{ roughness: .4, metalness: .3 }} />
      <Box position={[-1.65, 1.6, -2.1]} args={[1.15, .12, .5]} color="#d6bca0" />
      <Bottle position={[-1.9, 1.7, -2]} color="#dea4a3" /><Bottle position={[-1.42, 1.7, -2]} color="#b9a9d0" />
      <Box position={[2, 1.25, -1.9]} args={[.75, .12, .3]} color="#c9b3a2" /><Box position={[2, 1, -1.76]} args={[.6, .5, .075]} color="#edc2b7" radius={.03} />
    </> : clinic ? <>
      <Box position={[0, .14, -.1]} args={[2.45, .2, 1.65]} color="#c1b4d0" radius={.09} />
      <Box position={[0, .25, -.1]} args={[2.22, .09, 1.47]} color="#e1d6e7" radius={.04} />
      <Box position={[-1.8, 1, -1.9]} args={[1.3, 1.85, .65]} color="#f3f0df" />
      <Box position={[-1.8, 1.1, -1.55]} args={[1.1, .035, .03]} color="#bfd0be" radius={.01} />
      <Box position={[-1.79, 1.05, -1.54]} args={[.035, 1.55, .03]} color="#bfd0be" radius={.01} />
      <Box position={[-1.55, 1.2, -1.5]} args={[.04, .18, .04]} color="#97ac9c" radius={.01} />
      <Box position={[-2.03, 1.2, -1.5]} args={[.04, .18, .04]} color="#97ac9c" radius={.01} />
      <Box position={[1.7, .43, -1.4]} args={[1, .72, .7]} color="#e1adaf" />
      <Box position={[1.7, .45, -.99]} args={[.45, .12, .07]} color="#fff4df" radius={.02} /><Box position={[1.7, .45, -.99]} args={[.12, .45, .07]} color="#fff4df" radius={.02} />
      <Box position={[.3, 2.7, -2.29]} args={[1.1, .75, .1]} color="#f5f2e6" />
      <Box position={[.3, 2.7, -2.21]} args={[.4, .12, .04]} color="#a0b6a5" radius={.015} /><Box position={[.3, 2.7, -2.2]} args={[.12, .4, .04]} color="#a0b6a5" radius={.015} />
      <Plant position={[2.4, 0, -1.1]} scale={.7} />
    </> : <>
      {Array.from({ length: 9 }, (_, i) => <Box key={i} position={[-4 + i, -.012, 0]} args={[.012, .008, 10]} radius={.002} color="#cbb18d" />)}
      <mesh rotation={[-Math.PI / 2, 0, 0]} position={[0, .012, .05]} receiveShadow><circleGeometry args={[1.65, 64]} /><meshStandardMaterial color="#e9dfba" roughness={1} /></mesh>
      <Ring position={[0, .022, .05]} radius={1.52} tube={.013} color="#cabb98" />
      <Ring position={[0, .022, .05]} radius={1.61} tube={.011} color="#f6eaca" />
      <Window />
      <Sofa />
      <Plant position={[1.92, 0, -.94]} scale={.9} />
      <Box position={[-2.15, 2.48, -2.04]} args={[1.45, .13, .58]} color="#d7b48d" radius={.035} />
      {['#bbaace', '#e4b78e', '#8fa98b'].map((color, i) => <Box key={color} position={[-2.63 + i * .19, 2.81, -2.06]} args={[.14, .52 - i * .04, .32]} color={color} radius={.015} rotation={[0, 0, i === 2 ? -.15 : 0]} />)}
      <Plant position={[-1.72, 2.55, -2.07]} scale={.3} color="#e0b3aa" />
      <Box position={[2.18, 2.29, -2.28]} args={[.73, .93, .12]} color="#fff4df" radius={.025} />
      <Box position={[2.18, 2.29, -2.2]} args={[.59, .78, .03]} color="#e4c3b8" radius={.01} />
      <Ball position={[2.18, 2.23, -2.16]} scale={[.13, .12, .025]} color="#fbf2db" />
      {[-1, 0, 1].map(i => <Ball key={i} position={[2.18 + i * .12, 2.43 - Math.abs(i) * .06, -2.16]} scale={[.055, .07, .025]} color="#fbf2db" />)}
      <FoodBowl position={[-1.42, .02, .74]} small />
      <Yarn position={[1.35, .17, .9]} scale={.75} />
    </>}
  </group>;
}
function Window() {
  return <group position={[-.05, 2.38, -2.23]}>
    <Box args={[1.92, 2.12, .16]} color="#f9f1db" radius={.05} />
    <Box position={[0, 0, .1]} args={[1.67, 1.86, .06]} color="#b4d7d8" radius={.02} />
    <Ball position={[.4, .49, .17]} scale={[.23, .23, .02]} color="#f8d88a" material={{ roughness: 1, emissive: '#edcf86', emissiveIntensity: .3 }} />
    <Ball position={[-.3, -.78, .18]} scale={[.63, .31, .025]} color="#97b98b" /><Ball position={[.5, -.75, .19]} scale={[.6, .4, .025]} color="#a8c599" />
    <Ball position={[-.4, .4, .17]} scale={[.22, .09, .02]} color="#f2f5e9" /><Ball position={[-.22, .37, .17]} scale={[.17, .07, .02]} color="#f2f5e9" />
    <Box position={[0, 0, .23]} args={[.065, 1.86, .075]} color="#fff4dd" radius={.015} />
    <Box position={[0, 0, .23]} args={[1.67, .065, .075]} color="#fff4dd" radius={.015} />
    <Box position={[0, -1.08, .1]} args={[2.14, .12, .39]} color="#ead4b2" radius={.03} />
    {[-1, 1].map(side => <group key={side} position={[side * 1.02, .02, .12]}>
      {[-1, 0, 1].map(i => <Ball key={i} position={[i * .1, 0, 0]} scale={[.105, 1.06, .09]} color={i % 2 ? '#e6b5ad' : '#e2aaa4'} />)}
      <Ring position={[0, -.45, 0]} radius={.12} tube={.025} color="#b9957a" scale={[1.1, 1, 1]} />
    </group>)}
    <Tube points={[[-1.25, 1.15, .12], [0, 1.15, .12], [1.25, 1.15, .12]]} color="#a69a7e" radius={.035} />
  </group>;
}
function Sofa() {
  return <group position={[-1.74, 0, -.95]} rotation={[0, .15, 0]}>
    {[-1, 1].map(x => <Box key={x} position={[x * .56, .13, 0]} args={[.11, .27, .52]} color="#bba17d" radius={.025} />)}
    <Box position={[0, .44, 0]} args={[1.56, .47, .83]} color="#dba7a0" radius={.17} />
    <Box position={[0, .87, -.28]} args={[1.56, .77, .3]} color="#e4b1a7" radius={.12} />
    <Box position={[0, .65, .11]} args={[1.13, .18, .66]} color="#edc3b3" radius={.075} />
    {[-1, 1].map(x => <Box key={x} position={[x * .7, .67, .02]} args={[.22, .59, .85]} color="#dfa89e" radius={.095} />)}
    <Box position={[-.32, .87, -.09]} args={[.43, .38, .16]} color="#eee2b7" radius={.08} rotation={[.08, 0, -.19]} />
  </group>;
}
function Bottle({ position, color }) {
  return <group position={position}><Box position={[0, .22, 0]} args={[.26, .42, .2]} color={color} radius={.05} /><Box position={[0, .47, 0]} args={[.14, .09, .14]} color="#a9b8a0" radius={.02} /><Box position={[0, .23, .105]} args={[.12, .13, .012]} color="#fff2dc" radius={.01} /></group>;
}
function Bathtub() {
  return <group position={[0, .02, .02]}>
    <Ball position={[0, .31, 0]} scale={[1.3, .43, .89]} color="#98bcb0" />
    <Ring position={[0, .56, 0]} radius={1} tube={.075} scale={[1.24, .83, 1]} color="#dcece0" />
    <mesh position={[0, .53, 0]} rotation={[-Math.PI / 2, 0, 0]} scale={[1.16, .76, 1]}><circleGeometry args={[1, 48]} /><meshStandardMaterial color="#aed5cc" roughness={.25} /></mesh>
    {[-1, 1].map(i => <Ball key={i} position={[i * .83, .06, .1]} scale={[.11, .09, .32]} color="#73998a" />)}
    <Ball position={[-1.13, .68, .05]} scale={.13} color="#f4fff7" /><Ball position={[1.03, .65, .26]} scale={.17} color="#f4fff7" />
  </group>;
}
function FoodBowl({ position, small = false, meal = 'kibble' }) {
  const color = meal === 'fish' ? '#9cbeb4' : meal === 'chicken' ? '#dea5a0' : '#debd7d';
  return <group position={position} scale={small ? .7 : 1}>
    <mesh position={[0, .1, 0]} castShadow><cylinderGeometry args={[.32, .26, .19, 32]} /><meshStandardMaterial color={color} roughness={.7} /></mesh>
    <Ring position={[0, .2, 0]} radius={.295} tube={.035} color="#f5ead3" />
    <mesh position={[0, .195, 0]} rotation={[-Math.PI / 2, 0, 0]}><circleGeometry args={[.27, 32]} /><meshStandardMaterial color="#aa8059" /></mesh>
    {Array.from({ length: 7 }, (_, i) => <Ball key={i} position={[Math.sin(i * 2.4) * .18, .22, Math.cos(i * 2.4) * .17]} scale={[.065, .04, .05]} color={meal === 'fish' ? '#d6d5bc' : '#d3a269'} />)}
  </group>;
}
function Yarn({ position, scale = 1, ...props }) {
  return <group position={position} scale={scale} name="yarn" {...props}>
    <Ball scale={.22} color="#b6a0c6" />
    {[0, 1, 2, 3].map(i => <Ring key={i} radius={.221} tube={.009} position={[0, 0, 0]} rotation={[i * .9, i * .5, i * .2]} color="#d5c2df" />)}
    <Tube points={[[.18, -.13, 0], [.32, -.2, .08], [.5, -.19, .05], [.56, -.2, .18]]} radius={.012} color="#aa92bc" />
  </group>;
}

function CareTool({ session, animate }) {
  const group = useRef(); const elapsed = useRef(0);
  useFrame((_, dt) => { elapsed.current += Math.min(dt, .1); if (group.current && animate) { group.current.position.y = 1.6 + Math.sin(elapsed.current * 7) * .08; group.current.rotation.z = -.25 + Math.sin(elapsed.current * 5) * .09; } });
  if (!session || session.type === 'play') return null;
  const bath = session.type === 'bath'; const groom = session.type === 'groom';
  return <group ref={group} position={[.85, 1.6, .75]} rotation={[0, -.4, -.25]} scale={.85} name="care-tool">
    {bath ? session.step === 0 ? <>
      <Box args={[.4, .22, .18]} color="#e9cf79" radius={.07} />
      {[0, 1, 2].map(i => <Ball key={i} position={[i * .12 - .13, .14, .04]} scale={.09} color="#f4fff7" />)}
    </> : session.step === 1 ? <>
      <Tube points={[[0, -.4, 0], [0, .1, 0], [-.13, .24, 0], [-.3, .2, 0]]} radius={.06} color="#aabfb4" />
      <Ball position={[-.32, .17, 0]} scale={[.14, .055, .14]} color="#bccdc1" />
      {[0, 1, 2, 3].map(i => <Tube key={i} points={[[-.42 + i * .065, .1, 0], [-.48 + i * .07, -.15, 0], [-.55 + i * .08, -.43, 0]]} radius={.013} color="#9dccd1" />)}
    </> : <>
      <Box position={[.05, -.18, 0]} args={[.15, .35, .18]} color="#d999a0" radius={.05} />
      <Box args={[.47, .27, .27]} color="#e9b4b5" radius={.09} />
      <Ring position={[-.245, 0, 0]} radius={.09} tube={.027} color="#be8e97" rotation={[0, Math.PI / 2, 0]} />
    </> : groom ? <>
      <Box position={[0, -.24, 0]} args={[.09, .34, .13]} color="#b69174" radius={.035} />
      <Box args={[.33, .38, .14]} color="#d9b38b" radius={.09} />
      {Array.from({ length: 6 }, (_, i) => <Box key={i} position={[-.12 + i % 3 * .12, -.08 + Math.floor(i / 3) * .16, -.1]} args={[.028, .03, .15]} color="#967b67" radius={.01} />)}
    </> : session.step === 0 ? <>
      <Box args={[.1, .55, .1]} color="#f7efd9" radius={.04} /><Ball position={[0, -.25, .03]} scale={[.08, .12, .06]} color="#dca7aa" />
      <Box position={[0, .06, .06]} args={[.045, .18, .012]} color="#9cb9b2" radius={.01} />
    </> : session.step === 1 ? <>
      <Ring position={[0, .07, 0]} radius={.17} tube={.026} color="#8fa9a0" rotation={[0, 0, 0]} />
      <Tube points={[[0, -.1, 0], [0, -.3, 0], [-.18, -.38, 0], [-.3, -.25, 0]]} radius={.023} color="#8d91a3" />
      <Ball position={[-.3, -.25, .02]} scale={[.08, .08, .04]} color="#b8c2c4" />
    </> : <>
      <Box args={[.22, .37, .2]} color="#b3bd91" radius={.05} /><Box position={[0, .23, 0]} args={[.16, .1, .15]} color="#f5ebd5" radius={.025} /><Box position={[0, 0, .11]} args={[.13, .15, .01]} color="#f8f2df" radius={.01} />
    </>}
  </group>;
}

function CameraRig({ turn, reset, animate }) {
  const { camera, size, invalidate } = useThree();
  const target = useMemo(() => new THREE.Vector3(0, 1.72, 0), []);
  const angle = useRef(.16);
  useEffect(() => { invalidate(); }, [turn, reset, invalidate]);
  useFrame((_, delta) => {
    const desired = .16 + turn * .38;
    angle.current = animate ? THREE.MathUtils.damp(angle.current, desired, 5, Math.min(delta, .1)) : desired;
    const a = angle.current;
    camera.position.set(Math.sin(a) * 8, 3.5, Math.cos(a) * 8);
    camera.lookAt(target);
    const nextZoom = Math.min(size.width / 4.85, size.height / 4.2);
    if (camera.zoom !== nextZoom) { camera.zoom = nextZoom; camera.updateProjectionMatrix(); }
  });
  return null;
}

function RenderPacer({ active, reduced }) {
  const { invalidate } = useThree();
  useEffect(() => {
    if (!active || reduced) { invalidate(); return; }
    const id = setInterval(() => { if (!document.hidden) invalidate(); }, 1000 / 30);
    return () => clearInterval(id);
  }, [active, reduced, invalidate]);
  return null;
}
function SceneTelemetry({ kind, name, stage, location, dirty, cleanedSpots, messy, session }) {
  const { gl, scene } = useThree();
  useEffect(() => {
    Object.assign(gl.domElement.dataset, { kind, stage, room: location, mud: dirty < .1 ? 0 : Math.max(0, 8 - cleanedSpots), messy: String(messy), care: session?.type || 'none', step: session?.step ?? -1 });
    gl.domElement.setAttribute('aria-label', `${name}的3D小窝`);
  }, [gl, kind, name, stage, location, dirty, cleanedSpots, messy, session]);
  useFrame(() => {
    gl.domElement.dataset.frames = String(Number(gl.domElement.dataset.frames || 0) + 1);
    gl.domElement.dataset.meshes = String(scene.children.length);
  });
  return null;
}

export default function PetWorld({ kind, name, stage, dirty, cleanedSpots, messy, accessory, happy, session, burst, meal, onHit, turn, reset, onFailure }) {
  const host = useRef(); const hitRef = useRef({ hit: onHit, last: 0 });
  hitRef.current.hit = onHit;
  const [active, setActive] = useState(true);
  const [reduced, setReduced] = useState(() => matchMedia('(prefers-reduced-motion: reduce)').matches);
  const location = session?.type === 'bath' ? 'bathroom' : session?.type === 'doctor' ? 'clinic' : 'room';
  useEffect(() => {
    const media = matchMedia('(prefers-reduced-motion: reduce)');
    const listener = () => setReduced(media.matches);
    media.addEventListener('change', listener);
    const observer = new IntersectionObserver(([entry]) => setActive(entry.isIntersecting));
    observer.observe(host.current);
    return () => { media.removeEventListener('change', listener); observer.disconnect(); document.body.style.cursor = ''; };
  }, []);
  return <div className="pet-world" ref={host}>
    <Canvas orthographic shadows frameloop="demand" dpr={[1, 1.5]} camera={{ position: [1.2, 3.5, 8], near: .1, far: 40, zoom: 80 }} gl={{ antialias: true, alpha: false, powerPreference: 'high-performance', preserveDrawingBuffer: true }} fallback={<div className="webgl-fallback"><p>当前设备暂时无法打开立体小窝</p><button onClick={onFailure}>回到绘本小窝</button></div>} onCreated={({ gl }) => { gl.setClearColor('#d6e4c9'); gl.toneMapping = THREE.ACESFilmicToneMapping; gl.toneMappingExposure = 1; gl.domElement.setAttribute('aria-label', `${name}的3D小窝`); gl.domElement.setAttribute('role', 'img'); }}>
      <RenderPacer active={active} reduced={reduced} />
      <CameraRig turn={turn} reset={reset} animate={!reduced} />
      <ambientLight intensity={.65} color="#fff9ed" />
      <hemisphereLight args={['#fff5e5', '#a7b5a0', 1]} />
      <directionalLight position={[-3, 7, 5]} intensity={2.5} color="#fff0d3" castShadow shadow-mapSize={[1024, 1024]} shadow-camera-left={-5} shadow-camera-right={5} shadow-camera-top={6} shadow-camera-bottom={-4} shadow-normalBias={.035} shadow-bias={-.0001} shadow-radius={4} />
      <directionalLight position={[4, 3, -1]} intensity={.8} color="#d3eceb" />
      <Room location={location} />
      <group position={[0, location === 'clinic' ? .29 : location === 'bathroom' ? .2 : .02, .12]}>
        <PetModel kind={kind} stage={stage} dirty={dirty} cleanedSpots={cleanedSpots} messy={messy} accessory={accessory} happy={happy} session={session} burst={burst} hitRef={hitRef} animate={!reduced && active} />
        <CareTool session={session} animate={!reduced && active} />
      </group>
      {burst === 'feed' && <FoodBowl position={[.6, .03, 1.15]} meal={meal} />}
      {session?.type === 'play' && <Yarn position={[[1.2, .24, .7], [-1.15, .24, .8], [1.35, .24, .2], [-1.1, .24, 1.1], [1.1, .24, 1.1]][session.hits]} onClick={event => { event.stopPropagation(); hitRef.current.hit(); }} />}
      <SceneTelemetry kind={kind} name={name} stage={stage} location={location} dirty={dirty} cleanedSpots={cleanedSpots} messy={messy} session={session} />
    </Canvas>
  </div>;
}
