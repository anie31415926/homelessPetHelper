// Review-only scene and interaction data. Does not implement Approach or Siege rules.
const copy = await fetch('./copy.json').then(response => {
  if (!response.ok) throw new Error(`Copy request failed: ${response.status}`);
  return response.json();
});
const $ = id => document.getElementById(id);
const text = (key, values = {}) => Object.entries(values).reduce((value, [name, replacement]) => value.replaceAll(`{${name}}`, replacement), copy[key]);
const icon = id => `<svg aria-hidden="true"><use href="#${id}"/></svg>`;
const place = (element, point) => { element.style.left = `${point[0]}%`; element.style.top = `${point[1]}%`; };
const scene = {
  props: [
    { id: 'entry-trash', type: 'litter', icon: 'trash', label: 'litter', point: [22, 83] },
    { id: 'entry-weeds', type: 'weeds', icon: 'grass', label: 'weeds', point: [23, 73] },
    { id: 'optional-weeds', type: 'weeds', icon: 'grass', label: 'optional', point: [67, 73], optional: true },
    { id: 'optional-mud', type: 'mud', icon: 'mud', label: 'mud', point: [40, 85], optional: true }
  ],
  units: [
    { id: 'anchor', point: [50, 58], icon: 'yarn', mergeable: true },
    { id: 'material-a', point: [16, 44], icon: 'yarn', mergeable: true },
    { id: 'material-b', point: [83, 65], icon: 'yarn', mergeable: true },
    { id: 'pinwheel', point: [83, 44], icon: 'pinwheel', upgraded: true, reroute: true }
  ],
  portals: [[16, 75], [50, 75], [83, 75]],
  routes: [
    'M58 360 Q48 270 64 198 Q78 120 264 116',
    'M180 360 L180 190 Q180 143 264 116',
    'M299 360 L300 203 Q300 141 264 116'
  ],
  box: [22, 84], boxLanding: [47, 84], gap: [22, 87]
};
const state = {
  stage: 'cleanup', tool: null, card: null, removed: new Set(), boxMoved: false,
  bridge: false, selected: null, mode: null, materials: new Set(), plan: null, undo: [],
  cleanupDone: false, puzzleDone: false
};
let toastTimer;
let work;
let dragging;
document.querySelectorAll('[data-copy]').forEach(element => { element.textContent = text(element.dataset.copy); });
document.querySelectorAll('[data-aria]').forEach(element => { element.setAttribute('aria-label', text(element.dataset.aria)); });
const params = new URLSearchParams(location.search);
copy.stages.forEach(stage => {
  const button = document.createElement('button');
  button.textContent = stage.tab;
  button.dataset.stage = stage.id;
  button.addEventListener('click', () => setStage(stage.id, true));
  $('stages').append(button);
});
copy.principles.forEach(label => { const span = document.createElement('span'); span.textContent = label; $('principles').append(span); });

function toast(key) {
  clearTimeout(toastTimer);
  $('toast').textContent = text(key);
  $('toast').hidden = false;
  toastTimer = setTimeout(() => { $('toast').hidden = true; }, 2200);
}
function instruction(key, values) { $('instruction').textContent = text(key, values); }
function isRescue() { return state.stage === 'rescue' || state.stage === 'merge'; }
function cleanupReady() { return state.removed.has('entry-trash') && state.removed.has('entry-weeds'); }
function stopWork() {
  if (work) clearTimeout(work.timer);
  work = null;
  $('work-ring').hidden = true;
}
function clearSelection() {
  state.card = null; state.selected = null; state.mode = null; state.materials.clear();
  $('route-preview').setAttribute('d', '');
  renderCards(); renderUnits(); renderPortals(); renderContext();
  instruction(isRescue() ? (state.plan ? 'readyHint' : 'rescueHint') : state.stage === 'cleanup' ? 'cleanupHint' : 'puzzleHint');
}
function setStage(id, reviewShortcut = false) {
  stopWork(); cancelDrag();
  state.stage = id; state.tool = null; state.card = null; state.selected = null;
  state.mode = null; state.materials.clear(); state.plan = null; state.undo = [];
  // Reviewer shortcuts show the same site after its shared entrance is cleared.
  if (reviewShortcut && isRescue()) { state.removed.add('entry-trash'); state.removed.add('entry-weeds'); state.cleanupDone = true; }
  if (id === 'merge') { state.selected = 'anchor'; state.mode = 'merge'; }
  render();
}
function render() {
  const stage = copy.stages.find(entry => entry.id === state.stage);
  $('phone').dataset.stage = state.stage;
  document.querySelectorAll('#stages button').forEach(button => button.classList.toggle('active', button.dataset.stage === state.stage));
  $('note-index').textContent = `0${copy.stages.indexOf(stage) + 1}`;
  $('note-title').textContent = stage.title; $('note-body').textContent = stage.body;
  $('phase-name').textContent = stage.phase;
  $('turn-row').hidden = !isRescue();
  $('progress-label').textContent = text(isRescue() ? 'rescueProgress' : 'cleanupProgress');
  const progress = isRescue() ? 38 : state.stage === 'cleanup' ? (Number(state.removed.has('entry-trash')) + Number(state.removed.has('entry-weeds'))) * 50 : (Number(state.boxMoved) + Number(state.bridge)) * 50;
  $('main-progress').style.width = `${progress}%`;
  $('progress-value').textContent = `${progress}%`;
  $('dock-label').textContent = text(isRescue() ? 'turnLabel' : state.stage === 'cleanup' ? 'cleanupLabel' : 'puzzleLabel');
  $('resource-label').textContent = isRescue() ? text('resource') : '';
  $('commit').hidden = !isRescue();
  $('commit').textContent = text(state.plan ? 'commit' : 'wait');
  $('undo').hidden = state.stage !== 'puzzle'; $('undo').disabled = !state.undo.length;
  $('cancel-plan').hidden = !state.plan;
  $('bridge-deck').classList.toggle('built', state.bridge);
  $('route-preview').setAttribute('d', '');
  renderProps(); renderUnits(); renderPortals(); renderCards(); renderContext(); renderPlan(); renderReady();
  instruction(state.stage === 'merge' ? 'mergeHint' : isRescue() ? 'rescueHint' : state.stage === 'cleanup' ? 'cleanupHint' : 'puzzleHint');
}
function renderProps() {
  $('props').replaceChildren();
  scene.props.forEach(prop => {
    if (state.removed.has(prop.id) || (state.stage === 'puzzle' && !prop.optional)) return;
    const button = document.createElement('button');
    button.className = `prop ${prop.optional ? 'optional' : 'reachable'}`;
    button.dataset.prop = prop.id; button.innerHTML = icon(prop.icon);
    button.setAttribute('aria-label', text(prop.label)); place(button, prop.point);
    if (state.stage === 'cleanup') {
      button.addEventListener('pointerdown', event => {
        if (!event.isPrimary) return;
        event.preventDefault(); event.stopPropagation(); button.setPointerCapture(event.pointerId);
        startWork(prop);
      });
      button.addEventListener('pointermove', event => {
        const bounds = button.getBoundingClientRect();
        if (event.clientX < bounds.left || event.clientX > bounds.right || event.clientY < bounds.top || event.clientY > bounds.bottom) stopWork();
      });
      button.addEventListener('pointerup', stopWork);
      button.addEventListener('pointercancel', stopWork);
      button.addEventListener('lostpointercapture', stopWork);
    } else button.disabled = true;
    $('props').append(button);
  });
  if (state.stage === 'puzzle' || state.puzzleDone) {
    const box = document.createElement('button'); box.innerHTML = icon('crate'); box.className = 'prop'; box.id = 'box';
    box.setAttribute('aria-label', text('box')); place(box, state.boxMoved ? scene.boxLanding : scene.box);
    box.disabled = isRescue(); box.addEventListener('click', () => {
      state.selected = 'box'; instruction('moveHint'); renderContext(); renderLanding();
    }); $('props').append(box);
    if (!state.bridge && !isRescue()) {
      const gap = document.createElement('button'); gap.id = 'gap'; gap.className = 'prop'; gap.innerHTML = icon('bridge');
      gap.setAttribute('aria-label', text('gap')); place(gap, [21, 91]);
      gap.addEventListener('click', () => { state.selected = 'gap'; instruction('bridgeHint'); renderContext(); }); $('props').append(gap);
    }
  }
}
function startWork(prop) {
  stopWork();
  if (state.tool !== prop.type) { toast('wrongTool'); return; }
  if (prop.id === 'entry-weeds' && !state.removed.has('entry-trash')) { toast('notReachable'); return; }
  if (prop.optional && !cleanupReady()) { toast('notReachable'); return; }
  place($('work-ring'), prop.point); $('work-ring').hidden = false;
  work = { timer: setTimeout(() => {
    state.removed.add(prop.id); stopWork(); state.cleanupDone = cleanupReady();
    renderProps(); renderReady();
    $('main-progress').style.width = `${(Number(state.removed.has('entry-trash')) + Number(state.removed.has('entry-weeds'))) * 50}%`;
    $('progress-value').textContent = cleanupReady() ? '100%' : '50%';
    toast('cleared'); if (cleanupReady()) instruction('clearHint');
  }, 600) };
}
function renderReady() {
  $('start-rescue').hidden = isRescue() || !(state.stage === 'cleanup' ? cleanupReady() : state.boxMoved && state.bridge);
}
function renderCards() {
  $('cards').replaceChildren();
  if (state.stage === 'puzzle') return;
  const entries = isRescue() ? copy.hand : copy.tools;
  entries.forEach(entry => {
    const selected = isRescue() ? state.card === entry.id : state.tool === entry.id;
    const button = document.createElement('button'); button.className = `card${selected ? ' selected' : ''}`;
    button.dataset.card = entry.id; button.innerHTML = `${icon(entry.icon)}<strong>${entry.name}</strong><small>${entry.detail}</small>`;
    button.addEventListener('click', () => {
      if (dragging?.moved) return;
      chooseCard(entry.id);
    });
    if (isRescue()) button.addEventListener('pointerdown', event => beginDrag(event, entry, button));
    if (selected && isRescue()) {
      const swap = document.createElement('span'); swap.className = 'swap'; swap.textContent = text('swap'); swap.tabIndex = 0; swap.setAttribute('role', 'button');
      const discard = event => { event.stopPropagation(); state.plan = null; clearSelection(); renderPlan(); renderReady(); $('commit').textContent = text('wait'); toast('discarded'); };
      swap.addEventListener('click', discard); swap.addEventListener('keydown', event => { if (event.key === 'Enter') discard(event); }); button.append(swap);
    }
    $('cards').append(button);
  });
}
function chooseCard(id) {
  state.selected = null; state.mode = null; state.materials.clear();
  if (isRescue()) { state.card = id; instruction('laneHint'); } else { state.tool = id; instruction('toolHint'); }
  renderCards(); renderContext(); renderUnits(); renderPortals();
}
function beginDrag(event, entry, element) {
  if (!event.isPrimary || event.target.closest('.swap')) return;
  dragging = { pointerId: event.pointerId, card: entry.id, start: [event.clientX, event.clientY], moved: false, element };
  element.setPointerCapture(event.pointerId);
  const move = next => {
    if (!dragging || next.pointerId !== dragging.pointerId) return;
    if (Math.hypot(next.clientX - dragging.start[0], next.clientY - dragging.start[1]) > 8) {
      if (!dragging.moved) { state.card = entry.id; state.selected = null; state.mode = null; renderPortals(); renderContext(); }
      dragging.moved = true; $('drag-ghost').hidden = false; $('drag-ghost').innerHTML = icon(entry.icon);
      $('drag-ghost').style.left = `${next.clientX}px`; $('drag-ghost').style.top = `${next.clientY}px`;
      const portal = document.elementFromPoint(next.clientX, next.clientY)?.closest('.portal');
      $('route-preview').setAttribute('d', portal ? scene.routes[Number(portal.dataset.lane)] : '');
    }
  };
  const release = next => {
    if (!dragging || next.pointerId !== dragging.pointerId) return;
    const wasMoved = dragging.moved;
    if (wasMoved) {
      const portal = document.elementFromPoint(next.clientX, next.clientY)?.closest('.portal');
      if (next.type === 'pointerup' && portal) planLane(Number(portal.dataset.lane));
      else { state.card = null; renderPortals(); $('route-preview').setAttribute('d', ''); toast('invalidDrop'); }
    }
    element.removeEventListener('pointermove', move); element.removeEventListener('pointerup', release); element.removeEventListener('pointercancel', release);
    $('drag-ghost').hidden = true;
    // Suppress the synthetic click after a completed drag.
    setTimeout(() => { dragging = null; renderCards(); }, wasMoved ? 50 : 0);
  };
  element.addEventListener('pointermove', move); element.addEventListener('pointerup', release); element.addEventListener('pointercancel', release);
}
function cancelDrag() { dragging = null; $('drag-ghost').hidden = true; }
function renderPortals() {
  $('portals').replaceChildren();
  if (!isRescue() || (!state.card && state.mode !== 'reroute')) return;
  scene.portals.forEach((point, lane) => {
    const button = document.createElement('button'); button.className = 'portal'; button.dataset.lane = lane; button.textContent = copy.lanes[lane];
    button.setAttribute('aria-label', text('portalAria', { lane: copy.lanes[lane] })); place(button, point);
    button.addEventListener('pointerenter', () => $('route-preview').setAttribute('d', scene.routes[lane]));
    button.addEventListener('pointerleave', () => { if (!state.plan) $('route-preview').setAttribute('d', ''); });
    button.addEventListener('click', () => planLane(lane)); $('portals').append(button);
  });
}
function planLane(lane) {
  if (!state.card && state.mode !== 'reroute') return;
  state.plan = { kind: state.mode === 'reroute' ? 'reroute' : 'deploy', lane, card: state.card };
  state.card = null; state.mode = null; state.selected = null; state.materials.clear();
  renderCards(); renderUnits(); renderPortals(); renderContext(); renderPlan(); instruction('readyHint');
  $('commit').textContent = text('commit'); $('cancel-plan').hidden = false;
}
function renderPlan() {
  const plan = state.plan; $('plan').hidden = !plan;
  if (!plan) return;
  const entry = plan.kind === 'reroute' ? {icon: 'pinwheel'} : copy.hand.find(item => item.id === plan.card) ?? copy.hand[0];
  const label = plan.kind === 'merge' ? text('mergePlan') : plan.kind === 'reroute' ? text('reroutePlan') : text('deployPlan', {name: entry.name, lane: copy.lanes[plan.lane]});
  $('plan').innerHTML = `${icon(entry.icon)}<span>${label}</span>`;
  place($('plan'), plan.kind === 'merge' ? scene.units[0].point : scene.portals[plan.lane]);
  $('route-preview').setAttribute('d', plan.kind === 'merge' ? '' : scene.routes[plan.lane]);
}
function renderUnits() {
  $('units').replaceChildren(); if (!isRescue()) return;
  scene.units.forEach((unit, index) => {
    const button = document.createElement('button');
    button.className = `unit${unit.upgraded ? ' upgraded' : ''}${state.selected === unit.id ? ' selected' : ''}${state.mode === 'merge' && unit.mergeable && state.selected !== unit.id ? ' candidate' : ''}${state.materials.has(unit.id) ? ' material' : ''}`;
    button.dataset.unit = unit.id; button.innerHTML = icon(unit.icon); place(button, unit.point);
    button.disabled = state.mode === 'merge' && !unit.mergeable;
    button.setAttribute('aria-label', text(unit.reroute ? 'pinwheelAria' : 'unitAria', {index: index + 1}));
    let label;
    if (state.selected === unit.id) label = text('anchor');
    if (state.materials.has(unit.id)) label = text('material', {count: [...state.materials].indexOf(unit.id) + 1});
    if (label) { const span = document.createElement('span'); span.textContent = label; button.append(span); }
    button.addEventListener('click', () => selectUnit(unit)); $('units').append(button);
  });
}
function selectUnit(unit) {
  state.card = null;
  if (state.mode === 'merge' && state.selected !== unit.id) {
    if (state.materials.has(unit.id)) state.materials.delete(unit.id); else state.materials.add(unit.id);
    instruction('materialsHint', {count: state.materials.size});
    if (state.materials.size === 2) {
      state.plan = {kind: 'merge', card: 'yarn'}; renderPlan(); $('commit').textContent = text('commit'); $('cancel-plan').hidden = false; instruction('plannedHint');
    } else if (state.plan?.kind === 'merge') { state.plan = null; renderPlan(); $('cancel-plan').hidden = true; $('commit').textContent = text('wait'); }
  } else { state.selected = unit.id; state.mode = null; state.materials.clear(); instruction('anchorHint'); }
  renderCards(); renderUnits(); renderPortals(); renderContext();
}
function contextButton(key, action) {
  const button = document.createElement('button'); button.textContent = text(key); button.addEventListener('click', action); $('context').append(button);
}
function renderContext() {
  const context = $('context'); context.replaceChildren(); context.hidden = !state.selected;
  if (!state.selected) return;
  if (state.selected === 'box') { place(context, [25, 72]); contextButton('boxSelected', () => {}); }
  else if (state.selected === 'gap') {
    place(context, [25, 76]); contextButton('bridgeAction', () => { state.undo.push({boxMoved: state.boxMoved, bridge: state.bridge}); state.bridge = true; state.puzzleDone = state.boxMoved; state.selected = null; render(); toast('bridgeBuilt'); });
  } else {
    const unit = scene.units.find(entry => entry.id === state.selected); place(context, [unit.point[0], unit.point[1] - 7]);
    if (unit.mergeable) contextButton('mergeAction', () => { state.mode = 'merge'; state.materials.clear(); renderUnits(); renderPortals(); instruction('mergeHint'); });
    if (unit.reroute) contextButton('rerouteAction', () => { state.mode = 'reroute'; renderUnits(); renderPortals(); instruction('laneHint'); });
  }
  contextButton('cancel', () => { clearSelection(); $('landing')?.remove(); });
}
function renderLanding() {
  $('landing')?.remove(); const button = document.createElement('button'); button.id = 'landing'; button.className = 'landing'; button.innerHTML = icon('crate');
  place(button, state.boxMoved ? scene.box : scene.boxLanding); button.setAttribute('aria-label', text('landing'));
  button.addEventListener('click', () => { state.undo.push({boxMoved: state.boxMoved, bridge: state.bridge}); state.boxMoved = !state.boxMoved; state.puzzleDone = state.boxMoved && state.bridge; state.selected = null; render(); toast('boxMoved'); });
  $('props').append(button);
}
$('field').addEventListener('click', event => {
  if (event.target === $('field') || event.target.classList.contains('field-art') || event.target.classList.contains('terrain')) clearSelection();
});
$('start-rescue').addEventListener('click', () => { setStage('rescue'); toast('preserved'); });
$('commit').addEventListener('click', () => { state.plan = null; clearSelection(); renderPlan(); $('commit').textContent = text('wait'); $('cancel-plan').hidden = true; toast('committed'); });
$('cancel-plan').addEventListener('click', () => { state.plan = null; clearSelection(); renderPlan(); $('commit').textContent = text('wait'); $('cancel-plan').hidden = true; });
$('undo').addEventListener('click', () => { const previous = state.undo.pop(); if (previous) { Object.assign(state, previous); state.selected = null; state.puzzleDone = state.boxMoved && state.bridge; render(); } });
$('pause').addEventListener('click', () => { stopWork(); cancelDrag(); $('pause-sheet').hidden = false; });
$('resume').addEventListener('click', () => { $('pause-sheet').hidden = true; });
$('reset').addEventListener('click', () => { state.removed.clear(); state.boxMoved = false; state.bridge = false; state.puzzleDone = false; state.cleanupDone = false; setStage('cleanup'); });
document.addEventListener('visibilitychange', () => { if (document.hidden) { stopWork(); cancelDrag(); $('pause-sheet').hidden = false; } });
setStage(copy.stages.some(stage => stage.id === params.get('stage')) ? params.get('stage') : 'cleanup', true);
