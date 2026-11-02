import sys,json,copy,struct,ctypes,hashlib
from pathlib import Path
sys.dont_write_bytecode=True
import argparse
parser=argparse.ArgumentParser(description='Build the private native-triangle vertex adapter from locally owned 1.0.6 runtime resources.')
parser.add_argument('--native-game',type=Path,required=True)
parser.add_argument('--editor',type=Path,required=True)
parser.add_argument('--output',type=Path,required=True)
args=parser.parse_args();game=args.native_game.resolve();root=args.output.resolve();root.mkdir(parents=True,exist_ok=True);(root/'Research').mkdir(exist_ok=True);node=Path(__file__).resolve().parent.parent
sys.path.insert(0,str(game/'tools/import-deps'))
import UnityPy,lz4.block
def blob(ptr):
 vt=ctypes.cast(ptr,ctypes.POINTER(ctypes.POINTER(ctypes.c_void_p))).contents;address=ctypes.WINFUNCTYPE(ctypes.c_void_p,ctypes.c_void_p)(vt[3])(ptr);size=ctypes.WINFUNCTYPE(ctypes.c_size_t,ctypes.c_void_p)(vt[4])(ptr);data=ctypes.string_at(address,size);ctypes.WINFUNCTYPE(ctypes.c_ulong,ctypes.c_void_p)(vt[2])(ptr);return data
dll=ctypes.WinDLL('d3dcompiler_47');fn=dll.D3DCompile;fn.restype=ctypes.c_long;fn.argtypes=[ctypes.c_void_p,ctypes.c_size_t,ctypes.c_char_p,ctypes.c_void_p,ctypes.c_void_p,ctypes.c_char_p,ctypes.c_char_p,ctypes.c_uint,ctypes.c_uint,ctypes.POINTER(ctypes.c_void_p),ctypes.POINTER(ctypes.c_void_p)]
hlsl=(node/'Source/Native/Shaders/NativeSideTriangles.hlsl').read_bytes();ptr=ctypes.c_void_p();err=ctypes.c_void_p();hr=fn(ctypes.create_string_buffer(hlsl),len(hlsl),None,None,None,b'main',b'vs_4_0',1<<11,0,ctypes.byref(ptr),ctypes.byref(err));message=blob(err).decode('utf8','replace') if err else '';assert hr>=0,message;dx=blob(ptr)
dis=dll.D3DDisassemble;dis.argtypes=[ctypes.c_void_p,ctypes.c_size_t,ctypes.c_uint,ctypes.c_char_p,ctypes.POINTER(ctypes.c_void_p)];dis.restype=ctypes.c_long;ptr=ctypes.c_void_p();assert dis(ctypes.create_string_buffer(dx),len(dx),0,None,ctypes.byref(ptr))>=0;text=blob(ptr).decode('utf8','replace');(root/'Research/Adapter-vs.asm').write_text(text,encoding='utf8');assert 'dcl_input v3.xyzw' in text
env=UnityPy.load(str(game/'infalsus_Data/StreamingAssets/aa/StandaloneWindows64/8719fa86ae7f9dbe9b11654f9dc69197.bundle'));original=next(o for o in env.objects if o.type.name=='Shader' and o.path_id==1723926395669787740);t=original.read_typetree();assert len(t['compressedLengths'])==1 and len(t['compressedLengths'][0])==1
raw=lz4.block.decompress(bytes(t['compressedBlob']),uncompressed_size=t['decompressedLengths'][0][0]);n=struct.unpack_from('<I',raw)[0];records=[bytearray(raw[a:a+size]) for a,size,_ in struct.iter_unpack('<3I',raw[4:4+12*n])];before=[hashlib.sha256(r).hexdigest() for r in records]
record=records[1];at=record.find(b'DXBC');assert at>32;oldsize=struct.unpack_from('<I',record,28)[0];tail=record[(32+oldsize+3)&~3:];newcode=record[32:at]+dx;newrecord=bytearray(record[:28])+struct.pack('<I',len(newcode))+newcode;newrecord.extend(b'\0'*(-len(newrecord)%4));newrecord.extend(tail);records[1]=newrecord
assert all(hashlib.sha256(r).hexdigest()==before[i] for i,r in enumerate(records) if i!=1)
program=bytearray(4+12*n);struct.pack_into('<I',program,0,n)
for i,r in enumerate(records):struct.pack_into('<3I',program,4+12*i,len(program),len(r),0);program.extend(r)
packed=lz4.block.compress(bytes(program),store_size=False);t['compressedBlob']=list(packed);t['compressedLengths']=[[len(packed)]];t['decompressedLengths']=[[len(program)]];t['m_ParsedForm']['m_Name']='InFalsusStudio/NativeSideTriangles'
base=args.editor.resolve()/'BepInEx/plugins/InfStudioVisualUI/Assets/nativevisual.bundle';outenv=UnityPy.load(str(base));bundle=next(iter(outenv.files.values()));a=next(a for a in outenv.assets if any(o.type.name=='AssetBundle' for o in a.objects.values()));ab=next(o for o in a.objects.values() if o.type.name=='AssetBundle');shader=copy.copy(original);shader.path_id=7100083;shader.assets_file=a;shader.type_id=next(i for i,x in enumerate(a.types) if x.class_id==48);shader.serialized_type=a.types[shader.type_id];shader.data=original.get_raw_data();a.objects={ab.path_id:ab,shader.path_id:shader};shader.save_typetree(t)
bt=ab.read_typetree();p=dict(m_FileID=0,m_PathID=shader.path_id);bt['m_PreloadTable']=[p];bt['m_Container']=[['native/compatibility/side-triangles',dict(preloadIndex=0,preloadSize=1,asset=p)]];bt['m_Name']='side-triangles.bundle';ab.save_typetree(bt);a.externals=[];a.mark_changed();a.name='CAB-'+hashlib.sha256(bytes(program)).hexdigest()[:32];bundle.files={a.name:a};path=root/'Payload/Assets/side-triangles.bundle';path.parent.mkdir(parents=True,exist_ok=True);path.write_bytes(bundle.save(packer='lz4'))
report=dict(nativeShaderSha256=hashlib.sha256(original.get_raw_data()).hexdigest(),modifiedRecords=[1],allOtherProgramRecordsUnchanged=True,sourceHlslSha256=hashlib.sha256(hlsl).hexdigest(),bundleSha256=hashlib.sha256(path.read_bytes()).hexdigest(),bundleBytes=path.stat().st_size);(root/'Research/adapter-proof.json').write_text(json.dumps(report,indent=2),encoding='utf8');print(report)
