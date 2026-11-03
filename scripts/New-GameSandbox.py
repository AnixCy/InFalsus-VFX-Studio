"""Create a new, physically independent local game copy. Never updates a source installation."""
import argparse
import hashlib
import json
import shutil
import struct
from pathlib import Path


def digest(path):
    with path.open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()


def main():
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument('--source', type=Path, required=True)
    p.add_argument('--destination', type=Path, required=True)
    p.add_argument('--profile-data', type=Path, required=True)
    p.add_argument('--product-name', default='inf-ad01')
    args = p.parse_args()
    source, dest = args.source.resolve(), args.destination.resolve()
    if dest.exists() or dest.is_relative_to(source) or source.is_relative_to(dest):
        raise RuntimeError('Destination must be a new, separate directory')
    if len(args.product_name.encode('ascii')) != 8:
        raise RuntimeError('The isolated product name must be eight ASCII bytes')
    names = ['GameAssembly.dll', 'infalsus_Data/globalgamemanagers',
             'infalsus_Data/il2cpp_data/Metadata/global-metadata.dat']
    before = {n: digest(source / n) for n in names}
    ggm = (source / names[1]).read_bytes()
    candidates = [struct.pack('<I', 8) + n for n in [b'inf-bg01', b'inf-mod7']]
    needle = next((n for n in candidates if ggm.count(n) == 1), None)
    if needle is None:
        raise RuntimeError('Unreviewed Unity product-name layout')
    dest.mkdir(parents=True)

    def copy(src, dst):
        if getattr(Path(src).lstat(), 'st_file_attributes', 0) & 0x400:
            raise RuntimeError('Links are not accepted in the source copy')
        return shutil.copy2(src, dst)

    for name in ['GameAssembly.dll', 'UnityPlayer.dll', 'UnityCrashHandler64.exe', 'baselib.dll', 'Licenses.txt', 'version.dll']:
        copy(source / name, dest / name)
    copy(source / 'infalsus.exe', dest / 'VfxAddonTest.exe')
    for name in ['infalsus_Data', 'D3D12', 'dlc', 'OfflineSteamData', 'CustomSongs', 'MelonLoader']:
        if (source / name).is_dir():
            shutil.copytree(source / name, dest / ('VfxAddonTest_Data' if name == 'infalsus_Data' else name),
                            copy_function=copy, ignore=shutil.ignore_patterns('backups', '*.log'))
    (dest / 'VfxAddonTest_Data/globalgamemanagers').write_bytes(
        ggm.replace(needle, struct.pack('<I', 8) + args.product_name.encode('ascii'), 1))
    for name in ['Mods', 'UserLibs']:
        (dest / name).mkdir()
    data = dest / 'UserData/InFalsusPresentation'
    data.mkdir(parents=True)
    for name in ['profiles.json', 'catalog.json']:
        copy(args.profile_data / name, data / name)
    (data / 'Addons').mkdir()
    (data / 'config.json').write_text(json.dumps(dict(enabled=True, reloadSerial=0), indent=2), 'utf8')
    (dest / 'UserData/Loader.cfg').write_text('''[loader]
capture_player_logs = true
disable_start_screen = true
[console]
hide_console = true
[unityengine]
force_offline_generation = true
force_regeneration = false
''', 'utf8')
    (dest / '.vfx-addon-sandbox.json').write_text(json.dumps(dict(source=str(source), productName=args.product_name,
        executable='VfxAddonTest.exe', sourceHashes=before, hardlinksUsed=False), indent=2), 'utf8')
    if {n: digest(source / n) for n in names} != before:
        raise RuntimeError('Source changed concurrently; inspect before using the test copy')
    print(json.dumps(dict(destination=str(dest), sourceUnchanged=True, isolatedProductName=args.product_name)))


if __name__ == '__main__':
    main()
