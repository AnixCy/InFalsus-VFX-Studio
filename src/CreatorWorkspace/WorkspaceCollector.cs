using InFalsus.Presentation.Authoring;

namespace InFalsus.Presentation.CreatorWorkspace;

/// <summary>In-memory collection plan. Absolute input paths never enter its persisted document.</summary>
public sealed class PreparedWorkspace
{
    private readonly CreatorWorkspaceDocument document;
    internal IReadOnlyDictionary<string, string> Inputs { get; }
    internal PreparedWorkspace(CreatorWorkspaceDocument document, IReadOnlyDictionary<string, string> inputs)
    { this.document = WorkspaceCopy.Document(document); Inputs = new Dictionary<string, string>(inputs); }
    public CreatorWorkspaceDocument Snapshot() => WorkspaceCopy.Document(document);
    public PreparedWorkspace WithDocument(CreatorWorkspaceDocument updated)
    {
        WorkspaceModel.Validate(updated);
        if (updated.WorkspaceId != document.WorkspaceId || WorkspaceFingerprint.Compute(updated.Resources) != WorkspaceFingerprint.Compute(document.Resources))
            throw new InvalidDataException("A collected resource inventory cannot be silently replaced.");
        return new PreparedWorkspace(updated, Inputs);
    }
}
public static class WorkspaceCollector
{
    public static PreparedWorkspace Collect(AuthoringProjectDocument manualProject, string projectDirectory,
        string audioFile = "", string backgroundFile = "", IReadOnlyList<SourceResource>? sourceFiles = null, CancellationToken cancellation = default)
    {
        if (manualProject == null || audioFile == null || backgroundFile == null) throw new ArgumentNullException();
        string directory = Path.GetFullPath(projectDirectory);
        var manual = AuthoringProjects.Copy(manualProject);
        _ = AuthoringProjects.Compile(manual, directory, manual.Runtime.Requires);
        string assetRoot = Path.GetFullPath(Path.Combine(directory, manual.AssetRoot));
        var resources = new List<WorkspaceResource>(); var inputs = new Dictionary<string, string>(StringComparer.Ordinal);
        var paths = new Dictionary<string, WorkspaceResource>(StringComparer.OrdinalIgnoreCase);
        string Add(string source, string relative, WorkspaceResourceKind kind, string? id = null)
        {
            cancellation.ThrowIfCancellationRequested(); relative = WorkspacePaths.Relative(relative);
            if (resources.Count >= 512 && !paths.ContainsKey(relative)) throw new InvalidDataException("Resource count budget exceeded.");
            source = Path.GetFullPath(Path.Combine(directory, source)); WorkspacePaths.NoLinks(source);
            var info = new FileInfo(source);
            if (!info.Exists) throw new FileNotFoundException("Declared resource is missing.", source);
            if (info.Length > 2L * 1024 * 1024 * 1024) throw new InvalidDataException("Resource exceeds 2 GiB.");
            string hash = WorkspacePaths.FileHash(source, cancellation);
            if (paths.TryGetValue(relative, out var prior))
            {
                if (prior.Path != relative || prior.Kind != kind || prior.Sha256 != hash || prior.Bytes != info.Length)
                    throw new InvalidDataException("Resource path collision: " + relative);
                return prior.Id;
            }
            id ??= "r-" + WorkspaceFingerprint.Compute(new object[] { kind, relative });
            if (inputs.ContainsKey(id)) throw new InvalidDataException("Duplicate resource ID: " + id);
            var resource = new WorkspaceResource { Id = id, Kind = kind, Path = relative, Bytes = info.Length, Sha256 = hash };
            paths.Add(relative, resource); inputs.Add(id, source); resources.Add(resource); return id;
        }
        foreach (var node in manual.Runtime.Nodes)
        {
            string relative = node.Bundle.Replace('\\', '/');
            Add(EffectPackage.ResolveAsset(assetRoot, node.Bundle), relative, WorkspaceResourceKind.ShaderBundle); node.Bundle = relative;
            foreach (string key in node.Textures.Keys.ToArray())
                if (node.Textures[key].StartsWith("file:", StringComparison.Ordinal))
                {
                    string file = node.Textures[key].Substring(5), portable = file.Replace('\\', '/');
                    Add(EffectPackage.ResolveAsset(assetRoot, file), portable, WorkspaceResourceKind.Texture); node.Textures[key] = "file:" + portable;
                }
        }
        manual.AssetRoot = ".";
        var document = new CreatorWorkspaceDocument { ManualProject = manual };
        if (audioFile.Length != 0) document.AudioResourceId = Add(audioFile, "media/audio" + Path.GetExtension(audioFile).ToLowerInvariant(), WorkspaceResourceKind.Audio, "audio");
        if (backgroundFile.Length != 0) document.BackgroundResourceId = Add(backgroundFile, "media/background" + Path.GetExtension(backgroundFile).ToLowerInvariant(), WorkspaceResourceKind.BackgroundImage, "background");
        foreach (var file in sourceFiles ?? Array.Empty<SourceResource>())
        {
            if (file == null || file.DestinationPath == null || !file.DestinationPath.StartsWith("sources/", StringComparison.Ordinal)) throw new InvalidDataException("Explicit source files must live below sources/.");
            Add(file.SourceFile, file.DestinationPath, WorkspaceResourceKind.Source, file.Id);
        }
        document.Resources = resources.ToArray(); WorkspaceModel.Validate(document);
        return new PreparedWorkspace(document, inputs);
    }
}
