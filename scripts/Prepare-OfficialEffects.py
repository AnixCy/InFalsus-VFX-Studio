"""Prepare local render-only assets from an existing read-only resource survey. No media in Git."""
import argparse
import hashlib
import json
from pathlib import Path
import UnityPy

def sha(p): return hashlib.sha256(p.read_bytes()).hexdigest()
def main():
    p=argparse.ArgumentParser(description=__doc__)
    p.add_argument('--game',type=Path,required=True)
    p.add_argument('--survey',type=Path,required=True)
    p.add_argument('--output',type=Path,required=True)
    a=p.parse_args(); out=a.output.resolve(); game=a.game.resolve()
    if out.is_relative_to(game): raise ValueError('Output must be outside the game installation.')
    out.mkdir(parents=True,exist_ok=False)
    survey=json.loads(a.survey.read_text('utf-8-sig'))
    shaders={x['name']:x for x in survey['shaders']}
    definitions=[];receipts=[]
    for index,scene in enumerate(survey['scenes'],1):
        bundle=scene['materials'][0]['bundle']
        source=game/'infalsus_Data/StreamingAssets/aa/StandaloneWindows64'/bundle
        before=sha(source)
        if before!=survey['bundlesRead'][bundle]['sha256']: raise ValueError('Source bundle changed since the survey; inspect it before preparing.')
        env=UnityPy.load(str(source))
        directory=next(o for o in env.objects if o.type.name=='AssetBundle')
        tree=directory.read_typetree(); entries=[];preload=[]; hashes={}
        family=f'official-{index:02d}'
        for obj in list(env.objects):
            if obj.type.name not in ('AssetBundle','Shader','Material','Texture2D'):
                del obj.assets_file.objects[obj.path_id]; continue
            if obj.type.name=='AssetBundle': continue
            pointer={'m_FileID':0,'m_PathID':obj.path_id};preload.append(pointer)
            data=obj.read_typetree()
            hashes[obj.path_id]=hashlib.sha256(obj.get_raw_data()).hexdigest()
            if obj.type.name=='Material':
                if data['m_Shader']['m_FileID']!=0: raise ValueError('External shader dependency requires an explicit resolver.')
                role='background' if 'BGShader' in next(m['shader'] for m in scene['materials'] if m['pathId']==obj.path_id) else 'postprocess'
                key=f'{family}/{role}.mat'
                entries.append([key,{'preloadIndex':0,'preloadSize':0,'asset':pointer}])
        for _,value in entries: value['preloadSize']=len(preload)
        tree.update(m_Name='vfx-'+family,m_AssetBundleName='vfx-'+family,m_Container=entries,
                    m_PreloadTable=preload,m_Dependencies=[])
        directory.save_typetree(tree)
        dest=out/(family+'.bundle');dest.write_bytes(env.file.save(packer='lz4'))
        checked=UnityPy.load(str(dest))
        for o in checked.objects:
            if o.path_id in hashes: assert hashlib.sha256(o.get_raw_data()).hexdigest()==hashes[o.path_id]
        assert sha(source)==before
        receipts.append({'family':family,'sourceSha256':before,'outputSha256':sha(dest),'renderObjectsUnchanged':True})
        for material in scene['materials']:
            info=shaders[material['shader']];role='background' if 'BGShader' in material['shader'] else 'postprocess'
            props=material['properties'];floats=dict(props['m_Floats']);colors=dict(props['m_Colors'])
            channels=[]
            for prop in info['properties']:
                name=prop['name'];kind=prop['type']
                if kind==4 or name in ('_FrameCount','_CurrentTime','_Aspect','_R2'): continue
                value=[floats.get(name,0.)] if kind in (2,3) else [colors.get(name,{}).get(k,1. if kind==0 else 0.) for k in ('r','g','b','a')]
                channels.append({'property':name,'label':prop['description'],'kind':'Float' if kind in (2,3) else 'Color' if kind==0 else 'Vector',
                                 'value':value,'min':-1. if 'Rotation' in name or 'Offset' in name else 0.,'max':6.28 if 'Rotation' in name else 2. if 'Color' in name or 'Phase' in name else 1.})
            keywords=[k for k in info['keywords'] if k.startswith('USE_')]
            for keyword in keywords:
                channels.append({'property':'_KW_'+keyword,'label':keyword[4:].replace('_',' ')+' · 开关','kind':'Float','value':[0.],'min':-1.,'max':1.})
            features=' / '.join(k[4:].replace('_',' ') for k in keywords[:3]) or 'COLOR'
            definitions.append({'id':family+'-'+role,'label':f'官谱 {index:02d} · '+features,'target':role,
                'bundle':'Effects/'+dest.name,'material':f'{family}/{role}.mat','parameters':channels,'keywords':keywords,'original':True})
    (out/'effects.json').write_text(json.dumps({'effects':definitions},ensure_ascii=False,indent=2),encoding='utf-8')
    (out/'provenance.json').write_text(json.dumps({'surveySha256':sha(a.survey),'resources':receipts,'gameFilesModified':False},indent=2),encoding='utf-8')
    print(json.dumps({'families':len(receipts),'materials':len(definitions),'bytes':sum(x.stat().st_size for x in out.iterdir())}))
if __name__=='__main__': main()
