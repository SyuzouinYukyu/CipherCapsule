from pathlib import Path
import zipfile
s=Path(__file__).resolve().parents[1];r=s.parent;old=r.parent/'v1.1.7/Source';dest=r/'Work/compat117'
names=['Container.cs','Models.cs','Sodium.cs']
zpath=s/'tests/fixtures/legacy117-crypto.zip'
if not zpath.exists():
 with zipfile.ZipFile(zpath,'w',zipfile.ZIP_DEFLATED) as z:
  for name in names:z.writestr(name,(old/'src/Core'/name).read_bytes())
  for name in ['GPL-2.0.txt','CipherCapsule-license.txt']:z.writestr(name,(old/'licenses'/name).read_bytes())
with zipfile.ZipFile(zpath) as z:
 assert set(z.namelist())==set(names+['GPL-2.0.txt','CipherCapsule-license.txt']) and z.testzip() is None
 if dest.exists():
  assert all((dest/name).read_bytes()==z.read(name) for name in names)
  print('Existing exact reference retained.');raise SystemExit(0)
 dest.mkdir()
 for name in names:(dest/name).write_bytes(z.read(name))
(dest/'Legacy117.csproj').write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net10.0-windows</TargetFramework><OutputType>Library</OutputType><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable><RestorePackagesWithLockFile>true</RestorePackagesWithLockFile></PropertyGroup></Project>',encoding='utf-8')
print('Exact v1.1.7 crypto source preserved in isolated reference fixture.')
