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
  # Encode exactly the supplied source-order note sequence.
  v=[n[k] for k in ['id','group_id','start_ms','end_ms','packed_flags','extra_flags']]
  coeff=[math.gcd(*r) or 1 for r in n['rats']]
  v+=coeff
  enc=[(x+st.key(i,j))&(MASK if j<2 else U32) for j,x in enumerate(v)]
  st.update(n);q=st.qab(i)
  for j,(a,b) in enumerate(n['rats']):
   g=((q>>(8*j))&255)+1
   enc.extend([(a//coeff[j]*g)&U32,(b//coeff[j]*g)&U32])
  relay=i^n['id'];n['relay']=(relay^(relay>>32))&U32
  out.extend(struct.pack('<2Q16I',*enc));st.relay(n['relay'])
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

def compile_spc(text,lookup,offset=0):
 """Encode source statements without gameplay validation or chart rewriting.

 The offset argument stays for caller compatibility, but timestamps are read
 from the source unchanged. No sorting, group repair, auto-joining, carrier
 checks, or gameplay bounds checks run here. Parsing and ICP1 encoding remain.
 """
 import re
 from fractions import Fraction
 notes=[];events=[];bpm=meter=None;implicit=0;declarations=0
 def rat(a,b):
  scale=math.lcm(a.denominator,b.denominator)
  return (int(a*scale),int(b*scale))
 def sky_rats(x,w,d):
  scale=math.lcm(x.denominator,w.denominator,d.denominator)
  den=int(d*scale);return (int(x*scale),den),(int(w*scale),den)
 for line_no,line in enumerate(text.splitlines(),1):
  line=line.split('//',1)[0].strip().rstrip(';')
  if not line:continue
  m=re.fullmatch(r'(\w+)\s*\(([^()]*)\)',line)
  if not m:raise ValueError(f'第 {line_no} 行无法解析为谱面语句: {line[:100]}')
  cmd=m[1].lower()
  # Legacy editor-only annotations have no ICP1 event representation.
  if cmd=='skyartcapture':declarations+=1;continue
  try:
   p=[Fraction(x.strip()) for x in m[2].split(',')]
   if cmd=='chart':bpm,meter=map(float,p);continue
   if cmd in ('bpm','track'):
    if cmd=='bpm':t,value,beats=p;payload=struct.pack('<dfI',float(value),float(beats),0)
    else:t,value=p;payload=struct.pack('<dQ',float(value),0)
    events.append(dict(timestamp_ms=round(t),type=1 if cmd=='bpm' else 0,payload=payload));continue
   i=len(notes);n=dict(id=i,group_id=i,start_ms=round(p[0]),end_ms=round(p[0]),packed_flags=0,extra_flags=0,relay=0)
   if cmd in ('tap','hold'):
    if cmd=='tap':t,w,lane=p;dur=0
    else:t,lane,w,dur=p
    side={0:2,5:3}.get(int(lane),1);sx=rat(lane,Fraction(4));sw=rat(w,Fraction(4))
    n['rats']=[sx,sx,sw,sw];n['end_ms']+=round(dur);n['packed_flags']=((1 if cmd=='tap' else 2)<<8)|side
   elif cmd=='flick':
    t,x,den,w,flag=p;sx,sw=sky_rats(x,w,den)
    n['rats']=[sx,sx,sw,sw];n['packed_flags']=0x404;n['extra_flags']=int(flag)<<8
   elif cmd=='skyarea':
    if len(p)==11:
     t,x,d,w,ex,ed,ew,el,er,dur,group=p;n['group_id']=int(group)
    else:
     t,x,d,w,ex,ed,ew,el,er,dur=p;implicit+=1
    sx,sw=sky_rats(x,w,d);ex,ew=sky_rats(ex,ew,ed)
    n['rats']=[sx,ex,sw,ew];n['packed_flags']=0x504
    n['extra_flags']=[4,8,16][int(el)]|[32,64,128][int(er)];n['end_ms']+=round(dur)
   else:raise ValueError(f'命令 {cmd} 没有对应的游戏二进制编码')
   notes.append(n)
  except (ValueError,TypeError,IndexError,ZeroDivisionError,struct.error) as error:
   raise ValueError(f'第 {line_no} 行无法编码: {error}') from error
 if bpm is None:raise ValueError('无法编码 ICP1 文件头：缺少 chart(BPM,拍数)')
 b=encode(notes,events,lookup,bpm,meter)
 sky=[n for n in notes if n['packed_flags']==0x504]
 return b,dict(note_count=len(notes),event_count=len(events),bpm=bpm,beats_per_bar=meter,
               raw_chart=True,skyarea_count=len(sky),sky_group_count=len({n['group_id'] for n in sky}),
               implicit_skyarea_count=implicit,auto_skyarea_count=0,auto_sky_group_splits=0,
               ignored_skyartcapture_count=declarations)
