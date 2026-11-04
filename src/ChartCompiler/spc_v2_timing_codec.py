"""Explicit groupend-based SPC2 timing groups and per-group clock maps.

No eval, parent-state inheritance or sky group renumbering here.
The release's track integral is seconds * rate, initially rate 1. BPM selects
the release tick cadence at each object's start, not a new judgement window.
"""
import bisect
import csv
import json
import math
import re
import struct

TIME_MAGIC=b'SPC2TIME'
FOOT_MAGIC=b'SPC2TEND'
MAX_GROUPS=65536
DIR_SIZE=48
NOINPUT=1
NOCLIP=2
MAX_HIDE_EVENTS=100000


def property_flags(properties):
    values={p.strip().lower() for p in properties}
    return (NOINPUT if 'noinput' in values or 'noclip' in values else 0)|(NOCLIP if 'noclip' in values else 0)


def split_groups(text):
    groups=[dict(name='',properties=[],line=0)]
    if not re.search(r'^\s*(?:timinggroup|groupend|hidegroup)\s*\(',text,re.M|re.I):return text,{},groups
    mapping={};flat=[];stack=[];hide=[]
    for number,original in enumerate(text.splitlines(),1):
        body=original.split('//',1)[0].rstrip()
        if not body.strip():flat.append('');continue
        line=body.strip()
        if re.fullmatch(r'groupend\s*\(\s*\)\s*;?',line,re.I):
            if not stack:raise ValueError(f'第 {number} 行 groupend() 没有对应的 timinggroup')
            stack.pop();flat.append('');continue
        match=re.fullmatch(r'timinggroup\s*\((.*)\)\s*:',line,re.I)
        if match:
            try:parts=next(csv.reader([match[1]],skipinitialspace=True,strict=True))
            except csv.Error as e:raise ValueError(f'第 {number} 行 timinggroup 参数格式无效') from e
            parts=[s.strip().strip("'\"") for s in parts]
            if not parts or not parts[0]:raise ValueError(f'第 {number} 行 timinggroup 必须有名称')
            if len(groups)>=MAX_GROUPS:raise ValueError('timinggroup 数量超过安全范围')
            groups.append(dict(name=parts[0],properties=[p for p in parts[1:] if p],line=number))
            stack.append(len(groups)-1);flat.append('');continue
        gid=stack[-1] if stack else 0
        match=re.fullmatch(r'hidegroup\s*\(([^()]*)\)\s*;?',line,re.I)
        if match:
            try:parts=next(csv.reader([match[1]],skipinitialspace=True,strict=True))
            except csv.Error as e:raise ValueError(f'第 {number} 行 hidegroup 参数格式无效') from e
            parts=[s.strip() for s in parts]
            if len(parts) not in (2,3):raise ValueError(f'第 {number} 行 hidegroup 需要时间、0或1，以及可选的目标名称')
            try:time=float(parts[0])
            except ValueError as e:raise ValueError(f'第 {number} 行 hidegroup 时间必须为毫秒数值') from e
            if not math.isfinite(time):raise ValueError(f'第 {number} 行 hidegroup 时间必须为有限值')
            if parts[1] not in ('0','1'):raise ValueError(f'第 {number} 行 hidegroup 状态只能为0或1')
            target=parts[2].strip("'\"") if len(parts)==3 else None
            if target=='':raise ValueError(f'第 {number} 行 hidegroup 目标名称不能为空')
            hide.append((gid,target,dict(time_ms=time,state=int(parts[1]),line=number)))
            if len(hide)>MAX_HIDE_EVENTS:raise ValueError('hidegroup 事件超过安全范围')
            flat.append('');continue
        if gid and re.match(r'chart\s*\(',line,re.I):raise ValueError(f'第 {number} 行 chart 只能写在主时间组')
        mapping[number]=gid;flat.append(line)
    if stack:raise ValueError(f'第 {groups[stack[-1]]["line"]} 行 timinggroup 缺少对应的 groupend()')
    names={}
    for i,g in enumerate(groups):names.setdefault(g['name'],[]).append(i)
    count=0
    for gid,target,event in hide:
        targets=[gid] if target is None else names.get(target,[])
        if not targets:raise ValueError(f'第 {event["line"]} 行 hidegroup 找不到时间组: {target}')
        count+=len(targets)
        if count>MAX_HIDE_EVENTS:raise ValueError('hidegroup 展开后的事件超过安全范围')
        for i in targets:groups[i].setdefault('hide_events',[]).append(event.copy())
    return '\n'.join(flat),mapping,groups


def clock_segments(events):
    segments=[(0.,0.,.001)]
    for e in events:
        if e['type']!=0:continue
        time=e['timestamp_ms'];rate=struct.unpack_from('<d',e['payload'])[0]
        a,integral,slope=segments[-1]
        row=(float(time),integral+(time-a)*slope,rate*.001)
        if time==a:segments[-1]=row
        else:segments.append(row)
    return segments


def scroll_at(segments,time):
    index=max(0,bisect.bisect_right(segments,(time,math.inf,math.inf))-1)
    start,integral,slope=segments[index]
    return integral+(time-start)*slope


def build_timing(notes,events,groups,bpm,meter,offset=0):
    by_group=[[] for _ in groups]
    for e in events:
        if not 0<=e['timestamp_ms']<=86400000:raise ValueError('timinggroup 事件时间越界')
        if e['type']==1 and (struct.unpack_from('<d',e['payload'])[0]<=0 or struct.unpack_from('<f',e['payload'],8)[0]<=0):
            raise ValueError('timinggroup BPM 和拍数必须大于零')
        by_group[e['timing_group']].append(e)
    clocks=[clock_segments(es) for es in by_group]
    bpms=[]
    for es in by_group:
        values=[(0,bpm,meter)]
        for e in es:
            if e['type']==1:
                row=(e['timestamp_ms'],struct.unpack_from('<d',e['payload'])[0],struct.unpack_from('<f',e['payload'],8)[0])
                if row[0]==values[-1][0]:values[-1]=row
                else:values.append(row)
        bpms.append(values)
    required=0
    for n in notes:
        values=bpms[n['timing_group']]
        value=values[max(0,bisect.bisect_right(values,(n['start_ms'],math.inf,math.inf))-1)][1]
        rate=value*(.5 if value>=249.99 else 1)/30000
        required+=math.floor((n['end_ms']-n['start_ms'])*rate)+3
    if required>2_000_000:raise ValueError('timinggroup 判定事件超过安全范围')
    data=bytearray(64)
    note_map=len(data);data.extend(struct.pack('<'+'I'*len(notes),*(n['timing_group'] for n in notes)))
    data.extend(bytes(-len(data)%8));directory=len(data);data.extend(bytes(len(groups)*DIR_SIZE))
    positions=len(data)
    for n in notes:data.extend(struct.pack('<dd',*(scroll_at(clocks[n['timing_group']],n[k]) for k in ('start_ms','end_ms'))))
    has_hide=any(g.get('hide_events') for g in groups)
    for i,g in enumerate(groups):
        segment_offset=len(data)
        for row in clocks[i]:data.extend(struct.pack('<3d',*row))
        bpm_offset=len(data)
        for time,value,beats in bpms[i]:data.extend(struct.pack('<i4xdd',time,value,beats))
        label=json.dumps(g,ensure_ascii=False,separators=(',',':')).encode('utf-8')
        label_offset=len(data);data.extend(label);data.extend(bytes(-len(data)%8))
        struct.pack_into('<4I2d4I',data,directory+i*DIR_SIZE,segment_offset,len(clocks[i]),bpm_offset,len(bpms[i]),
                         0.,0.,label_offset,len(label),0,0)
        if has_hide:
            values=[(0,0)]
            shifted=[]
            for event in g.get('hide_events',[]):
                time=round(event['time_ms']+offset)
                if not 0<=time<=86400000:raise ValueError(f'第 {event["line"]} 行 hidegroup 偏移后时间越界')
                shifted.append((time,event['line'],event['state']))
            for time,_,state in sorted(shifted):
                row=(time,state)
                if time==values[-1][0]:values[-1]=row
                else:values.append(row)
            hide_offset=len(data)
            for row in values:data.extend(struct.pack('<iI',*row))
            struct.pack_into('<II',data,directory+i*DIR_SIZE+40,hide_offset,len(values))
    struct.pack_into('<8s6I2f2I',data,0,TIME_MAGIC,2 if has_hide else 1,len(groups),len(notes),note_map,directory,required,bpm,meter,0,positions)
    return bytes(data),clocks


def append_timing(metadata,timing):
    data=bytearray(metadata);offset=len(data);data.extend(timing)
    data.extend(struct.pack('<8sII',FOOT_MAGIC,offset,len(timing)))
    struct.pack_into('<I',data,28,len(data))
    data.extend(bytes(-len(data)%128))
    return bytes(data)
