"""python scripts/build.py --stage [--test]; --test-only runs all test groups on the staged build.
Formal promotion is a separate step requiring recorded GUI validation.
Build tools: .NET SDK in global.json, Python 3 and Node.js on PATH.
"""
import pathlib,subprocess,sys,os,shutil,json,hashlib
source=pathlib.Path(__file__).resolve().parents[1];root=source.parent;os.chdir(source)
os.environ['DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE']='true'
def run(*args):subprocess.run(args,check=True)
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
if '--test-only' not in sys.argv:
    run(sys.executable,'scripts/prepare.py')
    if not (source/'licenses/GPL-2.0.txt').is_file():raise SystemExit('GPL license missing; do not publish')
    for project in ['Viewer','Builder']:
        csproj=f'src/{project}/{project}.csproj';dest=root/'Work/stage'/project
        run('dotnet','restore',csproj,'--locked-mode');run('dotnet','publish',csproj,'-c','Release','--no-restore','-o',str(dest))
        if project=='Viewer':shutil.copy2(dest/'CipherCapsule_Viewer.exe',source/'templates/CipherCapsule_Viewer.exe')
    manifest={'version':'1.1.3','builder_sha256':sha(root/'Work/stage/Builder/CipherCapsule_v1.1.3.exe'),'viewer_sha256':sha(source/'templates/CipherCapsule_Viewer.exe')}
    (root/'Work/stage/manifest.json').write_text(json.dumps(manifest,indent=2),encoding='utf-8')
if '--test' in sys.argv or '--test-only' in sys.argv:
    run('dotnet','restore','tests/Tests.csproj','--locked-mode');run('dotnet','build','tests/Tests.csproj','-c','Release','--no-restore')
    exe=root/'Work/bin/Tests/Release/net10.0-windows/win-x64/Tests.exe'
    for args in [['--skip-verified-large','--skip-verified-legacy'],['--editor'],['--html'],['--extra'],['--lifecycle'],['--layout'],['--v112'],['--v113'],['--folder113'],['--viewer-folder113'],['--viewer-folder-dpi113']]:run(str(exe),*args)
    results=[json.loads((root/'Work/test-results'/name).read_text(encoding='utf-8')) for name in ['core-results.json','editor-results.json','html-results.json','extra-results.json','lifecycle-results.json','layout-results.json','v112-results.json','v113-results.json','folder113-results.json','viewer-folder113-results.json','viewer-folder-dpi113-results.json']]
    summary={'passed':sum(x['passed'] for x in results),'failed':sum(x['failed'] for x in results)}
    (root/'Work/test-results/all-results.json').write_text(json.dumps(summary,indent=2),encoding='utf-8');print(summary)
print('Staged only. EXE/ is unchanged. Formal promotion requires separate GUI validation.')
