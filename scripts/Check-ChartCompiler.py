"""Creator-only codec checks with the bundled interpreter; no game launch or source edits."""
import base64
import json
import pathlib
import struct
import sys

from compiler import compile_request
from icp_codec import decode


def main():
    output=pathlib.Path(sys.argv[1])
    if output.exists():
        raise ValueError("Use a new output directory")
    output.mkdir(parents=True)
    checks=[]
    def check(ok,name):
        if not ok:
            raise AssertionError(name)
        checks.append(name)
    def reject(request,name):
        try:
            compile_request(request)
        except (ValueError,KeyError,TypeError,AssertionError):
            checks.append(name)
            return
        raise AssertionError(name)
    text="chart(120,4)\ntap(1000,1,1)\n"
    def request(**options):
        return dict(Version=1,Charts=[dict(Index=3,Name="fixture3.spc",Text=text,**options)])
    response=compile_request(request())
    binary=base64.b64decode(response["Charts"][0]["Bytes"])
    expected="SUNQMQEAHABQACAAAQAAAAAAAAAAAPBCAACAQLFfDbSHZB4U5C0tfBeOopsjZoyZESTY7bJADbTkLSV8PGKMGSog2O2ynwy05S09fN4AAAB4AwAAnwAAAHwCAABDAAAADAEAAAUAAAAUAAAA"
    check(base64.b64encode(binary).decode()==expected,"bundled interpreter matches the independent ICP1 fixture byte for byte")
    shifted=base64.b64decode(compile_request(request(AudioOffsetMs=125))["Charts"][0]["Bytes"])
    check(decode(shifted,"fixture3.spc")[0]["start_ms"]==1125,"audio offset is compiled once")
    source="chart(120,4)\ntap(1000,1,1)\ntiminggroup(\"fixture\",noinput):\ntrack(1200,0)\nskyarea(1500,1,4,1,2,4,1,0,0,100,1)\ngroupend()\n"
    v2=request(SpcV2=True);v2["Charts"][0]["Text"]=source
    compiled=compile_request(v2)["Charts"][0];spc2=base64.b64decode(compiled["Bytes"])
    check(spc2[:4]==b"SPC2" and compiled["Stats"]["timing_group_count"]==1,"SPC2 timinggroup and noinput survive creator compilation")
    check(struct.unpack_from("<I",spc2,20)[0]==2,"SPC2 retains all authored notes")
    (output/"fixture3.spc").write_bytes(spc2)
    reject(dict(Version=2,Charts=[]),"unsupported protocol is rejected")
    reject(dict(Version=1,Charts=[]),"empty request is rejected")
    duplicate=request();duplicate["Charts"]*=2;reject(duplicate,"duplicate difficulty is rejected")
    invalid=request();invalid["Charts"][0]["Name"]="../outside.spc";reject(invalid,"file-name traversal is rejected")
    reject(request(AudioOffsetMs=float("nan")),"nonfinite offset is rejected")
    reject(request(AudioOffsetMs=60001),"excessive offset is rejected")
    invalid=request();invalid["Charts"][0]["Text"]="chart(120,4)\nunknown(1000)\n";reject(invalid,"unknown syntax is rejected without dropping events")
    invalid=request();invalid["Charts"][0]["Text"]="chart(120,4)\nflick(1000,1,4,0,4)\n";reject(invalid,"zero-width flick cannot be silently repaired")
    invalid=request();invalid["Charts"][0]["Text"]=source;reject(invalid,"community syntax is not silently downgraded to ICP1")
    report=dict(ok=True,checks=checks,python=sys.version.split()[0],gameStarted=False)
    (output/"checks.json").write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding="utf-8")
    print(json.dumps(dict(ok=True,count=len(checks),output=str(output))))


if __name__=="__main__":
    main()
