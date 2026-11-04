using System.Reflection;
using System.Text.Json;
using InFalsus.Vfx.Addon;

namespace InFalsus.Presentation.Game;

public interface IChartPackBindings { IReadOnlyList<string> RefreshChartPacks(Action<string> report); }

public sealed unsafe partial class NativeGameBridge:IChartPackBindings
{
    string gameRoot="";
    readonly Dictionary<string,ChartIdentity> packCatalog=new(StringComparer.Ordinal);
    public IReadOnlyList<string> RefreshChartPacks(Action<string> report)
    {
        var found=new List<string>();var next=new Dictionary<string,ChartIdentity>(StringComparer.Ordinal);
        var ambiguous=new HashSet<string>(StringComparer.Ordinal);
        string root=Path.Combine(gameRoot,"Charts");
        if(Directory.Exists(root)&&(File.GetAttributes(root)&FileAttributes.ReparsePoint)==0)
        foreach(string directory in Directory.EnumerateDirectories(root).OrderBy(d=>d,StringComparer.OrdinalIgnoreCase)) {
            if(!File.Exists(Path.Combine(directory,ChartPack.ManifestPath)))continue;
            try {
                var song=ChartPack.ReadSong(directory,ReadPackField);
                foreach(var binding in ChartPack.ReadBindings(song,t=>JsonSerializer.Deserialize<ChartPackManifest>(t,PackJson)!)) {
                    if(!next.TryAdd(binding.LogicalName,new(binding.SongKey,binding.Difficulty,binding.Fingerprint)))ambiguous.Add(binding.LogicalName);
                    found.Add(binding.Manifest);
                }
            }catch(Exception error){report("Chart-pack VFX skipped: "+Path.GetFileName(directory)+": "+error.Message);}
        }
        foreach(string name in ambiguous){next.Remove(name);report("Ambiguous chart-pack id: "+name);}
        packCatalog.Clear();foreach(var entry in next)packCatalog.Add(entry.Key,entry.Value);
        return found;
    }
    static readonly JsonSerializerOptions PackJson=new(){PropertyNameCaseInsensitive=true};
    static string?[] ReadPackField(string text,string key)
    {
        using var doc=JsonDocument.Parse(text,new JsonDocumentOptions{AllowTrailingCommas=true,CommentHandling=JsonCommentHandling.Skip});
        if(!doc.RootElement.TryGetProperty(key,out var field))return Array.Empty<string?>();
        string? Scalar(JsonElement value)=>value.ValueKind switch{JsonValueKind.Null=>null,JsonValueKind.String=>value.GetString(),_=>throw new InvalidDataException("曲包配置字段类型错误。")};
        return field.ValueKind==JsonValueKind.Array?field.EnumerateArray().Select(Scalar).ToArray():new[]{Scalar(field)};
    }
    // Optional bridge supplied by the chart loader: compare the bytes it actually imported,
    // not a changed file on disk. Reflection occurs only when entering a chart.
    static string? ImportedChartHash(string name)
    {
        var assembly=AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a=>a.GetName().Name=="InFalsusChartLoader");
        return assembly?.GetType("InFalsusChartLoader.ChartSession")?.GetMethod("HashOf",BindingFlags.Public|BindingFlags.Static)?.Invoke(null,new object[]{name}) as string;
    }
}
