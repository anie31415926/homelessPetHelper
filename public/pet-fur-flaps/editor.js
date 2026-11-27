import { axis, lightFields, maxPatches, patchCount, regionFields, regionOutline, validateProfile } from './profile.js';

export function attachEditor(main, profile, apply) {
    const original = structuredClone(profile);
    const toggle = main.querySelector('.tune');
    const panel = main.querySelector('.editor');
    const picker = panel.querySelector('.region-picker');
    const fields = panel.querySelector('.editor-fields');
    const count = panel.querySelector('.patch-count');
    const guide = main.querySelector('.region-guide');
    const showGuide = panel.querySelector('.guide-toggle input');
    const region = () => profile.regions[Number(picker.value)];

    for (const [index, entry] of profile.regions.entries()) picker.add(new Option(entry.label, String(index)));
    const crop = profile.portrait.crop;
    guide.setAttribute('viewBox', `${crop.x} ${crop.y} ${crop.width} ${crop.height}`);

    function refreshGuide() {
        guide.toggleAttribute('data-visible', !panel.hidden && showGuide.checked);
        const entry = region();
        guide.querySelector('polygon').setAttribute('points', regionOutline(entry).map(point => point.join(',')).join(' '));
        const [gx, gy] = axis(entry.angle);
        const length = Math.min(33, entry.length + (entry.layers > 1 ? entry.depth : 0));
        const x = entry.x + gx * length;
        const y = entry.y + gy * length;
        guide.querySelector('polyline').setAttribute('points',
            `${entry.x},${entry.y} ${x},${y} ${x - gx * 7 + gy * 4},${y - gy * 7 - gx * 4} ${x},${y} ${x - gx * 7 - gy * 4},${y - gy * 7 + gx * 4}`);
    }

    function updateCount() {
        count.value = `${patchCount(profile.regions)} / ${maxPatches}`;
        const entry = region();
        const available = maxPatches - patchCount(profile.regions) + entry.columns * entry.layers;
        fields.querySelector('[data-field="columns"]').max = Math.min(9, Math.floor(available / entry.layers));
        fields.querySelector('[data-field="layers"]').max = Math.min(5, Math.floor(available / entry.columns));
    }

    function makeField([key, label, min, max, step, unit = ''], settings, light) {
        const row = document.createElement('label');
        const name = document.createElement('span');
        const input = document.createElement('input');
        const value = document.createElement('output');
        row.className = 'editor-field';
        name.textContent = label;
        input.type = 'range';
        input.dataset.field = key;
        input.setAttribute('aria-label', label);
        input.min = min;
        input.max = key === 'x' ? profile.portrait.width : key === 'y' ? profile.portrait.height : max;
        input.step = step;
        input.value = settings[key];
        function updateValue() { value.value = `${input.value}${unit}`; }
        updateValue();
        input.addEventListener('input', () => {
            const before = settings[key];
            settings[key] = input.valueAsNumber;
            try { validateProfile(profile); }
            catch {
                settings[key] = before;
                input.value = before;
                return;
            }
            updateValue();
            updateCount();
            refreshGuide();
            apply(light ? 'light' : 'geometry');
        });
        row.append(name, input, value);
        return row;
    }

    function showFields() {
        const entry = region();
        fields.replaceChildren(...regionFields.map(field => makeField(field, entry, false)),
            ...lightFields.map(field => makeField(field, profile.light, true)));
        updateCount();
        refreshGuide();
    }

    toggle.addEventListener('click', () => {
        panel.hidden = !panel.hidden;
        toggle.setAttribute('aria-expanded', String(!panel.hidden));
        main.classList.toggle('editor-open', !panel.hidden);
        refreshGuide();
    });
    picker.addEventListener('change', showFields);
    showGuide.addEventListener('change', refreshGuide);
    panel.querySelector('.defaults').addEventListener('click', () => {
        profile.regions = structuredClone(original.regions);
        profile.light = { ...original.light };
        showFields();
        apply('restore');
    });
    panel.querySelector('.download').addEventListener('click', () => {
        const blob = new Blob([JSON.stringify(profile, null, 2) + '\n'], { type: 'application/json' });
        const url = URL.createObjectURL(blob);
        const link = document.createElement('a');
        link.href = url;
        link.download = `${profile.id}-fur.json`;
        link.click();
        setTimeout(() => URL.revokeObjectURL(url), 1000);
    });
    showFields();
}
