"""Prepare a small v1.1.3 reference reader and fixture inside this version's Work.
Only public dummy data is used. Original version directories are never required or modified.
"""
from pathlib import Path
import zipfile,subprocess,shutil,json,hashlib,sys
s=Path(__file__).resolve().parents[1];r=s.parent;archive=s/'tests/fixtures/legacy113-reference.zip';dest=r/'Work/compat113'
expected={'Container.cs','PackageWriter.cs','Models.cs','Sodium.cs','Program.cs','CipherCapsule-license.txt','GPL-2.0.txt'}
with zipfile.ZipFile(archive) as z:
    assert set(z.namelist())==expected and z.testzip() is None
    if '--check-fixture' in sys.argv:
        print('Legacy reference archive: exact expected source/license entries verified.');raise SystemExit(0)
    if dest.exists():
        assert (dest/'legacy113.exe').is_file() and (dest/'bin/Release/net10.0-windows/Legacy.dll').is_file(),'Unknown incomplete compatibility workspace; preserve and inspect'
        for name in ['Container.cs','PackageWriter.cs','Models.cs','Sodium.cs','Program.cs']:assert (dest/name).read_text(encoding='utf-8')==z.read(name).decode('utf-8').replace('\r\n','\n')
        print('Existing checked reference fixture retained.');raise SystemExit(0)
    dest.mkdir(parents=True)
    for name in expected:(dest/name).write_bytes(z.read(name))
shutil.copy2(s/'templates/CipherCapsule_Viewer.exe',dest/'Viewer113.exe')
(dest/'Legacy.csproj').write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net10.0-windows</TargetFramework><OutputType>Exe</OutputType><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable><AllowUnsafeBlocks>true</AllowUnsafeBlocks><NuGetAudit>false</NuGetAudit></PropertyGroup></Project>',encoding='utf-8')
shutil.copy2(s/'global.json',dest/'global.json')
with (dest/'build.log').open('w',encoding='utf-8') as log:subprocess.run(['dotnet','build','Legacy.csproj','-c','Release','--ignore-failed-sources'],cwd=dest,stdout=log,stderr=subprocess.STDOUT,check=True)
shutil.copy2(s/'vendor/libsodium.dll',dest/'bin/Release/net10.0-windows/libsodium.dll')
with (dest/'generate.log').open('w',encoding='utf-8') as log:subprocess.run(['dotnet',str(dest/'bin/Release/net10.0-windows/Legacy.dll')],cwd=dest,stdout=log,stderr=subprocess.STDOUT,check=True)
print('Reference old-crypto fixture prepared; full regression runs separately.')
