import {Bone, BufferAttribute, BufferGeometry, Group, Matrix4, MeshBasicMaterial, Skeleton, SkinnedMesh, Vector3, DoubleSide} from 'three';
import {CCDIKSolver} from 'three/addons/animation/CCDIKSolver.js';

export const DURATION=5.6;
export const smooth=t=>{t=Math.max(0,Math.min(1,t));return t*t*(3-2*t);};
const mix=(a,b,t)=>a+(b-a)*t;
const W=452,H=512;
export const SPEC=[
    ['root',226,256],['chest',155,337],['pelvis',315,363],['head',145,275],['tail',372,390],
    ['nearFrontUpper',146,355],['nearFrontLower',143,423],['nearFrontFoot',134,477],
    ['farFrontUpper',125,355],['farFrontLower',125,420],['farFrontFoot',111,475],
    ['nearHindUpper',337,350],['nearHindLower',275,420],['nearHindHock',330,451],['nearHindFoot',291,477],['nearHindTarget',291,477],
    ['farHindUpper',307,344],['farHindLower',245,414],['farHindHock',300,445],['farHindFoot',261,471],['farHindTarget',261,471],
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
        const upper=result[`${side}HindUpper`];
        upper.x-=26*p;upper.y+=55*p;upper.angle=-.65*p;
    }
    return result;
}

export function weightsFor(id,x,y) {
    if(id==='head'||id==='tail') return [[index[id],1]];
    if(id==='torso') {const p=smooth((x-230)/140);return [[index.chest,1-p],[index.pelvis,p]];}
    if(id.includes('Hind')) {
        const limb=id.startsWith('near')?'nearHind':'farHind';
        if(id.endsWith('Thigh'))return [[index[`${limb}Upper`],1]];
        if(id.endsWith('Paw'))return [[index[`${limb}Target`],1]];
        return [[index[`${limb}Hock`],1]];
    }
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
    const parents={};
    for(const side of ['near','far']) {
        parents[`${side}HindLower`]=`${side}HindUpper`;
        parents[`${side}HindHock`]=`${side}HindLower`;
        parents[`${side}HindFoot`]=`${side}HindHock`;
    }
    group.add(bones[0]);
    for(let i=bones.length-1;i>0;i--){
        const parent=bones[index[parents[SPEC[i][0]]||'root']];
        bones[i].position.sub(parent.position);parent.add(bones[i]);
    }
    group.updateMatrixWorld(true);
    const skeleton=new Skeleton(bones);skeleton.calculateInverses();
    const meshes=manifest.layers.map((layer,order)=>{
        const material=new MeshBasicMaterial({map:texture,transparent:true,depthWrite:false,depthTest:false,side:DoubleSide,alphaTest:.015});
        const mesh=new SkinnedMesh(geometryFor(layer,manifest),material);mesh.name=layer.id;mesh.renderOrder=order;
        mesh.bind(skeleton,new Matrix4());mesh.frustumCulled=false;group.add(mesh);return mesh;
    });
    const rest=bones.map(b=>b.position.clone());
    const iks=['near','far'].map(side=>({
        target:index[`${side}HindTarget`],effector:index[`${side}HindFoot`],iteration:50,maxAngle:.15,
        links:['Hock','Lower'].map(joint=>({index:index[`${side}Hind${joint}`],limitation:new Vector3(0,0,1),
            rotationMin:new Vector3(0,0,0),rotationMax:new Vector3(0,0,2.5)})),
    }));
    const solver=new CCDIKSolver(meshes[0],iks);
    function apply(progress) {
        const controls=controlsAt(progress);
        for(let i=1;i<bones.length;i++) {
            const c=controls[SPEC[i][0]];
            bones[i].position.copy(rest[i]);bones[i].rotation.set(0,0,0);
            if(!parents[SPEC[i][0]]) {
                bones[i].position.copy(point(c.x,c.y)).sub(bones[0].position);bones[i].rotation.z=c.angle;
            }
        }
        // Reset before every solve so seeking and reverse playback do not depend on the preceding frame.
        for(const side of ['near','far']) {
            bones[index[`${side}HindLower`]].rotation.z=.8*progress;
            bones[index[`${side}HindHock`]].rotation.z=.4*progress;
        }
        group.updateMatrixWorld(true);solver.update();
        for(const side of ['near','far']) {
            const foot=bones[index[`${side}HindFoot`]],hock=foot.parent;
            foot.rotation.z=-(hock.rotation.z+hock.parent.rotation.z+hock.parent.parent.rotation.z);
        }
        group.updateMatrixWorld(true);skeleton.update();
    }
    apply(0);
    return {group,bones,skeleton,meshes,apply,dispose(){for(const mesh of meshes){mesh.geometry.dispose();mesh.material.dispose();}skeleton.dispose();}};
}
