from pathlib import Path
import zipfile
s=Path(__file__).resolve().parents[1];dest=s.parent/'Work/dom-assets'
assert not dest.exists(),'Preserve existing DOM assets'
dest.mkdir(parents=True)
with zipfile.ZipFile(s/'assets/editor.zip') as z:
 for entry in z.infolist():
  path=Path(entry.filename);assert not path.is_absolute() and '..' not in path.parts
  p=dest/path;p.parent.mkdir(parents=True,exist_ok=True);p.write_bytes(z.read(entry.filename))
print('Offline DOM measurement assets prepared.')
