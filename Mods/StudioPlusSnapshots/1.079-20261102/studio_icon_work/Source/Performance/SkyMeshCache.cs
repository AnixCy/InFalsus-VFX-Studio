using InFalsusStudio.Core;
using InFalsusStudio.TimeGroups;
namespace InFalsusStudio.Performance {
 internal static class SkyMeshCache {
  internal static bool Enabled=true;
  internal static bool Active {get{var boot=TimingGroups.Boot;var ui=InFalsusStudio.NativeEditorUI.NativeEditorUIPlugin.I;return Enabled&&boot!=null&&boot.History!=null&&boot.History.InTransaction&&(GroupRuntime.Get(boot,"dragEvent")!=null||ui!=null&&ui.SkyNodeDragging(boot))&&!((StudioTransport)GroupRuntime.Get(boot,"transport")).Playing;}}
 }
}
