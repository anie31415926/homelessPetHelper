import React, { useEffect, useRef, useState } from 'react';
import { ArrowDownLeft, ArrowDownRight, Check, Clock3, Fish, Footprints, Pause, Pin, Play, RotateCcw } from 'lucide-react';
import { copy } from './copy.js';
import { HOLD_DURATION_MS, useHoldConfirm } from './useHoldConfirm.js';
import catImage from '../../unity/HomelessPetHelper/Assets/Art/Station/T_Station_CatTemporary.png';
import backdrop from '../../art-source/rescue-layout-b/clean-backdrop.jpg';

const icons = { space: Footprints, wait: Clock3, food: Fish };

function ActionRow({ option, confirmed, retained, blocked, onConfirm, onRetain }) {
    const Icon = icons[option.id];
    const { holding, handlers } = useHoldConfirm(onConfirm, blocked);
    return <div className={`action-row ${holding ? 'is-holding' : ''} ${confirmed ? 'is-confirmed' : ''}`}
        data-action={option.id} data-holding={holding} role="group" aria-label={option.title}>
        <button className={`retain-action ${retained ? 'is-retained' : ''}`} type="button"
            aria-label={(retained ? copy.unretainLabel : copy.retainLabel)(option.title)} aria-pressed={retained}
            title={(retained ? copy.unretainLabel : copy.retainLabel)(option.title)}
            disabled={blocked} onClick={onRetain}>
            <Pin size={17} strokeWidth={1.8} fill={retained ? 'currentColor' : 'none'} aria-hidden="true" />
        </button>
        <button className="select-action" type="button" aria-describedby="hold-hint" aria-description={option.detail}
            disabled={blocked} {...handlers} style={{ '--hold-duration': `${HOLD_DURATION_MS}ms` }}>
            <span className={`action-icon action-icon-${option.id}`}>
                {confirmed ? <Check size={18} strokeWidth={2} aria-hidden="true" /> : <Icon size={18} strokeWidth={1.8} aria-hidden="true" />}
            </span>
            <strong>{option.title}</strong>
            {holding && <span className="hold-progress" aria-hidden="true" />}
        </button>
    </div>;
}

export default function LayoutBApp() {
    const [hand, setHand] = useState('left');
    const [confirmation, setConfirmation] = useState({ id: null, count: 0 });
    const [retained, setRetained] = useState(['wait']);
    const [receipt, setReceipt] = useState('');
    const [notice, setNotice] = useState('');
    const [paused, setPaused] = useState(false);
    const dialogRef = useRef(null);
    const pauseRef = useRef(null);

    useEffect(() => {
        const dialog = dialogRef.current;
        if (paused && !dialog.open) dialog.showModal();
        else if (!paused && dialog.open) dialog.close();
    }, [paused]);

    function retain(id) {
        if (paused) return;
        if (retained.includes(id)) {
            setRetained(current => current.filter(value => value !== id));
            setNotice('');
            return;
        }
        if (retained.length === 2) { setNotice(copy.limit); return; }
        setRetained(current => [...current, id]);
        setNotice('');
    }

    function execute(option) {
        if (paused) return;
        // Layout-only receipt: no animal reaction, turn settlement or resource change.
        setReceipt(copy.executed(option.title));
        setConfirmation(current => ({ id: option.id, count: current.count + 1 }));
        setNotice('');
    }

    function reset() {
        setConfirmation({ id: null, count: 0 });
        setRetained(['wait']);
        setReceipt('');
        setNotice('');
        setPaused(false);
    }

    return <main className="baseline-shell" data-hand={hand} data-confirmations={confirmation.count}>
        <section className="observation" aria-label={copy.title}>
            <img className="scene-backdrop" src={backdrop} alt={copy.sceneAlt} />
            <header className="scene-header">
                <button ref={pauseRef} className="icon-button pause-button" type="button" aria-label={copy.pause} title={copy.pause}
                    onClick={() => setPaused(true)}><Pause size={22} strokeWidth={2} aria-hidden="true" /></button>
                <div className="location"><span>{copy.area}</span><h1>{copy.title}</h1></div>
                <span className="round-label">{copy.round}</span>
            </header>
            <img className="animal" src={catImage} alt={copy.catAlt} />
        </section>

        <section className="animal-feedback" aria-live="polite" aria-atomic="true">
            <span className="feedback-line" aria-hidden="true" />
            <p><strong>{notice || receipt || copy.cue}</strong>{!notice && <span>{copy.cueDetail}</span>}</p>
        </section>

        <section className="action-dock" aria-label={copy.actions}>
            <div className="dock-heading"><p id="hold-hint">{copy.holdHint}</p><span role="status">{copy.retained(retained.length)}</span></div>
            <div className="action-list">
                {copy.actionOptions.map(option => <ActionRow key={option.id} option={option} confirmed={confirmation.id === option.id}
                    retained={retained.includes(option.id)} blocked={paused}
                    onConfirm={() => execute(option)} onRetain={() => retain(option.id)} />)}
            </div>
        </section>

        <dialog ref={dialogRef} className="pause-sheet" aria-labelledby="pause-title" onCancel={() => setPaused(false)}
            onClose={() => { setPaused(false); pauseRef.current?.focus({ preventScroll: true }); }}>
            <div className="sheet-handle" aria-hidden="true" />
            <h2 id="pause-title">{copy.pause}</h2>
            <div className="hand-setting"><span id="hand-label">{copy.hand}</span>
                <div className="hand-segment" role="radiogroup" aria-labelledby="hand-label" onKeyDown={event => {
                    if (!['ArrowLeft', 'ArrowRight'].includes(event.key)) return;
                    event.preventDefault();
                    const next = hand === 'left' ? 'right' : 'left';
                    setHand(next);
                    event.currentTarget.querySelector(`[data-hand-option="${next}"]`)?.focus();
                }}>
                    {[['left', copy.left, ArrowDownLeft], ['right', copy.right, ArrowDownRight]].map(([id, label, Icon]) =>
                        <button key={id} type="button" role="radio" data-hand-option={id} tabIndex={hand === id ? 0 : -1}
                            aria-checked={hand === id} onClick={() => setHand(id)}>
                            <Icon size={19} aria-hidden="true" />{label}
                        </button>)}
                </div>
            </div>
            <button type="button" className="resume-button" onClick={() => setPaused(false)}><Play size={19} aria-hidden="true" />{copy.resume}</button>
            <button type="button" className="reset-button" onClick={reset}><RotateCcw size={18} aria-hidden="true" />{copy.reset}</button>
        </dialog>
    </main>;
}
