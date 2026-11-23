import {Bone, BufferAttribute, BufferGeometry, Group, Matrix4, MeshBasicMaterial, Skeleton, SkinnedMesh, Vector3, DoubleSide} from 'three';

export const DURATION=5.6;
export const smooth=t=>{t=Math.max(0,Math.min(1,t));return t*t*(3-2*t);};
const mix=(a,b,t)=>a+(b-a)*t;
const W=452,H=512;
export const SPEC=[
    ['root',226,256],['chest',155,337],['pelvis',315,363],['head',145,275],['tail',372,390],
    ['nearFrontUpper',146,355],['nearFrontLower',143,423],['nearFrontFoot',134,477],
    ['farFrontUpper',125,355],['farFrontLower',125,420],['farFrontFoot',111,475],
    ['nearHindUpper',308,365],['nearHindLower',280,434],['nearHindFoot',291,478],
    ['farHindUpper',278,365],['farHindLower',250,434],['farHindFoot',264,475],
];
const index=Object.fromEntries(SPEC.map(([id],i)=>[id,i]));
const point=(x,y)=>new Vector3(x-W/2,H/2-y,0);

export function sampleTransition(seconds) {
    if(!Number.isFinite(seconds)) throw new Error('Invalid time');
    const t=((seconds%DURATION)+DURATION)%DURATION;
    const progress=t<.65?0:t<1.85?smooth((t-.65)/1.2):t<3.35?1:t<4.85?1-smooth((t-3.35)/1.5):0;
    return {progress,phase:t<.65?'站立':t<1.85?'坐下':t<3.35?'坐姿':t<4.85?'站起':'站立'};
}

export function controlsAt(progress) {
    if(!Number.isFinite(progress)||progress<0||progress>1) throw new Error('Invalid posture');
    const p=progress, result=Object.fromEntries(SPEC.map(([id,x,y])=>[id,{x,y,angle:0}]));
    Object.assign(result.chest,{x:155+16*p,y:337-2*p,angle:.05*p});
    Object.assign(result.pelvis,{x:315-26*p,y:363+68*p,angle:-.08*p});
    Object.assign(result.head,{x:145+16*p,y:275-2*p,angle:0});
    Object.assign(result.tail,{x:372-26*p,y:390+58*p,angle:.8*p});
    for(const side of ['near','far']) {
        const front=result[`${side}FrontUpper`],lower=result[`${side}FrontLower`];
        front.x+=16*p;front.y-=2*p;front.angle=-.08*p;
        lower.x+=8*p;lower.angle=-.1*p;
        const upper=result[`${side}HindUpper`],shin=result[`${side}HindLower`];
        upper.x-=26*p;upper.y+=68*p;
        shin.x-=10*p;shin.y+=25*p;
    }
    return result;
}

export function weightsFor(id,x,y) {
    if(id==='head'||id==='tail') return [[index[id],1]];
    if(id==='torso') {const p=smooth((x-230)/140);return [[index.chest,1-p],[index.pelvis,p]];}
    if(id.endsWith('Hind')) {const p=smooth((y-315)/153);return [[index[`${id}Upper`],1-p],[index[`${id}Foot`],p]];}
    const lower=smooth((y-399)/43),foot=smooth((y-454)/20);
    return [[index[`${id}Upper`],(1-lower)*(1-foot)],[index[`${id}Lower`],lower*(1-foot)],[index[`${id}Foot`],foot]];
}

function geometryFor(layer,manifest) {
    const columns=Math.max(2,Math.ceil(layer.width/14)),rows=Math.max(2,Math.ceil(layer.height/14));
    const positions=[],uv=[],indices=[],weights=[],triangles=[];
    for(let row=0;row<=rows;row++) for(let col=0;col<=columns;col++) {
        const x=layer.left+col/columns*layer.width,y=layer.top+row/rows*layer.height;
        positions.push(x-W/2,H/2-y,0);
        uv.push((layer.atlasX+col/columns*layer.width)/manifest.width,1-(layer.atlasY+row/rows*layer.height)/manifest.height);
        const influence=weightsFor(layer.id,x,y);
        for(let k=0;k<4;k++){indices.push(influence[k]?.[0]||0);weights.push(influence[k]?.[1]||0);}
        if(row<rows&&col<columns){const a=row*(columns+1)+col,b=a+1,c=a+columns+1,d=c+1;triangles.push(a,c,b,b,c,d);}
    }
    const geometry=new BufferGeometry();
    geometry.setAttribute('position',new BufferAttribute(new Float32Array(positions),3));
    geometry.setAttribute('uv',new BufferAttribute(new Float32Array(uv),2));
    geometry.setAttribute('skinIndex',new BufferAttribute(new Uint16Array(indices),4));
    geometry.setAttribute('skinWeight',new BufferAttribute(new Float32Array(weights),4));
    geometry.setIndex(triangles);return geometry;
}

export function createTransitionRig(manifest,texture) {
    const group=new Group(),bones=SPEC.map(([id,x,y])=>{const bone=new Bone();bone.name=id;bone.position.copy(point(x,y));return bone;});
    // Control bones share one root and one Skeleton; no separate stand/sit body is ever made visible.
    group.add(bones[0]);
    for(let i=1;i<bones.length;i++){bones[i].position.sub(bones[0].position);bones[0].add(bones[i]);}
    group.updateMatrixWorld(true);
    const skeleton=new Skeleton(bones);skeleton.calculateInverses();
    const meshes=manifest.layers.map((layer,order)=>{
        const material=new MeshBasicMaterial({map:texture,transparent:true,depthWrite:false,depthTest:false,side:DoubleSide,alphaTest:.015});
        const mesh=new SkinnedMesh(geometryFor(layer,manifest),material);mesh.name=layer.id;mesh.renderOrder=order;
        mesh.bind(skeleton,new Matrix4());mesh.frustumCulled=false;group.add(mesh);return mesh;
    });
    function apply(progress) {
        const controls=controlsAt(progress);
        for(let i=1;i<bones.length;i++) {
            const c=controls[SPEC[i][0]];
            bones[i].position.copy(point(c.x,c.y)).sub(bones[0].position);bones[i].rotation.z=c.angle;
        }
        group.updateMatrixWorld(true);skeleton.update();
    }
    apply(0);
    return {group,bones,skeleton,meshes,apply,dispose(){for(const mesh of meshes){mesh.geometry.dispose();mesh.material.dispose();}skeleton.dispose();}};
}
