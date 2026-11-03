using System.Security.Cryptography;
using System.Text;

namespace InFalsus.Presentation.Authoring;

public sealed class ProjectFileVersion
{
    public string FileName { get; }
    public string Sha256 { get; }
    internal ProjectFileVersion(string path, string hash) => (FileName, Sha256) = (path, hash);
}
public sealed class OpenedProject
{
    public AuthoringSession Session { get; }
    public ProjectFileVersion FileVersion { get; }
    internal OpenedProject(AuthoringSession session, ProjectFileVersion version) => (Session, FileVersion) = (session, version);
}
public sealed class ProjectSaveResult
{
    public ProjectFileVersion FileVersion { get; }
    public long SavedRevision { get; }
    public bool MatchesCurrentContent { get; }
    internal ProjectSaveResult(ProjectFileVersion version, long revision, bool matches)
        => (FileVersion, SavedRevision, MatchesCurrentContent) = (version, revision, matches);
}

/// <summary>The host supplies its JSON codec; no dependency on a particular editor/serializer.</summary>
public sealed class ProjectFileStore
{
    private readonly Func<AuthoringProjectDocument, string> _encode;
    private readonly Func<string, AuthoringProjectDocument> _decode;
    private readonly string[] _capabilities;
    public ProjectFileStore(Func<AuthoringProjectDocument, string> encode,
        Func<string, AuthoringProjectDocument> decode, IReadOnlyCollection<string> capabilities)
    {
        _encode = encode ?? throw new ArgumentNullException(nameof(encode));
        _decode = decode ?? throw new ArgumentNullException(nameof(decode)); _capabilities = capabilities.ToArray();
    }
    private static string Hash(byte[] bytes)
    {
        using var sha = SHA256.Create();
        return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
    }
    public OpenedProject Open(string fileName)
    {
        var path = Path.GetFullPath(fileName); var bytes = File.ReadAllBytes(path);
        var doc = _decode(Encoding.UTF8.GetString(bytes).TrimStart('\uFEFF'));
        var session = new AuthoringSession(doc, Path.GetDirectoryName(path)!, _capabilities, isSaved: true);
        return new OpenedProject(session, new ProjectFileVersion(path, Hash(bytes)));
    }
    private static void RequireFileVersion(string path, ProjectFileVersion? expected)
    {
        var comparison = Path.DirectorySeparatorChar == '\\' ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        if (expected == null)
        {
            if (File.Exists(path)) throw new AuthoringEditException(AuthoringEditError.FileConflict);
        }
        else if (!path.Equals(expected.FileName, comparison) || !File.Exists(path) || Hash(File.ReadAllBytes(path)) != expected.Sha256)
            throw new AuthoringEditException(AuthoringEditError.FileConflict);
    }
    public ProjectSaveResult Save(AuthoringSession session, string fileName, long expectedRevision,
        ProjectFileVersion? expectedFileVersion = null)
    {
        var path = Path.GetFullPath(fileName); var directory = Path.GetDirectoryName(path)!;
        var captured = session.CaptureForSave(expectedRevision, path);
        RequireFileVersion(path, expectedFileVersion);
        var text = _encode(AuthoringProjects.Copy(captured));
        if (string.IsNullOrWhiteSpace(text)) throw new InvalidDataException("JSON codec produced an empty project.");
        // Catch broken codecs before replacing an existing artwork file.
        var roundTrip = _decode(text);
        AuthoringProjects.Compile(roundTrip, directory, _capabilities, requireResources: false);
        if (roundTrip.ProjectId != captured.ProjectId || roundTrip.ContentId != captured.ContentId
            || roundTrip.Revision != captured.Revision || roundTrip.AssetRoot != captured.AssetRoot
            || !AuthoringProjects.SameEditableContent(captured, roundTrip))
            throw new InvalidDataException("JSON codec changed project identity or edited data.");
        var bytes = new UTF8Encoding(false).GetBytes(text);
        Directory.CreateDirectory(directory);
        var temp = Path.Combine(directory, "." + Path.GetFileName(path) + "." + Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            { stream.Write(bytes, 0, bytes.Length); stream.Flush(true); }
            RequireFileVersion(path, expectedFileVersion);
            if (expectedFileVersion == null) File.Move(temp, path);
            else File.Replace(temp, path, null);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
        var matches = session.MarkSaved(captured.ContentId);
        return new ProjectSaveResult(new ProjectFileVersion(path, Hash(bytes)), captured.Revision, matches);
    }
}
