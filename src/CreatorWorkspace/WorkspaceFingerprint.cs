using System.Collections;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using InFalsus.Presentation.Authoring;
using InFalsus.Presentation.AudioReactive;

namespace InFalsus.Presentation.CreatorWorkspace;

/// <summary>Canonical binary change detection, independent of JSON formatting or serializer. No user types are reflected.</summary>
internal static class WorkspaceFingerprint
{
    private static readonly HashSet<Type> Types = new() { typeof(AuthoringProjectDocument), typeof(PackageDocument), typeof(TempoDocument),
        typeof(MaterialNodeDocument), typeof(ChannelDocument), typeof(KeyDocument), typeof(CurvePointDocument), typeof(TrackIdentityDocument),
        typeof(ParameterPresentation), typeof(WorkspaceResource), typeof(LinkedAudioDriver), typeof(AudioDriverRecipe), typeof(EnvelopeSettings),
        typeof(ParameterMapping), typeof(BakeSettings), typeof(CreatorWorkspaceDocument), typeof(WorkspaceBake) };
    internal static string Compute(object? value, CancellationToken cancellation = default)
    {
        using var sha = SHA256.Create();
        using var crypto = new CryptoStream(Stream.Null, sha, CryptoStreamMode.Write);
        using var writer = new BinaryWriter(crypto, Encoding.UTF8, true);
        writer.Write("creator-workspace-semantic-v1"); int items = 0;
        void Write(object? item, int depth)
        {
            if (++items % 256 == 0) cancellation.ThrowIfCancellationRequested();
            if (depth > 40 || items > 20_000_000) throw new InvalidDataException("Workspace digest budget exceeded.");
            if (item == null) { writer.Write((byte)0); return; }
            var type = item.GetType();
            // Generic FullName contains core-library assembly versions, which differ between .NET and Unity/Mono.
            writer.Write(item is IDictionary<string, string> ? "map:string:string" : type.FullName ?? "");
            switch (item)
            {
                case string s: writer.Write(s); return;
                case bool b: writer.Write(b); return;
                case int i: writer.Write(i); return;
                case long l: writer.Write(l); return;
                case uint u: writer.Write(u); return;
                case double d: writer.Write(d); return;
                case float f: writer.Write(f); return;
                case Enum e: writer.Write(Convert.ToInt64(e)); return;
                case IDictionary<string, string> map:
                    writer.Write(map.Count); foreach (var pair in map.OrderBy(p => p.Key, StringComparer.Ordinal)) { Write(pair.Key, depth + 1); Write(pair.Value, depth + 1); } return;
                case Array array:
                    writer.Write(array.Length); foreach (var entry in array) Write(entry, depth + 1); return;
            }
            if (!Types.Contains(type)) throw new InvalidDataException("Unknown workspace digest type: " + type.FullName);
            var properties = type.GetProperties(BindingFlags.Instance | BindingFlags.Public).OrderBy(p => p.Name, StringComparer.Ordinal).ToArray();
            writer.Write(properties.Length);
            foreach (var property in properties) { writer.Write(property.Name); Write(property.GetValue(item), depth + 1); }
        }
        cancellation.ThrowIfCancellationRequested(); Write(value, 0); writer.Flush(); crypto.FlushFinalBlock();
        return BitConverter.ToString(sha.Hash!).Replace("-", "").ToLowerInvariant();
    }
    internal static string Input(CreatorWorkspaceDocument d, BakeSettings settings, int budget) => Compute(new object[] {
        d.FormatVersion, d.WorkspaceId, d.ManualProject, d.Resources, d.AudioResourceId, d.BackgroundResourceId, d.AudioDrivers, settings, budget });
}
