using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;
using InFalsus.Presentation;
using InFalsus.Presentation.Authoring;
using InFalsus.Vfx.Addon;

if(args.Length!=1)throw new ArgumentException("<new output directory>");
string output=Path.GetFullPath(args[0]);if(Directory.Exists(output))throw new IOException("Use a new directory.");Directory.CreateDirectory(output);
var json=new JsonSerializerOptions{WriteIndented=true,Converters={new JsonStringEnumConverter()}};
var checks=new List<string>();void Check(bool v,string n){if(!v)throw new Exception(n);checks.Add(n);}
void Reject(Action a,string n){bool bad=false;try{a();}catch(Exception e)when(e is InvalidDataException or NotSupportedException){bad=true;}Check(bad,n);}
BackgroundPropertyDocument P(string name,double value=0)=>new(){Property=name,Value=new[]{value}};
var schema=new BackgroundBindingDocument{SourceId="generic-source",Native=true,Properties=new[]{P("_FlowAmount"),P("_WarpAmount"),P("_WarpRotation"),P("_OtherAmount"),P("_KW_USE_FLOW"),P("_KW_USE_WARP"),P("_KW_USE_OTHER"),
    new BackgroundPropertyDocument{Property="_BaseColor",Kind=ParameterKind.Color,Value=new[]{1d,1d,1d,1d}}},
    Keywords=new[]{"USE_FLOW","USE_WARP","USE_OTHER"},ExclusiveKeywordGroups=new[]{new[]{"USE_WARP","USE_OTHER"}}};
var flow=schema.Copy();flow.StrengthProperty="_FlowAmount";var warp=schema.Copy();warp.StrengthProperty="_WarpAmount";
var doc=new PackageDocument{Id="material-test",Requires=new[]{"effect.instances",BackgroundMaterialPlan.Capability},Nodes=new[]{
    new MaterialNodeDocument{Id="background",Bundle="base.bundle",Material="base",BackgroundBinding=new(){SourceId="input",Properties=new[]{P("_Brightness",2),new BackgroundPropertyDocument{Property="_Tint",Kind=ParameterKind.Color,Value=new[]{.5,.75,1d,1d}}}}},
    new MaterialNodeDocument{Id="flow",Target="postprocess",Bundle="source.bundle",Material="source",StartSeconds=0,EndSeconds=5,BackgroundBinding=flow},
    new MaterialNodeDocument{Id="warp",Target="postprocess",Bundle="source.bundle",Material="source",StartSeconds=0,EndSeconds=5,BackgroundBinding=warp}},Channels=new[]{
    new ChannelDocument{Property="_Brightness",Default=new[]{2d}},new ChannelDocument{Property="_Tint",Kind=ParameterKind.Color,Default=new[]{.5,.75,1d,1d}},
    new ChannelDocument{Node="flow",Property="_FlowAmount",Default=new[]{.5},Keys=new[]{new KeyDocument{Seconds=0,Value=new[]{0d}},new KeyDocument{Seconds=2,Value=new[]{1d}}}},
    new ChannelDocument{Node="flow",Property="_VfxOpacity",Default=new[]{.5}},new ChannelDocument{Node="warp",Property="_WarpAmount",Default=new[]{.6}},new ChannelDocument{Node="warp",Property="_WarpRotation",Default=new[]{-.2}},
    new ChannelDocument{Node="warp",Property="_VfxOpacity",Default=new[]{1d}}}};
var package=EffectPackage.FromDocument(doc,output,AddonPackage.Capabilities,false);var plan=new BackgroundMaterialPlan(doc);var values=new ParameterValue[doc.Channels.Length];
BackgroundMaterialPlan.State Sample(double t){package.Timeline.Sample(t,values);return plan.Sample(t,values)!;}
float Value(BackgroundMaterialPlan.State state,string name)=>state.Values[Array.FindIndex(state.Definition.Properties,p=>p.Property==name)].Number.X;
var state=Sample(1);
Check(plan.States.Count==2,"controls sharing a material use one source state plus the input state");
Check(Math.Abs(Value(state,"_FlowAmount")-.25)<.00001,"linear flow animation and opacity multiply at the requested time");
Check(Math.Abs(Value(state,"_WarpAmount")-.6)<.00001&&Math.Abs(Value(state,"_WarpRotation")+.2)<.00001,"arbitrary related properties and negative rotation pass through metadata");
Check(state.Values[^1].Number==new Vector4(1,1.5f,2,1),"independent input brightness/tint carry into a native source");
Check(state.Keywords[0]&&state.Keywords[1]&&!state.Keywords[2],"keyword activation derives from metadata and property names");
Check(Sample(5)==plan.Base,"control intervals restore the input material exactly at their exclusive end");
Sample(3);Check(Math.Abs(Value(Sample(1),"_FlowAmount")-.25)<.00001,"random seek has no accumulated state");
values[3]=new ParameterValue(Vector4.Zero);values[6]=new ParameterValue(Vector4.Zero);
Check(plan.Sample(1,values)==plan.Base,"zero opacity bypasses source materials");
Sample(1);for(int i=0;i<10000;i++)plan.Sample(1+(i%10)*.1,values);
long before=GC.GetAllocatedBytesForCurrentThread();for(int i=0;i<10000;i++)plan.Sample(1+(i%10)*.1,values);
long allocated=GC.GetAllocatedBytesForCurrentThread()-before;
Check(allocated==0,"material-plan sampling allocates zero bytes across 10000 calls; measured="+allocated);
var project=new AuthoringProjectDocument{ProjectId="generic-export-fixture",ContentId="source",Revision=1,Runtime=doc};
var copy=AuthoringProjects.Copy(project);copy.Runtime.Nodes[1].BackgroundBinding!.Properties[0].Value[0]=9;
Check(doc.Nodes[1].BackgroundBinding!.Properties[0].Value[0]==0,"authoring snapshots deep-copy derived background metadata");
Check(!BackgroundBindingDocument.Same(copy.Runtime.Nodes[1].BackgroundBinding,doc.Nodes[1].BackgroundBinding)&&BackgroundBindingDocument.Same(flow,flow.Copy()),"metadata equality distinguishes changes and preserves equivalent snapshots");
File.WriteAllText(Path.Combine(output,"base.bundle"),"synthetic resource; not a Unity asset");File.WriteAllText(Path.Combine(output,"source.bundle"),"second synthetic resource");
BackgroundExportContext Context()=>new(output,n=>new BackgroundExportAsset(Path.Combine(output,n.Bundle),n.Material));
var target=new AddonTarget{SongKey="pack:fixture",Difficulty=3,ChartFingerprint=new string('a',64)};
var compiled=BackgroundExport.Compile(project,target,"Gamma",Context());
Check(compiled.Nodes.Length==3&&compiled.Channels.Length==doc.Channels.Length,"generic export preserves logical controls and the original animation channels");
Check(compiled.Nodes[1].Bundle==compiled.Nodes[2].Bundle,"export deduplicates shared shader bundles");
var basic=new AuthoringProjectDocument{ProjectId="basic",Runtime=new PackageDocument{Id="basic",Nodes=new[]{new MaterialNodeDocument{Bundle="preview.bundle",Material="vfx/parameter-preview"}},Channels=new[]{new ChannelDocument{Property="_Brightness",Default=new[]{1d}}}}};
Check(!BackgroundExport.Compile(basic,target,"Gamma").Requires.Contains(BackgroundMaterialPlan.Capability),"basic exports retain the previous player's capability contract");
Reject(()=>EffectPackage.FromDocument(compiled,output,AddonPackage.Capabilities.Where(c=>c!=BackgroundMaterialPlan.Capability).ToArray(),false),"old players reject new material semantics explicitly");
string folder=Path.Combine(output,"Addon");BackgroundExport.Publish(project,target,"","unused","Gamma",folder,o=>JsonSerializer.Serialize(o,o.GetType(),json),Context());
var loaded=AddonPackage.Load(Path.Combine(folder,"addon.json"),s=>JsonSerializer.Deserialize<AddonManifest>(s,json)!,s=>JsonSerializer.Deserialize<PackageDocument>(s,json)!,AddonPackage.Capabilities);
Check(loaded.Document.Nodes.Length==3&&Directory.GetFiles(Path.Combine(folder,"materials")).Length==2,"published package contains exactly its two referenced resources and round-trips");
var invalid=AuthoringProjects.Copy(project);invalid.Runtime.Requires=Array.Empty<string>();Reject(()=>new BackgroundMaterialPlan(invalid.Runtime),"missing capability cannot silently reinterpret control nodes as rendering passes");
invalid=AuthoringProjects.Copy(project);invalid.Runtime.Nodes[2].BackgroundBinding!.Properties[0].Value[0]=1;Reject(()=>new BackgroundMaterialPlan(invalid.Runtime),"one source id cannot carry inconsistent neutral parameters");
invalid=AuthoringProjects.Copy(project);invalid.Runtime.Nodes[2].BackgroundBinding!.StrengthProperty="_Missing";Reject(()=>new BackgroundMaterialPlan(invalid.Runtime),"an unknown strength property is rejected");
invalid=AuthoringProjects.Copy(project);invalid.Runtime.Channels=invalid.Runtime.Channels.Append(new ChannelDocument{Node="warp",Property="_NotExposed"}).ToArray();Reject(()=>new BackgroundMaterialPlan(invalid.Runtime),"undeclared control channels are rejected");
string image=Path.Combine(output,"detail.png");
File.WriteAllBytes(image,Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAIAAAACCAYAAABytg0kAAAAG0lEQVR4nGPQcMz7nxcw8T9DQMWC/7Mn7vsPAEnSCXVyxwqKAAAAAElFTkSuQmCC"));
BackgroundExportContext ImageContext(){var c=Context();c.SetTextureInput("generic-source","_DetailMap",image);return c;}
var imageRuntime=BackgroundExport.Compile(project,target,"Gamma",ImageContext());
Check(imageRuntime.Requires.Contains(BackgroundMaterialPlan.TextureCapability)&&imageRuntime.Nodes.Skip(1).All(n=>n.BackgroundBinding!.TextureBindings.ContainsKey("_DetailMap")),"arbitrary auxiliary texture properties bind to every shared-source operator with a protected capability");
Check(new BackgroundMaterialPlan(imageRuntime).States.Count==2,"auxiliary images do not add material states or rendering stages");
var imageCopy=AuthoringProjects.Copy(new AuthoringProjectDocument{Runtime=imageRuntime});
imageCopy.Runtime.Nodes[1].BackgroundBinding!.TextureBindings["_DetailMap"]="different";
Check(imageRuntime.Nodes[1].BackgroundBinding!.TextureBindings["_DetailMap"]!="different"&&!BackgroundBindingDocument.Same(imageCopy.Runtime.Nodes[1].BackgroundBinding,imageRuntime.Nodes[1].BackgroundBinding),"texture bindings participate in deep-copy and equality semantics");
Reject(()=>EffectPackage.FromDocument(imageRuntime,output,AddonPackage.Capabilities.Where(c=>c!=BackgroundMaterialPlan.TextureCapability).ToArray(),false),"previous players explicitly reject auxiliary texture overrides instead of using their default images");
invalid=AuthoringProjects.Copy(new AuthoringProjectDocument{Runtime=imageRuntime});invalid.Runtime.Requires=invalid.Runtime.Requires.Where(c=>c!=BackgroundMaterialPlan.TextureCapability).ToArray();
Reject(()=>new BackgroundMaterialPlan(invalid.Runtime),"missing auxiliary-texture capability is rejected");
invalid=AuthoringProjects.Copy(new AuthoringProjectDocument{Runtime=imageRuntime});invalid.Runtime.Nodes[2].Textures["input_DetailMap"]="file:other.png";
Reject(()=>new BackgroundMaterialPlan(invalid.Runtime),"shared source operators cannot disagree about auxiliary image resources");
string imageOutput=Path.Combine(output,"ImageAddon");BackgroundExport.Publish(project,target,"","unused","Gamma",imageOutput,o=>JsonSerializer.Serialize(o,o.GetType(),json),ImageContext());
var imagePackage=AddonPackage.Load(Path.Combine(imageOutput,"addon.json"),s=>JsonSerializer.Deserialize<AddonManifest>(s,json)!,s=>JsonSerializer.Deserialize<PackageDocument>(s,json)!,AddonPackage.Capabilities);
Check(Directory.GetFiles(Path.Combine(imageOutput,"images")).Length==1&&AddonPackage.Hash(Directory.GetFiles(Path.Combine(imageOutput,"images"))[0])==AddonPackage.Hash(image),"auxiliary images are published once with exact bytes and manifest hashes");
Check(imagePackage.Document.Nodes.Skip(1).All(n=>n.Textures[n.BackgroundBinding!.TextureBindings["_DetailMap"]].StartsWith("file:images/",StringComparison.Ordinal)),"published texture bindings survive validated portable loading");
File.WriteAllText(Path.Combine(output,"bad.png"),"invalid");var badImage=Context();badImage.SetTextureInput("generic-source","_DetailMap",Path.Combine(output,"bad.png"));
Reject(()=>BackgroundExport.Publish(project,target,"","unused","Gamma",Path.Combine(output,"BadAddon"),o=>JsonSerializer.Serialize(o,o.GetType(),json),badImage),"invalid auxiliary images are rejected before publication");
Check(!Directory.Exists(Path.Combine(output,"BadAddon")),"auxiliary-image validation leaves no partial addon");
File.WriteAllText(Path.Combine(output,"checks.json"),JsonSerializer.Serialize(new{ok=true,checks,samplingAllocatedBytes=allocated,visualCalls=0,gameStarted=false},json));
Console.WriteLine(JsonSerializer.Serialize(new{ok=true,count=checks.Count,samplingAllocatedBytes=allocated},json));
