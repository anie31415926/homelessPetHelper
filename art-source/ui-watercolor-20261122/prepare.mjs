import sharp from 'sharp';
const base='art-source/ui-watercolor-20261122';
const guides=[['paper',768,768,'<rect x="48" y="48" width="672" height="672" rx="44" fill="#f5edd7" stroke="#b09b73" stroke-width="3"/>'],['button',768,256,'<rect x="38" y="40" width="692" height="176" rx="30" fill="#c6cdb2" stroke="#788c64" stroke-width="3"/>'],['symbols',1024,512,'']];
for(const [name,w,h,shape] of guides){
 const svg=`<svg xmlns="http://www.w3.org/2000/svg" width="${w}" height="${h}"><rect width="100%" height="100%" fill="white"/>${shape}</svg>`;
 await sharp(Buffer.from(svg)).png().toFile(`${base}/${name}-guide.png`);
}
