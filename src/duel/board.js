import PF from 'pathfinding';
import { COLS, ROWS, TYPES } from './content.js';

export function roll(state) {
    // Session-owned Mulberry32 state makes invalid commands and previews consume no randomness.
    state.rng = (state.rng + 0x6D2B79F5) >>> 0;
    let value = state.rng;
    value = Math.imul(value ^ value >>> 15, value | 1);
    value ^= value + Math.imul(value ^ value >>> 7, value | 61);
    return ((value ^ value >>> 14) >>> 0) / 4294967296;
}

export function shuffle(state, list) {
    const result = [...list];
    for (let i = result.length - 1; i > 0; i--) {
        const swap = Math.floor(roll(state) * (i + 1));
        [result[i], result[swap]] = [result[swap], result[i]];
    }
    return result;
}

export function tile(state, type) {
    return { id: ++state.serial, type: type || TYPES[Math.floor(roll(state) * TYPES.length)] };
}

export function groupAt(board, index) {
    if (!Number.isInteger(index) || index < 0 || index >= board.length || board[index].type === 'noise') return [];
    const type = board[index].type;
    const matrix = Array.from({ length: ROWS }, (_, y) =>
        board.slice(y * COLS, (y + 1) * COLS).map(item => item.type === type ? 0 : 1));
    const grid = new PF.Grid(matrix);
    const finder = new PF.AStarFinder({ allowDiagonal: false });
    // Reuse the project's pathfinding core so row boundaries and diagonal rules stay explicit.
    return board.flatMap((item, i) => item.type === type && finder.findPath(
        index % COLS, Math.floor(index / COLS), i % COLS, Math.floor(i / COLS), grid.clone()).length ? [i] : []);
}

export function groups(board, minimum = 2) {
    const seen = new Set();
    const result = [];
    board.forEach((item, index) => {
        if (seen.has(index) || item.type === 'noise') return;
        const group = groupAt(board, index);
        group.forEach(i => seen.add(i));
        if (group.length >= minimum) result.push({ type: item.type, cells: group });
    });
    return result;
}

export function refill(state, removed) {
    const empty = new Set(removed);
    const board = [...state.board];
    for (let x = 0; x < COLS; x++) {
        const column = [];
        for (let y = ROWS - 1; y >= 0; y--) {
            const index = y * COLS + x;
            if (!empty.has(index)) column.push(board[index]);
        }
        while (column.length < ROWS) column.push(tile(state));
        for (let y = ROWS - 1; y >= 0; y--) board[y * COLS + x] = column[ROWS - 1 - y];
    }
    state.board = board;
}

export function ensureMove(state) {
    if (groups(state.board).length) return false;
    // Repair only an actual no-move board. No repeated random rerolls or disguised free score.
    const type = state.board.find(item => item.type !== 'noise')?.type || 'space';
    state.board[0] = tile(state, type);
    state.board[1] = tile(state, type);
    return true;
}

export function connect(state, type, count) {
    const candidates = groups(state.board, 1).filter(group => group.type === type)
        .sort((a, b) => b.cells.length - a.cells.length || a.cells[0] - b.cells[0]);
    const connected = new Set(candidates[0]?.cells || []);
    const changed = [];
    if (!connected.size && count > 0) {
        const center = Math.floor(state.board.length / 2);
        state.board[center] = tile(state, type);
        connected.add(center);
        changed.push(center);
    }
    while (changed.length < count) {
        const frontier = new Set();
        for (const i of connected) {
            const x = i % COLS;
            for (const j of [x > 0 ? i - 1 : -1, x < COLS - 1 ? i + 1 : -1, i - COLS, i + COLS]) {
                if (j >= 0 && j < state.board.length && !connected.has(j) && state.board[j].type !== type) {
                    frontier.add(j);
                }
            }
        }
        const next = [...frontier].sort((a, b) =>
            Number(state.board[b].type === 'noise') - Number(state.board[a].type === 'noise') || a - b)[0];
        if (next === undefined) break;
        state.board[next] = tile(state, type);
        groupAt(state.board, next).forEach(i => connected.add(i));
        changed.push(next);
    }
    return changed;
}
