import {Color, Group, OrthographicCamera, Scene, SkeletonHelper, SRGBColorSpace, TextureLoader, WebGLRenderer} from 'three';
import {createTransitionRig} from './rig.js';

export async function createPreview(host) {
    const renderer=new WebGLRenderer({antialias:true,alpha:true,preserveDrawingBuffer:true});
    renderer.setPixelRatio(Math.min(devicePixelRatio||1,2));renderer.outputColorSpace=SRGBColorSpace;
    renderer.setClearColor(new Color('#ffffff'),0);renderer.domElement.setAttribute('aria-label','橘猫同骨架站坐过渡');host.append(renderer.domElement);
    let rig,texture,observer,helper;
    const scene=new Scene(),camera=new OrthographicCamera(-300,300,300,-300,.1,100);
    camera.position.z=10;
    try {
        const response=await fetch('/pet-transition/atlas.json');if(!response.ok) throw new Error('图集清单加载失败');
        const manifest=await response.json();texture=await new TextureLoader().loadAsync('/pet-transition/atlas.png');texture.colorSpace=SRGBColorSpace;
        rig=createTransitionRig(manifest,texture);scene.add(rig.group);
        helper=new Group();
        for(const side of ['near','far']) {
            const chain=new SkeletonHelper(rig.bones.find(b=>b.name===`${side}HindUpper`));
            chain.geometry.setDrawRange(2,chain.geometry.attributes.position.count-2);
            chain.material.depthTest=false;chain.renderOrder=50;helper.add(chain);
        }
        scene.add(helper);
        const resize=()=>{
            const {width,height}=host.getBoundingClientRect(),aspect=Math.max(1,width)/Math.max(1,height);
            const half=Math.max(288,275/aspect);camera.top=half;camera.bottom=-half;camera.left=-half*aspect;camera.right=half*aspect;
            camera.updateProjectionMatrix();renderer.setSize(width,height,false);render(current);
        };
        let current={progress:0,bones:false,wire:false};
        function render(state) {
            current=state;rig.apply(state.progress);helper.visible=state.bones;
            renderer.domElement.dataset.progress=String(state.progress);
            for(const mesh of rig.meshes) mesh.material.wireframe=state.wire;
            renderer.render(scene,camera);
        }
        observer=new ResizeObserver(resize);observer.observe(host);resize();
        return {render,rig,renderer,camera,dispose};
    } catch(error) {dispose();throw error;}
    function dispose(){observer?.disconnect();helper?.children.forEach(h=>{h.geometry.dispose();h.material.dispose();});rig?.dispose();texture?.dispose();renderer.dispose();renderer.domElement.remove();}
}
