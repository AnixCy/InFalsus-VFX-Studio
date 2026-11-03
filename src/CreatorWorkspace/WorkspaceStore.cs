using System.Security.Cryptography;
using System.Text;

namespace InFalsus.Presentation.CreatorWorkspace;

public sealed class WorkspaceFileVersion
{
    public string File { get; }
    public string Sha256 { get; }
    public string WorkspaceId { get; }
    internal WorkspaceFileVersion(string file, string hash, string id) => (File, Sha256, WorkspaceId) = (file, hash, id);
}
public sealed class OpenedWorkspace
{
    private readonly CreatorWorkspaceDocument document;
    public WorkspaceFileVersion Version { get; }
    public IReadOnlyList<ResourceIssue> ResourceIssues { get; }
    public WorkspaceBakeStatus BakeStatus { get; }
    internal OpenedWorkspace(CreatorWorkspaceDocument doc, WorkspaceFileVersion version, List<ResourceIssue> issues)
    { document = WorkspaceCopy.Document(doc); Version = version; ResourceIssues = issues.AsReadOnly(); BakeStatus = WorkspaceModel.BakeStatus(document); }
    public CreatorWorkspaceDocument Snapshot() => WorkspaceCopy.Document(document);
}

/// <summary>Creator package store. Publishes fresh folders, saves metadata atomically and never moves original media.
/// This is not the Studio project codec or a game addon installer.</summary>
public sealed class WorkspaceStore
{
    private readonly Func<CreatorWorkspaceDocument, string> encode;
    private readonly Func<string, CreatorWorkspaceDocument> decode;
    public const int MaximumDocumentBytes = 64 * 1024 * 1024;
    public WorkspaceStore(Func<CreatorWorkspaceDocument, string> encode, Func<string, CreatorWorkspaceDocument> decode)
    { this.encode = encode ?? throw new ArgumentNullException(nameof(encode)); this.decode = decode ?? throw new ArgumentNullException(nameof(decode)); }

    public OpenedWorkspace Open(string directory, bool requireResources = true, CancellationToken cancellation = default)
    {
        string root = Path.GetFullPath(directory), file = WorkspacePaths.Resolve(root, WorkspaceModel.FileName);
        cancellation.ThrowIfCancellationRequested();
        using var input = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete);
        if (input.Length > MaximumDocumentBytes) throw new InvalidDataException("Creator document exceeds 64 MiB.");
        using var memory = new MemoryStream(); input.CopyTo(memory); var bytes = memory.ToArray();
        cancellation.ThrowIfCancellationRequested();
        var document = decode(new UTF8Encoding(false, true).GetString(bytes).TrimStart('\uFEFF'));
        WorkspaceModel.Validate(document);
        var issues = Inspect(directory, document, cancellation).ToList();
        if (requireResources && issues.Count != 0) throw new InvalidDataException("Workspace resources failed: " + string.Join(", ", issues.Select(i => i.ResourceId + ":" + i.Code)));
        using var sha = SHA256.Create();
        var version = new WorkspaceFileVersion(file, BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant(), document.WorkspaceId);
        return new OpenedWorkspace(document, version, issues);
    }
    public IReadOnlyList<ResourceIssue> Inspect(string directory, CreatorWorkspaceDocument document, CancellationToken cancellation = default)
    {
        WorkspaceModel.Validate(document); var issues = new List<ResourceIssue>();
        foreach (var r in document.Resources)
        {
            cancellation.ThrowIfCancellationRequested(); string file;
            try { file = WorkspacePaths.Resolve(directory, r.Path); }
            catch (InvalidDataException) { issues.Add(new ResourceIssue(r, "resource.unsafe-link")); continue; }
            if (!System.IO.File.Exists(file)) { issues.Add(new ResourceIssue(r, "resource.missing")); continue; }
            if (new FileInfo(file).Length != r.Bytes) { issues.Add(new ResourceIssue(r, "resource.size-mismatch")); continue; }
            if (WorkspacePaths.FileHash(file, cancellation) != r.Sha256) issues.Add(new ResourceIssue(r, "resource.hash-mismatch"));
        }
        return issues.AsReadOnly();
    }
    public OpenedWorkspace Publish(PreparedWorkspace prepared, string destination, CancellationToken cancellation = default)
    {
        if (prepared == null) throw new ArgumentNullException(nameof(prepared));
        string target = Path.GetFullPath(destination).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        string parent = Path.GetDirectoryName(target) ?? throw new InvalidDataException("Workspace destination requires a parent directory.");
        WorkspacePaths.NoLinks(target);
        if (Directory.Exists(target) || System.IO.File.Exists(target)) throw new IOException("Destination already exists; existing artwork is never replaced.");
        var document = prepared.Snapshot(); var bytes = CheckedBytes(document);
        cancellation.ThrowIfCancellationRequested(); Directory.CreateDirectory(parent);
        string staging = Path.Combine(parent, ".vfxwork-stage-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(staging);
        try
        {
            foreach (var resource in document.Resources)
            {
                if (!prepared.Inputs.TryGetValue(resource.Id, out var source)) throw new InvalidDataException("Resource input is missing: " + resource.Id);
                string file = WorkspacePaths.Resolve(staging, resource.Path);
                Directory.CreateDirectory(Path.GetDirectoryName(file)!);
                CopyResource(source, file, resource, cancellation);
            }
            Write(WorkspacePaths.Resolve(staging, WorkspaceModel.FileName), bytes, cancellation);
            _ = Open(staging, cancellation: cancellation);
            cancellation.ThrowIfCancellationRequested();
            // Rename only this owned staging directory into the explicitly named fresh destination.
            RequireOwnedStage(staging, parent); WorkspacePaths.NoLinks(target);
            if (Directory.Exists(target) || System.IO.File.Exists(target)) throw new IOException("Destination was created during publication.");
            Directory.Move(staging, target);
        }
        finally { if (Directory.Exists(staging)) CleanStage(staging, parent); }
        // Rename is the commit point. Cancellation after it cannot turn a committed complete package into a failure.
        return Open(target);
    }
    public OpenedWorkspace Relocate(string sourceDirectory, string destination, CancellationToken cancellation = default)
    {
        var opened = Open(sourceDirectory, cancellation: cancellation); var document = opened.Snapshot();
        var inputs = document.Resources.ToDictionary(r => r.Id, r => WorkspacePaths.Resolve(sourceDirectory, r.Path), StringComparer.Ordinal);
        // Publication reads an independent validated snapshot; original metadata and media are never rewritten.
        return Publish(new PreparedWorkspace(document, inputs), destination, cancellation);
    }
    public WorkspaceFileVersion Save(string directory, CreatorWorkspaceDocument document, WorkspaceFileVersion expected,
        CancellationToken cancellation = default)
    {
        string file = WorkspacePaths.Resolve(directory, WorkspaceModel.FileName);
        WorkspaceModel.Validate(document); document = WorkspaceCopy.Document(document);
        WorkspacePaths.NoLinks(file + ".lock");
        using var writerLock = new FileStream(file + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None, 1, FileOptions.DeleteOnClose);
        RequireVersion(file, expected);
        if (document.WorkspaceId != expected.WorkspaceId) throw new InvalidDataException("Workspace identity cannot be changed by a metadata save.");
        var bytes = CheckedBytes(document);
        var issues = Inspect(directory, document, cancellation);
        if (issues.Count != 0) throw new InvalidDataException("Cannot save references to missing or changed resources.");
        string temp = Path.Combine(Path.GetDirectoryName(file)!, ".vfxwork-save-" + Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            Write(temp, bytes, cancellation); cancellation.ThrowIfCancellationRequested(); RequireVersion(file, expected);
            System.IO.File.Replace(temp, file, null);
        }
        finally { if (System.IO.File.Exists(temp)) System.IO.File.Delete(temp); }
        return new WorkspaceFileVersion(file, WorkspacePaths.FileHash(file), document.WorkspaceId);
    }
    private byte[] CheckedBytes(CreatorWorkspaceDocument document)
    {
        WorkspaceModel.Validate(document);
        string expected = WorkspaceFingerprint.Compute(document);
        string text = encode(WorkspaceCopy.Document(document));
        if (string.IsNullOrWhiteSpace(text) || Encoding.UTF8.GetByteCount(text) > MaximumDocumentBytes) throw new InvalidDataException("Empty/oversized creator document.");
        var restored = decode(text); WorkspaceModel.Validate(restored);
        if (WorkspaceFingerprint.Compute(restored) != expected) throw new InvalidDataException("Creator codec changed source animation, links or provenance.");
        return new UTF8Encoding(false).GetBytes(text);
    }
    private static void RequireVersion(string file, WorkspaceFileVersion expected)
    {
        var comparison = Path.DirectorySeparatorChar == '\\' ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        if (expected == null || !file.Equals(expected.File, comparison) || !System.IO.File.Exists(file) || WorkspacePaths.FileHash(file) != expected.Sha256)
            throw new IOException("Workspace metadata was changed externally; refusing to overwrite it.");
    }
    private static void Write(string file, byte[] bytes, CancellationToken cancellation)
    {
        WorkspacePaths.NoLinks(file);
        using var stream = new FileStream(file, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        for (int i = 0; i < bytes.Length; i += 65536) { cancellation.ThrowIfCancellationRequested(); stream.Write(bytes, i, Math.Min(65536, bytes.Length - i)); }
        stream.Flush(true);
    }
    private static void CopyResource(string source, string target, WorkspaceResource resource, CancellationToken cancellation)
    {
        WorkspacePaths.NoLinks(source);
        using var input = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (input.Length != resource.Bytes) throw new InvalidDataException("Source size changed: " + resource.Id);
        using var output = new FileStream(target, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        using var sha = IncrementalHash.CreateHash(HashAlgorithmName.SHA256); var buffer = new byte[65536]; int read;
        while ((read = input.Read(buffer, 0, buffer.Length)) != 0)
        { cancellation.ThrowIfCancellationRequested(); output.Write(buffer, 0, read); sha.AppendData(buffer, 0, read); }
        output.Flush(true);
        string hash = BitConverter.ToString(sha.GetHashAndReset()).Replace("-", "").ToLowerInvariant();
        if (hash != resource.Sha256) throw new InvalidDataException("Source content changed: " + resource.Id);
    }
    private static void RequireOwnedStage(string staging, string parent)
    {
        string path = Path.GetFullPath(staging);
        if (Path.GetDirectoryName(path) != parent || !Path.GetFileName(path).StartsWith(".vfxwork-stage-", StringComparison.Ordinal))
            throw new IOException("Refusing operation outside the owned staging directory.");
        WorkspacePaths.NoLinks(path);
    }
    private static void CleanStage(string staging, string parent)
    {
        RequireOwnedStage(staging, parent);
        var pending = new Stack<string>(); pending.Push(staging);
        while (pending.Count != 0)
            foreach (string entry in Directory.EnumerateFileSystemEntries(pending.Pop()))
            {
                WorkspacePaths.NoLinks(entry);
                if (Directory.Exists(entry)) pending.Push(entry);
            }
        Directory.Delete(staging, true);
    }
}
