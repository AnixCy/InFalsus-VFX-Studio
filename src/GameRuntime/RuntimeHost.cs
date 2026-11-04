using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using InFalsus.Vfx.Addon;

namespace InFalsus.Presentation.Game;

public sealed class HostConfiguration
{
    public bool Enabled { get; set; } = true;
    public int ReloadSerial { get; set; }
    public int DiagnosticsSerial { get; set; }
}

/// <summary>Optional addon host. Selection never controls chart loading, inputs, saves or transport.</summary>
public sealed class RuntimeHost : IDisposable
{
    static readonly JsonSerializerOptions Json = new() {
        PropertyNameCaseInsensitive=true,MaxDepth=32,
        Converters={new JsonStringEnumConverter(JsonNamingPolicy.CamelCase,allowIntegerValues:false)} };
    readonly Action<string> report;
    readonly string dataRoot;
    readonly UnityRenderBackend backend;
    readonly PresentationSession session;
    Dictionary<string,EffectPackage> addons=new(StringComparer.OrdinalIgnoreCase);
    HostConfiguration config=new();
    DateTime configStamp;
    bool configRead,disposed,holdingBackground;
    long ambiguousGeneration=long.MinValue;
    double nextPoll;
    (long Generation,EffectPackage Package)? requested;
    (RenderResourceCounts Resources,string? Id,PlaybackPhase? Phase,bool Holding)? diagnostic;
    public IGameBindings Bridge { get; }
    public bool IsActive=>session.IsActive;
    public string? ActivePackageId=>session.ActivePackageId;
    public RenderDiagnostics Diagnostics=>backend.Diagnostics;

    public RuntimeHost(string gameRoot,string dataRoot,Action<string> report)
        :this(new NativeGameBridge(gameRoot,Path.Combine(dataRoot,"profiles.json"),Path.Combine(dataRoot,"catalog.json")),dataRoot,report) { }
    public RuntimeHost(IGameBindings bridge,string dataRoot,Action<string> report)
    {
        (Bridge,this.dataRoot,this.report)=(bridge,dataRoot,report);
        backend=new(bridge,report);session=new(backend,report);
        ReloadAddons();
        report("Optional addon runtime initialized; no input, chart, transport or save writes.");
    }
    public void Tick()
    {
        if(disposed)return;
        holdingBackground=false;
        try {
            PollConfig();backend.Collect();
            if(!config.Enabled||addons.Count==0){session.End();requested=null;return;}
            if(!Bridge.TrySample(out var snapshot)){
                if(Bridge is ISceneOpacityBindings scene)scene.RestoreSceneOpacity();
                // Freeze the last frame through the native scene transition, rather than
                // exposing the original image as soon as audio stops. Explicit disable wins above.
                holdingBackground=session.IsActive&&Bridge.CanRetainBackground;
                if(!holdingBackground){session.End();requested=null;}
                return;
            }
            EffectPackage? match=null;
            foreach(var pair in addons)if(pair.Value.Matches(snapshot)) {
                if(match!=null){if(ambiguousGeneration!=snapshot.SessionGeneration){report("Ambiguous addons for this chart; using original presentation.");ambiguousGeneration=snapshot.SessionGeneration;}match=null;break;}
                match=pair.Value;
            }
            if(match==null){session.End();requested=null;return;}
            if(requested is not {} previous||previous.Generation!=snapshot.SessionGeneration||!ReferenceEquals(previous.Package,match)) {
                requested=(snapshot.SessionGeneration,match);session.Request(match,snapshot);
            }
            session.Tick(snapshot);
        } catch(Exception e) {
            report("Addon runtime stopped safely: "+e.Message);Dispose();
        } finally {if(!disposed)PublishDiagnostics();}
    }
    public void BeforeUpdate()
    {
        if(disposed)return;
        try{if(Bridge is ISceneOpacityBindings scene)scene.BeginOpacityFrame();}
        catch(Exception error){report("Scene opacity restore failed: "+error.Message);Dispose();}
    }
    public void BeforeUiRender()=>UiScope(true);
    public void AfterUiRender()=>UiScope(false);
    void UiScope(bool before)
    {
        if(disposed)return;
        try{if(Bridge is ISceneOpacityBindings scene){if(before)scene.BeforeUiRender();else scene.AfterUiRender();}}
        catch(Exception error){report("Scene UI scope failed: "+error.Message);Dispose();}
    }
    void PollConfig()
    {
        double now=Stopwatch.GetTimestamp()/(double)Stopwatch.Frequency;
        if(now<nextPoll)return;nextPoll=now+.5;
        var path=Path.Combine(dataRoot,"config.json");var stamp=File.GetLastWriteTimeUtc(path);
        if(configRead&&stamp==configStamp)return;configStamp=stamp;configRead=true;
        if(!File.Exists(path))return;
        try {
            var next=JsonSerializer.Deserialize<HostConfiguration>(File.ReadAllText(path),Json)??throw new InvalidDataException("Empty config.");
            bool reload=next.ReloadSerial!=config.ReloadSerial;config=next;
            if(reload)ReloadAddons();
        } catch(Exception e){report("Addon config retained: "+e.Message);}
    }
    void ReloadAddons()
    {
        var root=Path.Combine(dataRoot,"Addons");var next=new Dictionary<string,EffectPackage>(StringComparer.OrdinalIgnoreCase);
        if(Directory.Exists(root))foreach(var directory in Directory.EnumerateDirectories(root).OrderBy(p=>p,StringComparer.OrdinalIgnoreCase)) {
            if((File.GetAttributes(directory)&FileAttributes.ReparsePoint)!=0)continue;
            var file=Path.Combine(directory,"addon.json");if(!File.Exists(file))continue;
            try {
                var candidate=AddonPackage.Load(file,t=>JsonSerializer.Deserialize<AddonManifest>(t,Json)!,
                    t=>JsonSerializer.Deserialize<PackageDocument>(t,Json)!,backend.Capabilities);
                if(string.IsNullOrWhiteSpace(candidate.Document.SongKey)||candidate.Document.Difficulty==null)
                    throw new InvalidDataException("Addon requires an exact chart identity.");
                next.Add(file,candidate);
            } catch(Exception e) {
                report("Addon skipped: "+Path.GetFileName(directory)+": "+e.Message);
                // A failed edit keeps the previous valid revision. A removed directory does not.
                if(addons.TryGetValue(file,out var previous))next.Add(file,previous);
            }
        }
        addons=next;ambiguousGeneration=long.MinValue;report("Addon catalog ready: "+addons.Count);
    }
    void PublishDiagnostics()
    {
        var state=(backend.Diagnostics.AllocationState,session.ActivePackageId,session.LastFrame?.Playback.Phase,holdingBackground);
        if(diagnostic==state)return;diagnostic=state;
        report("Addon diagnostics: "+JsonSerializer.Serialize(new { Package=session.ActivePackageId,
            Phase=holdingBackground?"Transition":session.LastFrame?.Playback.Phase.ToString()??"Idle",Seconds=session.LastFrame?.Seconds,Resources=backend.Diagnostics }));
    }
    public void Dispose()
    {
        if(disposed)return;disposed=true;
        try{session.Dispose();}finally{backend.Dispose();if(Bridge is ISceneOpacityBindings scene)scene.RestoreSceneOpacity();}
        report("Addon runtime disposed: "+JsonSerializer.Serialize(backend.Diagnostics));
    }
}
