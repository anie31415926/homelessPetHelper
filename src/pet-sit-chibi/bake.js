import {createChibiSkin} from './skin.js';

export function bakeAppearance(profile, manifest, {afterSit, prepareSkin} = {}) {
    const skin = createChibiSkin(manifest, null, profile), round = value => Math.round(value * 1e7) / 1e7;
    try {
        prepareSkin?.(skin);
        const sit = progress => {skin.apply(progress); afterSit?.(skin, progress);};
        const bones = skin.skeleton.bones.map(bone => ({id: bone.name, parent: skin.skeleton.bones.indexOf(bone.parent),
            position: bone.position.toArray().map(round), rotation: bone.quaternion.toArray().map(round)}));
        const bindposes = skin.skeleton.boneInverses.map(matrix => matrix.elements.map(round));
        const meshes = skin.meshes.map(mesh => {
            const a = mesh.geometry.attributes;
            return {id: mesh.name, order: Math.round(mesh.renderOrder * 100), tint: mesh.material.color.getHexString(),
                positions: Array.from(a.position.array, round), uv: Array.from(a.uv.array, round), triangles: Array.from(mesh.geometry.index.array),
                indices: Array.from(a.skinIndex.array), weights: Array.from(a.skinWeight.array, round)};
        });
        const bonePath = bone => bone.parent?.isBone ? bonePath(bone.parent) + '/' + bone.name : bone.name;
        function reduce(track, tolerance) {
            const keep = new Set([0, track.times.length - 1]);
            function visit(a, b) {
                let worst = tolerance, chosen = -1;
                for (let i = a + 1; i < b; i++) {
                    const predicted = track.values[a] + (track.values[b] - track.values[a]) * (track.times[i] - track.times[a]) / (track.times[b] - track.times[a]);
                    if (Math.abs(track.values[i] - predicted) > worst) {worst = Math.abs(track.values[i] - predicted); chosen = i;}
                }
                if (chosen >= 0) {keep.add(chosen); visit(a, chosen); visit(chosen, b);}
            }
            visit(0, track.times.length - 1);
            const indices = [...keep].sort((a, b) => a - b);
            return {...track, times: indices.map(i => track.times[i]), values: indices.map(i => track.values[i])};
        }
        function bake(id, duration, sample) {
            const count = Math.ceil(duration * 60), tracks = [];
            for (const bone of skin.skeleton.bones) for (const [prefix, axes] of [['m_LocalPosition', ['x','y','z']], ['m_LocalRotation', ['x','y','z','w']]])
                for (const axis of axes) tracks.push({path: bonePath(bone), property: prefix + '.' + axis, times: [], values: []});
            const previous = skin.skeleton.bones.map(() => null);
            for (let frame = 0; frame <= count; frame++) {
                const seconds = duration * frame / count; sample(seconds);
                let index = 0;
                skin.skeleton.bones.forEach((bone, b) => {
                    const q = bone.quaternion.clone();
                    if (previous[b] && q.dot(previous[b]) < 0) q.set(-q.x, -q.y, -q.z, -q.w);
                    previous[b] = q;
                    for (const vector of [bone.position, q]) for (const axis of vector === q ? ['x','y','z','w'] : ['x','y','z']) {
                        tracks[index].times.push(round(seconds)); tracks[index++].values.push(round(vector[axis]));
                    }
                });
            }
            // Keep sibling components to preserve Unity's grouped vector/quaternion sampling.
            for (const track of tracks) if (track.values.every(value => Math.abs(value - track.values[0]) < 1e-7)) {
                track.times = [0, round(duration)]; track.values = [track.values[0], track.values[0]];
            }
            return {id, duration, curves: tracks.map(track => reduce(track, .00003))};
        }
        const stride = profile.walk.seconds;
        const takes = [bake('SitDown', profile.sit.seconds, t => sit(t / profile.sit.seconds)),
            bake('Tread', stride, t => {skin.apply(0, 2 * Math.PI * t / stride, 1); afterSit?.(skin, 0);})];
        const fixtures = [];
        for (const progress of [0, .035, .2, .37, .55, .7, .86, 1]) {
            sit(progress);
            for (const mesh of skin.meshes) {
                const indices = [0, Math.floor(mesh.geometry.attributes.position.count / 2), mesh.geometry.attributes.position.count - 1];
                fixtures.push({progress, id: mesh.name, indices, vertices: indices.flatMap(index => mesh.getVertexPosition(index, mesh.position.clone()).toArray().map(round))});
            }
        }
        const data = {schema: 2, appearanceId: profile.id, template: profile.template, unity: profile.unity, framing: profile.framing, sampleRate: 60, bones, bindposes, meshes, takes, fixtures, tailMotion: {...profile.tail, joinLocal: skin.tailJoinLocal.toArray().map(round)}};
        return data;
    } finally {skin.dispose();}
}
