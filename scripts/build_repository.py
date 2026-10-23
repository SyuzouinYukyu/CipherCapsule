"""Build an isolated Source-layout copy of the flat public repository.

No published EXE or original development directory is modified.
--check-inputs verifies dependency inputs without building or creating a workspace.
"""
import argparse
import base64
import hashlib
import json
import pathlib
import shutil
import subprocess
import sys
import datetime

repository = pathlib.Path(__file__).resolve().parents[1]
folders = ['src', 'tests', 'scripts', 'assets', 'licenses', 'vendor', 'templates']
settings = ['Directory.Build.props', 'Directory.Build.targets', 'global.json']

def verify_inputs():
    for name in folders + settings:
        if not (repository / name).exists():
            raise SystemExit('Required build input missing: ' + name)
    archive = repository / 'vendor/libsodium-1.0.22-stable-msvc.zip'
    if hashlib.sha256(archive.read_bytes()).hexdigest() != '2aeac7e7f00bc0d980096ea8b908e66dfad5c858d08f1ca3b980d413833303d9':
        raise SystemExit('Pinned libsodium archive hash mismatch')
    node = shutil.which('node')
    if not node:
        raise SystemExit('Node.js must be installed to verify the official libsodium signature')
    subprocess.run([node, str(repository / 'scripts/verify-minisign.cjs'), str(archive)], check=True)
    lock = json.loads((repository / 'vendor/editor-lock.json').read_text(encoding='utf-8'))
    for entry in lock['packages']:
        algorithm, digest = entry['integrity'].split('-', 1)
        actual = base64.b64encode(hashlib.new(algorithm, (repository / 'vendor' / entry['file']).read_bytes()).digest()).decode()
        if actual != digest:
            raise SystemExit('Pinned editor archive integrity mismatch: ' + entry['file'])

def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check-inputs', action='store_true')
    parser.add_argument('--stage', action='store_true', help='Keep outputs staged; never issue a formal EXE')
    parser.add_argument('--test', action='store_true', help='Run the noninteractive link tests')
    parser.add_argument('--test-headless', action='store_true', help='Alias for --test')
    parser.add_argument('--test-only', action='store_true', help='Build and run only the noninteractive link harness')
    args = parser.parse_args()
    verify_inputs()
    if args.check_inputs:
        print('Required inputs, pinned archive hashes and official signature verified; no build performed.')
        return
    if not shutil.which('dotnet'):
        raise SystemExit('Install the .NET SDK version pinned in global.json')
    run = repository / 'Work/rebuild' / datetime.datetime.now(datetime.timezone.utc).strftime('%Y%m%dT%H%M%S.%fZ')
    source = run / 'Source'
    source.mkdir(parents=True, exist_ok=False)
    for name in folders:
        shutil.copytree(repository / name, source / name, ignore=shutil.ignore_patterns('__pycache__', '*.pyc'))
    for name in settings:
        shutil.copy2(repository / name, source / name)
    command = [sys.executable, 'scripts/build.py']
    if not args.test_only:
        command.append('--stage')
    if args.test or args.test_headless or args.test_only:
        command.append('--test-headless')
    subprocess.run(command, cwd=source, check=True)
    print(('Headless test results: ' + str(run / 'Work/link117-tests/results.json')) if args.test_only else ('Staged outputs: ' + str(run / 'Work/link117-stage')))
    print('The published release EXE has not been replaced.')

if __name__ == '__main__':
    main()
