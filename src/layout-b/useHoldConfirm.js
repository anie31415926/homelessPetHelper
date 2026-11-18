import { useCallback, useEffect, useRef, useState } from 'react';

export const HOLD_DURATION_MS = 400;
const MOVEMENT_TOLERANCE_PX = 12;

export function useHoldConfirm(onConfirm, disabled) {
    const [holding, setHolding] = useState(false);
    const pressRef = useRef(null);
    const timerRef = useRef(null);
    const callbackRef = useRef(onConfirm);
    const disabledRef = useRef(disabled);
    callbackRef.current = onConfirm;
    disabledRef.current = disabled;

    const cancel = useCallback(() => {
        clearTimeout(timerRef.current);
        timerRef.current = null;
        pressRef.current = null;
        setHolding(false);
    }, []);

    const start = useCallback(press => {
        if (disabledRef.current || pressRef.current) return false;
        pressRef.current = press;
        setHolding(true);
        timerRef.current = setTimeout(() => {
            if (pressRef.current !== press || disabledRef.current || document.hidden) {
                cancel();
                return;
            }
            // Keep the completed press until release so one gesture cannot repeat.
            timerRef.current = null;
            press.confirmed = true;
            setHolding(false);
            callbackRef.current();
        }, HOLD_DURATION_MS);
        return true;
    }, [cancel]);

    useEffect(() => {
        if (disabled) cancel();
    }, [disabled, cancel]);

    useEffect(() => {
        const visibility = () => { if (document.hidden) cancel(); };
        const otherPointer = event => {
            const press = pressRef.current;
            if (press?.type === 'pointer' && event.pointerId !== press.id) cancel();
        };
        window.addEventListener('blur', cancel);
        window.addEventListener('resize', cancel);
        window.addEventListener('scroll', cancel, true);
        window.addEventListener('pointerdown', otherPointer);
        document.addEventListener('visibilitychange', visibility);
        return () => {
            clearTimeout(timerRef.current);
            pressRef.current = null;
            window.removeEventListener('blur', cancel);
            window.removeEventListener('resize', cancel);
            window.removeEventListener('scroll', cancel, true);
            window.removeEventListener('pointerdown', otherPointer);
            document.removeEventListener('visibilitychange', visibility);
        };
    }, [cancel]);

    return {
        holding,
        handlers: {
            onPointerDown(event) {
                if (!event.isPrimary || event.button !== 0) return;
                if (!start({ type: 'pointer', id: event.pointerId, x: event.clientX, y: event.clientY })) return;
                event.currentTarget.focus({ preventScroll: true });
                event.currentTarget.setPointerCapture(event.pointerId);
            },
            onPointerMove(event) {
                const press = pressRef.current;
                if (press?.type !== 'pointer' || press.id !== event.pointerId || press.confirmed) return;
                const distance = Math.hypot(event.clientX - press.x, event.clientY - press.y);
                const hit = document.elementFromPoint(event.clientX, event.clientY);
                if (distance > MOVEMENT_TOLERANCE_PX || !hit || !event.currentTarget.contains(hit)) cancel();
            },
            onPointerUp(event) {
                if (pressRef.current?.type === 'pointer' && pressRef.current.id === event.pointerId) cancel();
            },
            onPointerCancel: cancel,
            onLostPointerCapture: cancel,
            onBlur: cancel,
            onKeyDown(event) {
                if (![' ', 'Enter'].includes(event.key)) return;
                event.preventDefault();
                if (!event.repeat) start({ type: 'keyboard', key: event.key });
            },
            onKeyUp(event) {
                if (![' ', 'Enter'].includes(event.key)) return;
                event.preventDefault();
                if (pressRef.current?.type === 'keyboard' && pressRef.current.key === event.key) cancel();
            },
            onClick(event) {
                event.preventDefault();
                // Screen-reader activation has no pointer/hold stream. Preserve native activation.
                if (event.detail === 0 && !pressRef.current && !disabledRef.current) callbackRef.current();
            },
            onContextMenu(event) { event.preventDefault(); },
        },
    };
}
