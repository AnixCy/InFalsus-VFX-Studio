using System.Security.Cryptography;

namespace InFalsus.Presentation.CreatorWorkspace;

internal static class WorkspacePaths
{
    internal static bool Digest(string? value) => value != null && value.Length == 64 && value.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f');
    internal static bool Id(string? value) => !string.IsNullOrEmpty(value) && value.Length <= 96 && value.All(c => c is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9' or '_' or '-' or '.');
    internal static string Relative(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 512 || value.Contains('\\') || value.Any(c => c < 32 || "<>:\"|?*".Contains(c)) || Path.IsPathRooted(value))
            throw new InvalidDataException("Resource path must be portable and relative.");
        foreach (string part in value.Split('/'))
        {
            string device = part.Split('.')[0].ToUpperInvariant();
            if (part.Length == 0 || part is "." or ".." || part.EndsWith(".") || part.EndsWith(" ")
                || device is "CON" or "PRN" or "AUX" or "NUL" or "CONIN$" or "CONOUT$"
                || (device.Length == 4 && (device.StartsWith("COM") || device.StartsWith("LPT")) && "123456789¹²³".Contains(device[3])))
                throw new InvalidDataException("Resource path contains an unsafe segment.");
        }
        return value;
    }
    internal static string Resolve(string root, string relative)
    {
        Relative(relative);
        root = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        string path = Path.GetFullPath(Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar)));
        var comparison = Path.DirectorySeparatorChar == '\\' ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        if (!path.StartsWith(root + Path.DirectorySeparatorChar, comparison)) throw new InvalidDataException("Resource escaped workspace root.");
        NoLinks(path); return path;
    }
    internal static void NoLinks(string path)
    {
        for (string? at = Path.GetFullPath(path); at != null; at = Path.GetDirectoryName(at))
        {
            FileAttributes attributes;
            try { attributes = File.GetAttributes(at); }
            catch (FileNotFoundException) { continue; }
            catch (DirectoryNotFoundException) { continue; }
            if ((attributes & FileAttributes.ReparsePoint) != 0) throw new InvalidDataException("Resource/workspace links are not supported.");
        }
    }
    internal static string FileHash(string path, CancellationToken cancellation = default)
    {
        NoLinks(path);
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var sha = IncrementalHash.CreateHash(HashAlgorithmName.SHA256); var buffer = new byte[65536]; int read;
        while ((read = stream.Read(buffer, 0, buffer.Length)) != 0) { cancellation.ThrowIfCancellationRequested(); sha.AppendData(buffer, 0, read); }
        cancellation.ThrowIfCancellationRequested();
        return BitConverter.ToString(sha.GetHashAndReset()).Replace("-", "").ToLowerInvariant();
    }
}
