using System;
using System.IO;
using UnityEditor;
using UnityEngine;
public static class BuildCustomEffect
{
    public static void Build()
    {
        PlayerSettings.colorSpace=ColorSpace.Linear;
        string output=Environment.GetEnvironmentVariable("VFX_CUSTOM_OUTPUT");
        string entry=Environment.GetEnvironmentVariable("VFX_CUSTOM_ENTRY");
        string shaderPath=Environment.GetEnvironmentVariable("VFX_CUSTOM_SHADER");
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        var shader=AssetDatabase.LoadAssetAtPath<Shader>(shaderPath);
        if(!shader||ShaderUtil.ShaderHasError(shader))throw new InvalidOperationException("Custom shader has compiler errors.");
        string bundleName="effect-"+Guid.NewGuid().ToString("N")+".bundle";
        var result=BuildPipeline.BuildAssetBundles(output,new[]{new AssetBundleBuild{
            assetBundleName=bundleName,assetNames=new[]{shaderPath},addressableNames=new[]{entry}}},
            BuildAssetBundleOptions.ChunkBasedCompression|BuildAssetBundleOptions.ForceRebuildAssetBundle,BuildTarget.StandaloneWindows64);
        if(result==null||ShaderUtil.ShaderHasError(shader))throw new InvalidOperationException("Custom effect build failed.");
        File.Move(Path.Combine(output,bundleName),Path.Combine(output,"effect.bundle"));
        var bundle=AssetBundle.LoadFromFile(Path.Combine(output,"effect.bundle"));
        if(!bundle)throw new InvalidOperationException("Built bundle cannot be loaded.");
        try{if(!bundle.LoadAsset<Shader>(entry))throw new InvalidOperationException("Shader entry missing.");}
        finally{bundle.Unload(true);}
        File.WriteAllText(Path.Combine(output,"build-ok.txt"),Application.unityVersion);
    }
}
