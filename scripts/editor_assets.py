import pathlib,tarfile,hashlib,base64,json,zipfile,shutil
root=pathlib.Path(__file__).resolve().parents[1]
lock=json.loads((root/'vendor/editor-lock.json').read_text(encoding='utf-8'))
for item in lock['packages']:
    data=(root/'vendor'/item['file']).read_bytes()
    expected=item['integrity'].split('-',1)
    if base64.b64encode(hashlib.new(expected[0],data).digest()).decode()!=expected[1]:raise SystemExit('Editor package integrity mismatch: '+item['file'])
(root/'licenses').mkdir(exist_ok=True)
with zipfile.ZipFile(root/'assets/editor.zip','w',zipfile.ZIP_DEFLATED) as z:
    with tarfile.open(root/'vendor/tinymce-8.10.0.tgz') as t:
        for m in t.getmembers():
            p=pathlib.PurePosixPath(m.name)
            if not m.isfile() or '..' in p.parts:continue
            data=t.extractfile(m).read();name='/'.join(p.parts[1:])
            if name in ['license.md','LICENSE.TXT','license.txt']:(root/'licenses/TinyMCE-license.txt').write_bytes(data)
            if name.endswith(('.js','.css','.woff','.woff2','.svg')):z.writestr('tiny/'+name,data)
    with tarfile.open(root/'vendor/tinymce-i18n.tgz') as t:
        for m in t.getmembers():
            if m.isfile() and m.name=='package/langs8/ja.js':z.writestr('ja.js',t.extractfile(m).read())
            if m.isfile() and m.name.lower() in ['package/license','package/license.txt','package/license.md']:(root/'licenses/TinyMCE-i18n-license.txt').write_bytes(t.extractfile(m).read())
    z.write(root/'assets/editor.js','editor.js')
    z.write(root/'assets/editor-ui-scale.js','editor-ui-scale.js')
    z.write(root/'assets/editor-bridge.js','editor-bridge.js')
    z.writestr('index.html','<!doctype html><html lang="ja"><head><meta charset="utf-8"><style>html,body{height:100%;margin:0}body{overflow:hidden}</style><script src="/tiny/tinymce.min.js"></script></head><body><textarea id="message"></textarea><script src="/editor-ui-scale.js"></script><script src="/editor-bridge.js"></script><script src="/editor.js"></script></body></html>')
    if 'ja.js' not in z.namelist():raise SystemExit('Japanese translation was not found')
shutil.copy2(root/'vendor/libsodium-LICENSE.txt',root/'licenses/libsodium-LICENSE.txt')
print('Pinned offline TinyMCE 8.10.0 assets packed')
