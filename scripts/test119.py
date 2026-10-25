from pathlib import Path
import subprocess,json,sys,datetime
s=Path(__file__).resolve().parents[1];w=s.parent/'Work';stamp=datetime.datetime.now(datetime.timezone.utc).strftime('%Y%m%dT%H%M%SZ');log=w/('armor-tests-'+stamp+'.log');data=w/('armor-tests-'+stamp)
project=s/'tests/Messages119/Messages119.Tests.csproj'
with log.open('w',encoding='utf-8') as out:
    r=subprocess.run(['dotnet','run','--project',str(project),'-c','Release','-p:NuGetAudit=false','--',str(data),str(s/'templates/CipherCapsule_Viewer.exe')],cwd=s,stdout=out,stderr=subprocess.STDOUT)
if r.returncode:raise SystemExit('Armor tests/build failed; see '+str(log))
report=json.loads((data/'results.json').read_text());assert report['failed']==0
print(json.dumps({k:v for k,v in report.items() if k!='tests'}))
