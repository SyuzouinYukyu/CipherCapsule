"""Publish v1.1.10 without showing GUI or changing the user's registry/settings."""
from pathlib import Path
import subprocess,sys,shutil,json,hashlib,datetime
s=Path(__file__).resolve().parents[1];w=s.parent/'Work';w.mkdir(exist_ok=True)
def sha(p):
 with p.open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
if sys.argv[1:]!=['--stage']:raise SystemExit('Use --stage')
stage=w/'stage1110'
if stage.exists():raise SystemExit('Preserve existing stage1110; inspect before republishing')
assert sha(s/'vendor/libsodium.dll')==sha(s.parent.with_name('v1.1.9')/'Source/vendor/libsodium.dll') if (s.parent.with_name('v1.1.9')/'Source/vendor/libsodium.dll').exists() else (s/'vendor/libsodium.dll').is_file()
def run(label,args):
 p=w/(label+'.log')
 if p.exists():raise SystemExit('Preserve existing log: '+str(p))
 with p.open('w',encoding='utf-8') as f:subprocess.run(args,cwd=s,stdout=f,stderr=subprocess.STDOUT,check=True)
for app in ['Viewer','Builder']:
 project='src/'+app+'/'+app+'.csproj'
 run(app.lower()+'-restore1110',['dotnet','restore',project,'--locked-mode','--ignore-failed-sources','-p:NuGetAudit=false','--disable-parallel'])
 run(app.lower()+'-publish1110',['dotnet','publish',project,'-c','Release','--no-restore','-m:1','-p:BuildInParallel=false','-o',str(stage/app)])
 if app=='Viewer':shutil.copy2(stage/'Viewer/CipherCapsule_Viewer.exe',s/'templates/CipherCapsule_Viewer.exe')
inputs=[s/'Directory.Build.props',s/'Directory.Build.targets',s/'global.json']
for folder in ['src','assets','licenses','vendor']:inputs.extend(p for p in (s/folder).rglob('*') if p.is_file())
b=stage/'Builder/CipherCapsule_v1.1.10.exe';v=s/'templates/CipherCapsule_Viewer.exe'
m={'version':'1.1.10','builder_path':str(b),'builder_bytes':b.stat().st_size,'builder_sha256':sha(b),'viewer_bytes':v.stat().st_size,'viewer_sha256':sha(v),'product_inputs':{p.relative_to(s).as_posix():sha(p) for p in inputs}}
(w/'stage-manifest1110.json').write_text(json.dumps(m,indent=2),encoding='utf-8')
print(json.dumps({k:v for k,v in m.items() if k!='product_inputs'}))
