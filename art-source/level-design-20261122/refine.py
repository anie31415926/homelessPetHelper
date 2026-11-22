import json, urllib.request, time, shutil
from pathlib import Path
from PIL import Image
folder=Path(__file__).resolve().parent/'clearing-v2'
workflow=json.loads((folder/'workflow.json').read_text(encoding='utf-8'))
api='http://127.0.0.1:8188'
def save(name,value):
 (folder/name).write_text(json.dumps(value,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
request=urllib.request.Request(api+'/prompt',data=json.dumps(dict(prompt=workflow['prompt'],client_id='level-design-20261122')).encode(),headers={'Content-Type':'application/json'})
with urllib.request.urlopen(request,timeout=30) as response: submission=json.load(response)
save('submission.json',submission)
pid=submission['prompt_id']
print('Submitted clearing-v2',pid,flush=True)
while True:
 with urllib.request.urlopen(api+'/history/'+pid,timeout=20) as response: result=json.load(response)
 if pid in result:
  entry=result[pid];save('history.json',entry)
  if entry.get('status',{}).get('status_str')=='error':raise RuntimeError('ComfyUI refinement failed')
  image=entry['outputs']['9']['images'][0]
  source=Path('C:/Users/Admin/ComfyUI-Shared/output')/image['subfolder']/image['filename']
  shutil.copy2(source,folder/'master.png')
  Image.open(source).convert('RGB').save(folder/'concept.jpg',quality=93)
  print('Completed clearing-v2',flush=True);break
 time.sleep(8)
