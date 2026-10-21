import pathlib,tarfile,shutil,hashlib,json
root=pathlib.Path(__file__).resolve().parents[1]
with tarfile.open(root/'vendor/tinymce-source-8.10.0.tar.gz') as t:
    members=[m for m in t.getmembers() if m.isfile() and m.name.split('/')[-1].lower() in ['license.txt','copying','license','license.md']]
    for m in members:
        data=t.extractfile(m).read()
        if b'GNU GENERAL PUBLIC LICENSE' in data and len(data)>10000:
            (root/'licenses/GPL-2.0.txt').write_bytes(data);break
    else:
        if not (root/'licenses/GPL-2.0.txt').is_file():raise SystemExit('Full GPL license missing; acquire https://www.gnu.org/licenses/old-licenses/gpl-2.0.txt before building')
packages=pathlib.Path.home()/'.nuget/packages'
for package,version,prefix in [('microsoft.web.webview2','1.0.4258.31','WebView2'),('microsoft.netcore.app.runtime.win-x64','10.0.13','NETCore'),('microsoft.windowsdesktop.app.runtime.win-x64','10.0.13','WindowsDesktop')]:
    for name in ['LICENSE.txt','LICENSE.TXT','NOTICE.txt','THIRD-PARTY-NOTICES.TXT']:
        p=packages/package/version/name
        if p.exists():shutil.copy2(p,root/'licenses'/f'{prefix}-{name}')
print('Licenses collected')
