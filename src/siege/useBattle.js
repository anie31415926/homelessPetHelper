import { useEffect, useRef, useState } from 'react';
import { CATALOG } from './catalog.js';
import { createSession, advanceSession, commitOrder, setPaused } from './session.js';

export function useBattle(options) {
    const [state, setState] = useState(() => createSession(options));
    const live = useRef(state);
    const publish = next => { live.current = next; setState(next); };
    useEffect(() => {
        let frame, previous = performance.now();
        function tick(now) {
            const dt = Math.min(100, Math.max(0, now - previous));
            previous = now;
            const next = advanceSession(live.current, dt, CATALOG);
            if (next !== live.current) publish(next);
            frame = requestAnimationFrame(tick);
        }
        const freeze = () => { previous = performance.now(); publish(setPaused(live.current, true)); };
        const visibility = () => { if (document.hidden) freeze(); };
        frame = requestAnimationFrame(tick);
        window.addEventListener('blur', freeze);
        document.addEventListener('visibilitychange', visibility);
        return () => {
            cancelAnimationFrame(frame);
            window.removeEventListener('blur', freeze);
            document.removeEventListener('visibilitychange', visibility);
        };
    }, []);
    return { state,
        submit(input) {
            const result = commitOrder(live.current, CATALOG, input);
            if (result.ok) publish(result.state);
            return result;
        },
        pause(value) { publish(setPaused(live.current, value)); },
        restart(nextOptions) { publish(createSession(nextOptions)); }
    };
}
