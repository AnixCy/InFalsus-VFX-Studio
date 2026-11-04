using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using InFalsus.Vfx.Addon;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace InFalsus.Vfx.StudioPlus;

/// <summary>Creator-only compilation from saved Studio files. Owns only a temporary release tree.</summary>
internal sealed class StudioSongPackage:IDisposable
{
    public string Root{get;private set;}="";
    public double ChartOffsetSeconds{get;private set;}
    bool owned;
    sealed class Request {public int Version=1;public CompileChart[] Charts=Array.Empty<CompileChart>();}
    sealed class CompileChart {public int Index;public string Name="",Text="";public bool SpcV2,RawChartImport;public double AudioOffsetMs;}
    sealed class Response {public bool Ok{get;set;}public string Error{get;set;}="";public CompiledChart[] Charts{get;set;}=Array.Empty<CompiledChart>();}
    sealed class CompiledChart {public int Index{get;set;}public string Name{get;set;}="";public string Bytes{get;set;}="";}
    public static bool CanRead(string path)=>File.Exists(Path.Combine(path,"if"))||File.Exists(Path.Combine(path,"infalsus.studio.json"));
    public static StudioSongPackage Prepare(string source,string projectId,string compilerDirectory,bool? forceSpcV2=null)
    {
        source=Path.GetFullPath(source);
        if(!File.Exists(Path.Combine(source,"infalsus.studio.json"))&&File.Exists(Path.Combine(source,"if")))return new(){Root=source};
        var result=new StudioSongPackage();
        try {
            var meta=JObject.Parse(ChartPack.ReadText(ChartPack.Resolve(source,"infalsus.studio.json")));
            if((string?)meta["format"]!="InFalsusStudioSong"||(int?)meta["version"]!=1)throw new InvalidDataException("请选择 Studio 歌曲工程文件夹。");
            double offset=(double?)meta["audioOffsetMs"]??0;
            if(!double.IsFinite(offset)||Math.Abs(offset)>60000)throw new InvalidDataException("音频偏移超出发布范围。");
            string id="vfx_"+new string(projectId.Where(c=>char.IsLetterOrDigit(c)||c is '_' or '-').Take(100).ToArray());
            if(id=="vfx_")throw new InvalidDataException("请先保存演出工程以建立稳定曲包 ID。");
            var rows=meta["difficulties"] as JArray??throw new InvalidDataException("工程没有难度资料。");
            var requests=new List<CompileChart>();var levels=new int?[4];var charters=new string?[4];var credits=new string?[4];var jackets=new string?[4];var charts=new string?[4];
            bool v2=forceSpcV2??(bool?)meta["spcV2"]??false,raw=(bool?)meta["rawChartImport"]??false;
            var used=new HashSet<int>();
            foreach(var row in rows) {
                int i=(int?)row["index"]??-1;
                if(i<0||i>3||!used.Add(i))throw new InvalidDataException("工程难度编号重复或无效。");
                string working=(string?)row["working"]??"";if(working.Length==0)continue;
                string chartPath=ChartPack.Resolve(source,working);
                if(new FileInfo(chartPath).Length>8*1024*1024)throw new InvalidDataException("单份工程谱面超过 8 MiB。");
                string text=File.ReadAllText(chartPath);
                if(Encoding.UTF8.GetByteCount(text)>8*1024*1024)throw new InvalidDataException("单份工程谱面超过 8 MiB。");
                if(!Regex.IsMatch(text,@"(?im)^\s*(tap|hold|flick|skyarea)\s*\("))continue;
                string rating=(string?)row["rating"]??"?";
                if(!int.TryParse(rating,NumberStyles.None,CultureInfo.InvariantCulture,out int level)||level<1||level>99)
                    throw new InvalidDataException(ChartPack.DifficultyNames[i]+" 请先填写 1～99 的整数难度等级。");
                // The saved project controls parser semantics; never silently convert a v2 chart to ICP1.
                requests.Add(new(){Index=i,Name="chart_"+ChartPack.DifficultyNames[i]+".spc",Text=text,SpcV2=v2,RawChartImport=raw,AudioOffsetMs=offset});
                levels[i]=level;charters[i]=string.IsNullOrWhiteSpace((string?)row["designer"])?"Unknown":(string?)row["designer"];
                string credit=(string?)row["jacketDesigner"]??"";if(string.IsNullOrWhiteSpace(credit))credit=(string?)meta["jacketDesigner"]??"";
                credits[i]=string.IsNullOrWhiteSpace(credit)?null:credit;
            }
            if(requests.Count==0)throw new InvalidDataException("工程没有非空、已评级的谱面。");
            var request=new Request{Charts=requests.ToArray()};string input=JsonConvert.SerializeObject(request);
            if(Encoding.UTF8.GetByteCount(input)>32*1024*1024)throw new InvalidDataException("全部难度文本超过 32 MiB。");
            string python=Path.Combine(compilerDirectory,"python.exe"),script=Path.Combine(compilerDirectory,"compiler.py");
            if(!File.Exists(python)||!File.Exists(script))throw new FileNotFoundException("编辑器缺少配套谱面编译工具；请使用包含 ChartCompiler 的完整更新。");
            var start=new ProcessStartInfo(python,"-I compiler.py"){WorkingDirectory=compilerDirectory,UseShellExecute=false,CreateNoWindow=true,RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true,
                StandardInputEncoding=new UTF8Encoding(false),StandardOutputEncoding=new UTF8Encoding(false),StandardErrorEncoding=new UTF8Encoding(false)};
            using var process=Process.Start(start)??throw new IOException("谱面编译器未能启动。");
            var stdout=process.StandardOutput.ReadToEndAsync();var stderr=process.StandardError.ReadToEndAsync();
            process.StandardInput.Write(input);process.StandardInput.Close();
            if(!process.WaitForExit(120000)){process.Kill();throw new IOException("谱面编译超过时限。");}
            string output=stdout.GetAwaiter().GetResult(),error=stderr.GetAwaiter().GetResult();
            var response=JsonConvert.DeserializeObject<Response>(output)??throw new InvalidDataException("谱面编译器没有返回有效结果。"+error);
            if(!response.Ok||process.ExitCode!=0)throw new InvalidDataException("谱面未导出："+response.Error);
            if(response.Charts.Length!=requests.Count)throw new InvalidDataException("编译器没有返回全部难度。");
            result.Root=Path.Combine(Path.GetTempPath(),".vfx-author-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(result.Root);result.owned=true;
            var copied=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
            string Image(string relative,string stem) {
                if(relative.Length==0)throw new InvalidDataException("请先选择歌曲或难度曲绘。");
                string from=ChartPack.Resolve(source,relative);if(copied.TryGetValue(from,out var prior))return prior;
                byte[] bytes=File.ReadAllBytes(from);ImageHeader.Validate(bytes);var texture=new Texture2D(2,2,TextureFormat.RGBA32,false);
                try{if(!ImageConversion.LoadImage(texture,bytes)||texture.width>4096||texture.height>4096)throw new InvalidDataException("曲绘无法读取或超过 4096 像素。");
                    string name=stem+".png";File.WriteAllBytes(Path.Combine(result.Root,name),ImageConversion.EncodeToPNG(texture));copied.Add(from,name);return name;}
                finally{UnityEngine.Object.Destroy(texture);}
            }
            foreach(var compiled in response.Charts) {
                var expected=requests.Single(r=>r.Index==compiled.Index);
                if(compiled.Name!=expected.Name)throw new InvalidDataException("编译器返回的编码文件名不匹配。");
                File.WriteAllBytes(Path.Combine(result.Root,compiled.Name),Convert.FromBase64String(compiled.Bytes));charts[compiled.Index]=compiled.Name;
                var row=rows.Single(r=>(int?)r["index"]==compiled.Index);
                string jacket=(string?)row["jacket"]??"";if(string.IsNullOrWhiteSpace(jacket))jacket=(string?)meta["jacket"]??"";
                jackets[compiled.Index]=Image(jacket,"jacket_"+ChartPack.DifficultyNames[compiled.Index]);
            }
            // ChartLoader requires a jacket for all four display slots, even absent charts.
            string fallbackJacket=jackets.First(j=>j!=null)!;
            for(int i=0;i<jackets.Length;i++)jackets[i]??=fallbackJacket;
            string audio=ChartPack.Resolve(source,(string?)meta["audio"]??"");string extension=Path.GetExtension(audio).ToLowerInvariant();
            if(extension is not (".ogg" or ".mp3" or ".wav"))throw new InvalidDataException("歌曲音频需要 OGG、MP3 或 WAV。");
            string audioName="audio"+extension;File.Copy(audio,Path.Combine(result.Root,audioName));
            string? background=null;string image=(string?)meta["gameplayBackgroundImage"]??"";
            if(image.Length>0)background=Image(image,"background");
            string title=(string?)meta["title"]??"",artist=(string?)meta["artist"]??"";
            double previewStart=(double?)meta["previewStartSeconds"]??0,previewEnd=(double?)meta["previewEndSeconds"]??0;
            if(!double.IsFinite(previewStart)||!double.IsFinite(previewEnd)||previewStart<0||previewEnd<=previewStart){previewStart=0;previewEnd=15;}
            var info=new {id,name=string.IsNullOrWhiteSpace(title)?"Untitled":title,composer=string.IsNullOrWhiteSpace(artist)?"Unknown":artist,song=audioName,jacket=jackets,background,
                chart=charts,charter=charters,illust=credits,lv=levels,preview_seconds=new[]{previewStart,previewEnd}};
            File.WriteAllText(Path.Combine(result.Root,"if"),JsonConvert.SerializeObject(info,Formatting.Indented),new UTF8Encoding(false));
            result.ChartOffsetSeconds=raw&&!v2?0:offset*.001;return result;
        }catch{result.Dispose();throw;}
    }
    public void Dispose()
    {
        if(!owned)return;owned=false;string path=Path.GetFullPath(Root);
        if(Directory.Exists(path)&&Path.GetDirectoryName(path)==Path.GetTempPath().TrimEnd(Path.DirectorySeparatorChar)
            &&Path.GetFileName(path).StartsWith(".vfx-author-",StringComparison.Ordinal)&&(File.GetAttributes(path)&FileAttributes.ReparsePoint)==0)Directory.Delete(path,true);
    }
}
