using InFalsusStudio;
namespace InFalsusStudio.NativeEditorUI {
 public sealed partial class NativeEditorUIPlugin {
  internal void ApplyTrackView(bool native){
   SetTrackView(boot.Profile,native);
   boot.RebuildGeometry();SharedPreferences.RecordCamera(boot);
   Set(boot,"status",native?"已应用本体 1.07b 视图":"已恢复原图像校准视图");
  }
  internal static void SetTrackView(StudioProfile p,bool native){
   p.verticalFov=native?51.945061f:50f; p.cameraPitch=native?24.33f:23.65212f;
   p.cameraHeight=native?3.069336f:3.086448f; p.cameraBack=native?3.038347f:3.219452f;
   p.stageFar=native?50.454528f:65f; p.lock16By9=true;
  }
 }
}
