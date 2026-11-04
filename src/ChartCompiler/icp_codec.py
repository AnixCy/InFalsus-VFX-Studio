"""In Falsus 1.0.7b ICP1 chart codec, reconstructed and cross-checked locally."""
import struct,math
MASK=(1<<64)-1
U32=(1<<32)-1
def rol(x,n):
 x&=MASK;return ((x<<n)|(x>>(64-n)))&MASK
def pair(a,b):return (a&U32)|((b&U32)<<32)
class State:
 def __init__(self,name,count):
  self.s=[(0xc4ceb9fe1a85ec53+count)&MASK,0x9e3779b185ebca87^(count<<32),0xd1b54a32d192ed04,0x94d049bb133111ea];self.i=0
  chars=struct.unpack('<'+'H'*(len(name.encode('utf-16-le'))//2),name.encode('utf-16-le'))
  for i,c in enumerate(chars):
   v=pair(c,i);self.mix(v,v+count,v^1)
  self.mix(count,1,len(chars))
 def mix(self,x,y,z):
  a,b,c,d=self.s;x&=MASK;y&=MASK;z&=MASK
  match self.i%4:
   case 0:
    a=(a+rol(d,7)+x)&MASK;b^=rol(a+y,13);c=(c-(z^b))&MASK;d=rol(d+c,27)^a
   case 1:
    b=(b+rol(a,9)+z)&MASK;c^=rol(b+x,19);d=(d-(y^c))&MASK;a=rol(a+d,31)^b
   case 2:
    c=(c+rol(b,15)+y)&MASK;d^=rol(c+z,23);a=(a-(x^d))&MASK;b=rol(b+a,39)^c
   case 3:
    d=(d+rol(c,21)+x)&MASK;a^=rol(d+y,29);b=(b-(a^z))&MASK;c=rol(c+b,43)^d
  self.s=[a,b,c,d];self.i+=1
 def key(self,index,field):
  a,b,c,d=self.s;v=pair(field,index)
  match field%4:
   case 0:return ((rol(v^c,11)+a)&MASK)^d
   case 1:return ((b-rol(d+v,17))&MASK)^a
   case 2:return ((rol(v^a,29)+c)&MASK)^b
   case 3:return rol(c+b+v,37)^d
 def update(self,n):
  sx,ex,sw,ew=n['rats'];self.mix(n['id'],n['group_id'],pair(n['start_ms'],n['end_ms']))
  self.mix(n['packed_flags'],n['extra_flags'],pair(*sx));self.mix(pair(*ex),pair(*sw),pair(*ew))
 def qab(self,index):
  a,b,c,d=self.s;v=((((rol(c,23)+d)&MASK)^((rol(b,9)+a)&MASK))+pair(index,index))&MASK
  return (v^(v>>32))&U32
 def relay(self,r):
  a,b,c,d=self.s;v=pair(r,r);self.s=[a^v,(b+rol(v,17))&MASK,(c-rol(v,31))&MASK,d^rol(v,47)]
def sam(b):
 k=bytes.fromhex('f016284b7d9ec3a5');return bytes(x^k[i%8] for i,x in enumerate(b))
def decode(b,name):
 magic,ver,hs,ns,es,nc,ec,bpm,meter=struct.unpack_from('<4s4H2I2f',b)
 assert (magic,ver,hs,ns,es)==(b'ICP1',1,28,80,32)
 st=State(name,nc);out=[]
 for i in range(nc):
  raw=struct.unpack_from('<2Q16I',b,28+i*80)
  v=[(raw[j]-st.key(i,j))& (MASK if j<2 else U32) for j in range(10)]
  signed=lambda x:x-(1<<32) if x>0x7fffffff else x
  rats=[];gcds=[]
  for j in range(4):
   num,den=map(signed,raw[10+j*2:12+j*2]);g=math.gcd(num,den) or 1;coeff=signed(v[6+j]);rats.append((num//g*coeff,den//g*coeff));gcds.append(g-1)
  n=dict(zip(['id','group_id','start_ms','end_ms','packed_flags','extra_flags'],v[:6]));n['rats']=rats
  st.update(n);q=st.qab(i);parts=[gcds[j]^((q>>(8*j))&255) for j in range(4)]
  r=parts[0]^rol(parts[1],13)^rol(parts[2],29)^rol(parts[3],47)^i^n['id'];r=(r^(r>>32))&U32
  n['relay']=r;st.relay(r);out.append(n)
 return out
def encode(notes,events,name,bpm,meter):
 st=State(name,len(notes));out=bytearray(struct.pack('<4s4H2I2f',b'ICP1',1,28,80,32,len(notes),len(events),bpm,meter))
 for i,n in enumerate(notes):
  assert n['id']==i
  v=[n[k] for k in ['id','group_id','start_ms','end_ms','packed_flags','extra_flags']]
  coeff=[math.gcd(*r) or 1 for r in n['rats']]
  v+=coeff
  enc=[(x+st.key(i,j))&(MASK if j<2 else U32) for j,x in enumerate(v)]
  st.update(n);q=st.qab(i)
  for j,(a,b) in enumerate(n['rats']):
   g=((q>>(8*j))&255)+1
   enc.extend([(a//coeff[j]*g)&U32,(b//coeff[j]*g)&U32])
  out.extend(struct.pack('<2Q16I',*enc));st.relay(0)
 for i,e in enumerate(events):
  out.extend(struct.pack('<qii',i,e['timestamp_ms'],e['type']))
  out.extend(e['payload'])
 return bytes(out)
def audio_transform(b):
 out=bytearray(b);seed=len(b)*0x9e3779b97f4a7c15&MASK
 for i in range(0,len(out),8):
  x=(seed+(i//8)*0xd6e8feb86659fd93)&MASK;x=(x^rol(x,17))*0xa24baed4963ee407&MASK;x^=rol(x,41);key=struct.pack('<Q',x)
  for j in range(min(8,len(out)-i)):out[i+j]^=key[j]
 return bytes(out)
def raw_import_enabled(meta):
 value=meta.get('rawChartImport',False)
 if type(value) is not bool:raise ValueError('原样导入设置必须为布尔值')
 return value

def compile_song_chart(text,lookup,meta):
 """One per-song mode for selection, validation and resource rebuilding."""
 if spc_v2_enabled(meta):
  from spc_v2_codec import compile_spc as v2_compile_spc
  return v2_compile_spc(text,lookup,float(meta.get('audioOffsetMs',0)))
 if raw_import_enabled(meta):
  from icp_raw_codec import compile_spc as raw_compile_spc
  return raw_compile_spc(text,lookup)
 return compile_spc(text,lookup,float(meta.get('audioOffsetMs',0)))

def spc_v2_enabled(meta):
 value=meta.get('spcV2',False)
 if type(value) is not bool:raise ValueError('SPC v2 设置必须为布尔值')
 return value

def _assign_sky_groups(notes,labels,source_lines):
 """Preserve explicit groups and fill missing labels by 2 ms time continuity.

 Explicit labels constrain components: automatic segments may extend them,
 but different explicit groups never merge. Revisited automatic components
 are split; revisited explicit components are rejected to preserve authorship.
 """
 sky=[n for n in notes if n['id'] in labels]
 parent={n['id']:n['id'] for n in sky};anchors=dict(labels)
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
 previous=None
 for n in sky:
  i=n['id']
  # Release decoding preserves encoded labels. This missing-label policy is
  # requested by the user, not an inferred release-reader grouping algorithm.
  # Compare only the preceding skyarea, after chronological sorting.
  if previous is not None and abs(n['start_ms']-previous['end_ms'])<=2:
   prior=previous['id']
   if labels[i] is None or labels[prior] is None:join(prior,i)
  previous=n
 last=None;seen={};first={};group_id=None;count=splits=0
 for n in sky:
  i=n['id'];root=find(i)
  if root!=last:
   if root in seen:
    if anchors[root] is not None:
     raise ValueError(f'第 {source_lines[i]} 行 skyarea 的显式分组 {anchors[root]} 与其他组交错（首次出现在第 {source_lines[first[root]]} 行），无法保留分组关系并使 groupid 单调不减')
    splits+=1
   # Native judgement expects a group to point to its first note, rather than
   # a dense independent group counter. Ground notes and flicks keep their IDs.
   group_id=i;count+=1;seen[root]=True;first.setdefault(root,i)
  n['group_id']=group_id;last=root
 return dict(skyarea_count=len(sky),sky_group_count=count,
             auto_skyarea_count=sum(label is None for label in labels.values()),
             auto_sky_group_splits=splits)

def parse_spc(text,offset=0,grouping=None,*,timing_groups=None):
 import re
 from fractions import Fraction
 notes=[];events=[];bpm=meter=None;sky_labels={};source_lines={}
 def rat(a,b):
  # Native mesh builders also use the integer numerators and shared divisors.
  # Reducing 2/4 to 1/2 changes lanes, and independently reducing sky width
  # breaks the shared divisor used by SkySafeArea._aB.
  a,b=Fraction(str(a)),Fraction(str(b));scale=math.lcm(a.denominator,b.denominator)
  return (int(a*scale),int(b*scale))
 def sky_rats(x,w,d):
  x,w,d=map(lambda v:Fraction(str(v)),(x,w,d));scale=math.lcm(x.denominator,w.denominator,d.denominator)
  den=int(d*scale);return (int(x*scale),den),(int(w*scale),den)
 for line_no,line in enumerate(text.splitlines(),1):
  line=line.split('//',1)[0].strip().rstrip(';')
  if not line:continue
  m=re.fullmatch(r'(\w+)\s*\(([^()]*)\)',line)
  if not m:raise ValueError(f'第 {line_no} 行格式无法识别: {line[:100]}')
  cmd=m[1].lower()
  try:p=[float(x.strip()) for x in m[2].split(',')]
  except ValueError:raise ValueError(f'第 {line_no} 行参数必须是数字')
  if not all(math.isfinite(x) for x in p):raise ValueError(f'第 {line_no} 行包含非有限值')
  def require(n):
   if len(p)!=n:raise ValueError(f'第 {line_no} 行 {cmd} 参数数量应为 {n}')
  if cmd=='chart':
   require(2)
   if bpm is not None or min(p)<=0:raise ValueError('必须且只能存在一个有效 chart(BPM,拍数)')
   bpm,meter=p;continue
  if cmd in ('bpm','track'):
   require(3 if cmd=='bpm' else 2);t=round(p[0]+offset)
   if t<0:raise ValueError(f'第 {line_no} 行事件偏移后时间为负')
   payload=struct.pack('<dfI',p[1],p[2],0) if cmd=='bpm' else struct.pack('<dQ',p[1],0)
   event=dict(timestamp_ms=t,type=1 if cmd=='bpm' else 0,payload=payload)
   if timing_groups is not None:event['timing_group']=timing_groups.get(line_no,0)
   events.append(event);continue
  i=len(notes);n=dict(id=i,group_id=i,start_ms=round(p[0]+offset),end_ms=round(p[0]+offset),packed_flags=0,extra_flags=0,relay=0)
  if cmd in ('tap','hold'):
   require(3 if cmd=='tap' else 4)
   if cmd=='tap':t,w,lane=p;dur=0
   else:t,lane,w,dur=p
   if lane!=int(lane) or w!=int(w) or not 0<=lane<=5 or not 1<=w<=4 or (lane in (0,5) and w!=1) or (lane in (1,2,3,4) and lane+w>5):raise ValueError(f'第 {line_no} 行地面轨道或宽度越界')
   side={0:2,5:3}.get(int(lane),1);sx=rat(lane,4);sw=rat(w,4)
   n['rats']=[sx,sx,sw,sw];n['end_ms']+=round(dur);n['packed_flags']=((1 if cmd=='tap' else 2)<<8)|side
  elif cmd=='flick':
   require(5);t,x,den,w,flag=p
   if den<=0 or w<=0 or flag not in (4,16):raise ValueError(f'第 {line_no} 行 flick 参数不支持')
   sx,sw=sky_rats(x,w,den);n['rats']=[sx,sx,sw,sw];n['packed_flags']=0x404;n['extra_flags']=int(flag)<<8
  elif cmd=='skyarea':
   if len(p) not in (10,11):raise ValueError(f'第 {line_no} 行 skyarea 必须有 10 或 11 个参数')
   t,x,d,w,ex,ed,ew,el,er,dur=p[:10]
   if min(d,ed,w,ew)<=0 or el not in (0,1,2) or er not in (0,1,2):raise ValueError(f'第 {line_no} 行 skyarea 参数不支持')
   sx,sw=sky_rats(x,w,d);ex,ew=sky_rats(ex,ew,ed)
   n['rats']=[sx,ex,sw,ew];n['packed_flags']=0x504;n['extra_flags']=[4,8,16][int(el)]|[32,64,128][int(er)];n['end_ms']+=round(dur)
   if len(p)==11 and p[10]>=0:
    if p[10]!=int(p[10]):raise ValueError('天空分组编号必须为整数')
    sky_labels[i]=int(p[10])
   else:sky_labels[i]=None
  else:raise ValueError(f'第 {line_no} 行命令 {cmd} 尚不支持，未导入任何变更')
  if n['start_ms']<0 or n['end_ms']<n['start_ms'] or n['end_ms']>86400000:raise ValueError(f'第 {line_no} 行音符时间越界')
  # Studio can preserve exact quarter/midpoint coordinates with denominators
  # above one million. Both ICP1 and SPC2 store signed int32 rational pairs;
  # enforce that actual storage boundary instead of rounding valid fractions.
  if any(not(-0x80000000<=a<=0x7fffffff and 1<=b<=0x7fffffff) for a,b in n['rats']):raise ValueError(f'第 {line_no} 行坐标分子或分母超出32位整数范围')
  if timing_groups is not None:n['timing_group']=timing_groups.get(line_no,0)
  source_lines[i]=line_no;notes.append(n)
 if bpm is None:raise ValueError('缺少 chart(BPM,拍数)')
 # Sky judgement keeps the greatest seen note/group ID. Native chart IDs
 # follow chronological order, even if the Studio source is arranged by type.
 notes.sort(key=lambda n:(n['start_ms'],n['id']))
 ordered_labels={};ordered_lines={}
 for i,n in enumerate(notes):
  old_id=n['id'];ordered_lines[i]=source_lines[old_id]
  if old_id in sky_labels:ordered_labels[i]=sky_labels[old_id]
  n['group_id']=i;n['id']=i
 group_stats=(grouping or _assign_sky_groups)(notes,ordered_labels,ordered_lines)
 events.sort(key=lambda e:e['timestamp_ms'])
 return notes,events,dict(note_count=len(notes),event_count=len(events),bpm=bpm,beats_per_bar=meter,**group_stats)

def compile_spc(text,lookup,offset=0):
 notes,events,stats=parse_spc(text,offset)
 bpm,meter=stats['bpm'],stats['beats_per_bar']
 b=encode(notes,events,lookup,bpm,meter)
 assert decode(b,lookup)==notes,'生成谱面回读校验失败'
 return b,stats
