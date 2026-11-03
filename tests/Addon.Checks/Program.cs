using System.Text.Json;
using System.Text.Json.Serialization;
using InFalsus.Presentation;
using InFalsus.Presentation.Authoring;
using InFalsus.Vfx.Addon;

var json=new JsonSerializerOptions{WriteIndented=true,PropertyNameCaseInsensitive=true,
    Converters={new JsonStringEnumConverter(JsonNamingPolicy.CamelCase,allowIntegerValues:false)}};
string Encode(object value)=>JsonSerializer.Serialize(value,value.GetType(),json);
T Decode<T>(string value)=>JsonSerializer.Deserialize<T>(value,json)!;

// Developer CLI uses the same exporter as the Studio button. No Unity/editor dependencies.
if(args.Length==8&&args[0]=="export") {
    var envelope=Decode<SourceFile>(File.ReadAllText(args[1]));
    var chosenTarget=Decode<AddonTarget[]>(File.ReadAllText(args[2])).Single(t=>t.SongKey==args[3]&&t.Difficulty==int.Parse(args[4]));
    Console.WriteLine(BackgroundExport.Publish(envelope.Project,chosenTarget,envelope.BackgroundPath,args[5],args[6],args[7],Encode));return;
}
if(args.Length!=2)throw new ArgumentException("Usage: <compiled background.bundle> <new check directory>, or export <project> <targets> <songKey> <difficulty> <bundle> <colorSpace> <new directory>");
var bundle=Path.GetFullPath(args[0]);var output=Path.GetFullPath(args[1]);
if(Directory.Exists(output))throw new IOException("Check output already exists");Directory.CreateDirectory(output);
var checks=new List<string>();
void Check(bool ok,string name){if(!ok)throw new Exception(name);checks.Add(name);}
void Reject(Action action,string name){bool rejected=false;try{action();}catch(Exception e)when(e is InvalidDataException or NotSupportedException or IOException){rejected=true;}Check(rejected,name);}

var source=new AuthoringProjectDocument{ProjectId="minimal-addon-fixture",ContentId="fixture",Revision=1,Runtime=new PackageDocument {
    Id="editor-project",UnityVersion="6000.3.25f1",ColorSpace="Linear",TimeDomain=TimeDomain.Chart,
    Requires=new[]{"effect.instances"},
    Nodes=new[]{new MaterialNodeDocument{Id="background",Bundle="preview.bundle",Material="vfx/parameter-preview"},
        new MaterialNodeDocument{Id="adjust",Target="postprocess",Bundle="preview.bundle",Material="vfx/utility-post",EffectId="color-grade",StartSeconds=0,EndSeconds=120}},
    Channels=new[]{
        new ChannelDocument{Node="background",Property="_Brightness",Default=new[]{.4},Keys=new[]{
            new KeyDocument{Seconds=0,Value=new[]{.4},Ease=Easing.Linear},new KeyDocument{Seconds=2,Value=new[]{1.4},Ease=Easing.Linear},
            new KeyDocument{Seconds=4,Value=new[]{.4},Ease=Easing.Linear}}},
        new ChannelDocument{Node="background",Property="_Tint",Kind=ParameterKind.Color,Default=new[]{1.0,1.0,1.0,1.0}},
        new ChannelDocument{Node="background",Property="_PatternMix",Default=new[]{0.0}},
        new ChannelDocument{Node="background",Property="_Distortion",Default=new[]{0.0}},
        new ChannelDocument{Node="adjust",Property="_BaseColor",Kind=ParameterKind.Color,Default=new[]{.8,1.0,1.0,1.0}},
        new ChannelDocument{Node="adjust",Property="_Exposure",Default=new[]{0.0},Keys=new[]{
            new KeyDocument{Seconds=0,Value=new[]{0.0},Ease=Easing.Linear},new KeyDocument{Seconds=4,Value=new[]{-.5},Ease=Easing.Linear}}},
        new ChannelDocument{Node="adjust",Property="_VfxOpacity",Default=new[]{1.0}}}
}};
var target=new AddonTarget{SongKey="game:2",Difficulty=3,Label="Background fixture / FBD"};
var image=Path.Combine(output,"source-background.png");
File.WriteAllBytes(image,Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAIAAAACCAYAAABytg0kAAAAG0lEQVR4nGPQcMz7nxcw8T9DQMWC/7Mn7vsPAEnSCXVyxwqKAAAAAElFTkSuQmCC"));
var sourceFile=Path.Combine(output,"source.vfx.json");File.WriteAllText(sourceFile,Encode(new SourceFile{Project=source,BackgroundPath=image}));
var sourceHash=AddonPackage.Hash(sourceFile);var imageHash=AddonPackage.Hash(image);
var destination=Path.Combine(output,"ValidAddon");var manifest=BackgroundExport.Publish(source,target,image,bundle,"Gamma",destination,Encode);
var loaded=AddonPackage.Load(manifest,Decode<AddonManifest>,Decode<PackageDocument>,AddonPackage.Capabilities);
Check(loaded.Document.Nodes.Length==1&&loaded.Document.Nodes[0].Target=="background","one background draw, no postprocess nodes");
Check(loaded.Document.UnityVersion=="6000.3.9f1"&&loaded.Document.ColorSpace=="Gamma","target resource contract differs explicitly from editor");
Check(loaded.Document.TimeDomain==TimeDomain.Chart&&loaded.Document.OffsetSeconds==0,"source time domain and offset retained once");
var values=new ParameterValue[loaded.Timeline.Count];loaded.Timeline.Sample(1,values);
int brightness=loaded.Document.Channels.ToList().FindIndex(c=>c.Property=="_Brightness");
Check(Math.Abs(values[brightness].Number.X-.9)<.00001,"linear key timing survives export");
Check(loaded.Document.Channels.Single(c=>c.Property=="_GradeEnd").Default[0]==120,"effect interval is lowered to shader constants");
Check(loaded.Document.Channels.Any(c=>c.Property=="_GradeOpacity")&&!loaded.Document.Channels.Any(c=>c.Property=="_VfxOpacity"),"mixing property maps to fused background grading");
Check(loaded.Document.Nodes[0].Textures["image"]=="file:background.png","image packaged by relative path");
Check(Directory.GetFiles(destination).Length==4,"package contains no chart, audio, undo stack or editor files");
Check(AddonPackage.Hash(sourceFile)==sourceHash&&AddonPackage.Hash(image)==imageHash,"source project and media remain byte-identical");
Reject(()=>BackgroundExport.Publish(source,target,image,bundle,"Gamma",destination,Encode),"existing addon cannot be overwritten");
var bad=AuthoringProjects.Copy(source);bad.Runtime.Nodes[1].EffectId="ripple";
var blocked=Path.Combine(output,"Blocked");Reject(()=>BackgroundExport.Publish(bad,target,image,bundle,"Gamma",blocked,Encode),"unsupported effects block export");
Check(!Directory.Exists(blocked),"unsupported export creates no partial output");
bad.Runtime.Nodes[1].Enabled=false;Check(BackgroundExport.Compile(bad,target,"Gamma").Nodes.Length==1,"disabled unsupported effect is omitted");
bad=AuthoringProjects.Copy(source);bad.Runtime.Nodes[0].Material="native-background";
Reject(()=>BackgroundExport.Compile(bad,target,"Gamma"),"native material families cannot silently change at export");
Reject(()=>BackgroundExport.Compile(source,new AddonTarget{SongKey="custom:fixture",Difficulty=3},"Gamma"),"custom charts need an exact fingerprint");
Reject(()=>BackgroundExport.Compile(source,new AddonTarget{SongKey="",Difficulty=3},"Gamma"),"no wildcard chart binding");
Reject(()=>BackgroundExport.Compile(source,target,"Unknown"),"unknown color space rejected");
Check(loaded.Matches(new PlaybackSnapshot(1,"game:2",3,"",1,1,0,1,TransportState.Running,PlaybackPhase.Playing,false,false,false,0,true,1)),"matches selected chart");
Check(!loaded.Matches(new PlaybackSnapshot(1,"game:2",2,"",1,1,0,1,TransportState.Running,PlaybackPhase.Playing,false,false,false,0,true,1)),"other difficulty is unaffected");
var savedRuntime=File.ReadAllText(Path.Combine(destination,"runtime.json"));File.AppendAllText(Path.Combine(destination,"runtime.json")," ");
Reject(()=>AddonPackage.Load(manifest,Decode<AddonManifest>,Decode<PackageDocument>,AddonPackage.Capabilities),"changed runtime hash rejected before loading assets");
File.WriteAllText(Path.Combine(destination,"runtime.json"),savedRuntime);
var descriptor=Decode<AddonManifest>(File.ReadAllText(manifest));descriptor.Files["../outside"]="0".PadLeft(64,'0');
File.WriteAllText(manifest,Encode(descriptor));Reject(()=>AddonPackage.Load(manifest,Decode<AddonManifest>,Decode<PackageDocument>,AddonPackage.Capabilities),"paths cannot escape addon directory");
descriptor.Files.Remove("../outside");File.WriteAllText(manifest,Encode(descriptor));
var noImage=Path.Combine(output,"NoImageAddon");BackgroundExport.Publish(source,target,"",bundle,"Gamma",noImage,Encode);
Check(!AddonPackage.Load(Path.Combine(noImage,"addon.json"),Decode<AddonManifest>,Decode<PackageDocument>,AddonPackage.Capabilities).Document.Requires.Contains(AddonPackage.ImageCapability),"procedural background export needs no image file");
Check(!Directory.EnumerateDirectories(output,".vfx-export-*").Any(),"no staging directories remain");
File.WriteAllText(Path.Combine(output,"checks.json"),Encode(new{ok=true,checks}));Console.WriteLine(Encode(new{ok=true,count=checks.Count,output}));

sealed class SourceFile {public AuthoringProjectDocument Project{get;set;}=new();public string BackgroundPath{get;set;}="";}
