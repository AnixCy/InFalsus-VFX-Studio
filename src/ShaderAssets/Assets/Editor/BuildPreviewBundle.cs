using System;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class BuildPreviewBundle
{
    public static void Build()
    {
        string output = Environment.GetEnvironmentVariable("VFX_SHADER_OUTPUT");
        if (string.IsNullOrWhiteSpace(output)) throw new InvalidOperationException("Set VFX_SHADER_OUTPUT.");
        Directory.CreateDirectory(output);
        var importer = AssetImporter.GetAtPath("Assets/ParameterPreview.shader");
        importer.assetBundleName = "preview.bundle"; importer.assetBundleVariant = "";
        AssetDatabase.SaveAssets();
        var build = new AssetBundleBuild {
            assetBundleName = "preview.bundle", assetNames = new[] { "Assets/ParameterPreview.shader", "Assets/UtilityPost.shader" },
            addressableNames = new[] { "vfx/parameter-preview", "vfx/utility-post" }
        };
        var result = BuildPipeline.BuildAssetBundles(output, new[] { build },
            BuildAssetBundleOptions.ChunkBasedCompression | BuildAssetBundleOptions.ForceRebuildAssetBundle,
            BuildTarget.StandaloneWindows64);
        if (result == null) throw new InvalidOperationException("Preview shader build failed.");
        var shader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/ParameterPreview.shader");
        if (ShaderUtil.ShaderHasError(shader)) throw new InvalidOperationException("Preview shader has compiler errors; see shader-build.log.");
        if (ShaderUtil.ShaderHasError(AssetDatabase.LoadAssetAtPath<Shader>("Assets/UtilityPost.shader"))) throw new InvalidOperationException("Utility shader compiler error.");
        var verified = AssetBundle.LoadFromFile(Path.Combine(output,"preview.bundle"));
        if (!verified) throw new InvalidOperationException("Generated file is not a loadable AssetBundle.");
        try { if (!verified.LoadAsset<Shader>("vfx/parameter-preview")) throw new InvalidOperationException("Preview shader entry missing."); }
        finally { verified.Unload(true); }
    }
}
