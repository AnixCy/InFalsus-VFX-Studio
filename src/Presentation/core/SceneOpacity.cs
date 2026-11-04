namespace InFalsus.Presentation;

public readonly record struct SceneOpacityValues(float Track, float Score, float SongInfo, float ClearState, float Judgement)
{
    public static SceneOpacityValues Visible => new(1, 1, 1, 1, 1);
    public float At(int group) => group switch { 0 => Track, 1 => Score, 2 => SongInfo, 3 => ClearState, 4 => Judgement, _ => throw new ArgumentOutOfRangeException(nameof(group)) };
}

/// <summary>Logical scene controls use existing instance/keyframe semantics; they never allocate a fullscreen pass.</summary>
public sealed class SceneOpacityPlan
{
    public const string Capability = "scene.opacity.v1";
    public const string Material = "vfx/scene-opacity";
    public static readonly string[] EffectIds = { "scene-track-opacity", "scene-score-opacity", "scene-song-opacity", "scene-clear-opacity", "scene-judgement-opacity" };
    public static readonly string[] Properties = { "_TrackOpacity", "_ScoreOpacity", "_SongInfoOpacity", "_ClearStateOpacity", "_JudgementOpacity" };
    private readonly (int Index, int Group, bool Enabled, double Start, double? End)[] bindings;
    public int Count => bindings.Length;
    public static bool IsControl(MaterialNodeDocument node) => node.Target == "postprocess" && node.Material == Material;
    public static int Group(MaterialNodeDocument node)
    {
        int group = Array.IndexOf(EffectIds, node.EffectId);
        return IsControl(node) && group >= 0 ? group : throw new InvalidDataException("Unknown scene opacity control.");
    }
    public SceneOpacityPlan(PackageDocument document)
    {
        var list = new List<(int, int, bool, double, double?)>();
        foreach (var node in document.Nodes.Where(IsControl))
        {
            if (!document.Requires.Contains(Capability)) throw new InvalidDataException("Scene opacity requires an explicit runtime capability.");
            int group = Group(node);
            var channels = document.Channels.Select((c, i) => (Channel: c, Index: i)).Where(c => c.Channel.Node == node.Id).ToArray();
            if (channels.Length != 1 || channels[0].Channel.Kind != ParameterKind.Float || channels[0].Channel.Property != Properties[group])
                throw new InvalidDataException("Scene opacity requires exactly its scalar opacity channel.");
            list.Add((channels[0].Index, group, node.Enabled, node.StartSeconds, node.EndSeconds));
        }
        bindings = list.ToArray();
    }
    public SceneOpacityValues Sample(double seconds, ReadOnlySpan<ParameterValue> values)
    {
        if (!double.IsFinite(seconds)) throw new ArgumentException("Non-finite scene time.");
        Span<float> result = stackalloc float[5]; result.Fill(1);
        foreach (var binding in bindings)
        {
            if (!binding.Enabled || seconds < binding.Start || binding.End.HasValue && seconds >= binding.End.Value) continue;
            float value = values[binding.Index].Number.X;
            if (!float.IsFinite(value)) throw new InvalidDataException("Scene opacity is not finite.");
            result[binding.Group] *= Math.Clamp(value, 0, 1);
        }
        return new(result[0], result[1], result[2], result[3], result[4]);
    }
}
