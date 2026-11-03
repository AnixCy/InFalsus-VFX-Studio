extern alias UnityCore;
using UnityCore::UnityEngine;

namespace InFalsus.Presentation.Game;

/// <summary>Only presentation targets and a read-only transport snapshot cross this boundary.</summary>
public interface IGameBindings
{
    IntPtr Background { get; }
    Camera? GameCamera { get; }
    bool HasSpecialScene { get; }
    string SceneControllerName { get; }
    // The last chart's background is still visible while its transport is being torn down.
    bool CanRetainBackground { get; }
    bool TrySample(out PlaybackSnapshot snapshot);
    bool TargetAlive(IntPtr target);
    IntPtr CurrentMaterial(IntPtr target);
    IntPtr RequestedMaterial(IntPtr target);
    byte RequestedMaterialOption(IntPtr target);
    void SetMaterial(IntPtr target,IntPtr material,byte option=1);
}
