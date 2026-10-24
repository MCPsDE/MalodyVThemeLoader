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
    'ThemeLoader/themes/Default/theme.json', 'ThemeLoader/active-theme.txt', 'ThemeLoader/settings.json']
required.extend(['BepInEx/plugins/MalodyVThemeLoader/NVorbis.dll',
    'BepInEx/plugins/MalodyVThemeLoader/NLayer.dll',
    'BepInEx/plugins/MalodyVThemeLoader/NAudio.Core.dll',
    'BepInEx/plugins/MalodyVThemeLoader/NAudio.WinMM.dll',
    'BepInEx/interop/UnityEngine.AudioModule.dll',
    'ThemeLoader/docs/licenses/SkiaSharp-LICENSE.txt', 'ThemeLoader/docs/licenses/NVorbis-LICENSE.txt',
    'ThemeLoader/docs/licenses/NLayer-LICENSE.txt',
    'ThemeLoader/docs/licenses/NAudio-LICENSE.txt',
    'ThemeLoader/docs/licenses/Skia-THIRD-PARTY-NOTICES.txt'])
for name in required:
    assert (payload / name).is_file(), f'Missing offline file: {name}'
for file in (repo / 'vendor/bepinex').rglob('*'):
    if file.is_file() and file.suffix.lower() == '.dll':
        installed = payload / file.relative_to(repo / 'vendor/bepinex')
        assert installed.is_file() and digest(installed.read_bytes()) == digest(file.read_bytes()), f'Pinned runtime differs: {file.name}'

catalog = json.loads((payload / 'BepInEx/plugins/MalodyVThemeLoader/catalog.json').read_text(encoding='utf-8-sig'))
assert catalog['unityVersion'] == '2022.3.62f2'
default_root = payload / 'ThemeLoader/themes/Default'
names = {file.relative_to(default_root).as_posix() for file in default_root.rglob('*') if file.is_file()}
manifest = json.loads((default_root / 'theme.json').read_text(encoding='utf-8-sig'))
assert manifest['id'] == 'default' and manifest['formatVersion'] == 1
resources = 0
for name in names:
    safe(name)
    if name == 'theme.json':
        continue
    prefix, _, logical = name.partition('/')
    root = {'images': source, 'audio': source, 'fonts': source / 'Fonts', 'live2d': source / 'Resources/Live2d'}.get(prefix)
    assert root is not None, f'Unrelated Default entry: {name}'
    original = root / logical
    assert original.is_file(), f'No source resource: {name}'
    assert digest((default_root / name).read_bytes()) == digest(original.read_bytes()), f'Default was altered: {name}'
    resources += 1
for entry in catalog['images']:
    safe(entry['logicalId'])
    name = 'images/' + entry['logicalId']
    assert name in names, f'Catalog image absent: {name}'
    assert digest((default_root / name).read_bytes()) == entry['sha256']
for entry in catalog['live2dFiles']:
    safe(entry['logicalId'])
    name = 'live2d/' + entry['logicalId']
    assert name in names and digest((default_root / name).read_bytes()) == entry['sha256']

ui_events = {'Click', 'MsgOK', 'MsgFail', 'Switch', 'Back', 'Cancel', 'Screenshot', 'MsgNotify', 'ClickLight', 'music_finish'}
assert {entry['event'] for entry in catalog['audio']} == ui_events
assert len(catalog['audio']) == len(ui_events)
for entry in catalog['audio']:
    safe(entry['logicalId'])
    original = source / entry['logicalId']
    copied = default_root / manifest.get('audioRoot', 'audio') / entry['logicalId']
    assert original.is_file() and copied.is_file(), entry
    assert digest(original.read_bytes()) == entry['sha256'] == digest(copied.read_bytes()), entry
scenes = set(catalog['scenes'])
assert not scenes.intersection({'ScenePlay', 'SceneReplay', 'SceneRoomWatch', 'SceneWatchSub'})
assert not any(scene.startswith('PlayMode') for scene in scenes)
for entry in catalog['images']:
    for usage in entry['usages']:
        assert usage['scene'] in scenes and usage['hierarchy'], usage
        assert usage['component'] in {'Image', 'RawImage', 'SpriteRenderer'}, usage

with zipfile.ZipFile(repo / 'artifacts/payload.zip') as archive:
    for name in archive.namelist():
        safe(name)
        assert 'authorization' not in name.lower() and not name.endswith('TERMS.txt'), name
    for name in required:
        assert name in archive.namelist(), f'Embedded payload incomplete: {name}'
        assert digest(archive.read(name)) == digest((payload / name).read_bytes()), f'Packed payload differs: {name}'
print(f'Offline payload verified; {resources} unchanged source resources; {len(catalog["images"])} catalog images.')
