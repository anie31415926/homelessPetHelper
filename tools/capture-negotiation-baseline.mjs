import assert from 'node:assert/strict';
import { createHash } from 'node:crypto';
import { mkdir, readFile, writeFile } from 'node:fs/promises';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { chooseAction, createOuting, updateOuting } from '../src/outing/engine.js';

const repoRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const reportDirectory = path.join(repoRoot, 'test-results');
const completedAction = action => ({ action, accepted: true, settle: true });
const successSequences = {
  hungry: ['food', 'wait', 'approach', 'approach', 'invite'],
  shy: ['wait', 'toy', 'wait', 'approach', 'wait', 'approach', 'invite'],
  curious: ['toy', 'approach', 'wait', 'approach', 'invite'],
};

// These traces describe the HTML experiment, not approved rules for the Unity game.
const scenarios = [
  ...Object.entries(successSequences).map(([profile, actions]) => ({
    id: `${profile}-success`, profile, expectedStage: 'success', steps: actions.map(completedAction),
  })),
  {
    id: 'hungry-double-tap', profile: 'hungry', expectedStage: 'negotiate',
    steps: [
      { action: 'invite', accepted: false },
      { action: 'food', accepted: true },
      { action: 'food', accepted: false, settle: true },
      completedAction('wait'),
    ],
  },
  {
    id: 'hungry-disturbed-then-space', profile: 'hungry', expectedStage: 'negotiate',
    steps: ['food', 'approach', 'wait'].map(completedAction),
  },
  {
    id: 'food-exhausted-waiting-is-not-rescue', profile: 'hungry', expectedStage: 'negotiate',
    steps: [
      ...Array.from({ length: 3 }, () => completedAction('food')),
      { action: 'food', accepted: false },
      ...Array.from({ length: 20 }, () => completedAction('wait')),
    ],
  },
  {
    id: 'completed-session-rejects-input', profile: 'curious', expectedStage: 'success',
    steps: [
      ...successSequences.curious.map(completedAction),
      { action: 'wait', accepted: false },
      { action: 'invite', accepted: false },
    ],
  },
];

function snapshot(game) {
  const turn = game.turn;
  return {
    stage: game.stage, alert: turn.alert, distance: turn.distance, mood: turn.mood,
    food: turn.food, fed: turn.fed, round: turn.round, phase: turn.phase,
    lastAction: turn.lastAction, streak: turn.streak, delta: turn.delta,
    effect: turn.effect, rescued: turn.rescued, reaction: turn.reaction, logCount: turn.log.length,
  };
}

function capture(scenario) {
  const game = createOuting({ seed: 15, profile: scenario.profile, direct: true });
  const initial = snapshot(game);
  const steps = scenario.steps.map(step => {
    const before = structuredClone(game);
    const accepted = chooseAction(game, step.action);
    assert.equal(accepted, step.accepted, `${scenario.id}: ${step.action} acceptance`);
    if (!accepted) assert.deepEqual(game, before, `${scenario.id}: rejection must not mutate state`);
    const immediate = snapshot(game);
    if (step.settle) {
      for (let frame = 0; frame < 65; frame++) updateOuting(game, 0.02);
    }
    return { input: step, accepted, immediate, settled: snapshot(game) };
  });
  assert.equal(game.stage, scenario.expectedStage, scenario.id);
  return { id: scenario.id, profile: scenario.profile, initial, steps };
}

const sources = [];
for (const sourcePath of ['src/outing/engine.js', 'src/playlab/engine.js']) {
  const bytes = await readFile(path.join(repoRoot, sourcePath));
  sources.push({ path: sourcePath, sha256: createHash('sha256').update(bytes).digest('hex') });
}
const report = {
  formatVersion: 1,
  purpose: 'Reference traces for discussion and future paired tests; not approved production gameplay.',
  timestepSeconds: 0.02,
  settleFrames: 65,
  sources,
  scenarios: scenarios.map(capture),
};
await mkdir(reportDirectory, { recursive: true });
await writeFile(path.join(reportDirectory, 'negotiation-baseline-v1.json'), `${JSON.stringify(report, null, 2)}\n`);
console.log(`Captured ${report.scenarios.length} deterministic reference scenarios.`);
console.log('Report: test-results/negotiation-baseline-v1.json (HTML prototype only).');
