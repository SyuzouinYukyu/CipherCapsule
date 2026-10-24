"""v1.1.8 offline UI geometry + transactional tab switching. Never launches displayed GUI."""
from pathlib import Path
import subprocess,sys,shutil,json,hashlib,datetime
s=Path(__file__).resolve().parents[1];r=s.parent;w=r/'Work';w.mkdir(exist_ok=True)
suffix='-'+datetime.datetime.now().strftime('%Y%m%d-%H%M%S-%f') if (w/'viewer-publish118.log').exists() else ''
stage=w/('stage'+suffix)
def run(label,*args):
 log=w/(label+suffix+'.log')
 if log.exists():raise SystemExit('Preserve existing log: '+str(log))
 with log.open('w',encoding='utf-8') as f:subprocess.run(args,cwd=s,stdout=f,stderr=subprocess.STDOUT,check=True)
def sha(p):
 with p.open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
if sys.argv[1:]!=['--stage']:raise SystemExit('Use --stage. See BUILDING.md for selected headless tests.')
assert not stage.exists(),'Preserve existing stage'
run('dependencies118',sys.executable,'scripts/prepare.py')
run('viewer-restore118','dotnet','restore','src/Viewer/Viewer.csproj','--ignore-failed-sources','-p:NuGetAudit=false')
run('viewer-publish118','dotnet','publish','src/Viewer/Viewer.csproj','-c','Release','--no-restore','-o',str(stage/'Viewer'))
shutil.copy2(stage/'Viewer/CipherCapsule_Viewer.exe',s/'templates/CipherCapsule_Viewer.exe')
run('builder-restore118','dotnet','restore','src/Builder/Builder.csproj','--ignore-failed-sources','-p:NuGetAudit=false')
run('builder-publish118','dotnet','publish','src/Builder/Builder.csproj','-c','Release','--no-restore','-o',str(stage/'Builder'))
b=stage/'Builder/CipherCapsule_v1.1.8.exe';v=s/'templates/CipherCapsule_Viewer.exe'
inputs=[s/'Directory.Build.props',s/'Directory.Build.targets',s/'global.json']
for name in ['src','assets','licenses','vendor']:inputs.extend(p for p in (s/name).rglob('*') if p.is_file())
m={'version':'1.1.8','builder_bytes':b.stat().st_size,'builder_sha256':sha(b),'viewer_bytes':v.stat().st_size,'viewer_sha256':sha(v),'product_inputs':{p.relative_to(s).as_posix():sha(p) for p in inputs},'physical_gui_verified':False,'desktop_automation_performed':False}
(w/('stage-manifest118'+suffix+'.json')).write_text(json.dumps(m,indent=2),encoding='utf-8');print(json.dumps({k:v for k,v in m.items() if k!='product_inputs'}))
