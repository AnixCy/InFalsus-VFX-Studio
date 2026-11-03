namespace InFalsus.Presentation.AudioReactive;

/// <summary>Random-access creator evaluation. No state advances when sampling, seeking or changing playback rate.</summary>
public sealed class AudioParameterDriver
{
    private readonly AudioDriverRecipe recipe;
    public ParameterBinding Binding { get; }
    public AudioEnvelope Envelope { get; }
    public double AudioOriginSeconds => recipe.AudioOriginSeconds;
    public AudioBlend Blend => recipe.Mapping.Blend;
    private AudioParameterDriver(AudioAnalysis analysis, AudioDriverRecipe recipe)
    {
        this.recipe = Copy(recipe);
        Binding = new ParameterBinding(recipe.Node, recipe.Property, ParameterKind.Float);
        Envelope = AudioEnvelope.Create(analysis, recipe.BandId, recipe.Envelope);
    }
    public static AudioParameterDriver Create(AudioAnalysis analysis, AudioDriverRecipe recipe)
    {
        if (analysis == null || recipe == null) throw new ArgumentNullException();
        if (recipe.FormatVersion != 1 || recipe.AnalysisCacheKey != analysis.CacheKey || !analysis.BandIds.Contains(recipe.BandId)
            || string.IsNullOrWhiteSpace(recipe.Node) || string.IsNullOrWhiteSpace(recipe.Property)
            || !double.IsFinite(recipe.AudioOriginSeconds) || recipe.Envelope == null || recipe.Mapping == null)
            throw new ArgumentException("Invalid recipe, binding or analysis cache identity.");
        recipe.Envelope.Validate(); recipe.Mapping.Validate();
        return new AudioParameterDriver(analysis, recipe);
    }
    public AudioDriverRecipe ToRecipe() => Copy(recipe);
    public double Sample(double timelineSeconds, double baseValue)
    {
        if (!double.IsFinite(timelineSeconds) || !double.IsFinite(baseValue)) throw new ArgumentException("Driver inputs must be finite.");
        double signal = Envelope.Sample(timelineSeconds - recipe.AudioOriginSeconds);
        double mapped = recipe.Mapping.Minimum + (recipe.Mapping.Maximum - recipe.Mapping.Minimum) * signal;
        double value = recipe.Mapping.Blend switch {
            AudioBlend.Replace => mapped, AudioBlend.Add => baseValue + mapped, AudioBlend.Multiply => baseValue * mapped,
            _ => throw new InvalidOperationException("Unknown blend mode.") };
        if (!double.IsFinite(value) || Math.Abs(value) > float.MaxValue) throw new InvalidOperationException("Driver output exceeds finite Shader float range.");
        return (float)value;
    }
    public double Sample(double timelineSeconds, ChannelTrack baseTrack)
    {
        if (baseTrack == null || baseTrack.Binding != Binding) throw new ArgumentException("Driver requires the matching scalar channel.");
        return Sample(timelineSeconds, baseTrack.Sample(timelineSeconds).Number.X);
    }
    private static AudioDriverRecipe Copy(AudioDriverRecipe r) => new() {
        FormatVersion = r.FormatVersion, AnalysisCacheKey = r.AnalysisCacheKey, BandId = r.BandId, Node = r.Node, Property = r.Property,
        AudioOriginSeconds = r.AudioOriginSeconds, Envelope = r.Envelope.Copy(), Mapping = r.Mapping.Copy() };
}
