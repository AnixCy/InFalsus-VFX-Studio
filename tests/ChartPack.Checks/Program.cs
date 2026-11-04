using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Serialization;
using InFalsus.Presentation;
using InFalsus.Presentation.Authoring;
using InFalsus.Vfx.Addon;

if(args.Length!=2)throw new ArgumentException("<background.bundle> <new output directory>");
string bundle=Path.GetFullPath(args[0]),output=Path.GetFullPath(args[1]);
if(Directory.Exists(output))throw new IOException("Use a new output directory.");Directory.CreateDirectory(output);
var json=new JsonSerializerOptions{PropertyNameCaseInsensitive=true,WriteIndented=true,Converters={new JsonStringEnumConverter(JsonNamingPolicy.CamelCase,false)}};
string Encode(object value)=>JsonSerializer.Serialize(value,value.GetType(),json);
T Decode<T>(string text)=>JsonSerializer.Deserialize<T>(text,json)!;
string?[] Fields(string text,string key){using var doc=JsonDocument.Parse(text);if(!doc.RootElement.TryGetProperty(key,out var v))return Array.Empty<string?>();string? Read(JsonElement e)=>e.ValueKind==JsonValueKind.Null?null:e.GetString();return v.ValueKind==JsonValueKind.Array?v.EnumerateArray().Select(Read).ToArray():new[]{Read(v)};}
var checks=new List<string>();
void Check(bool ok,string name){if(!ok)throw new Exception(name);checks.Add(name);}
void Reject(Action action,string name){bool rejected=false;try{action();}catch(Exception e)when(e is InvalidDataException or IOException or ArgumentException or NotSupportedException){rejected=true;}Check(rejected,name);}
string source=Path.Combine(output,"ReadySong");Directory.CreateDirectory(source);
File.WriteAllText(Path.Combine(source,"if"),Encode(new{id="pack_fixture",name="Presentation Fixture",composer="Fixture",song="audio.wav",jacket="jacket.png",
    chart=new string?[]{null,null,"fixture2.spc","fixture3.spc"},charter=new string?[]{null,null,"Fixture","Fixture"},lv=new int?[]{null,null,1,1},preview_seconds=new[]{0,1}}));
string[] golden={"SUNQMQEAHABQACAAAQAAAAAAAAAAAPBCAACAQJToJIKmhi3QrmnTX1oiFu9BeTY1ohXo/JUJI4KuadtfWnU2tbsR6PyVKCOCr2nDXw8AAAA8AAAAogAAAIgCAACHAAAAHAIAAJcAAABcAgAA",
    "SUNQMQEAHABQACAAAQAAAAAAAAAAAPBCAACAQLFfDbSHZB4U5C0tfBeOopsjZoyZESTY7bJADbTkLSV8PGKMGSog2O2ynwy05S09fN4AAAB4AwAAnwAAAHwCAABDAAAADAEAAAUAAAAUAAAA"};
// Independent encoder fixture: one authored tap at 1000 ms, no copyrighted chart data.
for(int i=0;i<2;i++)File.WriteAllBytes(Path.Combine(source,"fixture"+(i+2)+".spc"),Convert.FromBase64String(golden[i]));
using(var writer=new BinaryWriter(File.Create(Path.Combine(source,"audio.wav")))){writer.Write("RIFF"u8);writer.Write(36+48000*2);writer.Write("WAVEfmt "u8);writer.Write(16);writer.Write((short)1);writer.Write((short)1);writer.Write(48000);writer.Write(96000);writer.Write((short)2);writer.Write((short)16);writer.Write("data"u8);writer.Write(48000*2);writer.Write(new byte[48000*2]);}
File.WriteAllBytes(Path.Combine(source,"jacket.png"),Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAIAAAACCAYAAABytg0kAAAAG0lEQVR4nGPQcMz7nxcw8T9DQMWC/7Mn7vsPAEnSCXVyxwqKAAAAAElFTkSuQmCC"));
Directory.CreateDirectory(Path.Combine(source,".ifstudio"));File.WriteAllText(Path.Combine(source,".ifstudio/private-cache"),"not for players");
File.WriteAllText(Path.Combine(source,"unrelated.txt"),"not declared");
var before=Directory.GetFiles(source,"*",SearchOption.AllDirectories).ToDictionary(p=>p,AddonPackage.Hash);
var project=new AuthoringProjectDocument{ProjectId="chart-pack-fixture",ContentId="fixture",Revision=1,Runtime=new PackageDocument{
    Id="fixture",TimeDomain=TimeDomain.Chart,Nodes=new[]{new MaterialNodeDocument{Id="background",Bundle="preview.bundle",Material="vfx/parameter-preview"}},
    Channels=new[]{new ChannelDocument{Property="_Brightness",Default=new[]{.4},Keys=new[]{new KeyDocument{Seconds=0,Value=new[]{.4},Ease=Easing.Linear},new KeyDocument{Seconds=2,Value=new[]{1.4},Ease=Easing.Linear},new KeyDocument{Seconds=4,Value=new[]{.4},Ease=Easing.Linear}}},
        new ChannelDocument{Property="_Tint",Kind=ParameterKind.Color,Default=new[]{1d,1d,1d,1d}},new ChannelDocument{Property="_PatternMix",Default=new[]{1d}},new ChannelDocument{Property="_Distortion",Default=new[]{0d}}}}};
string Publish(string from,int diff,string to,bool zip,Func<object,string>? codec=null)=>ChartPackExport.Publish(from,diff,project,Path.Combine(from,"jacket.png"),bundle,"Gamma",to,zip,codec??Encode,Fields,Decode<ChartPackManifest>,Decode<AddonManifest>);
string zip=Path.Combine(output,"Fixture.zip");Publish(source,3,zip,true);
Check(File.Exists(zip),"whole-song export publishes one ZIP");
using(var archive=ZipFile.OpenRead(zip)) {
    Check(archive.Entries.Where(e=>e.Name.Length>0).All(e=>e.FullName.StartsWith("Song_pack_fixture/",StringComparison.Ordinal)),"ZIP has exactly one song folder for extraction into Charts");
    Check(!archive.Entries.Any(e=>e.FullName.Contains(".ifstudio")||e.FullName.EndsWith("unrelated.txt")),"only declared gameplay assets and VFX are distributed");
}
string charts=Path.Combine(output,"Charts");ZipFile.ExtractToDirectory(zip,charts);string unpacked=Path.Combine(charts,"Song_pack_fixture");
var song=ChartPack.ReadSong(unpacked,Fields);var bindings=ChartPack.ReadBindings(song,Decode<ChartPackManifest>);
Check(bindings.Length==1&&bindings[0].Difficulty==3&&bindings[0].LogicalName=="pack_fixture3.spc","pack binds by if.id and positional difficulty, never machine catalog");
var runtime=AddonPackage.Load(bindings[0].Manifest,Decode<AddonManifest>,Decode<PackageDocument>,AddonPackage.Capabilities);
Check(runtime.Document.SongKey==ChartPack.SongKey(song.Id)&&runtime.Document.ChartFingerprint==AddonPackage.Hash(Path.Combine(unpacked,"fixture3.spc")),"runtime chart identity is generated from packaged bytes");
var values=new ParameterValue[runtime.Timeline.Count];runtime.Timeline.Sample(1,values);
Check(Math.Abs(values[0].Number.X-.9f)<.00001,"existing VFX animation survives the whole-song ZIP path");
Check(song.Charts[2]!=null&&ChartPack.ReadBindings(song,Decode<ChartPackManifest>).All(b=>b.Difficulty!=2),"other declared difficulty remains available without VFX");
Check(File.ReadAllBytes(Path.Combine(source,"audio.wav")).SequenceEqual(File.ReadAllBytes(Path.Combine(unpacked,"audio.wav"))),"audio is copied once without conversion");
Check(before.All(e=>AddonPackage.Hash(e.Key)==e.Value),"source charts, metadata, media and editor cache remain unchanged");
string relocated=Path.Combine(charts,"RenamedSong");Directory.Move(unpacked,relocated);
Check(ChartPack.ReadBindings(ChartPack.ReadSong(relocated,Fields),Decode<ChartPackManifest>)[0].SongKey==runtime.Document.SongKey,"folder relocation preserves identity and asset loading");
string both=Path.Combine(output,"BothDifficulties");Publish(relocated,2,both,false);
var all=ChartPack.ReadBindings(ChartPack.ReadSong(both,Fields),Decode<ChartPackManifest>);
Check(all.Length==2&&all.Select(b=>b.Difficulty).SequenceEqual(new[]{2,3}),"exporting another difficulty preserves the existing difficulty addon");
Check(AddonPackage.Hash(all.Single(b=>b.Difficulty==3).Manifest)==AddonPackage.Hash(bindings[0].Manifest.Replace(unpacked,relocated)),"unmodified difficulty presentation is copied exactly");
Reject(()=>Publish(source,0,Path.Combine(output,"Missing"),false),"missing difficulty is rejected before publishing");
Reject(()=>Publish(source,3,zip,true),"an existing ZIP is never replaced");
Reject(()=>Publish(source,3,Path.Combine(source,"Nested"),false),"export cannot write inside the source song");
Reject(()=>ChartPack.Resolve(source,"../outside"),"relative traversal outside a song is rejected");
Reject(()=>ChartPack.Resolve(source,Path.Combine(output,"outside")),"absolute file references are rejected");
string chartFile=Path.Combine(both,"fixture3.spc");File.AppendAllText(chartFile,"changed");
Reject(()=>ChartPack.ReadBindings(ChartPack.ReadSong(both,Fields),Decode<ChartPackManifest>),"changed chart disables only its stale VFX association");
string failed=Path.Combine(output,"Failed.zip");Reject(()=>Publish(source,3,failed,true,_=>throw new IOException("injected codec failure")),"publication failure is reported");
Check(!File.Exists(failed)&&!Directory.EnumerateFileSystemEntries(output,".vfx-chartpack-*").Any(),"failed publication leaves no partial ZIP or staging tree");
string metaPath=Path.Combine(source,"if"),meta=File.ReadAllText(metaPath);File.WriteAllText(metaPath,meta.Replace("audio.wav","../audio.wav"));
Reject(()=>ChartPack.ReadSong(source,Fields),"declared media cannot escape the package");File.WriteAllText(metaPath,meta);
File.WriteAllBytes(Path.Combine(source,"fixture3.spc"),System.Text.Encoding.UTF8.GetBytes("chart(120,4)\ntap(1000,1,1)\n"));
Reject(()=>ChartPack.ReadSong(source,Fields),"plaintext authoring SPC is rejected rather than shipped as a playable binary");File.WriteAllBytes(Path.Combine(source,"fixture3.spc"),Convert.FromBase64String(golden[1]));
Check(before.All(e=>AddonPackage.Hash(e.Key)==e.Value),"negative fixtures restore original source data");
File.WriteAllText(Path.Combine(output,"checks.json"),Encode(new{ok=true,checks,source,zip,gameStarted=false,visualCalls=0}));
Console.WriteLine(Encode(new{ok=true,count=checks.Count,source,zip}));
