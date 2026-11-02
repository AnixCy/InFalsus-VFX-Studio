using UnityEditor;
using UnityEngine;
public static class BuildKey {
 public static void Build(){PlayerSettings.colorSpace=ColorSpace.Linear;var output=System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath,"../../Payload"));System.IO.Directory.CreateDirectory(output);var result=BuildPipeline.BuildAssetBundles(output,new[]{new AssetBundleBuild{assetBundleName="key-gamma.bundle",assetNames=new[]{"Assets/NativeKey.shader"},addressableNames=new[]{"native/compatibility/key"}}},BuildAssetBundleOptions.ChunkBasedCompression|BuildAssetBundleOptions.ForceRebuildAssetBundle,BuildTarget.StandaloneWindows64);if(!result)throw new System.Exception("Key shader build failed");}
}
