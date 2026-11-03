using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
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
        if(!shader||ShaderUtil.ShaderHasError(shader)) {
            string error=shader?string.Join("\n",ShaderUtil.GetShaderMessages(shader).Where(m=>m.severity.ToString()=="Error").Take(6).Select(m=>Path.GetFileName(m.file)+":"+m.line+" "+m.message)):"Shader could not be imported.";
            File.WriteAllText(Path.Combine(output,"compile-error.txt"),error);throw new InvalidOperationException(error);
        }
        var parameters=new List<Parameter>();var defaults=new Material(shader);
        try{for(int i=0;i<shader.GetPropertyCount();i++) {
            string name=shader.GetPropertyName(i);var type=shader.GetPropertyType(i);
            if((shader.GetPropertyFlags(i)&ShaderPropertyFlags.HideInInspector)!=0||name.StartsWith("_Vfx",StringComparison.Ordinal)
                ||new[]{"_PresentationTime","_CurrentTime","_EffectTime","_EffectDuration","_EffectProgress","_FrameCount","_R2","_Aspect"}.Contains(name))continue;
            var p=new Parameter{Property=name,Label=shader.GetPropertyDescription(i)};if(string.IsNullOrWhiteSpace(p.Label))p.Label=name.TrimStart('_');
            if(type==ShaderPropertyType.Color){var c=defaults.GetColor(name);p.Kind="Color";p.Value=new double[]{c.r,c.g,c.b,c.a};p.Min=0;p.Max=2;}
            else if(type==ShaderPropertyType.Vector){var v=defaults.GetVector(name);p.Kind="Vector";p.Value=new double[]{v.x,v.y,v.z,v.w};p.Min=-2;p.Max=2;}
            else if(type==ShaderPropertyType.Float||type==ShaderPropertyType.Range||type==ShaderPropertyType.Int) {
                double v=type==ShaderPropertyType.Int?defaults.GetInteger(name):defaults.GetFloat(name);p.Kind="Float";p.Value=new[]{v};
                if(type==ShaderPropertyType.Range){var range=shader.GetPropertyRangeLimits(i);p.Min=range.x;p.Max=range.y;}
                else{p.Min=v<0?v*2:0;p.Max=Math.Max(1,Math.Abs(v)*2);}
            }else continue;
            parameters.Add(p);
        }}finally{UnityEngine.Object.DestroyImmediate(defaults);}
        File.WriteAllText(Path.Combine(output,"shader-properties.json"),JsonUtility.ToJson(new Schema{Parameters=parameters.ToArray()},true));
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
    [Serializable] sealed class Schema{public Parameter[] Parameters;}
    [Serializable] sealed class Parameter{public string Property,Label,Kind;public double[] Value;public double Min,Max;}
}
