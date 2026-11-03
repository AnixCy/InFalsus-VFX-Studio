using InFalsus.Presentation.Authoring;
using InFalsus.Presentation.AudioReactive;

namespace InFalsus.Presentation.CreatorWorkspace;

public enum WorkspaceResourceKind { Audio, BackgroundImage, ShaderBundle, Texture, Source }
public sealed class WorkspaceResource
{
    public string Id { get; set; } = "";
    public WorkspaceResourceKind Kind { get; set; }
    public string Path { get; set; } = "";
    public long Bytes { get; set; }
    public string Sha256 { get; set; } = "";
}
public sealed class SourceResource
{
    public string Id { get; set; } = "";
    public string SourceFile { get; set; } = "";
    public string DestinationPath { get; set; } = "";
}
public sealed class LinkedAudioDriver
{
    public string Id { get; set; } = "";
    public string TargetTrackId { get; set; } = "";
    public string AudioResourceId { get; set; } = "";
    public string AudioFileSha256 { get; set; } = "";
    public string DecodedPcmFingerprint { get; set; } = "";
    public AudioDriverRecipe Recipe { get; set; } = new();
}
public sealed class WorkspaceBake
{
    public string SourceContentId { get; set; } = "";
    public long SourceRevision { get; set; }
    public string InputSha256 { get; set; } = "";
    public string OutputSha256 { get; set; } = "";
    public BakeSettings Settings { get; set; } = new();
    public int MaximumTotalKeys { get; set; } = 100_000;
    public ChannelDocument[] Channels { get; set; } = Array.Empty<ChannelDocument>();
}
/// <summary>Creator-only container. ManualProject is always authoritative; Bake is derived and never its next input.</summary>
public sealed class CreatorWorkspaceDocument
{
    public int FormatVersion { get; set; } = 1;
    public string WorkspaceId { get; set; } = Guid.NewGuid().ToString("N");
    public AuthoringProjectDocument ManualProject { get; set; } = new();
    public WorkspaceResource[] Resources { get; set; } = Array.Empty<WorkspaceResource>();
    public string AudioResourceId { get; set; } = "";
    public string BackgroundResourceId { get; set; } = "";
    public LinkedAudioDriver[] AudioDrivers { get; set; } = Array.Empty<LinkedAudioDriver>();
    public WorkspaceBake? Bake { get; set; }
}
public enum WorkspaceBakeStatus { None, Current, Stale, Invalid }
public sealed class ResourceIssue
{
    public string ResourceId { get; }
    public string RelativePath { get; }
    public string Code { get; }
    internal ResourceIssue(WorkspaceResource resource, string code) => (ResourceId, RelativePath, Code) = (resource.Id, resource.Path, code);
}

internal static class WorkspaceCopy
{
    internal static ChannelDocument[] Channels(ChannelDocument[] channels) => AuthoringProjects.Copy(new AuthoringProjectDocument {
        Runtime = new PackageDocument { Channels = channels } }).Runtime.Channels;
    internal static BakeSettings Settings(BakeSettings s) => new() { StartSeconds = s.StartSeconds, EndSeconds = s.EndSeconds,
        SamplesPerSecond = s.SamplesPerSecond, MaximumError = s.MaximumError, MaximumSamples = s.MaximumSamples, MaximumKeys = s.MaximumKeys };
    internal static AudioDriverRecipe Recipe(AudioDriverRecipe r) => new() { FormatVersion = r.FormatVersion, AnalysisCacheKey = r.AnalysisCacheKey,
        BandId = r.BandId, Node = r.Node, Property = r.Property, AudioOriginSeconds = r.AudioOriginSeconds,
        Envelope = new EnvelopeSettings { FloorDb = r.Envelope.FloorDb, CeilingDb = r.Envelope.CeilingDb, GainDb = r.Envelope.GainDb,
            AttackSeconds = r.Envelope.AttackSeconds, ReleaseSeconds = r.Envelope.ReleaseSeconds, Exponent = r.Envelope.Exponent },
        Mapping = new ParameterMapping { Minimum = r.Mapping.Minimum, Maximum = r.Mapping.Maximum, Blend = r.Mapping.Blend } };
    internal static CreatorWorkspaceDocument Document(CreatorWorkspaceDocument d) => new() {
        FormatVersion = d.FormatVersion, WorkspaceId = d.WorkspaceId, ManualProject = AuthoringProjects.Copy(d.ManualProject),
        AudioResourceId = d.AudioResourceId, BackgroundResourceId = d.BackgroundResourceId,
        Resources = d.Resources.Select(r => new WorkspaceResource { Id = r.Id, Kind = r.Kind, Path = r.Path, Bytes = r.Bytes, Sha256 = r.Sha256 }).ToArray(),
        AudioDrivers = d.AudioDrivers.Select(a => new LinkedAudioDriver { Id = a.Id, TargetTrackId = a.TargetTrackId, AudioResourceId = a.AudioResourceId,
            AudioFileSha256 = a.AudioFileSha256, DecodedPcmFingerprint = a.DecodedPcmFingerprint, Recipe = Recipe(a.Recipe) }).ToArray(),
        Bake = d.Bake == null ? null : new WorkspaceBake { SourceContentId = d.Bake.SourceContentId, SourceRevision = d.Bake.SourceRevision,
            InputSha256 = d.Bake.InputSha256, OutputSha256 = d.Bake.OutputSha256, Settings = Settings(d.Bake.Settings),
            MaximumTotalKeys = d.Bake.MaximumTotalKeys, Channels = Channels(d.Bake.Channels) } };
}
