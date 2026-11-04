using System.Text.Json;

namespace InFalsus.Presentation.Game;

internal static class RuntimeDefaults
{
    internal static GameProfile[] Profiles(string optionalFile)
    {
        using var stream=typeof(RuntimeDefaults).Assembly.GetManifestResourceStream("InFalsus.Presentation.Profiles.json")!;
        var built=JsonSerializer.Deserialize<GameProfile[]>(stream)!;
        return (File.Exists(optionalFile)?JsonSerializer.Deserialize<GameProfile[]>(File.ReadAllText(optionalFile))!:Array.Empty<GameProfile>())
            .Concat(built).GroupBy(p=>p.Sha256,StringComparer.OrdinalIgnoreCase).Select(g=>g.First()).ToArray();
    }
    internal static void EnsureConfig(string dataRoot)
    {
        Directory.CreateDirectory(dataRoot);string file=Path.Combine(dataRoot,"config.json");
        if(!File.Exists(file))using(var stream=new FileStream(file,FileMode.CreateNew,FileAccess.Write,FileShare.Read))
        using(var writer=new StreamWriter(stream))writer.Write("{\"enabled\":true,\"reloadSerial\":0}");
    }
}
