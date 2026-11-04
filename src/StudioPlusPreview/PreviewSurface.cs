using InFalsus.Presentation;
using InFalsusStudio;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object=UnityEngine.Object;

namespace InFalsus.Vfx.StudioPlus;

internal sealed partial class PreviewSurface:IDisposable
{
    sealed class Resource {public string Path="";public Material Material=null!;public EffectDefinition Definition=null!;public readonly HashSet<string> Integers=new();}
    readonly string folder;
    readonly Camera hostCamera,previewCamera;
    readonly GameObject root,quad;
    readonly Mesh mesh;
    readonly Dictionary<string,AssetBundle> bundles=new(StringComparer.OrdinalIgnoreCase);
    readonly Dictionary<string,Resource> resources=new();
    readonly Dictionary<string,int> ids=new();
    readonly ClockSource clock=new();
    readonly CommandBuffer command=new(){name="VFX isolated postprocess"};
    readonly UniversalRenderPipeline.SingleCameraRequest request=new();
    RenderTexture? scene,post,alternate;
    RTHandle? sceneHandle,postHandle,alternateHandle;
    Texture2D? ownedTexture;
    Material? blendMaterial;
    bool imageOverride;
    int width,height;
    internal bool Enabled {get;set;}=true;
    internal Material Material=>resources["background"].Material;
    internal int ResourceCount=>resources.Count;
    internal int LastPostProcessDraws {get;private set;}
    internal Material NodeMaterial(string id)=>resources[id].Material;
    internal PreviewSurface(Camera view,string folder)
    {
        this.folder=folder;hostCamera=view;
        root=new GameObject("VFX isolated preview");
        var cameraObject=new GameObject("VFX preview camera");cameraObject.transform.SetParent(root.transform);
        previewCamera=cameraObject.AddComponent<Camera>();previewCamera.enabled=false;previewCamera.orthographic=true;
        previewCamera.orthographicSize=.5f;previewCamera.transform.position=new Vector3(0,0,-10);
        previewCamera.cullingMask=1<<31;previewCamera.clearFlags=CameraClearFlags.SolidColor;previewCamera.backgroundColor=Color.black;
        previewCamera.allowHDR=true;previewCamera.allowMSAA=false;
        previewCamera.GetUniversalAdditionalCameraData().renderPostProcessing=false;
        mesh=new Mesh{name="VFX background plane"};
        mesh.vertices=new[]{new Vector3(-.5f,-.5f,0),new Vector3(.5f,-.5f,0),new Vector3(.5f,.5f,0),new Vector3(-.5f,.5f,0)};
        mesh.uv=new[]{Vector2.zero,Vector2.right,Vector2.one,Vector2.up};mesh.triangles=new[]{0,2,1,0,3,2};mesh.RecalculateBounds();
        quad=new GameObject("VFX image plane",typeof(MeshFilter),typeof(MeshRenderer)){layer=31};quad.transform.SetParent(root.transform);
        quad.GetComponent<MeshFilter>().sharedMesh=mesh;
        quad.SetActive(false);
        Ensure(PreviewModel.Template(Application.unityVersion,QualitySettings.activeColorSpace.ToString()),EffectLibrary.Load(folder));
    }
    Resource Load(MaterialNodeDocument node,EffectLibrary library)
    {
        if(!bundles.TryGetValue(node.Bundle,out var bundle)) {
            bundle=AssetBundle.LoadFromFile(EffectPackage.ResolveAsset(folder,node.Bundle));
            if(!bundle)throw new InvalidDataException("无法加载效果资源："+node.Bundle);bundles.Add(node.Bundle,bundle);
        }
        var asset=bundle.LoadAsset<Object>(node.Material);
        Material material=asset is Shader shader?new Material(shader):asset is Material original?new Material(original):
            throw new InvalidDataException("找不到效果材质："+node.Material);
        if(!material.shader.isSupported){Object.DestroyImmediate(material);throw new NotSupportedException("当前设备不支持该效果。");}
        var resource=new Resource{Path=node.Bundle+":"+node.Material,Material=material,Definition=library.Resolve(node)};
        for(int i=0;i<material.shader.GetPropertyCount();i++)if(material.shader.GetPropertyType(i)==ShaderPropertyType.Int)resource.Integers.Add(material.shader.GetPropertyName(i));
        return resource;
    }
    void Ensure(PackageDocument document,EffectLibrary library,Func<MaterialNodeDocument,bool>? active=null,MaterialNodeDocument? backgroundOverride=null)
    {
        foreach(var original in document.Nodes) {
            if(SceneOpacityPlan.IsControl(original))continue;
            var node=original.Target=="background"?backgroundOverride??original:original;
            if(library.Resolve(node).BackgroundSource.Length>0)continue;
            if(active!=null&&!active(node))continue;
            var path=node.Bundle+":"+node.Material;
            if(resources.TryGetValue(node.Id,out var old)&&old.Path==path){old.Definition=library.Resolve(node);continue;}
            var candidate=Load(node,library);
            if(old!=null)Object.DestroyImmediate(old.Material);
            resources[node.Id]=candidate;
            if(node.Target=="background") {
                candidate.Material.renderQueue=1100;quad.GetComponent<MeshRenderer>().sharedMaterial=candidate.Material;
                ApplyBackgroundTexture();
            }
        }
        foreach(var key in resources.Keys.Where(id=>!document.Nodes.Any(n=>n.Id==id)).ToArray()) {
            Object.DestroyImmediate(resources[key].Material);resources.Remove(key);
        }
        foreach(var path in bundles.Keys.Where(path=>!document.Nodes.Any(n=>n.Bundle==path)&&!(path=="preview.bundle"&&blendMaterial)).ToArray()) {
            bundles[path].Unload(true);bundles.Remove(path);
        }
    }
    int Id(string name){if(!ids.TryGetValue(name,out int id))ids[name]=id=Shader.PropertyToID(name);return id;}
    internal FrameContext Draw(PreviewModel model,in PlaybackSnapshot snapshot,bool committedOnly)
    {
        var frame=clock.Sample(snapshot,model.Package.Tempo,model.Package.Document.TimeDomain,model.Package.Document.OffsetSeconds,model.Package.Document.Seed);
        var document=model.RenderDocument;
        model.Sample(frame.Seconds,committedOnly);
        backgroundFrame=ResolveBackgroundControls(model,frame.Seconds);
        Ensure(document,model.Library,n=>model.IsActive(n,frame.Seconds),backgroundFrame?.Node);
        for(int i=0;i<model.Values.Length;i++) {
            var p=model.Catalog.Parameters[i];if(!resources.TryGetValue(p.Binding.Node,out var resource))continue;
            var m=resource.Material;var v=model.Values[i].Number;
            if(p.Binding.Property.StartsWith("_KW_",StringComparison.Ordinal)||!m.HasProperty(Id(p.Binding.Property)))continue;
            if(p.Binding.Kind==ParameterKind.Color)m.SetColor(Id(p.Binding.Property),new Color(v.X,v.Y,v.Z,v.W));
            else if(p.Binding.Kind==ParameterKind.Vector)m.SetVector(Id(p.Binding.Property),new Vector4(v.X,v.Y,v.Z,v.W));
            else if(resource.Integers.Contains(p.Binding.Property))m.SetInteger(Id(p.Binding.Property),Mathf.RoundToInt(v.X));
            else m.SetFloat(Id(p.Binding.Property),v.X);
        }
        foreach(var pair in resources) {
            var node=document.Nodes.Single(n=>n.Id==pair.Key);if(!model.IsActive(node,frame.Seconds))continue;
            var r=pair.Value;var m=r.Material;
            foreach(var keyword in r.Definition.Keywords) {
                string canonical=(keyword.StartsWith("USE_",StringComparison.Ordinal)?keyword.Substring(4):keyword).Replace("_","");bool enabled=false;float control=0;
                for(int i=0;i<model.Values.Length;i++) {
                    var p=model.Catalog.Parameters[i];if(p.Binding.Node!=pair.Key)continue;
                    if(p.Binding.Property=="_KW_"+keyword)control=model.Values[i].Number.X;
                    else if(p.Binding.Property.Replace("_","").Replace("Amount","").ToUpperInvariant()==canonical)
                        enabled|=Math.Abs(model.Values[i].Number.X)>.000001f;
                }
                if(control>.5f)enabled=true;else if(control<-.5f)enabled=false;
                var local=new LocalKeyword(m.shader,keyword);if(local.isValid)m.SetKeyword(local,enabled);
            }
            m.SetFloat("_PresentationTime",(float)frame.Seconds);m.SetFloat("_CurrentTime",(float)frame.Seconds);
            m.SetFloat("_EffectTime",(float)(frame.Seconds-node.StartSeconds));
            m.SetFloat("_EffectDuration",(float)(node.EndSeconds.HasValue?node.EndSeconds.Value-node.StartSeconds:0));
            m.SetFloat("_EffectProgress",node.EndSeconds.HasValue?(float)Math.Clamp((frame.Seconds-node.StartSeconds)/(node.EndSeconds.Value-node.StartSeconds),0,1):0);
            m.SetInt("_FrameCount",(int)Math.Floor(frame.Seconds*120));
            float aspect=width>0?(float)width/height:hostCamera.aspect;
            if(r.Definition.Original)m.SetVector("_Aspect",new Vector4(aspect,1/aspect,0,0));else m.SetFloat("_Aspect",aspect);
            m.SetVector("_R2",new Vector4((float)(frame.VisualTick*.754877666%1),(float)(frame.VisualTick*.569840296%1),0,0));
        }
        ApplyBackgroundControls(model);
        return frame;
    }
    internal RenderTexture Render(PreviewModel model,in PlaybackSnapshot snapshot,int requestedWidth,int requestedHeight)
    {
        int w=Mathf.Clamp(requestedWidth,32,2560),h=Mathf.Clamp(requestedHeight,32,1440);
        if(scene==null||w!=width||h!=height) {
            ReleaseTargets();width=w;height=h;
            scene=Target("VFX scene",24);post=Target("VFX post",0);alternate=Target("VFX alternate",0);
            sceneHandle=RTHandles.Alloc(scene);postHandle=RTHandles.Alloc(post);alternateHandle=RTHandles.Alloc(alternate);
        }
        var frame=Draw(model,snapshot,false);previewCamera.aspect=(float)w/h;quad.transform.localScale=new Vector3(previewCamera.aspect,1,1);
        request.destination=scene;quad.SetActive(model.RenderDocument.Nodes.Any(n=>n.Target=="background"&&model.IsActive(n,frame.Seconds)));
        try{RenderPipeline.SubmitRenderRequest(previewCamera,request);}finally{quad.SetActive(false);}
        var handles=new[]{sceneHandle!,postHandle!,alternateHandle!};var targets=new[]{scene!,post!,alternate!};
        RenderTexture result=scene!;int index=0;
        command.Clear();command.SetGlobalVector("_BlitTexture_TexelSize",new Vector4(1f/w,1f/h,w,h));
        bool hasCommands=false;
        LastPostProcessDraws=0;
        double seconds=frame.Seconds;
        foreach(var node in model.RenderDocument.Nodes.Where(n=>n.Target=="postprocess"&&!SceneOpacityPlan.IsControl(n)&&model.Definition(n).BackgroundSource.Length==0&&model.IsActive(n,seconds))) {
            float opacity=model.Opacity(node.Id);if(opacity<=0)continue;
            int before=index;index=(index+1)%3;command.SetRenderTarget(targets[index]);
            Blitter.BlitTexture(command,handles[before],new Vector4(1,1,0,0),resources[node.Id].Material,0);hasCommands=true;LastPostProcessDraws++;
            if(opacity<.99999f) {
                if(!blendMaterial) {
                    if(!bundles.TryGetValue("preview.bundle",out var bundle)){bundle=AssetBundle.LoadFromFile(Path.Combine(folder,"preview.bundle"));bundles.Add("preview.bundle",bundle);}
                    blendMaterial=new Material(bundle.LoadAsset<Shader>("vfx/effect-blend"));
                }
                int effected=index;index=(index+1)%3;command.SetRenderTarget(targets[index]);
                command.SetGlobalTexture("_VfxBeforeTexture",targets[before]);command.SetGlobalFloat("_VfxBlend",opacity);
                Blitter.BlitTexture(command,handles[effected],new Vector4(1,1,0,0),blendMaterial,0);
                LastPostProcessDraws++;
            }
            result=targets[index];
        }
        if(hasCommands)Graphics.ExecuteCommandBuffer(command);return result;
    }
    RenderTexture Target(string name,int depth){var rt=new RenderTexture(width,height,depth,RenderTextureFormat.ARGBHalf){name=name};if(!rt.Create())throw new InvalidOperationException("无法分配预览画布。");return rt;}
    internal Texture2D PrepareImage(string path)
    {
        var info=new FileInfo(path);if(!info.Exists||info.Length>32*1024*1024)throw new InvalidDataException("请选择不超过 32 MiB 的 PNG/JPEG 图片。");
        var bytes=File.ReadAllBytes(path);typeof(StudioBootstrap).Assembly.GetType("InFalsusStudio.Core.LocalImageSize")!
            .GetMethod("Validate",StudioBridge.Flags)!.Invoke(null,new object[]{bytes,8192,33554432});
        var image=new Texture2D(2,2,TextureFormat.RGBA32,false);
        try{if(!ImageConversion.LoadImage(image,bytes))throw new InvalidDataException("图片解码失败。");image.wrapMode=TextureWrapMode.Clamp;return image;}
        catch{Object.Destroy(image);throw;}
    }
    internal void ApplyImage(Texture2D? image)
    {
        var old=ownedTexture;ownedTexture=image;imageOverride=image!=null;ApplyBackgroundTexture();if(old)Object.Destroy(old);
    }
    void ApplyBackgroundTexture()
    {
        if(!resources.TryGetValue("background",out var r))return;
        string property=r.Definition.Original?"_BaseMap":"_MainTex";
        if(backgroundFrame!=null)r.Material.SetTexture(property,BackgroundInputTexture());
        else if(imageOverride)r.Material.SetTexture(property,ownedTexture);
        else if(!r.Definition.Original)r.Material.SetTexture(property,Texture2D.whiteTexture);
        else if(bundles.TryGetValue(r.Definition.Bundle,out var bundle))r.Material.SetTexture(property,bundle.LoadAsset<Material>(r.Definition.Material).GetTexture(property));
    }
    void ReleaseTargets()
    {
        sceneHandle?.Release();postHandle?.Release();alternateHandle?.Release();sceneHandle=postHandle=alternateHandle=null;
        foreach(var rt in new[]{scene,post,alternate})if(rt){rt!.Release();Object.DestroyImmediate(rt);}
        scene=post=alternate=null;width=height=0;
    }
    internal void Suspend()=>ReleaseTargets();
    internal void ReleaseSource(string directory)
    {
        foreach(var pair in resources.Where(p=>string.Equals(p.Value.Definition.SourceDirectory,directory,StringComparison.OrdinalIgnoreCase)).ToArray()) {
            Object.DestroyImmediate(pair.Value.Material);resources.Remove(pair.Key);
        }
        foreach(var path in bundles.Keys.Where(path=>!resources.Values.Any(r=>r.Definition.Bundle==path)&&path!="preview.bundle").ToArray()) {
            bundles[path].Unload(true);bundles.Remove(path);
        }
    }
    public void Dispose()
    {
        ReleaseTargets();foreach(var r in resources.Values)Object.DestroyImmediate(r.Material);resources.Clear();
        Object.DestroyImmediate(root);Object.DestroyImmediate(mesh);if(ownedTexture)Object.Destroy(ownedTexture);
        if(blendMaterial)Object.DestroyImmediate(blendMaterial);
        foreach(var b in bundles.Values)b.Unload(true);bundles.Clear();command.Release();
    }
}
