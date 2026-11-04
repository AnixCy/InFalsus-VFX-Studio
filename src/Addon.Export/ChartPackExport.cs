using System.IO.Compression;
using InFalsus.Presentation;
using InFalsus.Presentation.Authoring;

namespace InFalsus.Vfx.Addon;

public static class ChartPackExport
{
    public static string Publish(string songDirectory,int difficulty,AuthoringProjectDocument project,string image,string bundle,string colorSpace,
        string destination,bool zip,Func<object,string> encode,Func<string,string,string?[]> readField,Func<string,ChartPackManifest> decodePack,Func<string,AddonManifest> decodeAddon,BackgroundExportContext? context=null)
    {
        if(difficulty<0||difficulty>3)throw new ArgumentOutOfRangeException(nameof(difficulty));
        var song=ChartPack.ReadSong(songDirectory,readField);
        if(string.IsNullOrWhiteSpace(song.Charts[difficulty]))throw new InvalidDataException("此曲包没有所选难度的谱面。");
        destination=Path.GetFullPath(destination);
        if(File.Exists(destination)||Directory.Exists(destination))throw new IOException("导出目标已存在；请选择新文件名。");
        if(destination.StartsWith(song.Root.TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("请导出到源曲包之外，保留原件。");
        var manifest=new ChartPackManifest{SongId=song.Id,Charts=song.Charts.ToArray()};
        for(int i=0;i<4;i++)if(!string.IsNullOrWhiteSpace(song.Charts[i]))manifest.ChartHashes[i]=AddonPackage.Hash(ChartPack.Resolve(song.Root,song.Charts[i]!));
        var target=new AddonTarget{SongKey=ChartPack.SongKey(song.Id),Difficulty=difficulty,ChartFingerprint=manifest.ChartHashes[difficulty]!};
        BackgroundExport.Compile(project,target,colorSpace,context);
        var old=ChartPack.ReadBindings(song,decodePack);
        string parent=Path.GetDirectoryName(destination)!;Directory.CreateDirectory(parent);
        string staging=Path.Combine(parent,".vfx-chartpack-"+Guid.NewGuid().ToString("N").Substring(0,16));
        if(Directory.Exists(staging)||File.Exists(staging))throw new IOException("曲包暂存位置冲突，请重试。");Directory.CreateDirectory(staging);
        string temporaryZip=staging+".zip";
        try {
            // Copy declared runtime media only; never collect editor caches or the whole project.
            var folder=Path.Combine(staging,SafeFolder(song.Id));Directory.CreateDirectory(folder);
            long bytes=0;
            void Copy(string from,string to){bytes+=new FileInfo(from).Length;if(bytes>2L*1024*1024*1024)throw new InvalidDataException("曲包超过 2 GiB。");Directory.CreateDirectory(Path.GetDirectoryName(to)!);File.Copy(from,to);}
            foreach(string file in song.Files)Copy(ChartPack.Resolve(song.Root,file),ChartPack.Resolve(folder,file,false));
            foreach(var binding in old.Where(b=>b.Difficulty!=difficulty)) {
                string relative=binding.Manifest.Substring(song.Root.TrimEnd(Path.DirectorySeparatorChar).Length+1);
                var addon=decodeAddon(ChartPack.ReadText(binding.Manifest));
                string oldRoot=Path.GetDirectoryName(binding.Manifest)!,newRoot=Path.GetDirectoryName(ChartPack.Resolve(folder,relative,false))!;
                Copy(binding.Manifest,ChartPack.Resolve(folder,relative,false));
                foreach(var file in addon.Files) {
                    var from=ChartPack.Resolve(oldRoot,file.Key);
                    if(!string.Equals(AddonPackage.Hash(from),file.Value,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("已有难度演出校验失败。");
                    Copy(from,ChartPack.Resolve(newRoot,file.Key,false));
                }
                manifest.Addons[binding.Difficulty]=relative.Replace('\\','/');
            }
            string addonRelative="vfx/"+ChartPack.DifficultyNames[difficulty]+"/addon.json";
            BackgroundExport.Publish(project,target,image,bundle,colorSpace,Path.GetDirectoryName(ChartPack.Resolve(folder,addonRelative,false))!,encode,context);
            manifest.Addons[difficulty]=addonRelative;
            string text=encode(manifest);
            if(System.Text.Encoding.UTF8.GetByteCount(text)>AddonPackage.MaximumDocumentBytes)throw new InvalidDataException("曲包清单超过上限。");
            File.WriteAllText(ChartPack.Resolve(folder,ChartPack.ManifestPath,false),text,new System.Text.UTF8Encoding(false));
            // Sources must not change halfway through publishing a portable bundle.
            for(int i=0;i<4;i++)if(manifest.Charts[i]!=null&&(AddonPackage.Hash(ChartPack.Resolve(song.Root,manifest.Charts[i]!))!=manifest.ChartHashes[i]
                ||AddonPackage.Hash(ChartPack.Resolve(folder,manifest.Charts[i]!))!=manifest.ChartHashes[i]))
                throw new IOException("导出时谱面发生变化，请保存后重试。");
            if(zip){ZipFile.CreateFromDirectory(staging,temporaryZip,CompressionLevel.Optimal,false);File.Move(temporaryZip,destination);}
            else Directory.Move(folder,destination);
            return destination;
        } finally {
            if(File.Exists(temporaryZip))File.Delete(temporaryZip);
            var owned=Path.GetFullPath(staging);
            if(Directory.Exists(owned)&&Path.GetDirectoryName(owned)==parent&&Path.GetFileName(owned).StartsWith(".vfx-chartpack-",StringComparison.Ordinal)
                &&(File.GetAttributes(owned)&FileAttributes.ReparsePoint)==0)Directory.Delete(owned,true);
        }
    }
    static string SafeFolder(string id)=>"Song_"+new string(id.Select(c=>Path.GetInvalidFileNameChars().Contains(c)?'_':c).ToArray());
}
