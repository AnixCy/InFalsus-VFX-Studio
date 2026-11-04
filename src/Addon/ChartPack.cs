using InFalsus.Presentation;

namespace InFalsus.Vfx.Addon;

public sealed class ChartPackManifest
{
    public int FormatVersion { get; set; }=1;
    public string SongId { get; set; }="";
    public string?[] Charts { get; set; }=new string?[4];
    public string?[] ChartHashes { get; set; }=new string?[4];
    public string?[] Addons { get; set; }=new string?[4];
}
public sealed class ChartPackSong
{
    public string Root{get;} public string Id{get;} public string[] Files{get;} public string?[] Charts{get;}
    public ChartPackSong(string root,string id,string[] files,string?[] charts)=>(Root,Id,Files,Charts)=(root,id,files,charts);
}
public sealed class ChartPackBinding
{
    public string LogicalName{get;} public string SongKey{get;} public int Difficulty{get;} public string Fingerprint{get;} public string Manifest{get;}
    public ChartPackBinding(string logicalName,string songKey,int difficulty,string fingerprint,string manifest)=>(LogicalName,SongKey,Difficulty,Fingerprint,Manifest)=(logicalName,songKey,difficulty,fingerprint,manifest);
}

/// <summary>Shared folder contract. Hosts supply JSON codecs; no loader or game dependency.</summary>
public static class ChartPack
{
    public const string ManifestPath="vfx/chart-pack.json";
    public static readonly string[] DifficultyNames={"MIN","EVO","ULT","FBD"};
    public static string SongKey(string id)=>"pack:"+id;
    public static string Resolve(string root,string relative,bool exists=true)
    {
        var path=EffectPackage.ResolveAsset(root,relative,exists);
        var fullRoot=Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar);
        for(var item=path;item.Length>=fullRoot.Length;item=Path.GetDirectoryName(item)??"") {
            if((File.Exists(item)||Directory.Exists(item))&&(File.GetAttributes(item)&FileAttributes.ReparsePoint)!=0)
                throw new InvalidDataException("曲包不能通过链接引用外部文件。");
            if(item==fullRoot)break;
        }
        if(exists&&!File.Exists(path))throw new FileNotFoundException("曲包缺少文件。",relative);
        return path;
    }
    public static string ReadText(string path)
    {
        if(new FileInfo(path).Length>AddonPackage.MaximumDocumentBytes)throw new InvalidDataException("曲包清单超过 4 MiB。");
        return File.ReadAllText(path);
    }
    // The if format uses one scalar for FBD, or four positional values.
    public static string?[] Slots(string?[] fields)
    {
        if(fields.Length==1)return new[]{null,null,null,fields[0]};
        if(fields.Length!=4)throw new InvalidDataException("谱面配置需要单值或四个难度槽位。");
        return fields;
    }
    public static ChartPackSong ReadSong(string root,Func<string,string,string?[]> readField)
    {
        root=Path.GetFullPath(root);string text=ReadText(Resolve(root,"if"));
        string Required(string key){var a=readField(text,key);return a.Length==1&&!string.IsNullOrWhiteSpace(a[0])?a[0]!:throw new InvalidDataException("曲包 if 缺少 "+key);}
        string id=Required("id");
        if(id.Length>120||id.IndexOfAny(new[]{'/','\\',':','\0'})>=0||id is "." or "..")throw new InvalidDataException("无效的曲包 id。");
        var charts=Slots(readField(text,"chart"));
        if(charts.All(string.IsNullOrWhiteSpace))throw new InvalidDataException("曲包没有可游玩的难度。");
        string audio=Required("song");
        void Extension(string value,params string[] allowed){if(!allowed.Contains(Path.GetExtension(value).ToLowerInvariant()))throw new InvalidDataException("曲包文件类型不受支持："+value);}
        Extension(audio,".ogg",".wav",".mp3");
        var files=new HashSet<string>(StringComparer.OrdinalIgnoreCase){"if",audio};
        foreach(var chart in charts)if(!string.IsNullOrWhiteSpace(chart))files.Add(chart!);
        foreach(string key in new[]{"jacket","background"})foreach(var value in readField(text,key))if(!string.IsNullOrWhiteSpace(value)){Extension(value!,key=="jacket"?new[]{".png"}:new[]{".png",".mp4"});files.Add(value!);}
        foreach(var file in files) {
            if(file.Replace('\\','/').StartsWith("vfx/",StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("谱面及媒体不得占用保留的 vfx 目录。");
            Resolve(root,file);
        }
        foreach(var chart in charts.Where(c=>!string.IsNullOrWhiteSpace(c))) {
            Extension(chart!,".spc");
            using var stream=File.OpenRead(Resolve(root,chart!));
            if(stream.Length<28||stream.Length>64*1024*1024)throw new InvalidDataException("谱面大小无效或超过 64 MiB。");
            byte[] magic=new byte[4];if(stream.Read(magic,0,4)!=4||magic[0]!='I'&&magic[0]!='S'
                ||System.Text.Encoding.ASCII.GetString(magic) is not ("ICP1" or "SPC2"))
                throw new InvalidDataException("请先导出可游玩的 ICP1/SPC2 谱面；编辑工程的明文 SPC 不能直接发布。");
        }
        return new(root,id,files.ToArray(),charts);
    }
    public static ChartPackBinding[] ReadBindings(ChartPackSong song,Func<string,ChartPackManifest> decode)
    {
        var path=Resolve(song.Root,ManifestPath,false);if(!File.Exists(path))return Array.Empty<ChartPackBinding>();
        var doc=decode(ReadText(path));
        if(doc==null||doc.FormatVersion!=1||doc.SongId!=song.Id||doc.Charts?.Length!=4||doc.ChartHashes?.Length!=4||doc.Addons?.Length!=4)
            throw new InvalidDataException("无效的随谱演出清单。");
        var found=new List<ChartPackBinding>();
        for(int i=0;i<4;i++) {
            if(string.IsNullOrWhiteSpace(doc.Addons[i]))continue;
            var chart=song.Charts[i];
            if(string.IsNullOrWhiteSpace(chart)||doc.Charts[i]!=chart||!string.Equals(AddonPackage.Hash(Resolve(song.Root,chart!)),doc.ChartHashes[i],StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("谱面已修改，随谱演出绑定需要重新导出。");
            string manifest=Resolve(song.Root,doc.Addons[i]!);
            if(!doc.Addons[i]!.Replace('\\','/').StartsWith("vfx/",StringComparison.Ordinal))throw new InvalidDataException("演出必须位于曲包 vfx 目录。");
            found.Add(new(song.Id+i+".spc",SongKey(song.Id),i,doc.ChartHashes[i]!,manifest));
        }
        return found.ToArray();
    }
}
