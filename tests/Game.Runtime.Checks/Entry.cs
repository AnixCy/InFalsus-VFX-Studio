extern alias UnityCore;
extern alias Il2CppBase;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using InFalsus.Presentation;
using InFalsus.Presentation.Game;
using InFalsus.Vfx.Addon;
#if BEPINEX
using MelonMod=InFalsus.Vfx.GameChecks.BepCheckMod;
#else
using MelonLoader;
#endif
using UnityCore::UnityEngine;
using Object=UnityCore::UnityEngine.Object;

#if !BEPINEX
[assembly:MelonInfo(typeof(InFalsus.Vfx.GameChecks.Entry),"VFX isolated lifecycle checks","0.7.0","Local")]
#endif
namespace InFalsus.Vfx.GameChecks;

/// <summary>Development-only assembly, copied exclusively to a marked sandbox. No keys, audio or gameplay actions.</summary>
public sealed partial class Entry:MelonMod
{
    static readonly JsonSerializerOptions Json=new(){WriteIndented=true,PropertyNameCaseInsensitive=true,
        Converters={new JsonStringEnumConverter(JsonNamingPolicy.CamelCase,false)}};
    readonly List<string> checks=new(),messages=new();
    readonly Stopwatch elapsed=Stopwatch.StartNew();
    NativeGameBridge? native;
    ProbeBindings? bindings;
    RuntimeHost? host;
    string root="",data="",output="",fixture="",stored="";
    int stage,frames;
    double since;
    bool quitting;
    bool playerLoaded,uiFrameChecked;
    string? failure;
    Material? original,foreign;
    float expectedOne=.9f,expectedTwo=1.4f,expectedThree=.9f;
    float expectedSceneOne=.5f;
    string expectedProperty="_Brightness",expectedTexture="_MainTex",expectedMaterialShader="InFalsusVFX/AddonBackground";
    bool materialPlanFixture;
    IntPtr fixtureVariant;
    public override void OnLateInitializeMelon()
    {
        try {
            root=Path.GetDirectoryName(Process.GetCurrentProcess().MainModule!.FileName)!;
            if(!File.Exists(Path.Combine(root,".vfx-addon-sandbox.json"))||!Environment.GetCommandLineArgs().Contains("-vfx-addon-check"))
                throw new InvalidOperationException("Checks require an explicitly marked isolated copy and command flag");
            data=Path.Combine(root,"UserData/InFalsusPresentation");output=Path.Combine(root,"addon-checks.json");
            var player=MelonMod.RegisteredMelons.FirstOrDefault(m=>m.GetType().FullName is "InFalsus.Presentation.Melon.Entry" or "InFalsus.Presentation.BepInEx.Entry");
            playerLoaded=player!=null;
            var playerHost=(player?.GetType().GetField("_host",BindingFlags.Instance|BindingFlags.NonPublic)?.GetValue(player)
                ??player?.GetType().GetProperty("Host")?.GetValue(player)) as RuntimeHost;
            native=playerHost?.Bridge as NativeGameBridge??new(root,Path.Combine(data,"profiles.json"),Path.Combine(data,"catalog.json"));
            if(player!=null)Check(playerHost!=null&&native.HasExactSeekSerial,"actual player loader initialized and attached its seek observer");
            UnityEngine.AudioListener.volume=0;
            LoggerInstance.Msg("Target Unity="+Application.unityVersion+" colorSpace="+QualitySettings.activeColorSpace);
            File.WriteAllText(Path.Combine(root,"target-runtime.json"),JsonSerializer.Serialize(new{unityVersion=Application.unityVersion,
                colorSpace=QualitySettings.activeColorSpace.ToString(),graphics=SystemInfo.graphicsDeviceType.ToString()},Json));
            fixture=Path.Combine(data,"Addons/Fixture");stored=Path.Combine(data,"StoredFixture");
            var args=Environment.GetCommandLineArgs();int packAt=Array.IndexOf(args,"-vfx-chart-package");
            int expectationAt=Array.IndexOf(args,"-vfx-material-expectations");
            if(expectationAt>=0){using var expected=JsonDocument.Parse(File.ReadAllText(args[expectationAt+1]));expectedProperty=expected.RootElement.GetProperty("Property").GetString()!;expectedTexture=expected.RootElement.GetProperty("Texture").GetString()!;expectedMaterialShader=expected.RootElement.GetProperty("Shader").GetString()!;materialPlanFixture=!expected.RootElement.TryGetProperty("Switching",out var switching)||switching.GetBoolean();}
            if(packAt>=0)fixture=Path.Combine(root,"Charts",args[packAt+1],"vfx/FBD");
            var expectedPackage=EffectPackage.FromDocument(JsonSerializer.Deserialize<PackageDocument>(File.ReadAllText(Path.Combine(fixture,"runtime.json")),Json)!,fixture,AddonPackage.Capabilities);
            float Expected(double seconds){var v=new ParameterValue[expectedPackage.Timeline.Count];expectedPackage.Timeline.Sample(seconds+expectedPackage.Document.OffsetSeconds,v);
                int i=Array.FindIndex(expectedPackage.Document.Channels,c=>c.Property==expectedProperty);return v[i].Number.X;}
            expectedOne=Expected(1);expectedTwo=Expected(2);expectedThree=Expected(3);
            double sceneSeconds=1+expectedPackage.Document.OffsetSeconds;
            expectedSceneOne=sceneSeconds<0?1:Math.Clamp(1-(float)sceneSeconds*.5f,0,1);
            Check(Directory.Exists(fixture),"exported addon installed in isolated copy");Next();
        }catch(Exception e){Fail(e);}
    }
    void Log(string message){messages.Add(message);LoggerInstance.Msg(message);}
    void Check(bool ok,string name){if(!ok)throw new InvalidOperationException(name);checks.Add(name);LoggerInstance.Msg("PASS "+name);}
    void Go(int next){stage=next;since=elapsed.Elapsed.TotalSeconds;frames=0;}
    void Next()=>Go(stage+1);
    bool Wait(double seconds)=>elapsed.Elapsed.TotalSeconds-since>=seconds;
    void Config(bool enabled,int serial=0)=>File.WriteAllText(Path.Combine(data,"config.json"),JsonSerializer.Serialize(new{enabled,reloadSerial=serial},Json));
    void Clean(string name){var d=host!.Diagnostics;Check(d.Effects==0&&d.Preparations==0&&d.Materials==0&&d.Bundles==0&&d.BundleLeases==0&&d.PendingLoads==0&&d.RetiredGroups==0&&d.RenderRegistrations==0&&d.Images==0&&d.CleanupFailures==0,name);}
    public override void OnLateUpdate()
    {
        if(quitting)return;
        try {
            // The game reapplies its own listener setting after initialization.
            // Keep this test-only override after native Update, never in the shipped runtime.
            UnityEngine.AudioListener.volume=0;
            frames++;
            frameOpacity?.Apply(new(1,.5f,1,1,1));
            if(elapsed.Elapsed.TotalSeconds>100)throw new TimeoutException("Isolated lifecycle checks exceeded 100 seconds at stage "+stage);
            if(stage==1) {
                if(!Wait(6))return;
                Check(!native!.TrySample(out _),"real game transport remains outside gameplay; no song/audio started");
                bindings=new(native,NativeType(native.BaseAddress,"Constrained2D"));
                var packArgs=Environment.GetCommandLineArgs();int packIndex=Array.IndexOf(packArgs,"-vfx-chart-package");
                if(packIndex>=0) {
                    var song=ChartPack.ReadSong(Path.Combine(root,"Charts",packArgs[packIndex+1]),PackFieldsForChecks);
                    var binding=ChartPack.ReadBindings(song,t=>JsonSerializer.Deserialize<ChartPackManifest>(t,Json)!)[0];
                    bindings.SongKey=binding.SongKey;bindings.Difficulty=binding.Difficulty;bindings.Fingerprint=binding.Fingerprint;
                    var importer=AppDomain.CurrentDomain.GetAssemblies().Single(a=>a.GetName().Name=="InFalsusChartLoader");
                    var hash=importer.GetType("InFalsusChartLoader.ChartSession")!.GetMethod("HashOf")!.Invoke(null,new object[]{binding.LogicalName}) as string;
                    Check(hash==binding.Fingerprint,"packaged SPC was decoded and imported by the real chart loader without a script");
                }
                var shader=Shader.Find("Hidden/InternalErrorShader");if(shader==null)throw new InvalidOperationException("Baseline shader missing");
                original=new Material(shader);foreign=new Material(shader);
                bindings.SetMaterial(bindings.Background,original.Pointer,1);Next();return;
            }
            host?.Tick();
            switch(stage) {
                case 2:
                    if(bindings!.CurrentMaterial(bindings.Background)!=original!.Pointer){if(Wait(8))throw new Exception($"C2D baseline mismatch: requested={bindings.RequestedMaterial(bindings.Background):x}, current={bindings.CurrentMaterial(bindings.Background):x}, expected={original.Pointer:x}");return;}
                    Check(bindings.RequestedMaterial(bindings.Background)==IntPtr.Zero,"native C2D consumes and clears pending material request");
                    CheckSceneTargets(NativeType(native!.BaseAddress,"Constrained2D"));
                    // Only one NativeHook owns a given machine-code entry. Run the standalone
                    // frame probe separately from the real loader, never stack two detours.
                    if(!playerLoaded)StartOpacityFrameProbe(NativeType(native.BaseAddress,"Constrained2D"));
                    AddSceneFixture();Config(true);host=new(bindings,data,Log);Next();break;
                case 3:
                    if(frames<5)return;
                    if(!host!.IsActive||bindings!.CurrentMaterial(bindings.Background)==original!.Pointer){if(Wait(10))throw new Exception("Addon did not activate");return;}
                    if(frameHud is not null){
                        float renderedAlpha=CalculatedOpacity(frameHud);
                        Check(Math.Abs(renderedAlpha-.4f)<.0001,"native UI calculation consumes frame opacity before rendering: "+renderedAlpha);
                        uiFrameChecked=true;StopOpacityFrameProbe();
                    }
                    var active=new Material(bindings.CurrentMaterial(bindings.Background));
                    var arguments=Environment.GetCommandLineArgs();int expectedAt=Array.IndexOf(arguments,"-vfx-expected-shader");
                    string expectedShader=expectedAt>=0?arguments[expectedAt+1]:expectedMaterialShader;
                    Check(active.shader.name==expectedShader,"actual IL2CPP component uses the exported shader");
                    Check(Math.Abs(active.GetFloat(expectedProperty)-expectedOne)<.001,"actual material receives the exported value at the current clock");
                    Check(active.GetTexture(expectedTexture)!=null&&host.Diagnostics.Images==1,"external addon image loaded and owned");
                    Check(host.Diagnostics.RenderRegistrations==0&&host.Diagnostics.PassesRecorded==0,"background addon registers no postprocess passes");
                    Check(bindings.Opacity==new SceneOpacityValues(expectedSceneOne,expectedSceneOne,expectedSceneOne,expectedSceneOne,expectedSceneOne),"game backend evaluates logical scene keys without a Shader pass");
                    if(materialPlanFixture){fixtureVariant=active.Pointer;Check(Math.Abs(active.GetFloat("_CurrentTime")-1)<.001,"native material clock matches the shared transport");bindings.Seconds=5.5;Go(31);break;}
                    bindings.Seconds=3;Next();break;
                case 31:
                    if(frames<3)return;
                    Check(new Material(bindings!.CurrentMaterial(bindings.Background)).shader.name=="InFalsusVFX/AddonBackground","ending all controls restores the input background without another rendering pass");
                    bindings.Seconds=6.5;Go(32);break;
                case 32:
                    if(frames<3)return;
                    Check(bindings!.CurrentMaterial(bindings.Background)!=fixtureVariant&&new Material(bindings.CurrentMaterial(bindings.Background)).shader.isSupported,"a later control selects a different compiled shader family");
                    Check(host!.Diagnostics.Materials==3&&host.Diagnostics.RenderRegistrations==0&&host.Diagnostics.PassesRecorded==0,"material variants are deduplicated and allocate no fullscreen render stage");
                    bindings.Seconds=1;Go(33);break;
                case 33:
                    if(frames<3)return;
                    Check(bindings!.CurrentMaterial(bindings.Background)==fixtureVariant,"backward seek returns to the same owned variant material");
                    bindings.Seconds=3;Go(4);break;
                case 4:
                    Check(Math.Abs(new Material(bindings!.CurrentMaterial(bindings.Background)).GetFloat(expectedProperty)-expectedThree)<.001,"random seek reevaluates current value without accumulated history");
                    bindings.Paused=true;bindings.Seconds=2;Next();break;
                case 5:
                    Check(Math.Abs(new Material(bindings!.CurrentMaterial(bindings.Background)).GetFloat(expectedProperty)-expectedTwo)<.001,"paused transport samples requested position");
                    Config(false);Next();break;
                case 6:
                    if(host!.IsActive||!Wait(.8))return;
                    Check(bindings!.CurrentMaterial(bindings.Background)==original!.Pointer,"disable restores effective original material after request was cleared");
                    Check(bindings.Opacity==SceneOpacityValues.Visible,"disable restores scene opacity through the optional binding");
                    Clean("disable releases all addon-owned resources");Config(true);Next();break;
                case 7:
                    if(!host!.IsActive||!Wait(.8))return;
                    File.AppendAllText(Path.Combine(fixture,"runtime.json")," ");Config(true,1);Next();break;
                case 8:
                    if(!Wait(.8))return;
                    Check(host!.IsActive&&messages.Any(m=>m.Contains("integrity mismatch")),"failed live update retains the last good presentation");
                    bindings!.Valid=false;bindings.TransitionVisible=true;Go(80);break;
                case 80:
                    if(frames<5)return;
                    Check(host!.IsActive&&bindings!.CurrentMaterial(bindings.Background)!=original!.Pointer,"transport teardown retains the addon while the native background is visible");
                    Check(Math.Abs(new Material(bindings!.CurrentMaterial(bindings.Background)).GetFloat(expectedProperty)-expectedTwo)<.001,"transition holds the last sampled frame without advancing its clock");
                    Config(false,1);Go(81);break;
                case 81:
                    if(!Wait(.8))return;
                    Check(!host!.IsActive&&bindings!.CurrentMaterial(bindings.Background)==original!.Pointer,"explicit disable restores the original even during a visible exit transition");
                    Clean("transition disable releases all owned resources");
                    bindings!.Valid=true;bindings.TransitionVisible=false;Config(true,1);Go(82);break;
                case 82:
                    if(!host!.IsActive||!Wait(.8))return;
                    bindings!.Valid=false;bindings.TransitionVisible=true;Go(83);break;
                case 83:
                    if(frames<5)return;
                    Check(host!.IsActive,"a later visible transition can retain a reenabled addon");
                    bindings!.TransitionVisible=false;Go(9);break;
                case 9:
                    if(frames<5)return;
                    Check(!host!.IsActive&&bindings!.CurrentMaterial(bindings.Background)==original!.Pointer,"leaving chart restores original background");
                    Clean("leaving chart releases resources and callbacks");host.Dispose();host=null;
                    Directory.Move(fixture,stored);bindings!.Valid=true;Config(true,2);
                    host=new(bindings,data,Log);Next();break;
                case 10:
                    if(frames<5)return;
                    Check(!host!.IsActive,"no addon leaves ordinary chart presentation active");Clean("no addon creates no GPU resources");
                    host.Dispose();host=null;Directory.Move(stored,fixture);Config(true,3);host=new(bindings!,data,Log);Next();break;
                case 11:
                    if(frames<8)return;
                    Check(!host!.IsActive,"cold load of corrupt addon falls back to original presentation");Clean("corrupt addon allocates no GPU resources");
                    host.Dispose();host=null;
                    RepairRuntime("missing-material");Config(true,4);host=new(bindings!,data,Log);Next();break;
                case 12:
                    if(!Wait(1.5))return;
                    Check(!host!.IsActive&&bindings!.CurrentMaterial(bindings.Background)==original!.Pointer,"missing compiled material cannot replace original background");
                    Clean("failed preparation releases its bundles and images");host.Dispose();host=null;
                    RepairRuntime(AddonPackage.MaterialPath);Config(true,5);host=new(bindings!,data,Log);Next();break;
                case 13:
                    if(!host!.IsActive||!Wait(.3))return;
                    bindings!.SetMaterial(bindings.Background,foreign!.Pointer,1);Next();break;
                case 14:
                    if(bindings!.CurrentMaterial(bindings.Background)!=foreign!.Pointer)return;
                    Config(false,5);Next();break;
                case 15:
                    if(host!.IsActive||!Wait(.8))return;
                    Check(bindings!.CurrentMaterial(bindings.Background)==foreign!.Pointer,"disable does not overwrite a newer external material owner");
                    Clean("ownership handoff still releases addon resources");
                    bindings.SetMaterial(bindings.Background,original!.Pointer,1);Config(true,5);Next();break;
                case 16:
                    if(!host!.IsActive||!Wait(.8))return;
                    var duplicate=Path.Combine(data,"Addons/Duplicate");Directory.CreateDirectory(duplicate);
                    foreach(var file in Directory.GetFiles(fixture,"*",SearchOption.AllDirectories)) {
                        var target=Path.Combine(duplicate,Path.GetRelativePath(fixture,file));Directory.CreateDirectory(Path.GetDirectoryName(target)!);File.Copy(file,target);
                    }
                    Config(true,6);Next();break;
                case 17:
                    if(!Wait(.8))return;
                    Check(!host!.IsActive&&bindings!.CurrentMaterial(bindings.Background)==original!.Pointer,"ambiguous addons fall back to ordinary presentation");
                    Clean("duplicate addons allocate no GPU resources");
                    Check(messages.Count(m=>m.Contains("Ambiguous addons"))==1,"duplicate-addon warning is emitted once rather than every frame");
                    Directory.Move(Path.Combine(data,"Addons/Duplicate"),Path.Combine(data,"StoredDuplicate"));Config(true,7);Next();break;
                case 18:
                    if(!host!.IsActive||!Wait(.8))return;
                    Check(Environment.GetCommandLineArgs().Contains("-noaudio"),"test launch disables Unity audio; the harness requests no playback");
                    Check(!native!.TrySample(out _),"real gameplay and save flow were never entered");
                    quitting=true;LoggerInstance.Msg("Requesting real application quit with active addon");Application.Quit(0);break;
            }
        }catch(Exception e){Fail(e);}
    }
    void RepairRuntime(string material)
    {
        var path=Path.Combine(fixture,"runtime.json");var document=JsonSerializer.Deserialize<PackageDocument>(File.ReadAllText(path),Json)!;
        document.Nodes[0].Material=material;File.WriteAllText(path,JsonSerializer.Serialize(document,Json));
        var manifestPath=Path.Combine(fixture,"addon.json");var manifest=JsonSerializer.Deserialize<AddonManifest>(File.ReadAllText(manifestPath),Json)!;
        manifest.Files["runtime.json"]=AddonPackage.Hash(path);File.WriteAllText(manifestPath,JsonSerializer.Serialize(manifest,Json));
    }
    void Fail(Exception error)
    {
        failure=error.ToString();LoggerInstance.Error(error);quitting=true;
        if(output.Length>0)WriteReport(false);Application.Quit(2);
    }
    public override void OnDeinitializeMelon()
    {
        try {
            host?.Dispose();
            StopOpacityFrameProbe();
            if(host!=null)Clean("actual application shutdown releases active addon resources");
            checks.Add("MelonLoader invoked shutdown during actual application exit");
            WriteReport(failure==null);
        }catch(Exception e){failure=e.ToString();WriteReport(false);}
    }
    void WriteReport(bool ok)
    {
        if(output.Length==0)return;
        File.WriteAllText(output,JsonSerializer.Serialize(new{ok,checks,error=failure,messages,
            realIl2Cpp=true,realConstrained2D=true,syntheticTransport=true,gameplayStarted=false,audioPlaybackRequested=false,fmodMuteVerified=false,
            playerLoaded,nativeUiFrameChecked=uiFrameChecked,
            visualInspection=false,resources=host?.Diagnostics},Json));
    }
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]delegate IntPtr Unary(IntPtr arg);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]delegate IntPtr NoArg();
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]delegate IntPtr Assemblies(IntPtr domain,out nuint count);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]delegate IntPtr Class(IntPtr image,[MarshalAs(UnmanagedType.LPUTF8Str)]string ns,[MarshalAs(UnmanagedType.LPUTF8Str)]string name);
    static Il2CppBase::Il2CppSystem.Type NativeType(IntPtr module,string name)
    {
        T Fn<T>(string symbol)where T:Delegate=>Marshal.GetDelegateForFunctionPointer<T>(System.Runtime.InteropServices.NativeLibrary.GetExport(module,symbol));
        var domain=Fn<NoArg>("il2cpp_domain_get")();var assemblies=Fn<Assemblies>("il2cpp_domain_get_assemblies")(domain,out var count);
        for(int i=0;i<(int)count;i++) {
            var image=Fn<Unary>("il2cpp_assembly_get_image")(Marshal.ReadIntPtr(assemblies+i*IntPtr.Size));
            var type=Fn<Class>("il2cpp_class_from_name")(image,"ifapp.Game.UI.Common",name);
            if(type!=IntPtr.Zero)return new(Fn<Unary>("il2cpp_type_get_object")(Fn<Unary>("il2cpp_class_get_type")(type)));
        }
        throw new TypeLoadException(name);
    }
    static string?[] PackFieldsForChecks(string text,string key){using var doc=JsonDocument.Parse(text);if(!doc.RootElement.TryGetProperty(key,out var v))return Array.Empty<string?>();string? Read(JsonElement e)=>e.ValueKind==JsonValueKind.Null?null:e.GetString();return v.ValueKind==JsonValueKind.Array?v.EnumerateArray().Select(Read).ToArray():new[]{Read(v)};}
    sealed class ProbeBindings:IGameBindings,ISceneOpacityBindings,IChartPackBindings
    {
        readonly NativeGameBridge native;readonly GameObject root;
        public bool Valid=true,Paused,TransitionVisible;public double Seconds=1;
        public string SongKey="game:2",Fingerprint="";public int Difficulty=3;
        public IReadOnlyList<string> RefreshChartPacks(Action<string> report)=>native.RefreshChartPacks(report);
        public IntPtr Background{get;}
        public Camera? GameCamera=>null;
        public bool HasSpecialScene=>false;
        public string SceneControllerName=>"isolated C2D lifecycle probe";
        public bool CanRetainBackground=>TransitionVisible;
        public SceneOpacityValues Opacity=SceneOpacityValues.Visible;
        public void BeginOpacityFrame(){}
        public void ApplySceneOpacity(in SceneOpacityValues values)=>Opacity=values;
        public void RestoreSceneOpacity()=>Opacity=SceneOpacityValues.Visible;
        public void BeforeUiRender(){}
        public void AfterUiRender(){}
        public ProbeBindings(NativeGameBridge native,Il2CppBase::Il2CppSystem.Type type)
        {
            this.native=native;root=GameObject.CreatePrimitive(PrimitiveType.Quad);root.name="VFX isolated native C2D";
            var component=root.AddComponent(type);Background=component.Pointer;
            var write=Marshal.GetDelegateForFunctionPointer<WriteBarrier>(System.Runtime.InteropServices.NativeLibrary.GetExport(native.BaseAddress,"il2cpp_gc_wbarrier_set_field"));
            // These serialized references are normally populated by the scene loader.
            // A newly added native component must receive them before CoreScene's next update.
            write(Background,Background+0x158,root.transform.Pointer);
            write(Background,Background+0x160,root.GetComponent<MeshRenderer>().Pointer);
            write(Background,Background+0x168,root.GetComponent<MeshFilter>().Pointer);
            Marshal.GetDelegateForFunctionPointer<TypeCall>(native.BaseAddress+0x5021a0)(Background,1,IntPtr.Zero); // Sprite, not the constructor's Container.
            Marshal.GetDelegateForFunctionPointer<ResetCall>(native.BaseAddress+0x502110)(Background,IntPtr.Zero);
        }
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]delegate void WriteBarrier(IntPtr obj,IntPtr field,IntPtr value);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]delegate void ResetCall(IntPtr self,IntPtr method);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]delegate void TypeCall(IntPtr self,byte type,IntPtr method);
        public bool TrySample(out PlaybackSnapshot snapshot){snapshot=new(1,SongKey,Difficulty,Fingerprint,Seconds,Seconds,0,1,
            Paused?TransportState.Frozen:TransportState.Running,Paused?PlaybackPhase.Paused:PlaybackPhase.Playing,
            Paused,false,false,(ulong)(Seconds*100),true,Stopwatch.GetTimestamp()/(double)Stopwatch.Frequency);return Valid;}
        public bool TargetAlive(IntPtr target)=>native.TargetAlive(target);
        public IntPtr CurrentMaterial(IntPtr target)=>native.CurrentMaterial(target);
        public IntPtr RequestedMaterial(IntPtr target)=>native.RequestedMaterial(target);
        public byte RequestedMaterialOption(IntPtr target)=>native.RequestedMaterialOption(target);
        public void SetMaterial(IntPtr target,IntPtr material,byte option=1)=>native.SetMaterial(target,material,option);
    }
}
