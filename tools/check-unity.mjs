import { readdir, readFile, access, mkdir, writeFile } from 'node:fs/promises';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const repo = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const project = path.join(repo, 'unity/HomelessPetHelper');
const assets = path.join(project, 'Assets');
const issues = [];
const assemblies = new Map();
const guids = new Map();
const manifest = JSON.parse(await readFile(path.join(project, 'Packages/manifest.json'), 'utf8'));
const packageAssemblyOwners = new Map([['UnityEngine.UI', 'com.unity.ugui']]);
let scriptCount = 0;

async function walk(folder) {
  const found = [];
  for (const entry of await readdir(folder, { withFileTypes: true })) {
    const fullPath = path.join(folder, entry.name);
    found.push({ fullPath, directory: entry.isDirectory() });
    if (entry.isDirectory()) found.push(...await walk(fullPath));
  }
  return found;
}

for (const { fullPath, directory } of await walk(assets)) {
  const relative = path.relative(project, fullPath).replaceAll('\\', '/');
  const complain = message => issues.push(`${relative}: ${message}`);
  if (!fullPath.endsWith('.meta')) {
    try { await access(`${fullPath}.meta`); } catch { complain('missing .meta'); }
  }
  if (directory) {
    if (path.basename(fullPath) === 'Resources') complain('Resources directory is not allowed');
    continue;
  }
  if (fullPath.endsWith('.meta')) {
    const content = await readFile(fullPath, 'utf8');
    const guid = /^guid: ([a-f0-9]{32})$/m.exec(content)?.[1];
    if (!guid) complain('missing or invalid GUID');
    else if (guids.has(guid)) complain(`duplicate GUID with ${guids.get(guid)}`);
    else guids.set(guid, relative);
  }
  if (fullPath.endsWith('.asmdef')) {
    const assembly = JSON.parse(await readFile(fullPath, 'utf8'));
    if (assemblies.has(assembly.name)) complain('duplicate assembly name');
    assemblies.set(assembly.name, assembly);
    if (relative.startsWith('Assets/Scripts/Editor/') && !assembly.includePlatforms?.includes('Editor')) {
      complain('Editor assembly is not Editor-only');
    }
  }
  if (!fullPath.endsWith('.cs')) continue;
  scriptCount++;
  const bytes = await readFile(fullPath);
  const content = new TextDecoder('utf-8', { fatal: true }).decode(bytes);
  if (bytes[0] === 0xef && bytes[1] === 0xbb && bytes[2] === 0xbf) complain('UTF-8 BOM is not allowed');
  if (content.includes('\r')) complain('expected LF newlines');
  if (!content.endsWith('\n')) complain('missing final newline');
  for (const [index, line] of content.split('\n').entries()) {
    if (line.includes('\t') || / +$/.test(line)) complain(`line ${index + 1}: tab or trailing whitespace`);
    if (line.length > 120) complain(`line ${index + 1}: over 120 characters`);
    if ((/^ */.exec(line)[0].length % 4) !== 0) complain(`line ${index + 1}: indentation is not 4 spaces`);
  }
  const folder = path.dirname(relative).replace(/^Assets\/Scripts\//, '').replace(/^Assets\//, '');
  const expectedNamespace = `HomelessPetHelper.${folder.replaceAll('/', '.')}`;
  if (!content.includes(`namespace ${expectedNamespace}\n{`)) complain(`expected namespace ${expectedNamespace}`);
  const mainType = path.basename(fullPath, '.cs');
  if (!new RegExp(`\\b(class|enum|struct|interface) ${mainType}\\b`).test(content)) complain('file/type name mismatch');
  if (relative.startsWith('Assets/Scripts/') && !relative.startsWith('Assets/Scripts/Editor/')) {
    if (/\bUnityEditor\b/.test(content)) complain('runtime code references UnityEditor');
  }
}

function visit(name, stack = []) {
  if (stack.includes(name)) {
    issues.push(`Assembly cycle: ${[...stack, name].join(' -> ')}`);
    return;
  }
  const assembly = assemblies.get(name);
  for (const reference of assembly.references ?? []) {
    const dependency = assemblies.get(reference);
    if (!dependency && manifest.dependencies?.[packageAssemblyOwners.get(reference)]) continue;
    if (!dependency) { issues.push(`${name}: unresolved local assembly ${reference}`); continue; }
    if (!assembly.includePlatforms?.includes('Editor') && dependency.includePlatforms?.includes('Editor')) {
      issues.push(`${name}: runtime assembly references Editor-only ${reference}`);
    }
    visit(reference, [...stack, name]);
  }
}
for (const name of assemblies.keys()) visit(name);

const report = {
  tool: 'check-unity.mjs v1',
  scope: 'Basic source formatting, namespaces, metadata and local assembly graph; not a full C# analyzer.',
  scripts: scriptCount,
  assemblies: assemblies.size,
  issues
};
const reportDirectory = path.join(repo, 'test-results/unity');
await mkdir(reportDirectory, { recursive: true });
await writeFile(path.join(reportDirectory, 'source-check.json'), `${JSON.stringify(report, null, 2)}\n`);
console.log(JSON.stringify(report, null, 2));
if (issues.length > 0) process.exitCode = 1;
