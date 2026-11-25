// 追加第二批登记：费用饭碗图标、Flux Kontext 试生成的四格母图。
import {readFile, writeFile} from 'node:fs/promises';

const catalogPath = 'art-source/resource-catalog.json';
const base = 'art-source/siege-codex-20261126';
const review = {visual: 'pending', rights: 'pending', target: 'pending'};

const entries = [
    {
        id: 'siege-codex.icon.cost',
        name: '情报费用图标·一碗饭',
        module: 'CommonUI',
        status: 'candidate',
        loadTiming: 'preview-only',
        availability: '逆塔防情报面板与手牌费用的通用小图标；尚未接入 Unity',
        use: '192×192 单色线稿饭碗，显示 20–24L，深绿 #2A3D30；代替"费用"文字',
        plannedBytes: 4096,
        files: [{role: 'candidate', path: `${base}/icons/cost-ink.png`}],
        generation: {tool: 'node art-source/siege-codex-20261126/make-icons.mjs', record: `${base}/icons/README.md`},
        review,
    },
    {
        id: 'siege-codex.kontext.dog-basics',
        name: 'Flux Kontext 狗侧道具试生成母图',
        module: 'CommonUI',
        status: 'candidate',
        loadTiming: 'preview-only',
        availability: '风格未通过评审：厚描边贴纸感，与现有水彩道具不一致；不可直接切片上线',
        use: '四格母图（骨头、飞盘、绳结、骨头风车），用于验证 Desktop 现有模型能否替代 Qwen 出图',
        plannedBytes: 786432,
        files: [{role: 'candidate', path: `${base}/kcx-dog-basics/master.png`}],
        generation: {tool: 'python art-source/siege-codex-20261126/generate-kontext.py kcx-dog-basics', record: `${base}/kcx-dog-basics/workflow.json`},
        review,
    },
];

const text = await readFile(catalogPath, 'utf8');
const marker = '\n  ]\n}';
const index = text.lastIndexOf(marker);
if (index < 0) throw new Error('catalog assets tail not found');
const head = text.slice(0, index);
const blocks = entries.map(entry => JSON.stringify(entry, null, 2).split('\n').map(l => '    ' + l).join('\n'));
await writeFile(catalogPath, head + ',\n' + blocks.join(',\n') + marker + '\n');
JSON.parse(await readFile(catalogPath, 'utf8'));
console.log(`registered ${entries.length}`);
