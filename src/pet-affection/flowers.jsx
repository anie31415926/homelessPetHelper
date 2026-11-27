import React from 'react';

const colors = ['#d97b8e', '#f1ab78', '#efd178', '#92b994', '#88b6c8', '#b4a2cc'];
const petals = [
    [43, 22, 34, 3, 5.4, 0],
    [47, 29, 55, 12, 3.7, 1],
    [49, 25, 72, 10, 4.5, 2],
    [35, 21, 21, 7, 3.8, 3],
    [50, 25, 64, -2, 3.2, 4],
    [29, 27, 8, 15, 4.2, 5],
];

function Flower({ color }) {
    return <>
        {[0, 72, 144, 216, 288].map(angle => (
            <ellipse key={angle} transform={`rotate(${angle})`} cx="0" cy="-.58" rx=".47" ry=".64" fill={color} />
        ))}
        <circle r=".35" fill="#fff4ca" />
        <circle cx="-.1" cy="-.12" r=".11" fill="#fffdf2" />
    </>;
}

export function FlowerBurst({ burst, onDone }) {
    return (
        <svg className="flower-burst" viewBox="0 0 100 100" aria-hidden="true"
            onAnimationEnd={event => { if (event.target === event.currentTarget) onDone(burst.id); }}>
            <g className="rainbow" transform={`translate(${burst.lean * 3} 0)`}>
                {colors.slice(0, 5).map((color, index) => (
                    <path key={color} d={`M49 ${17 + index * 1.5} Q61 ${-2 + index * 1.8} 74 ${10 + index * 1.3}`}
                        fill="none" stroke={color} strokeWidth="1.55" strokeLinecap="round" />
                ))}
            </g>
            {petals.map(([x, y, endX, endY, scale, color], i) => (
                <g key={color} className="flying-flower" style={{
                    '--start-x': `${x + burst.lean * 3}px`, '--start-y': `${y}px`,
                    '--end-x': `${endX + burst.lean * 4}px`, '--end-y': `${endY}px`,
                    '--size': scale / 2.4, '--spin': `${i % 2 ? 35 : -38}deg`, '--delay': `${i * 25}ms`,
                }}>
                    <Flower color={colors[color]} />
                </g>
            ))}
        </svg>
    );
}
