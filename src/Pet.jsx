import React, { useId } from 'react';

export function Pet({ kind = 'cat', dirty = 0, cleanedSpots = 0, messy = false, accessory = 'none', happy = false, className = '' }) {
  const textureId = useId();
  const cat = kind === 'cat';
  const fur = cat ? '#fff9ec' : '#e9bb7f';
  const edge = cat ? '#837565' : '#99714f';
  return <svg className={`pet-art ${className} ${happy ? 'pet-happy' : ''}`} viewBox="0 0 300 320" fill="none" xmlns="http://www.w3.org/2000/svg" aria-hidden="true">
    <defs>
      <pattern id={textureId} width="13" height="15" patternUnits="userSpaceOnUse"><path d="M2 8l2-2m5 6 1-2" stroke={edge} opacity=".055" strokeWidth="1.3" strokeLinecap="round" /></pattern>
    </defs>
    <ellipse cx="150" cy="292" rx="88" ry="12" fill="#63735a" opacity=".12" />
    <g className="pet-body" stroke={edge} strokeWidth="2.8" strokeLinecap="round" strokeLinejoin="round">
      <path className="pet-tail" d={cat ? 'M212 262c52 3 59-45 39-49-21-4-10 31-43 21' : 'M211 260c37 6 65-16 53-32-13-16-29 22-53 8'} fill={cat ? '#b3a18c' : '#d3a16d'} />
      <path d="M106 183c-19 22-28 68-14 94 13 24 99 27 119 3 18-23 2-80-17-98" fill={fur} />
      <path d="M133 208c-20 19-24 49-15 64 12 14 49 15 64-1 9-19-1-48-13-61" fill={cat ? '#fffef6' : '#fff0d6'} stroke="none" />
      <path d="M105 248c-18 2-26 30-10 37 11 5 26 3 32-3l4-31m41 0 3 29c8 12 41 7 41-3 1-14-14-29-23-29" fill={fur} />
      <path d="M102 278v6m8-5v6m78-5v6m8-7v6" opacity=".55" />
      {cat ? <>
        <path d="M73 123c-10-25-13-64-3-70 9-6 39 20 52 32m58 0c17-21 47-39 54-31 8 9-1 55-11 74" fill="#c1ae97" />
        <path d="m76 67 4 39 26-16m92 1 27-23-7 41" fill="#e3c1b6" stroke="none" />
      </> : <>
        <path d="M90 87C47 68 37 118 46 159c5 24 29 26 41 5l22-56m87-18c43-22 58 23 51 65-4 30-27 31-43 9l-20-50" fill="#bf885b" />
        <path d="M59 118c-5 22 1 40 8 42m165-43c4 20 2 34-3 42" opacity=".35" />
      </>}
      <path d="M75 122c5-34 35-52 75-52 42 0 75 22 79 58 1 8 1 15-2 23l8 8-12 3 9 12-15 1c-10 29-39 44-67 44-30 0-61-15-73-42l-12-2 9-10-12-5 10-9c-2-9-1-19 3-29Z" fill={fur} />
      {cat ? <>
        <path d="M78 123c8-28 27-41 46-44-9 16-8 32-1 45 7 15 7 30-6 37-17 9-35 0-42-15m148-23c-8-25-27-40-46-44 9 16 8 32 1 45-7 15-7 30 6 37 17 9 35 0 42-15" fill="#b6a38e" stroke="none" />
        <path d="m133 79 5 21m13-24 1 19m13-15-4 18" stroke="#e6d4bd" strokeWidth="5" />
      </> : <path d="M132 74c-4 19-7 31-2 48 3 8 0 15-10 22-12 11-9 34 7 42 17 7 43 6 53-8 10-13 3-24-7-35-10-10-4-19-7-32l-8-38" fill="#fff2d9" stroke="none" />}
      <g className="pet-eyes">
        <ellipse cx="112" cy="144" rx="9" ry="12" fill="#4a4740" stroke="none" />
        <ellipse cx="188" cy="144" rx="9" ry="12" fill="#4a4740" stroke="none" />
        <ellipse cx="109" cy="140" rx="3" ry="4" fill="white" stroke="none" />
        <ellipse cx="185" cy="140" rx="3" ry="4" fill="white" stroke="none" />
        <circle cx="115" cy="149" r="1.5" fill="#d4bc8b" stroke="none" /><circle cx="191" cy="149" r="1.5" fill="#d4bc8b" stroke="none" />
      </g>
      <path d="m103 124 13-2m67 0 13 3" strokeWidth="2" opacity=".5" />
      <ellipse cx="94" cy="164" rx="13" ry="7" fill="#eeb6a9" opacity=".7" stroke="none" />
      <ellipse cx="206" cy="164" rx="13" ry="7" fill="#eeb6a9" opacity=".7" stroke="none" />
      <path d="M141 161c3-5 15-5 18 0 1 3-5 8-9 8s-11-5-9-8Z" fill={cat ? '#c38e83' : '#67594e'} strokeWidth="1.5" />
      <path d="M150 170v5m-11 1c2 7 9 5 11 0 3 5 10 7 13 0" strokeWidth="2" />
      {happy && <path d="M145 181q5 17 12 0" fill="#e7a4a0" strokeWidth="1.5" />}
      {cat && <path d="m80 167-25-4m25 11-23 5m164-12 25-4m-25 11 23 5" strokeWidth="1.7" opacity=".7" />}
      <path d="M115 213q33 16 69 0l-4 11q-30 14-61-1Z" fill="#a5c6aa" stroke="#7e9b80" strokeWidth="1.8" />
      <circle cx="150" cy="230" r="8" fill="#efcf7b" stroke="#bd9b51" strokeWidth="1.6" /><path d="M147 230h6m-3-3v6" stroke="#bd9b51" strokeWidth="1.3" />
      {messy && <g className="messy-fur" strokeWidth="2.5"><path d="m87 102-15-13 4 19-15-1m149-2 14-15-3 23 15-3M96 204l-12 8 13 1-7 11m103-19 12 7-12 3 10 10M112 80l-6-15 14 12m55 3 9-13-3 16" fill={fur} /><path d="m95 236-8 3 11 5m105-8 8 4-10 5" /></g>}
      <path d="M75 122c5-34 35-52 75-52 42 0 75 22 79 58 1 8 1 15-2 23l8 8-12 3 9 12-15 1c-10 29-39 44-67 44-30 0-61-15-73-42l-12-2 9-10-12-5 10-9c-2-9-1-19 3-29Z" fill={`url(#${textureId})`} stroke="none" />
      {dirty > 0 && <g className="mud" fill="#9c8064" stroke="none" opacity={Math.min(.85, dirty)}>
        {[
          <path d="M87 117c-10-3-15 8-8 14-10 9 1 19 12 12 13 5 22-5 13-13 5-9-7-17-17-13Z" />,
          <path d="M184 179c-5-7-14-3-12 3-10 4-5 14 3 12 5 9 18 2 15-5 10-6 3-12-6-10Z" />,
          <path d="M106 251c-6-7-15-2-12 6-9 7-3 14 6 11 8 8 16-1 12-7 8-4 2-12-6-10Z" />,
          <path d="M181 245c-6-9-17-5-14 3-10 5-4 18 7 13 9 5 19-4 11-11 7-3 2-8-4-5Z" />,
          <circle cx="201" cy="115" r="5" />, <circle cx="127" cy="194" r="4" />,
          <circle cx="156" cy="256" r="3" />, <circle cx="94" cy="154" r="3" />,
        ].map((spot, index) => <g key={index} className="mud-spot" style={{ opacity: index < cleanedSpots ? 0 : 1 }}>{spot}</g>)}
      </g>}
      {accessory === 'bow' && <g transform="translate(208 94) rotate(25)"><path d="M0 0C-25-23-28 12-3 8 18 28 29-5 5 0Z" fill="#dc999e" stroke="#ab717a" strokeWidth="1.8" /><circle cx="2" cy="4" r="5" fill="#e9b6ba" stroke="#ab717a" strokeWidth="1.5" /></g>}
      {accessory === 'scarf' && <g><path d="m115 212 5 18q29 9 60-2l5-17q-36 17-70 1Z" fill="#b7aad0" stroke="#9284ac" strokeWidth="1.5" /><path d="m173 224 17 29-20 3-6-29" fill="#b7aad0" stroke="#9284ac" strokeWidth="1.5" /></g>}
    </g>
  </svg>;
}

export function FoodArt({ type = 'kibble' }) {
  return <svg viewBox="0 0 100 80" aria-hidden="true"><ellipse cx="50" cy="67" rx="33" ry="5" fill="#657257" opacity=".12" /><path d="m17 35 7 25q26 17 52 0l7-25" fill={type === 'fish' ? '#a6c6c1' : type === 'chicken' ? '#e9b6b2' : '#e5c17f'} stroke="#8c8a70" strokeWidth="2" /><ellipse cx="50" cy="35" rx="33" ry="13" fill="#fffcf0" stroke="#8c8a70" strokeWidth="2" /><ellipse cx="50" cy="36" rx="27" ry="9" fill="#ae825d" />{type === 'fish' ? <g fill="#d4dedd" stroke="#829a94" strokeWidth="1.5"><path d="M32 33q13-15 28 0-13 16-28 0Zm28 0 12-9v18Z" /><circle cx="39" cy="31" r="1" fill="#526a63" /></g> : type === 'chicken' ? <g fill="#e6b38d" stroke="#b68661" strokeWidth="1.5"><path d="M33 31q-1-15 16-10 14 7 0 16l-10 3Z" /><path d="m47 38 12-5 8 5-13 8Z" fill="#a7b981" /></g> : <g fill="#dab178" stroke="#b48a5f" strokeWidth="1"><ellipse cx="34" cy="33" rx="6" ry="4" /><ellipse cx="49" cy="29" rx="6" ry="4" /><ellipse cx="64" cy="33" rx="6" ry="4" /><ellipse cx="46" cy="39" rx="6" ry="4" /><ellipse cx="61" cy="40" rx="5" ry="3" /></g>}<path d="M43 53q7-7 14 0m-11 4h8" stroke="#fff6dc" strokeWidth="2" strokeLinecap="round" /></svg>;
}
