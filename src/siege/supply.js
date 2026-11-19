function roll(state) {
    state.rng = (state.rng + 0x6D2B79F5) >>> 0;
    let value = state.rng;
    value = Math.imul(value ^ value >>> 15, value | 1);
    value ^= value + Math.imul(value ^ value >>> 7, value | 61);
    return ((value ^ value >>> 14) >>> 0) / 4294967296;
}
export function fillHand(state, catalog, limit) {
    let added = 0;
    while (state.hand.length < catalog.rules.handSize && added < limit) {
        if (!state.bag.length) {
            state.bag = catalog.units.filter(unit => unit.bag).flatMap(unit => Array(catalog.rules.bagCopies).fill(unit.id));
            for (let i = state.bag.length - 1; i > 0; i--) {
                const other = Math.floor(roll(state) * (i + 1));
                [state.bag[i], state.bag[other]] = [state.bag[other], state.bag[i]];
            }
        }
        state.hand.push({ instanceId: ++state.serial, definitionId: state.bag.shift() });
        added++;
    }
}
export function record(state, catalog, key, detail = {}) {
    state.log.push({ key, atMs: state.elapsedMs, turn: state.turn, ...detail });
    if (state.log.length > catalog.rules.maxLog) state.log.splice(0, state.log.length - catalog.rules.maxLog);
}
