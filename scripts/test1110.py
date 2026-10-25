from pathlib import Path
import subprocess,datetime,json
s=Path(__file__).resolve().parents[1];w=s.parent/'Work';stamp=datetime.datetime.now().strftime('%Y%m%d-%H%M%S-%f');data=w/('privacy-tests-'+stamp);log=w/('privacy-tests-'+stamp+'.log')
old=s/'tests/Privacy1110/fixtures';project=s/'tests/Privacy1110/Privacy1110.Tests.csproj'
with log.open('w',encoding='utf-8') as out:
 for name,proj,arguments in [('Privacy1110.Tests',project,[str(data),str(s/'assets/viewer.ico'),str(s),str(old/'windows-message.txt'),str(old/'android-host-message.txt'),str(s/'templates/CipherCapsule_Viewer.exe')]),('Behavior116.Tests',s/'tests/Behavior116/Behavior116.Tests.csproj',[str(s),str(data/'routing')])]:
  subprocess.run(['dotnet','build',str(proj),'-c','Release','-p:NuGetAudit=false','-m:1','-p:BuildInParallel=false','--verbosity','quiet'],cwd=s,stdout=out,stderr=subprocess.STDOUT,check=True)
  exe=w/'bin'/name/'Release/net10.0-windows/win-x64'/(name+'.exe')
  subprocess.run([str(exe),*arguments],cwd=s,stdout=out,stderr=subprocess.STDOUT,check=True)
report=json.loads((data/'results.json').read_text());routing=json.loads((data/'routing/results.json').read_text());assert report['failed']==routing['failed']==0
report['tests'] += [{**t,'category':'regression'} for t in routing['tests']];report['passed']+=routing['passed'];report['regression_passed']+=routing['passed']
(data/'combined-results.json').write_text(json.dumps(report,indent=2),encoding='utf-8');print(json.dumps({k:v for k,v in report.items() if k!='tests'}))
