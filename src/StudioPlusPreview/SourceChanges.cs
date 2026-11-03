using System.Security.Cryptography;
using System.Text;

namespace InFalsus.Vfx.StudioPlus;
internal sealed class SourceChanges
{
    string observed="",attempted="";
    string cachedFiles="",cachedFingerprint="";
    double changedAt;
    internal static string Fingerprint(SourceWorkspace workspace)
    {
        var paths=workspace.SourceFiles();
        using var hash=SHA256.Create();var text=new StringBuilder();
        foreach(var path in paths){var info=new FileInfo(path);if(info.Length>4*1024*1024)throw new InvalidDataException("单个源码文件不能超过 4 MiB。");text.Append(path).Append(':').Append(BitConverter.ToString(hash.ComputeHash(File.ReadAllBytes(path)))).Append('\n');}
        return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(text.ToString())));
    }
    internal string Read(SourceWorkspace workspace)
    {
        string files=string.Join("\n",workspace.SourceFiles().Select(p=>{var f=new FileInfo(p);return p+":"+f.Length+":"+f.LastWriteTimeUtc.Ticks;}));
        if(files==cachedFiles)return cachedFingerprint;cachedFingerprint=Fingerprint(workspace);cachedFiles=files;return cachedFingerprint;
    }
    internal void Reset(string fingerprint,bool alreadyBuilt){observed=fingerprint;attempted=alreadyBuilt?fingerprint:"";changedAt=0;}
    internal void Observe(string fingerprint,double now){if(fingerprint==observed)return;observed=fingerprint;changedAt=now;}
    internal bool Ready(double now)=>observed.Length>0&&observed!=attempted&&now-changedAt>=.8;
    internal void Attempt(){attempted=observed;}
}
