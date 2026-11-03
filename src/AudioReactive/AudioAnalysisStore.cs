using System.Security.Cryptography;
using System.Text;

namespace InFalsus.Presentation.AudioReactive;

public sealed class StoredAudioAnalysis
{
    public int FormatVersion { get; set; } = 1;
    public string PayloadSha256 { get; set; } = "";
    public AudioAnalysisDocument Payload { get; set; } = new();
}

public enum AudioCacheStatus { Missing, Hit, Invalid }
public sealed class AudioCacheRead
{
    public AudioCacheStatus Status { get; }
    public AudioAnalysis? Analysis { get; }
    public string Diagnostic { get; }
    internal AudioCacheRead(AudioCacheStatus status, AudioAnalysis? analysis = null, string diagnostic = "")
        => (Status, Analysis, Diagnostic) = (status, analysis, diagnostic);
}

/// <summary>Disposable, content-addressed creator cache. Artwork/project paths must never be used as its root.
/// A checksum covers validated metadata identity and every RMS float. Codecs are supplied by the host.</summary>
public sealed class AudioAnalysisStore
{
    private readonly string root;
    private readonly Func<StoredAudioAnalysis, string> encode;
    private readonly Func<string, StoredAudioAnalysis> decode;
    private readonly int maximumFileBytes;
    public AudioAnalysisStore(string directory, Func<StoredAudioAnalysis, string> encode,
        Func<string, StoredAudioAnalysis> decode, int maximumFileBytes = 128 * 1024 * 1024)
    {
        root = Path.GetFullPath(directory);
        this.encode = encode ?? throw new ArgumentNullException(nameof(encode));
        this.decode = decode ?? throw new ArgumentNullException(nameof(decode));
        if (maximumFileBytes < 1024 || maximumFileBytes > 256 * 1024 * 1024) throw new ArgumentException("Invalid cache file budget.");
        this.maximumFileBytes = maximumFileBytes;
    }
    public string PathFor(string cacheKey)
    {
        if (!Guard.IsDigest(cacheKey)) throw new ArgumentException("Cache keys must be lowercase SHA-256 values.");
        return Path.Combine(root, cacheKey + ".json");
    }
    public AudioCacheRead Read(string cacheKey, CancellationToken cancellation = default)
    {
        string file = PathFor(cacheKey);
        cancellation.ThrowIfCancellationRequested();
        byte[] bytes;
        try
        {
            // Readers see one complete old/new file and do not block an atomic replace on Windows.
            using var input = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete);
            if (input.Length > maximumFileBytes) return new AudioCacheRead(AudioCacheStatus.Invalid, diagnostic: "cache.too-large");
            bytes = new byte[checked((int)input.Length)]; int offset = 0;
            while (offset < bytes.Length)
            {
                cancellation.ThrowIfCancellationRequested();
                int read = input.Read(bytes, offset, Math.Min(65536, bytes.Length - offset));
                if (read == 0) return new AudioCacheRead(AudioCacheStatus.Invalid, diagnostic: "cache.truncated");
                offset += read;
            }
        }
        catch (FileNotFoundException) { return new AudioCacheRead(AudioCacheStatus.Missing); }
        catch (DirectoryNotFoundException) { return new AudioCacheRead(AudioCacheStatus.Missing); }
        cancellation.ThrowIfCancellationRequested();
        try
        {
            var stored = decode(new UTF8Encoding(false, true).GetString(bytes).TrimStart('\uFEFF'));
            var analysis = Validate(stored, cacheKey);
            cancellation.ThrowIfCancellationRequested();
            return new AudioCacheRead(AudioCacheStatus.Hit, analysis);
        }
        catch (Exception e) when (CodecFailure(e))
        { return new AudioCacheRead(AudioCacheStatus.Invalid, diagnostic: "cache.invalid-payload"); }
    }
    /// <returns>True when written/repaired; false when a valid entry already exists. Existing valid content wins.</returns>
    public bool Save(AudioAnalysis analysis, CancellationToken cancellation = default)
    {
        if (analysis == null) throw new ArgumentNullException(nameof(analysis));
        cancellation.ThrowIfCancellationRequested();
        string path = PathFor(analysis.CacheKey);
        var stored = new StoredAudioAnalysis { Payload = analysis.ToDocument() };
        string expectedHash = PayloadHash(stored.Payload);
        stored.PayloadSha256 = expectedHash;
        string text = encode(stored);
        if (string.IsNullOrWhiteSpace(text)) throw new InvalidDataException("Cache codec produced no data.");
        if (Encoding.UTF8.GetByteCount(text) > maximumFileBytes) throw new InvalidDataException("Cache file budget exceeded.");
        var roundTrip = decode(text);
        _ = Validate(roundTrip, analysis.CacheKey);
        if (roundTrip.PayloadSha256 != expectedHash) throw new InvalidDataException("Cache codec changed analysis values.");
        var bytes = new UTF8Encoding(false).GetBytes(text);
        cancellation.ThrowIfCancellationRequested();
        Directory.CreateDirectory(root);
        // A crashed process releases this OS lock. Contention is an I/O failure for the host to retry, not a cache miss.
        using var writerLock = new FileStream(path + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None, 1, FileOptions.DeleteOnClose);
        if (Read(analysis.CacheKey, cancellation).Status == AudioCacheStatus.Hit) return false;
        string temp = Path.Combine(root, "." + analysis.CacheKey + "." + Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                for (int offset = 0; offset < bytes.Length; offset += 65536)
                { cancellation.ThrowIfCancellationRequested(); stream.Write(bytes, offset, Math.Min(65536, bytes.Length - offset)); }
                stream.Flush(true);
            }
            cancellation.ThrowIfCancellationRequested();
            if (File.Exists(path)) File.Replace(temp, path, null); else File.Move(temp, path);
            return true;
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
    private static AudioAnalysis Validate(StoredAudioAnalysis stored, string key)
    {
        if (stored == null || stored.FormatVersion != 1 || !Guard.IsDigest(stored.PayloadSha256)) throw new InvalidDataException("Unknown cache envelope.");
        var analysis = AudioAnalysis.FromDocument(stored.Payload);
        if (analysis.CacheKey != key || PayloadHash(stored.Payload) != stored.PayloadSha256) throw new InvalidDataException("Cache integrity/identity mismatch.");
        return analysis;
    }
    private static bool CodecFailure(Exception error) => error is not (IOException or UnauthorizedAccessException or OperationCanceledException or OutOfMemoryException or AccessViolationException)
        || error is InvalidDataException;
    private static string PayloadHash(AudioAnalysisDocument document)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        hash.AppendData(Encoding.ASCII.GetBytes("audio-analysis-payload-v1:" + document.CacheKey));
        var bytes = new byte[4096]; int offset = 0;
        foreach (var band in document.Levels)
            foreach (float value in band)
            {
                int bits = BitConverter.SingleToInt32Bits(value);
                for (int b = 0; b < 4; b++) bytes[offset++] = (byte)(bits >> (8 * b));
                if (offset == bytes.Length) { hash.AppendData(bytes); offset = 0; }
            }
        if (offset != 0) hash.AppendData(bytes, 0, offset);
        return BitConverter.ToString(hash.GetHashAndReset()).Replace("-", "").ToLowerInvariant();
    }
}
