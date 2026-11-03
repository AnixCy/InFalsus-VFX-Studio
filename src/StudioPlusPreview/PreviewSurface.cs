using InFalsus.Presentation;
using InFalsusStudio;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace InFalsus.Vfx.StudioPlus;

internal sealed class PreviewSurface : IDisposable
{
    readonly AssetBundle bundle;
    readonly GameObject root;
    readonly Mesh mesh;
    readonly Material material;
    readonly Camera camera;
    readonly Dictionary<string, int> ids = new();
    readonly ClockSource clock = new();
    Texture2D? ownedTexture;
    internal Material Material => material;
    internal bool Enabled { get => root.activeSelf; set => root.SetActive(value); }
    internal PreviewSurface(Camera view, string folder)
    {
        camera = view;
        bundle = AssetBundle.LoadFromFile(Path.Combine(folder, "preview.bundle"));
        if (!bundle) throw new InvalidDataException("无法加载背景 Shader 资源。");
        var shader = bundle.LoadAsset<Shader>("vfx/parameter-preview");
        if (!shader || !shader.isSupported) { bundle.Unload(false); throw new InvalidDataException("背景 Shader 不受当前设备支持。"); }
        material = new Material(shader) { name = "VFX parameter preview (owned)", renderQueue = 1100 };
        material.SetTexture("_MainTex", Texture2D.whiteTexture);
        mesh = new Mesh { name = "VFX background plane" };
        mesh.vertices = new[] { new Vector3(-1,-1,0), new Vector3(1,-1,0), new Vector3(1,1,0), new Vector3(-1,1,0) };
        mesh.uv = new[] { Vector2.zero, Vector2.right, Vector2.one, Vector2.up };
        mesh.triangles = new[] { 0,2,1,0,3,2 }; mesh.RecalculateBounds();
        root = new GameObject("VFX background preview", typeof(MeshFilter), typeof(MeshRenderer)) { layer = 30 };
        root.transform.SetParent(camera.transform, false);
        root.GetComponent<MeshFilter>().sharedMesh = mesh;
        var renderer = root.GetComponent<MeshRenderer>(); renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
        foreach (var c in PreviewModel.Template(Application.unityVersion, QualitySettings.activeColorSpace.ToString()).Channels)
            ids[c.Property] = Shader.PropertyToID(c.Property);
        Enabled = true;
    }
    internal Texture2D PrepareImage(string path)
    {
        var info = new FileInfo(path);
        if (!info.Exists || info.Length > 32 * 1024 * 1024) throw new InvalidDataException("请选择不超过 32 MiB 的 PNG/JPEG 图片。");
        var bytes = File.ReadAllBytes(info.FullName);
        var validator = typeof(StudioBootstrap).Assembly.GetType("InFalsusStudio.Core.LocalImageSize")!;
        validator.GetMethod("Validate", StudioBridge.Flags)!.Invoke(null, new object[] { bytes, 8192, 33554432 });
        var image = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        try {
            if (!ImageConversion.LoadImage(image, bytes)) throw new InvalidDataException("图片解码失败。");
            image.wrapMode = TextureWrapMode.Clamp; image.filterMode = FilterMode.Bilinear;
            return image;
        } catch { Object.Destroy(image); throw; }
    }
    internal void ApplyImage(Texture2D? image)
    {
        var old = ownedTexture; ownedTexture = image;
        material.SetTexture("_MainTex", image ? image : Texture2D.whiteTexture);
        if (old) Object.Destroy(old);
    }
    internal FrameContext Draw(PreviewModel model, in PlaybackSnapshot snapshot, bool committedOnly)
    {
        var frame = clock.Sample(snapshot, model.Package.Tempo, model.Package.Document.TimeDomain,
            model.Package.Document.OffsetSeconds, model.Package.Document.Seed);
        model.Sample(frame.Seconds, committedOnly);
        for (int i = 0; i < model.Values.Length; i++) {
            var p = model.Catalog.Parameters[i]; var value = model.Values[i].Number;
            if (p.Binding.Kind == ParameterKind.Color)
                material.SetColor(ids[p.Binding.Property], new Color(value.X,value.Y,value.Z,value.W));
            else material.SetFloat(ids[p.Binding.Property], value.X);
        }
        material.SetFloat("_PresentationTime", (float)frame.Seconds);
        material.SetFloat("_Aspect", camera.aspect);
        float depth = camera.farClipPlane * .90f;
        float hh = camera.orthographic ? camera.orthographicSize : depth * Mathf.Tan(camera.fieldOfView * Mathf.Deg2Rad * .5f);
        root.transform.localPosition = new Vector3(0,0,depth);
        root.transform.localScale = new Vector3(hh*camera.aspect*1.002f,hh*1.002f,1);
        return frame;
    }
    public void Dispose()
    {
        if (root) Object.Destroy(root); if (mesh) Object.Destroy(mesh);
        if (material) Object.Destroy(material); if (ownedTexture) Object.Destroy(ownedTexture);
        if (bundle) bundle.Unload(false);
    }
}
