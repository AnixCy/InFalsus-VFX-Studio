using UnityEngine;
using Object=UnityEngine.Object;

namespace InFalsus.Vfx.StudioPlus;

// An opaque background-only display, after the host backdrop and before chart geometry.
// The host backdrop, notes, camera settings and input handlers remain untouched.
internal sealed class ChartBackgroundDisplay:IDisposable
{
    readonly Camera camera;
    readonly GameObject root;
    readonly Mesh mesh;
    readonly Material material;
    internal bool Visible=>root&&root.activeSelf;
    internal Texture? Texture=>material.mainTexture;
    internal ChartBackgroundDisplay(Camera camera)
    {
        this.camera=camera;
        var shader=Resources.Load<Shader>("InFalsusStudio/Background");
        if(!shader||!shader.isSupported)throw new NotSupportedException("制谱背景显示材质不可用。");
        material=new Material(shader){name="VFX chart background display",renderQueue=1100};
        material.SetFloat("_Brightness",1);
        mesh=new Mesh{name="VFX chart background plane"};
        mesh.vertices=new[]{new Vector3(-1,-1,0),new Vector3(1,-1,0),new Vector3(1,1,0),new Vector3(-1,1,0)};
        mesh.uv=new[]{Vector2.zero,Vector2.right,Vector2.one,Vector2.up};
        mesh.triangles=new[]{0,2,1,0,3,2};mesh.RecalculateBounds();
        root=new GameObject("VFX chart background (view only)",typeof(MeshFilter),typeof(MeshRenderer)){layer=30};
        root.transform.SetParent(camera.transform,false);
        root.GetComponent<MeshFilter>().sharedMesh=mesh;
        var renderer=root.GetComponent<MeshRenderer>();renderer.sharedMaterial=material;
        renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;renderer.receiveShadows=false;
        root.SetActive(false);
    }
    internal void Show(RenderTexture texture)
    {
        float depth=camera.farClipPlane*.93f;
        float halfHeight=camera.orthographic?camera.orthographicSize:depth*Mathf.Tan(camera.fieldOfView*Mathf.Deg2Rad*.5f);
        root.transform.localPosition=new Vector3(0,0,depth);root.transform.localRotation=Quaternion.identity;
        root.transform.localScale=new Vector3(halfHeight*camera.aspect*1.001f,halfHeight*1.001f,1);
        material.mainTexture=texture;root.SetActive(true);
    }
    internal void Hide(){if(root)root.SetActive(false);material.mainTexture=null;}
    public void Dispose(){Object.DestroyImmediate(root);Object.DestroyImmediate(mesh);Object.DestroyImmediate(material);}
}
