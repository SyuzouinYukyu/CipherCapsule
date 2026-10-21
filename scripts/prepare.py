"""Verify official minisign using the bundled Node crypto Ed25519 implementation, then extract x64 only."""
import pathlib, hashlib, zipfile, json, subprocess, struct, shutil, os
ROOT=pathlib.Path(__file__).resolve().parents[1]
ARCHIVE=ROOT/'vendor/libsodium-1.0.22-stable-msvc.zip'
PIN='2aeac7e7f00bc0d980096ea8b908e66dfad5c858d08f1ca3b980d413833303d9'
if hashlib.sha256(ARCHIVE.read_bytes()).hexdigest()!=PIN: raise SystemExit('libsodium archive hash mismatch; do not continue')
NODE=shutil.which('node')
if not NODE: raise SystemExit('Node.js was not found on PATH. Install Node.js to verify the official libsodium minisign signature. Verification is mandatory.')
subprocess.run([NODE,str(ROOT/'scripts/verify-minisign.cjs'),str(ARCHIVE)],check=True)
with zipfile.ZipFile(ARCHIVE) as z:
    (ROOT/'vendor/libsodium.dll').write_bytes(z.read('libsodium/x64/Release/v143/dynamic/libsodium.dll'))
    licenses=[x for x in z.namelist() if x.upper().endswith(('LICENSE','LICENSE.TXT'))]
    if licenses: (ROOT/'vendor/libsodium-LICENSE.txt').write_bytes(z.read(licenses[0]))
    elif not (ROOT/'vendor/libsodium-LICENSE.txt').is_file(): raise SystemExit('Missing upstream license: retrieve https://raw.githubusercontent.com/jedisct1/libsodium/1.0.22/LICENSE')
print('Verified libsodium DLL SHA256',hashlib.sha256((ROOT/'vendor/libsodium.dll').read_bytes()).hexdigest())
