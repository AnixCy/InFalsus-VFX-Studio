"""SPC v2 interchange format and release-compatible value records.

The text grammar/validation is shared with the editor. SPC2 has a separate
native decoder; it never passes through the encrypted ICP1 release decoder.
Group membership is explicit and may interleave in time. IDs are chronological.
The hidden metadata is carried in the *capacity*, outside List<Note>.Count,
so the GC owns it without adding gameplay objects or events.
"""
from collections import defaultdict
import bisect
import math
import struct
from icp_codec import parse_spc

MAGIC = b'SPC2'
MARKER = int.from_bytes(MAGIC,'little')
META_MAGIC = b'SPC2META'
HEADER = struct.Struct('<4s6I2f3I')  # 48 bytes, version/stride/counts/metadata
MAX_NOTES = 1_000_000
MAX_EVENTS = 100_000
MAX_BYTES = 512 * 1024 * 1024
JOIN_MS = 2


def assign_groups(notes, labels, source_lines):
    sky=[n for n in notes if n['id'] in labels]
    parent={n['id']:n['id'] for n in sky}
    anchors=dict(labels)
    def find(i):
        while parent[i]!=i:
            parent[i]=parent[parent[i]];i=parent[i]
        return i
    def join(a,b):
        a,b=find(a),find(b)
        if a==b:return
        if anchors[a] is not None and anchors[b] is not None and anchors[a]!=anchors[b]:return
        parent[b]=a
        if anchors[a] is None:anchors[a]=anchors[b]
    explicit={}
    for n in sky:
        i=n['id'];label=labels[i]
        if label is not None:
            if label in explicit:join(explicit[label],i)
            else:explicit[label]=i
    for before,after in zip(sky,sky[1:]):
        a,b=before['id'],after['id']
        if abs(after['start_ms']-before['end_ms'])<=JOIN_MS and (labels[a] is None or labels[b] is None):join(a,b)
    first={}
    for n in sky:
        root=find(n['id']);first.setdefault(root,n['id']);n['group_id']=first[root]
    return dict(skyarea_count=len(sky),sky_group_count=len(first),
                auto_skyarea_count=sum(v is None for v in labels.values()),auto_sky_group_splits=0)


def bounds(note, end=False):
    x,w=note['rats'][int(end)],note['rats'][2+int(end)]
    center=x[0]/x[1];width=w[0]/w[1]
    return center-width/2,center+width/2


def merge_intervals(intervals):
    out=[]
    for lo,hi in sorted(intervals):
        if hi<=lo:continue
        if out and lo<=out[-1][1]+1e-9:out[-1]=(out[-1][0],max(hi,out[-1][1]))
        else:out.append((lo,hi))
    return out


def seam_ranges(notes):
    """All same-group end/start contacts, including forks and disjoint ranges.

    Positions are chart coordinates, matching the formal mesh's UV0.x.
    Free ends have no cut, except the first member's head as requested.
    A point contact has zero area and must not remove
    an otherwise visible edge. An inert record selects v2 rendering when
    neither end touches another member, without falling back to v1 ranges.
    """
    by_group=defaultdict(list);result={}
    for n in notes:
        if n['packed_flags']>>8==5:by_group[n['group_id']].append(n)
    for group in by_group.values():
        first=min(group,key=lambda x:(x['start_ms'],x['id']))
        starts=defaultdict(list);ends=defaultdict(list)
        for n in group:starts[n['start_ms']].append(n);ends[n['end_ms']].append(n)
        for n in group:
            pairs=[]
            for endpoint,opposites in ((False,ends),(True,starts)):
                time=n['end_ms' if endpoint else 'start_ms'];lo,hi=bounds(n,endpoint);cuts=[]
                for t in range(time-JOIN_MS,time+JOIN_MS+1):
                    for other in opposites.get(t,()):
                        if other is n:continue
                        a,b=bounds(other,not endpoint)
                        if min(hi,b)>max(lo,a):cuts.append((max(lo,a),min(hi,b)))
                intervals=merge_intervals(cuts)
                pairs.extend((a,b,float(not endpoint),float(endpoint)) for a,b in intervals)
            if n is first:
                pairs.insert(0,(-1e6,1e6,1.,0.))
            result[n['id']]=pairs or [(0.,0.,0.,0.)]
    return result


def native_note(n):
    record=bytearray(128);side=n['packed_flags']&255;kind=n['packed_flags']>>8
    struct.pack_into('<qq4i',record,0,n['id'],n['group_id'],side,kind,n['start_ms'],n['end_ms'])
    for offset,(num,den) in zip((0x20,0x2c,0x38,0x44),n['rats']):
        struct.pack_into('<iif',record,offset,num,den,num/den)
    # Formal _fA constructor sentinels. The visual clock overwrites them.
    struct.pack_into('<dddq',record,0x50,0.,-1800000.,-1800000.,-1)
    struct.pack_into('<I',record,0x74,n['extra_flags'])
    # SPC2-only padding, distinct from the release judged byte (+70) and
    # extra flags (+74). Copied with the native value into every side list.
    record[0x71]=n.get('timing_flags',0)
    lane,width=n['rats'][0][0],n['rats'][2][0]
    a,b={1:(lane,lane+width-1),2:(0,0),3:(5,5)}.get(side,(-1,-1))
    struct.pack_into('<ii',record,0x78,a,b)
    return bytes(record)


def native_event(i,event):
    # Release _Gab expands payloads from the compact ICP1 layout into this
    # exact _eA value layout. Shared logical clocks consume only these records.
    record=bytearray(32)
    struct.pack_into('<qii',record,0,i,event['timestamp_ms'],event['type'])
    payload=event['payload']
    if event['type'] in (0,1,2):
        record[16:24]=payload[:8]
        if event['type']!=0:record[24:28]=payload[8:12]
    elif event['type']==3:record[20]=bool(int.from_bytes(payload[:8],'little')&256)
    return bytes(record)


def build_metadata(notes):
    nc=len(notes);groups=defaultdict(list)
    for n in notes:
        if n['packed_flags']>>8==5 and (not n.get('timing_flags',0) or n.get('zero_event_visual')):groups[n['group_id']].append(n['id'])
    seam_index=64;group_index=64+nc*8;members_offset=64+nc*16
    data=bytearray(members_offset);membership={}
    for ids in groups.values():
        start=len(data)
        data.extend(struct.pack('<'+'I'*len(ids),*ids))
        for i in ids:membership[i]=(start,len(ids))
    data.extend(b'\0'*(-len(data)%16))
    # A pure predicate cache per group anchor avoids re-scanning the entire
    # group for every simultaneous member. Time + cursor are the whole key;
    # backwards practice seeks are safe without another reset protocol.
    # Header +40/+44 are reserved in the serialized format. The native SPC2
    # loader appends zeroed runtime group tolerance slots and fills them there;
    # old SPC2 files need no recompilation or metadata migration.
    cache_offset=len(data);data.extend(struct.pack('<dII',math.nan,0,0)*nc)
    seams_offset=len(data);ranges=seam_ranges(notes)
    for n in notes:
        i=n['id'];cuts=ranges.get(i,[])
        struct.pack_into('<II',data,seam_index+8*i,len(data),len(cuts))
        struct.pack_into('<II',data,group_index+8*i,*membership.get(i,(0,0)))
        for cut in cuts:data.extend(struct.pack('<4f',*cut))
    struct.pack_into('<8s6I',data,0,META_MAGIC,nc,seam_index,group_index,members_offset,seams_offset,len(data))
    struct.pack_into('<II',data,32,cache_offset,nc*16)
    # Enables region-filtered particles during Auto Play only for charts
    # containing passive objects. +48 is runtime count; +56 mesh identity.
    struct.pack_into('<I',data,52,int(any(n.get('timing_flags',0) for n in notes)))
    data.extend(b'\0'*(-len(data)%128))
    return bytes(data),ranges


def mark_zero_event_sky(notes, events, bpm):
    """Use existing passive rendering for Sky pieces with no native ticks.

    Native _pb emits half-beat ticks and a remaining tail only at >=2ms;
    _kb and renderers nevertheless index event[0]/event[-1]. These pieces
    cannot score on their own. Keep their exact geometry in the group's
    union, but skip their empty scoring span through the existing +71 flag.
    Explicit noinput/noclip remains excluded from union membership.
    """
    bpms = defaultdict(lambda: [(0, bpm)])
    for e in events:
        if e['type'] == 1:
            value = struct.unpack_from('<d', e['payload'])[0]
            if not math.isfinite(value) or value <= 0:
                raise ValueError('SkyArea BPM must be positive and finite')
            bpms[e.get('timing_group', 0)].append((e['timestamp_ms'], value))
    marked = 0
    for n in notes:
        if n['packed_flags'] >> 8 != 5 or n.get('timing_flags', 0) & 1:
            continue
        duration = n['end_ms'] - n['start_ms']
        if duration >= 2:
            continue
        rows = bpms[n.get('timing_group', 0)]
        value = rows[max(0, bisect.bisect_right(rows, (n['start_ms'], math.inf)) - 1)][1]
        effective = value * (.5 if value >= 249.99 else 1)
        if duration * effective / 30000 >= 1:
            continue  # An unusually high BPM can still produce real checkpoints.
        n['timing_flags'] = n.get('timing_flags', 0) | 1
        n['zero_event_visual'] = True
        marked += 1
    return marked


def compile_spc(text, lookup, offset=0):
    from spc_v2_timing_codec import split_groups,build_timing,append_timing,property_flags
    flat,mapping,groups=split_groups(text)
    has_timing=len(groups)>1 or any(g.get('hide_events') for g in groups)
    notes,events,stats=parse_spc(flat,offset,assign_groups,timing_groups=mapping if has_timing else None)
    source_events=events
    if len(notes)>MAX_NOTES or len(events)>MAX_EVENTS:raise ValueError('SPC v2 谱面规模超过安全读取范围')
    if has_timing:
        timing,_=build_timing(notes,events,groups,stats['bpm'],stats['beats_per_bar'],offset)
        for n in notes:
            n['timing_flags']=property_flags(groups[n['timing_group']]['properties'])
        stats.update(timing_group_count=len(groups)-1,timing_groups=groups[1:],
                     timing_group_event_count=sum(e['timing_group']!=0 for e in events),
                     hidegroup_event_count=len({e['line'] for g in groups for e in g.get('hide_events',[])}),
                     hidegroup_target_event_count=sum(len(g.get('hide_events',[])) for g in groups))
        stats['event_count']+=stats['hidegroup_event_count']
        events=[e for e in events if e['timing_group']==0]
    stats['zero_event_visual_skyareas']=mark_zero_event_sky(notes,source_events,stats['bpm'])
    metadata,ranges=build_metadata(notes)
    if has_timing:metadata=append_timing(metadata,timing)
    payload=b''.join(map(native_note,notes))+b''.join(native_event(i,e) for i,e in enumerate(events))+metadata
    if len(payload)+HEADER.size>MAX_BYTES:raise ValueError('SPC v2 谱面数据超过 512 MiB')
    binary=HEADER.pack(MAGIC,2,HEADER.size,128,32,len(notes),len(events),stats['bpm'],stats['beats_per_bar'],len(metadata),0,0)+payload
    stats.update(chart_system='SPC v2',format='SPC2',metadata_bytes=len(metadata),
                 seam_interval_count=sum(map(len,ranges.values())))
    return binary,stats
