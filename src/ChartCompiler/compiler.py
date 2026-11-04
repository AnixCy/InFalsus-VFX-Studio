"""Creator-only bounded JSON compiler. No game paths, executable chart scripts or disk writes."""
import base64
import json
import math
import sys
from icp_codec import compile_song_chart


def compile_request(request):
    if request.get("Version") != 1:
        raise ValueError("不支持的谱面编译请求")
    charts = request.get("Charts")
    if not isinstance(charts, list) or not 1 <= len(charts) <= 4:
        raise ValueError("需要 1～4 个难度")
    result, seen = [], set()
    for chart in charts:
        index, seed, text = chart["Index"], chart["Name"], chart["Text"]
        if type(index) is not int or index not in range(4) or index in seen:
            raise ValueError("难度编号无效或重复")
        seen.add(index)
        if not isinstance(seed, str) or not seed.endswith(".spc") or len(seed) > 128 or any(c in seed for c in '/\\:\0'):
            raise ValueError("无效的谱面编码文件名")
        if not isinstance(text, str) or len(text.encode("utf-8")) > 8 * 1024 * 1024:
            raise ValueError("单份谱面文本超过 8 MiB")
        offset = chart.get("AudioOffsetMs", 0)
        if not isinstance(offset, (int, float)) or not math.isfinite(offset) or abs(offset) > 60000:
            raise ValueError("音频偏移无效")
        options = dict(spcV2=chart.get("SpcV2", False), rawChartImport=chart.get("RawChartImport", False), audioOffsetMs=offset)
        binary, stats = compile_song_chart(text, seed, options)
        if not 0 < stats["note_count"] <= 100000 or len(binary) > 64 * 1024 * 1024:
            raise ValueError("谱面为空或超过发布上限")
        result.append(dict(Index=index, Name=seed, Bytes=base64.b64encode(binary).decode("ascii"), Stats=stats))
    return dict(Ok=True, Charts=result)


def main():
    sys.stdin.reconfigure(encoding="utf-8")
    sys.stdout.reconfigure(encoding="utf-8")
    try:
        data = sys.stdin.buffer.read(32 * 1024 * 1024 + 1)
        if len(data) > 32 * 1024 * 1024:
            raise ValueError("编译请求超过 32 MiB")
        response = compile_request(json.loads(data))
        print(json.dumps(response, ensure_ascii=False, allow_nan=False))
        return 0
    except Exception as error:
        print(json.dumps(dict(Ok=False, Error=str(error)), ensure_ascii=False))
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
