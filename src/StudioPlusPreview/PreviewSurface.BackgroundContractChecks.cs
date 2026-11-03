using InFalsus.Presentation;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;
using Object=UnityEngine.Object;

namespace InFalsus.Vfx.StudioPlus;
internal sealed partial class PreviewSurface
{
    // Optional development fixture. Reads pixels numerically; creates no image files.
    internal void CheckBackgroundRenderContract(PreviewModel model,PlaybackSnapshot snapshot,Action<bool,string> check)
    {
        var probe=GameObject.CreatePrimitive(PrimitiveType.Quad);probe.name="VFX transparent depth probe";
        probe.layer=31;probe.transform.SetParent(root.transform);probe.transform.localScale=new Vector3(.25f,.25f,1);
        var native=AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a=>a.GetName().Name=="InfStudioVisualUI")
            ?.GetType("InFalsusStudio.NativeVisual.NativeVisualPlugin")
            ?.GetMethod("Mat",BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic);
        var tap=native?.Invoke(null,new object[]{"matid_notesingle"}) as Material;
        var flick=native?.Invoke(null,new object[]{"FlickV5 Left"}) as Material;
        if(!tap||!flick){Object.DestroyImmediate(probe);throw new InvalidOperationException("Native note probe materials unavailable.");}
        var material=new Material(tap!){renderQueue=3000};
        material.SetTexture("_BaseMap",Texture2D.whiteTexture);material.SetFloat("_Surface",1);
        material.SetFloat("_SrcBlend",(float)BlendMode.SrcAlpha);material.SetFloat("_DstBlend",(float)BlendMode.OneMinusSrcAlpha);
        material.SetFloat("_ZWrite",0);material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.SetShaderPassEnabled("DepthOnly",false);material.SetShaderPassEnabled("DepthNormals",false);
        var flickMaterial=new Material(flick!){renderQueue=3000};
        foreach(var texture in new[]{"_Base","_Bar","_Core"})flickMaterial.SetTexture(texture,Texture2D.whiteTexture);
        flickMaterial.SetFloat("_Scale",1);
        var readback=new Texture2D(1,1,TextureFormat.RGBAFloat,false,true);
        Color Pixel()
        {
            var target=Render(model,snapshot,64,64);var previous=RenderTexture.active;
            try{RenderTexture.active=target;readback.ReadPixels(new Rect(32,32,1,1),0,0);readback.Apply();return readback.GetPixel(0,0);}
            finally{RenderTexture.active=previous;}
        }
        static float Distance(Color a,Color b)=>Mathf.Abs(a.r-b.r)+Mathf.Abs(a.g-b.g)+Mathf.Abs(a.b-b.b);
        try
        {
            probe.SetActive(false);Color baseline=Pixel();
            foreach(var note in new[]{(Name:"Tap",Material:material),(Name:"Flick",Material:flickMaterial)})
            {
                probe.GetComponent<MeshRenderer>().sharedMaterial=note.Material;
                void Alpha(float a){if(note.Name=="Tap")note.Material.SetColor("_BaseColor",new Color(1,1,1,a));else note.Material.SetFloat("_Opacity",a);}
                probe.SetActive(true);probe.transform.localPosition=new Vector3(0,0,1);Alpha(1);
                check(Distance(Pixel(),baseline)<.002f,note.Name+": background depth occludes the native note shader behind its plane");
                probe.transform.localPosition=new Vector3(0,0,-1);Alpha(0);Color zero=Pixel();
                Alpha(.5f);Color half=Pixel();Alpha(1);Color full=Pixel();
                check(Distance(zero,baseline)<.002f&&Distance(full,baseline)>.02f,note.Name+": foreground native note is visible and zero alpha is hidden");
                check(Distance(half,zero)>.005f&&Distance(half,full)>.005f,note.Name+": native note shader retains an intermediate GPU fade state");
            }
            probe.SetActive(false);var block=new MaterialPropertyBlock();block.SetColor("_Color",Color.black);
            quad.GetComponent<MeshRenderer>().SetPropertyBlock(block);
            check(Distance(Pixel(),baseline)>.02f,"background honors native Constrained2D scene color");
            quad.GetComponent<MeshRenderer>().SetPropertyBlock(null);
            check(Distance(Pixel(),baseline)<.002f,"clearing native scene color restores the unchanged background");
        }
        finally
        {
            quad.GetComponent<MeshRenderer>().SetPropertyBlock(null);
            Object.DestroyImmediate(readback);Object.DestroyImmediate(probe);Object.DestroyImmediate(material);Object.DestroyImmediate(flickMaterial);
        }
    }
}
