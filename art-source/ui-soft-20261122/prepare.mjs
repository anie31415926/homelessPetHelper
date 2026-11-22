import sharp from 'sharp';
const base='art-source/ui-soft-20261122';
const guides=[['paper',768,768,'<rect x="48" y="48" width="672" height="672" rx="80" fill="#f7f9f0" stroke="#aec9aa" stroke-width="3"/>'],['button',768,256,'<rect x="38" y="40" width="692" height="176" rx="84" fill="#b4d8a5" stroke="#86b38b" stroke-width="3"/>'],['symbols',1024,512,''],['knob',384,384,'<circle cx="192" cy="192" r="146" fill="#faf8ef" stroke="#a9c3a1" stroke-width="3"/>']];
for(const [name,w,h,shape] of guides){await sharp(Buffer.from(`<svg xmlns="http://www.w3.org/2000/svg" width="${w}" height="${h}"><rect width="100%" height="100%" fill="white"/>${shape}</svg>`)).png().toFile(`${base}/${name}-guide.png`);}
