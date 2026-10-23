"""v1.1.7 UI/checksum revision. Builds/tests without desktop automation."""
from pathlib import Path
import subprocess,sys,shutil,json,hashlib,datetime
s=Path(__file__).resolve().parents[1];r=s.parent;w=r/'Work';prefix='link117'
if any(w.glob('link117-*.log')):prefix+='-rerun-'+datetime.datetime.now().strftime('%Y%m%d-%H%M%S-%f')
stage=w/(prefix+'-stage')
w.mkdir(parents=True,exist_ok=True)
def run(label,*args):
    log=w/(prefix+'-'+label+'.log')
    if log.exists():raise SystemExit('Preserve existing log: '+str(log))
    with log.open('w',encoding='utf-8') as f:p=subprocess.run(args,cwd=s,stdout=f,stderr=subprocess.STDOUT)
    if p.returncode:raise SystemExit('Failed: '+label)
def sha(p):
    with p.open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
if set(sys.argv[1:])-{'--stage','--test-headless'}:raise SystemExit('Use --stage and/or --test-headless only')
if '--stage' in sys.argv:
    run('dependencies',sys.executable,'scripts/prepare.py')
    run('viewer-restore','dotnet','restore','src/Viewer/Viewer.csproj','--ignore-failed-sources','-p:NuGetAudit=false')
    run('viewer-publish','dotnet','publish','src/Viewer/Viewer.csproj','-c','Release','--no-restore','-o',str(stage/'Viewer'))
    shutil.copy2(stage/'Viewer/CipherCapsule_Viewer.exe',s/'templates/CipherCapsule_Viewer.exe')
if '--test-headless' in sys.argv:
    project='tests/Link117/Link117.Tests.csproj'
    run('tests-restore','dotnet','restore',project,'--ignore-failed-sources','-p:NuGetAudit=false')
    run('tests-build','dotnet','build',project,'-c','Release','--no-restore')
    run('tests',str(w/'bin/Link117.Tests/Release/net10.0-windows/win-x64/Link117.Tests.exe'),str(w/(prefix+'-tests/results.json')))
    tests=json.loads((w/(prefix+'-tests/results.json')).read_text(encoding='utf-8'));assert tests['failed']==0
    print(f"Limited checks: {tests['passed']} passed / {tests['failed']} failed",flush=True)
if '--stage' in sys.argv:
    run('builder-restore','dotnet','restore','src/Builder/Builder.csproj','--ignore-failed-sources','-p:NuGetAudit=false')
    run('builder-publish','dotnet','publish','src/Builder/Builder.csproj','-c','Release','--no-restore','-o',str(stage/'Builder'))
    inputs=[s/'Directory.Build.props',s/'Directory.Build.targets',s/'global.json']
    for name in ['src','assets','licenses','vendor']:inputs.extend(p for p in (s/name).rglob('*') if p.is_file())
    b=stage/'Builder/CipherCapsule_v1.1.7.exe';v=s/'templates/CipherCapsule_Viewer.exe'
    m={'version':'1.1.7','revision':'specification-link','product_inputs':{p.relative_to(s).as_posix():sha(p) for p in inputs},'builder_bytes':b.stat().st_size,'builder_sha256':sha(b),'viewer_bytes':v.stat().st_size,'viewer_sha256':sha(v),'manual_gui_verified':False,'desktop_automation_performed':False}
    with (stage/'product-manifest.json').open('x',encoding='utf-8') as f:json.dump(m,f,ensure_ascii=False,indent=2)
    print(json.dumps({k:v for k,v in m.items() if k!='product_inputs'}),flush=True)
