import fs from 'node:fs/promises';
import { chromium } from 'playwright';

const base = 'art-source/forest-edge-20261121';
const catalog = JSON.parse(await fs.readFile('art-source/resource-catalog.json', 'utf8'));
const names = { parking: '单车棚', yard: '居民小院', porch: '临街门廊', park: '林缘公园', market: '市场后院', riverside: '河岸步道', shelter: '救助站' };
async function dataUrl(id) {
    const file = catalog.assets.find(a => a.id === id)?.files.find(f => f.role === 'candidate');
    if (!file) throw new Error(`Missing candidate: ${id}`);
    const mime = file.path.endsWith('.png') ? 'image/png' : 'image/jpeg';
    return `data:${mime};base64,${(await fs.readFile(file.path)).toString('base64')}`;
}
async function cards(prefix, slugs, type) {
    const result = [];
    for (const slug of slugs) result.push(`<article class="${type}"><h2>${names[slug]}</h2><img src="${await dataUrl(`${prefix}${slug}`)}" /></article>`);
    return result.join('');
}
const scenes = await cards('rescue.forest-edge.scene.', ['parking', 'yard', 'porch', 'park', 'market', 'riverside'], 'scene');
const landmarks = await cards('city-map.forest-edge.place.', Object.keys(names), 'landmark');
const style = `<style>*{box-sizing:border-box}body{margin:0;padding:24px;background:#faf5e9;color:#605743;font-family:'Microsoft YaHei',sans-serif;width:1200px}h1{font-size:27px;margin:0 0 6px}p{font-size:16px;margin:0 0 20px;color:#827560}main{display:grid;gap:16px}h2{font-size:20px;font-weight:500;margin:10px 0;text-align:center}article{background:#fffbf2;border:1px solid #e5dcc8;border-radius:8px;overflow:hidden}img{display:block;margin:auto}.scene img{width:100%;height:auto}.landmark img{max-width:100%;max-height:220px;object-fit:contain}.landmark{height:278px}.scenes{grid-template-columns:repeat(3,1fr)}.landmarks{grid-template-columns:repeat(4,1fr)}</style>`;
const browser = await chromium.launch({ executablePath: 'C:/Program Files/Google/Chrome/Application/chrome.exe', headless: true, args: ['--disable-gpu'] });
try {
    const page = await browser.newPage({ viewport: { width: 1200, height: 1200 }, deviceScaleFactor: 1 });
    for (const [name, title, description, body] of [
        ['scenes-review', '六处地点 · 进入后的现场背景', '水彩候选｜中央留作玩法操作区，动物、障碍、文字与按钮后续独立叠加', scenes],
        ['landmarks-review', '林缘社区 · 七处独立地标', '透明 PNG 候选｜六个外出地点 + 救助站；展示名称不在实际素材中', landmarks],
    ]) {
        const className = name.startsWith('scenes') ? 'scenes' : 'landmarks';
        const html = `<!doctype html><meta charset="utf-8">${style}<h1>${title}</h1><p>${description}</p><main class="${className}">${body}</main>`;
        await fs.writeFile(`${base}/${name}.html`, html);
        await page.setContent(html);
        await page.evaluate(async () => { await document.fonts.ready; await Promise.all([...document.images].map(i => i.decode())); });
        await page.locator('body').screenshot({ path: `${base}/${name}.jpg`, type: 'jpeg', quality: 92 });
    }
} finally { await browser.close(); }
console.log('Saved scenes-review.jpg and landmarks-review.jpg.');
