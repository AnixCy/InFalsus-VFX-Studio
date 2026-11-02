using UnityEditor;
using UnityEngine;
public static class BuildFloorSampling {
 public static void Build(){PlayerSettings.colorSpace=ColorSpace.Linear;var output=System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath,"../../Payload"));System.IO.Directory.CreateDirectory(output);var result=BuildPipeline.BuildAssetBundles(output,new[]{new AssetBundleBuild{assetBundleName="floor-sampling-test.bundle",assetNames=new[]{"Assets/FloorSampling.shader"},addressableNames=new[]{"test/floor-sampling"}}},BuildAssetBundleOptions.ChunkBasedCompression|BuildAssetBundleOptions.ForceRebuildAssetBundle,BuildTarget.StandaloneWindows64);if(!result)throw new System.Exception("Floor sampling test shader build failed");}
}
