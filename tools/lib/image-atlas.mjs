// Offline raster atlas helpers. No file IO, scaling, rotation, gameplay rules, or global sharp dependency.
function validateLayout({width,height,regions}) {
    if(!Number.isInteger(width) || !Number.isInteger(height) || width<=0 || height<=0)
        throw new Error('Atlas dimensions must be positive integers');
    const ids=new Set();
    for(const r of regions) {
        if(typeof r.id!=='string' || !r.id || ids.has(r.id)) throw new Error(`Missing/duplicate atlas region id: ${r.id}`);
        ids.add(r.id);
        if(!['x','y','width','height'].every(k=>Number.isInteger(r[k])) || r.x<0 || r.y<0 ||
            r.width<=0 || r.height<=0 || r.x+r.width>width || r.y+r.height>height)
            throw new Error(`Atlas region outside page: ${r.id}`);
    }
    for(let i=0;i<regions.length;i++) for(let j=0;j<i;j++) {
        const a=regions[i],b=regions[j];
        if(a.x<b.x+b.width && b.x<a.x+a.width && a.y<b.y+b.height && b.y<a.y+a.height)
            throw new Error(`Overlapping atlas regions: ${a.id}/${b.id}`);
    }
}

function packWidth(regions, width, maxSize, alignment) {
    let free=[{x:0,y:0,width,height:maxSize}];
    const placed=[];
    for(const region of [...regions].sort((a,b)=>b.height-a.height || (a.id<b.id?-1:a.id>b.id?1:0))) {
        const candidates=free.filter(r=>r.width>=region.width && r.height>=region.height);
        const score=r=>Math.min(r.width-region.width,r.height-region.height);
        candidates.sort((a,b)=>score(a)-score(b) || a.y-b.y || a.x-b.x);
        if(!candidates.length) return null;
        const p={...region,x:candidates[0].x,y:candidates[0].y};
        placed.push(p);
        free=free.flatMap(r=>{
            const right=r.x+r.width,bottom=r.y+r.height,pr=p.x+p.width,pb=p.y+p.height;
            if(p.x>=right || pr<=r.x || p.y>=bottom || pb<=r.y) return [r];
            const next=[];
            if(p.x>r.x) next.push({...r,width:p.x-r.x});
            if(pr<right) next.push({...r,x:pr,width:right-pr});
            if(p.y>r.y) next.push({...r,height:p.y-r.y});
            if(pb<bottom) next.push({...r,y:pb,height:bottom-pb});
            return next;
        });
        free=free.filter((r,i)=>!free.some((s,j)=>j!==i && s.x<=r.x && s.y<=r.y &&
            s.x+s.width>=r.x+r.width && s.y+s.height>=r.y+r.height &&
            (s.width*s.height>r.width*r.height || j<i)));
    }
    return {width,height:Math.ceil(Math.max(...placed.map(r=>r.y+r.height))/alignment)*alignment,regions:placed};
}

/** Stable MaxRects packing. Integer pixel regions are neither rotated nor resized; metadata is retained. */
export function packAtlasRegions(regions, {maxSize=1024,alignment=4}={}) {
    if(!Number.isInteger(maxSize) || !Number.isInteger(alignment) || alignment<=0 || maxSize<alignment)
        throw new Error('Invalid atlas packing limits');
    if(!regions.length) throw new Error('Cannot pack an empty atlas');
    const ids=new Set();
    for(const r of regions) {
        if(typeof r.id!=='string' || !r.id || ids.has(r.id) || !Number.isInteger(r.width) || !Number.isInteger(r.height) ||
            r.width<=0 || r.height<=0 || r.width>maxSize || r.height>maxSize)
            throw new Error(`Invalid atlas region: ${r.id}`);
        ids.add(r.id);
    }
    let best;
    for(let width=Math.ceil(Math.max(...regions.map(r=>r.width))/alignment)*alignment;width<=maxSize;width+=alignment) {
        const candidate=packWidth(regions,width,maxSize,alignment);
        if(candidate && candidate.height<=maxSize && (!best || width*candidate.height<best.width*best.height)) best=candidate;
    }
    if(!best) throw new Error(`Atlas does not fit a ${maxSize}px page`);
    best.regions.sort((a,b)=>regions.findIndex(r=>r.id===a.id)-regions.findIndex(r=>r.id===b.id));
    validateLayout(best);
    return best;
}

/** Compose an explicit layout (packed or fixed), preserving every RGBA byte before PNG encoding. */
export async function composeAtlas(sharp, layout, images, pngOptions={}) {
    validateLayout(layout);
    const output=Buffer.alloc(layout.width*layout.height*4);
    for(const r of layout.regions) {
        const input=images.get(r.id);
        if(!input) throw new Error(`Missing atlas image: ${r.id}`);
        const {data,info}=await sharp(input).ensureAlpha().raw().toBuffer({resolveWithObject:true});
        if(info.width!==r.width || info.height!==r.height) throw new Error(`Atlas image size mismatch: ${r.id}`);
        // Alpha compositing can round translucent RGB or clear earlier tiles with source blending.
        for(let y=0;y<r.height;y++) data.copy(output,((r.y+y)*layout.width+r.x)*4,y*r.width*4,(y+1)*r.width*4);
    }
    return sharp(output,{raw:{width:layout.width,height:layout.height,channels:4}}).png(pngOptions).toBuffer();
}
