using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public static class BuildAddonAssets
{
    public static void Run()
    {
        if(Application.unityVersion!="6000.3.9f1")throw new InvalidOperationException("Unexpected game resource Unity version");
        var space=Environment.GetEnvironmentVariable("VFX_GAME_COLOR_SPACE");
        PlayerSettings.colorSpace=space=="Linear"?ColorSpace.Linear:ColorSpace.Gamma;
        var renderer=AssetDatabase.LoadAssetAtPath<UniversalRendererData>("Assets/Renderer.asset");
        if(!renderer){renderer=ScriptableObject.CreateInstance<UniversalRendererData>();AssetDatabase.CreateAsset(renderer,"Assets/Renderer.asset");}
        var pipeline=AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/Pipeline.asset");
        if(!pipeline){pipeline=UniversalRenderPipelineAsset.Create(renderer);AssetDatabase.CreateAsset(pipeline,"Assets/Pipeline.asset");}
        GraphicsSettings.defaultRenderPipeline=pipeline;QualitySettings.renderPipeline=pipeline;
        PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.StandaloneWindows64,false);
        PlayerSettings.SetGraphicsAPIs(BuildTarget.StandaloneWindows64,new[]{GraphicsDeviceType.Direct3D11,GraphicsDeviceType.Direct3D12});
        AssetDatabase.Refresh();
        var shader=AssetDatabase.LoadAssetAtPath<Shader>("Assets/AddonBackground.shader");
        if(!shader)throw new InvalidOperationException("Background Shader missing");
        if(ShaderUtil.ShaderHasError(shader)){
            foreach(var message in ShaderUtil.GetShaderMessages(shader))Debug.LogError("AddonBackground.shader:"+message.line+" "+message.message);
            throw new InvalidOperationException("Background Shader failed compilation");
        }
        var material=AssetDatabase.LoadAssetAtPath<Material>("Assets/Background.mat");
        if(!material){material=new Material(shader);AssetDatabase.CreateAsset(material,"Assets/Background.mat");}
        material.shader=shader;EditorUtility.SetDirty(material);AssetDatabase.SaveAssets();
        var output=Environment.GetEnvironmentVariable("VFX_GAME_ASSET_OUTPUT");Directory.CreateDirectory(output);
        var bundleName=Environment.GetEnvironmentVariable("VFX_GAME_BUNDLE_NAME")??"background.bundle";
        var result=BuildPipeline.BuildAssetBundles(output,new[]{new AssetBundleBuild{
            assetBundleName=bundleName,assetNames=new[]{"Assets/Background.mat"},addressableNames=new[]{"vfx/addon-background"}}},
            BuildAssetBundleOptions.ChunkBasedCompression|BuildAssetBundleOptions.StrictMode|BuildAssetBundleOptions.ForceRebuildAssetBundle,BuildTarget.StandaloneWindows64);
        if(result==null)throw new InvalidOperationException("AssetBundle build failed");
        var builtFile=Path.Combine(output,bundleName);
        var bundle=AssetBundle.LoadFromFile(builtFile);
        if(!bundle)throw new InvalidOperationException("Built bundle did not load");
        try{var test=bundle.LoadAsset<Material>("vfx/addon-background");if(!test||!test.HasProperty("_MainTex"))throw new InvalidOperationException("Built material contract failed");}
        finally{bundle.Unload(true);}
        if(bundleName!="background.bundle")File.Copy(builtFile,Path.Combine(output,"background.bundle"),true);
        File.WriteAllText(Path.Combine(output,"build-info.json"),"{\"unityVersion\":\""+Application.unityVersion+"\",\"colorSpace\":\""+space+"\",\"platform\":\"StandaloneWindows64\"}");
        Debug.Log("VFX_GAME_ASSETS_VERIFIED "+output);
    }
}
