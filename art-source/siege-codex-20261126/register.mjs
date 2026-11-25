// 把本轮 9 张图登记进 art-source/resource-catalog.json（只在 assets 数组尾部插入，保留原格式）。
import {readFile, writeFile} from 'node:fs/promises';

const catalogPath = 'art-source/resource-catalog.json';
const base = 'art-source/siege-codex-20261126';
const review = {visual: 'pending', rights: 'pending', target: 'pending'};
const icons = {
    durability: ['耐久', '打了补丁的心形'],
    power: ['威力', '爪印'],
    attack: ['出手', '沙漏'],
    range: ['射程', '圆点 + 箭头'],
    speed: ['步速', '双箭头'],
    delivery: ['抵达', '小旗'],
    merge: ['合体', '两圆合成大圆'],
};

const entries = Object.entries(icons).map(([key, [label, shape]]) => ({
    id: `siege-codex.icon.${key}`,
    name: `情报数值图标·${label}`,
    module: 'CommonUI',
    status: 'candidate',
    loadTiming: 'preview-only',
    availability: '逆塔防情报面板的数值行小图标；尚未接入 Unity，未做真机验收',
    use: `192×192 单色线稿（${shape}），显示 16–24L，深绿 #2A3D30，用于奶油纸底`,
    plannedBytes: 4096,
    files: [{role: 'candidate', path: `${base}/icons/${key}-ink.png`}],
    generation: {
        tool: 'node art-source/siege-codex-20261126/make-icons.mjs',
        record: `${base}/icons/README.md`,
    },
    review,
}));

entries.push({
    id: 'siege-codex.icons.preview',
    name: '情报数值图标条预览',
    module: 'CommonUI',
    status: 'candidate',
    loadTiming: 'preview-only',
    availability: '评审用图标条；不是游戏内资源',
    use: '7 个图标按 96px 并排、奶油底，用于确认线宽与辨识度',
    plannedBytes: 65536,
    files: [{role: 'candidate', path: `${base}/icons/preview.png`}],
    generation: {tool: 'node art-source/siege-codex-20261126/make-icons.mjs', record: `${base}/icons/README.md`},
    review,
});

entries.push({
    id: 'siege-codex.panel.preview',
    name: '情报面板版式预览',
    module: 'CommonUI',
    status: 'candidate',
    loadTiming: 'preview-only',
    availability: '评审用版式图；字体为系统字、卡图为 64px 运行图放大，均非最终 UI',
    use: '780×1010（2x）展示面板标题、双 tab、我方道具卡与对方机关卡字段顺序',
    plannedBytes: 262144,
    files: [{role: 'candidate', path: `${base}/card-preview.png`}],
    generation: {tool: 'node art-source/siege-codex-20261126/make-card-preview.mjs', record: 'docs/逆塔防单位情报与首战指引设计.md'},
    review,
});

const text = await readFile(catalogPath, 'utf8');
const marker = '\n  ]\n}';
const index = text.lastIndexOf(marker);
if (index < 0) throw new Error('catalog assets tail not found');
const head = text.slice(0, index);
const blocks = entries.map(entry => JSON.stringify(entry, null, 2).split('\n').map(l => '    ' + l).join('\n'));
await writeFile(catalogPath, head + ',\n' + blocks.join(',\n') + marker + '\n');
JSON.parse(await readFile(catalogPath, 'utf8'));
console.log(`registered ${entries.length}`);
