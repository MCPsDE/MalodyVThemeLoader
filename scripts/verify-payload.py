"""Verify offline payload completeness, Default provenance and logical catalog paths."""
import hashlib
import json
from pathlib import Path, PurePosixPath
import sys
import zipfile

repo = Path(__file__).resolve().parent.parent
source = Path(sys.argv[1] if len(sys.argv) > 1 else 'E:/UnityProjects/Emiria') / 'Assets/Malody'
payload = repo / 'artifacts/payload'

def digest(data):
    return hashlib.sha256(data).hexdigest()

def safe(path):
    parts = PurePosixPath(path.replace('\\', '/'))
    assert not parts.is_absolute() and all(p not in ('.', '..') and ':' not in p for p in parts.parts), path

required = ['winhttp.dll', 'doorstop_config.ini', '.doorstop_version',
    'BepInEx/core/BepInEx.Core.dll', 'BepInEx/core/BepInEx.Unity.IL2CPP.dll',
    'BepInEx/core/BepInEx.Preloader.Core.dll', 'BepInEx/core/Il2CppInterop.Runtime.dll',
    'BepInEx/core/Il2CppInterop.HarmonySupport.dll', 'BepInEx/core/0Harmony.dll',
    'BepInEx/core/dobby.dll', 'dotnet/coreclr.dll', 'dotnet/hostpolicy.dll',
    'dotnet/System.Private.CoreLib.dll', 'BepInEx/config/BepInEx.cfg',
    'BepInEx/unity-libs/2022.3.62.zip', 'BepInEx/interop/Assembly-CSharp.dll',
    'BepInEx/plugins/MalodyVThemeLoader/MalodyVThemeLoader.Plugin.dll',
    'BepInEx/plugins/MalodyVThemeLoader/MalodyVThemeLoader.Shared.dll',
    'BepInEx/plugins/MalodyVThemeLoader/catalog.json', 'ThemeLoader/MalodyVThemeManager.exe',
    'ThemeLoader/themes/Default.zip', 'ThemeLoader/active-theme.txt', 'ThemeLoader/settings.json']
for name in required:
    assert (payload / name).is_file(), f'Missing offline file: {name}'
for file in (repo / 'vendor/bepinex').rglob('*'):
    if file.is_file() and file.suffix.lower() == '.dll':
        installed = payload / file.relative_to(repo / 'vendor/bepinex')
        assert installed.is_file() and digest(installed.read_bytes()) == digest(file.read_bytes()), f'Pinned runtime differs: {file.name}'

catalog = json.loads((payload / required[18]).read_text(encoding='utf-8-sig'))
assert catalog['unityVersion'] == '2022.3.62f2'
with zipfile.ZipFile(payload / 'ThemeLoader/themes/Default.zip') as theme:
    names = set(theme.namelist())
    manifest = json.loads(theme.read('theme.json'))
    assert manifest['id'] == 'default' and manifest['formatVersion'] == 1
    resources = 0
    for name in names:
        safe(name)
        if name.endswith('/') or name == 'theme.json':
            continue
        prefix, _, logical = name.partition('/')
        root = {'images': source, 'fonts': source / 'Fonts', 'live2d': source / 'Resources/Live2d'}.get(prefix)
        assert root is not None, f'Unrelated Default entry: {name}'
        original = root / logical
        assert original.is_file(), f'No source resource: {name}'
        assert digest(theme.read(name)) == digest(original.read_bytes()), f'Default was altered: {name}'
        resources += 1
    for entry in catalog['images']:
        safe(entry['logicalId'])
        name = 'images/' + entry['logicalId']
        assert name in names, f'Catalog image absent: {name}'
        assert digest(theme.read(name)) == entry['sha256']
    for entry in catalog['live2dFiles']:
        safe(entry['logicalId'])
        name = 'live2d/' + entry['logicalId']
        assert name in names and digest(theme.read(name)) == entry['sha256']

with zipfile.ZipFile(repo / 'artifacts/payload.zip') as archive:
    for name in archive.namelist():
        safe(name)
        assert 'authorization' not in name.lower() and not name.endswith('TERMS.txt'), name
    for name in required:
        assert name in archive.namelist(), f'Embedded payload incomplete: {name}'
        assert digest(archive.read(name)) == digest((payload / name).read_bytes()), f'Packed payload differs: {name}'
print(f'Offline payload verified; {resources} unchanged source resources; {len(catalog["images"])} catalog images.')
