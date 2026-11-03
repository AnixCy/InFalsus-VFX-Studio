using System.Security.Cryptography;
using InFalsus.Presentation;

namespace InFalsus.Vfx.Addon;

public sealed class AddonManifest
{
    public int AddonVersion { get; set; } = 1;
    public string Runtime { get; set; } = "runtime.json";
    public Dictionary<string,string> Files { get; set; } = new();
}
public sealed class AddonTarget
{
    public string Label { get; set; } = "";
    public string SongKey { get; set; } = "";
    public int Difficulty { get; set; }
    public string ChartFingerprint { get; set; } = "";
}
public static class AddonPackage
{
    public const string ImageCapability = "texture.image-file";
    public const string MaterialPath = "vfx/addon-background";
    public static readonly string[] Capabilities = { "clock.chart", "clock.audio", "background.material", "shader.bundle", ImageCapability };
    public static string Hash(string path)
    {
        using var input=File.OpenRead(path);using var sha=SHA256.Create();
        return BitConverter.ToString(sha.ComputeHash(input)).Replace("-", "").ToLowerInvariant();
    }
    public static EffectPackage Load(string manifestFile, Func<string,AddonManifest> decodeManifest,
        Func<string,PackageDocument> decodeRuntime, IReadOnlyCollection<string> capabilities)
    {
        if(new FileInfo(manifestFile).Length>4*1024*1024)throw new InvalidDataException("Addon manifest exceeds 4 MiB.");
        var manifest=decodeManifest(File.ReadAllText(manifestFile));var root=Path.GetDirectoryName(Path.GetFullPath(manifestFile))!;
        if(manifest==null||manifest.AddonVersion!=1||manifest.Files==null||manifest.Files.Count==0||manifest.Files.Count>32)
            throw new InvalidDataException("Unsupported addon manifest.");
        if(!manifest.Files.ContainsKey(manifest.Runtime))throw new InvalidDataException("Runtime is not covered by addon hashes.");
        foreach(var item in manifest.Files) {
            var file=EffectPackage.ResolveAsset(root,item.Key);
            if(item.Value==null||item.Value.Length!=64||!string.Equals(Hash(file),item.Value,StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Addon integrity mismatch: "+item.Key);
        }
        var runtimePath=EffectPackage.ResolveAsset(root,manifest.Runtime);
        if(new FileInfo(runtimePath).Length>4*1024*1024)throw new InvalidDataException("Runtime exceeds 4 MiB.");
        var package=EffectPackage.FromDocument(decodeRuntime(File.ReadAllText(runtimePath)),root,capabilities);
        foreach(var node in package.Document.Nodes) {
            if(!manifest.Files.ContainsKey(node.Bundle))throw new InvalidDataException("Bundle is not covered by addon hashes.");
            foreach(var image in node.Textures.Values.Where(v=>v.StartsWith("file:",StringComparison.Ordinal)))
                if(!manifest.Files.ContainsKey(image.Substring(5)))throw new InvalidDataException("Image is not covered by addon hashes.");
        }
        return package;
    }
}
