using InFalsus.Presentation.Authoring;
using InFalsus.Presentation.AudioReactive;

namespace InFalsus.Presentation.CreatorWorkspace;

public static class WorkspaceModel
{
    public const string FileName = "workspace.vfxwork.json";
    private static string ValidationRoot() => Path.Combine(Path.GetTempPath(), "vfxwork-validation-" + Guid.NewGuid().ToString("N"));
    public static void Validate(CreatorWorkspaceDocument d)
    {
        if (d == null || d.FormatVersion != 1 || !WorkspacePaths.Id(d.WorkspaceId) || d.ManualProject == null
            || d.Resources == null || d.Resources.Length > 512 || d.AudioDrivers == null || d.AudioDrivers.Length > 64
            || d.AudioResourceId == null || d.BackgroundResourceId == null || d.ManualProject.AssetRoot != ".")
            throw new InvalidDataException("Invalid creator workspace format or layout.");
        var project = d.ManualProject;
        if (project.Runtime == null || project.Runtime.Channels == null || project.Runtime.Channels.Length > 4096
            || project.Runtime.Channels.Any(c => c == null || c.Keys == null)
            || project.Runtime.Channels.Sum(c => (long)c.Keys.Length) > 1_000_000)
            throw new InvalidDataException("Manual channel/key budget exceeded.");
        _ = AuthoringProjects.Compile(project, ValidationRoot(), project.Runtime.Requires, requireResources: false);
        if (d.Bake != null && (d.Bake.Settings == null || d.Bake.Channels == null || d.Bake.Channels.Any(c => c == null || c.Keys == null)))
            throw new InvalidDataException("Malformed derived bake record.");
        var ids = new HashSet<string>(StringComparer.Ordinal); var paths = new Dictionary<string, WorkspaceResource>(StringComparer.OrdinalIgnoreCase);
        long total = 0;
        foreach (var r in d.Resources)
        {
            if (r == null || r.Path == null || !WorkspacePaths.Id(r.Id) || !ids.Add(r.Id) || !WorkspacePaths.Digest(r.Sha256)
                || !Enum.IsDefined(typeof(WorkspaceResourceKind), r.Kind) || r.Bytes < 0 || r.Bytes > 2L * 1024 * 1024 * 1024
                || string.Equals(r.Path.Split('/')[0], FileName, StringComparison.OrdinalIgnoreCase)
                || string.Equals(r.Path.Split('/')[0], FileName + ".lock", StringComparison.OrdinalIgnoreCase)
                || r.Path.StartsWith(".vfxwork-", StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Invalid resource identity/size or reserved store path.");
            WorkspacePaths.Relative(r.Path);
            if (!paths.TryAdd(r.Path, r)) throw new InvalidDataException("Case-insensitive resource path collision.");
            total += r.Bytes;
        }
        if (total > 8L * 1024 * 1024 * 1024) throw new InvalidDataException("Workspace exceeds 8 GiB of declared resources.");
        foreach (string path in paths.Keys)
            for (int slash = path.IndexOf('/'); slash >= 0; slash = path.IndexOf('/', slash + 1))
                if (paths.ContainsKey(path.Substring(0, slash))) throw new InvalidDataException("Resource file/directory collision.");
        WorkspaceResource Resource(string id, WorkspaceResourceKind kind) => d.Resources.SingleOrDefault(r => r.Id == id && r.Kind == kind)
            ?? throw new InvalidDataException("Missing resource binding: " + id);
        if (d.AudioResourceId.Length != 0) _ = Resource(d.AudioResourceId, WorkspaceResourceKind.Audio);
        if (d.BackgroundResourceId.Length != 0) _ = Resource(d.BackgroundResourceId, WorkspaceResourceKind.BackgroundImage);
        foreach (var node in project.Runtime.Nodes)
        {
            WorkspacePaths.Relative(node.Bundle);
            if (!paths.TryGetValue(node.Bundle, out var bundle) || bundle.Path != node.Bundle || bundle.Kind != WorkspaceResourceKind.ShaderBundle)
                throw new InvalidDataException("Undeclared Shader bundle: " + node.Bundle);
            foreach (var texture in node.Textures.Values.Where(v => v.StartsWith("file:", StringComparison.Ordinal)))
            {
                string file = WorkspacePaths.Relative(texture.Substring(5));
                if (!paths.TryGetValue(file, out var image) || image.Path != file || image.Kind is not (WorkspaceResourceKind.Texture or WorkspaceResourceKind.BackgroundImage))
                    throw new InvalidDataException("Undeclared file texture: " + file);
            }
        }
        var driverIds = new HashSet<string>(StringComparer.Ordinal); var targets = new HashSet<string>(StringComparer.Ordinal);
        foreach (var linked in d.AudioDrivers)
        {
            if (linked == null || !WorkspacePaths.Id(linked.Id) || !driverIds.Add(linked.Id) || !targets.Add(linked.TargetTrackId)
                || linked.Recipe == null || linked.Recipe.FormatVersion != 1 || linked.Recipe.Envelope == null || linked.Recipe.Mapping == null
                || !WorkspacePaths.Digest(linked.DecodedPcmFingerprint) || !WorkspacePaths.Digest(linked.Recipe.AnalysisCacheKey)
                || string.IsNullOrWhiteSpace(linked.Recipe.BandId) || !double.IsFinite(linked.Recipe.AudioOriginSeconds))
                throw new InvalidDataException("Invalid or duplicate audio driver association.");
            linked.Recipe.Envelope.Validate(); linked.Recipe.Mapping.Validate();
            var resource = Resource(linked.AudioResourceId, WorkspaceResourceKind.Audio);
            if (linked.AudioFileSha256 != resource.Sha256) throw new InvalidDataException("Audio file identity changed; driver must be explicitly relinked.");
            var track = project.Tracks.SingleOrDefault(t => t.TrackId == linked.TargetTrackId)
                ?? throw new InvalidDataException("Audio target track no longer exists.");
            var parameterId = ParameterDescriptor.MakeId(linked.Recipe.Node, linked.Recipe.Property);
            if (track.ParameterId != parameterId || !project.Runtime.Channels.Any(c => c.Node == linked.Recipe.Node && c.Property == linked.Recipe.Property && c.Kind == ParameterKind.Float))
                throw new InvalidDataException("Audio target binding changed; refusing positional reassignment.");
        }
    }
    public static LinkedAudioDriver Link(CreatorWorkspaceDocument document, string id, string targetTrackId,
        string audioResourceId, AudioAnalysis analysis, AudioDriverRecipe recipe)
    {
        var candidate = WorkspaceCopy.Document(document);
        candidate.AudioDrivers = candidate.AudioDrivers.Where(a => a.Id != id).ToArray();
        Validate(candidate);
        _ = AudioParameterDriver.Create(analysis, recipe);
        var resource = candidate.Resources.SingleOrDefault(r => r.Id == audioResourceId && r.Kind == WorkspaceResourceKind.Audio)
            ?? throw new InvalidDataException("Audio resource is not declared.");
        var link = new LinkedAudioDriver { Id = id, TargetTrackId = targetTrackId, AudioResourceId = audioResourceId,
            AudioFileSha256 = resource.Sha256, DecodedPcmFingerprint = analysis.SourceFingerprint, Recipe = WorkspaceCopy.Recipe(recipe) };
        candidate.AudioDrivers = candidate.AudioDrivers.Append(link).ToArray();
        Validate(candidate); return link;
    }
    public static CreatorWorkspaceDocument Bake(CreatorWorkspaceDocument document, IReadOnlyDictionary<string, AudioAnalysis> analyses,
        BakeSettings settings, int maximumTotalKeys = 100_000, CancellationToken cancellation = default)
    {
        Validate(document);
        if (analyses == null || settings == null) throw new ArgumentNullException();
        settings = WorkspaceCopy.Settings(settings);
        settings.Validate();
        var result = WorkspaceCopy.Document(document);
        var drivers = result.AudioDrivers.Select(link => {
            if (!analyses.TryGetValue(link.Recipe.AnalysisCacheKey, out var analysis) || analysis == null || analysis.SourceFingerprint != link.DecodedPcmFingerprint)
                throw new InvalidDataException("Matching decoded analysis is missing: " + link.Id);
            return AudioParameterDriver.Create(analysis, link.Recipe);
        }).ToArray();
        var batch = AudioBakeBatch.Bake(result.ManualProject.Runtime.Channels,
            new TempoMap(result.ManualProject.Runtime.Tempo.Select(t => new TempoChange(t.Seconds, t.Bpm))), drivers, settings, maximumTotalKeys, cancellation);
        result.Bake = new WorkspaceBake { SourceContentId = result.ManualProject.ContentId, SourceRevision = result.ManualProject.Revision,
            InputSha256 = WorkspaceFingerprint.Input(result, settings, maximumTotalKeys), Settings = WorkspaceCopy.Settings(settings),
            MaximumTotalKeys = maximumTotalKeys, Channels = batch.ToChannels() };
        result.Bake.OutputSha256 = WorkspaceFingerprint.Compute(result.Bake.Channels, cancellation);
        return result;
    }
    public static WorkspaceBakeStatus BakeStatus(CreatorWorkspaceDocument document)
    {
        Validate(document); var bake = document.Bake;
        if (bake == null) return WorkspaceBakeStatus.None;
        try
        {
            if (bake.Settings == null || bake.Channels == null || !WorkspacePaths.Digest(bake.InputSha256) || !WorkspacePaths.Digest(bake.OutputSha256)
                || bake.OutputSha256 != WorkspaceFingerprint.Compute(bake.Channels)) return WorkspaceBakeStatus.Invalid;
            bake.Settings.Validate();
            if (bake.SourceContentId != document.ManualProject.ContentId || bake.SourceRevision != document.ManualProject.Revision
                || bake.InputSha256 != WorkspaceFingerprint.Input(document, bake.Settings, bake.MaximumTotalKeys)) return WorkspaceBakeStatus.Stale;
            // Validate the result with the existing runtime, without changing the authoritative manual track IDs.
            var runtime = AuthoringProjects.Copy(document.ManualProject).Runtime; runtime.Channels = WorkspaceCopy.Channels(bake.Channels);
            _ = EffectPackage.FromDocument(runtime, ValidationRoot(), runtime.Requires, requireResources: false);
            return WorkspaceBakeStatus.Current;
        }
        catch (Exception e) when (e is ArgumentException or InvalidDataException or InvalidOperationException) { return WorkspaceBakeStatus.Invalid; }
    }
    public static PackageDocument BakedRuntime(CreatorWorkspaceDocument document)
    {
        if (BakeStatus(document) != WorkspaceBakeStatus.Current) throw new InvalidDataException("Baked output is absent, stale or invalid; rebake from the manual source.");
        var runtime = AuthoringProjects.Copy(document.ManualProject).Runtime;
        runtime.Channels = WorkspaceCopy.Channels(document.Bake!.Channels); return runtime;
    }
}
