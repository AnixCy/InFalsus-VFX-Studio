"""Prepare local target identities and a precompiled game resource for the editor test copy."""
import argparse
import json
import shutil
from pathlib import Path


def main():
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument('--catalog', type=Path, required=True)
    p.add_argument('--runtime-info', type=Path, required=True)
    p.add_argument('--resource', type=Path, required=True)
    p.add_argument('--output', type=Path, required=True)
    p.add_argument('--songs', type=Path)
    args = p.parse_args()
    runtime = json.loads(args.runtime_info.read_text('utf-8-sig'))
    build = json.loads((args.resource / 'build-info.json').read_text('utf-8-sig'))
    if runtime['unityVersion'] != build['unityVersion'] or runtime['colorSpace'] != build['colorSpace']:
        raise RuntimeError('Game resource does not match the measured target runtime')
    if build['platform'] != 'StandaloneWindows64':
        raise RuntimeError('Unsupported resource platform')
    labels = {}
    if args.songs:
        for song in json.loads(args.songs.read_text('utf-8-sig'))['songs']:
            labels[song.get('songKey') or 'game:' + str(song['id'])] = song['title']
    targets = {}
    for filename, identity in json.loads(args.catalog.read_text('utf-8-sig')).items():
        key, difficulty = identity['SongKey'], identity['Difficulty']
        fingerprint = identity.get('ChartFingerprint', '')
        if not key.startswith('game:') and not fingerprint:
            continue
        label = labels.get(key, Path(filename).stem)
        targets[key, difficulty] = dict(label=label+' · '+['MIN', 'EVO', 'ULT', 'FBD'][difficulty],
            songKey=key, difficulty=difficulty, chartFingerprint=fingerprint)
    out = args.output.resolve()
    out.mkdir(parents=True, exist_ok=True)
    resource = out / build['colorSpace']
    resource.mkdir(exist_ok=True)
    shutil.copy2(args.resource / 'background.bundle', resource / 'background.bundle')
    (out / 'profile.json').write_text(json.dumps(dict(unityVersion=build['unityVersion'], colorSpace=build['colorSpace']), indent=2), 'utf8')
    (out / 'targets.json').write_text(json.dumps(sorted(targets.values(), key=lambda t: (t['label'].casefold(), t['difficulty'])), ensure_ascii=False, indent=2), 'utf8')
    print(json.dumps(dict(targets=len(targets), colorSpace=build['colorSpace'], output=str(out))))


if __name__ == '__main__':
    main()
