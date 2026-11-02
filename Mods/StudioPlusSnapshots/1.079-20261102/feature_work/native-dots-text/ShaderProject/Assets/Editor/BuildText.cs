using UnityEditor;
using UnityEngine;
public static class BuildText {
 public static void Build(){PlayerSettings.colorSpace=ColorSpace.Linear;var output=System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath,"../../Payload"));System.IO.Directory.CreateDirectory(output);var result=BuildPipeline.BuildAssetBundles(output,new[]{new AssetBundleBuild{assetBundleName="native-key-text.bundle",assetNames=new[]{"Assets/NativeKeyText.shader"},addressableNames=new[]{"native/key-text"}}},BuildAssetBundleOptions.ChunkBasedCompression|BuildAssetBundleOptions.ForceRebuildAssetBundle,BuildTarget.StandaloneWindows64);if(!result)throw new System.Exception("Text shader build failed");}
}
