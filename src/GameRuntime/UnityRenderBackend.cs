extern alias UnityCore;
extern alias Il2CppBase;
using Il2CppInterop.Runtime;
using InFalsus.Vfx.Addon;
using UnityEngine;
using UnityCore::UnityEngine;
using UnityCore::UnityEngine.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;
using GraphUtils = UnityEngine.Rendering.RenderGraphModule.Util.RenderGraphUtils;
using Hub = Il2Cpp.CommandBufferHubFeature.Hub;
using Object = UnityCore::UnityEngine.Object;

namespace InFalsus.Presentation.Game;

/// <summary>Owns only its bundles, material instances and Hub handles; never clears global effects.</summary>
public sealed class UnityRenderBackend : IRenderBackend, IDisposable
{
    public IReadOnlyCollection<string> Capabilities { get; }
    private readonly IGameBindings _bridge;
    private readonly Action<string> _report;
    private readonly Dictionary<string, BundleEntry> _bundles = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<(long Due, MaterialResource[] Resources, BundleEntry[] Bundles)> _retired = new();
    private long _frame;
    private int _effects, _preparations, _materials, _registrations, _cleanupFailures, _images;
    private long _postCallbacks, _passesRecorded, _backbufferSkips, _renderErrors;
    public RenderDiagnostics Diagnostics
    {
        get
        {
            var leases = 0; var pending = 0;
            foreach (var pair in _bundles) { leases += pair.Value.References; pending += pair.Value.PendingLoads; }
            return new(_effects, _preparations, _materials, _bundles.Count, leases, pending, _retired.Count,
                _registrations, _postCallbacks, _passesRecorded, _backbufferSkips, _renderErrors, _cleanupFailures, _images);
        }
    }
    private Material Own(Material material) { _materials++; return material; }
    private void DestroyOwned(Material material) { Object.Destroy(material); _materials--; }
    public UnityRenderBackend(IGameBindings bridge, Action<string> report)
    {
        (_bridge,_report)=(bridge,report);
        var caps=new HashSet<string>{"clock.audio","clock.chart","background.material","shader.bundle",AddonPackage.ImageCapability};
        if(bridge is ISceneOpacityBindings){caps.Add("effect.instances");caps.Add(SceneOpacityPlan.Capability);}
        Capabilities=caps;
    }

    public IEffectPreparation Prepare(EffectPackage package, in PlaybackSnapshot snapshot)
    {
        if (!package.Matches(snapshot)) throw new InvalidOperationException("Package/chart mismatch.");
        package.ValidateMaterialGraph(Application.unityVersion, "StandaloneWindows64", QualitySettings.activeColorSpace.ToString());
        _=new SceneOpacityPlan(package.Document);
        if (package.Document.Nodes.Any(n => !SceneOpacityPlan.IsControl(n)&&(n.Target != "background" || !n.Enabled || n.StartSeconds != 0 || n.EndSeconds.HasValue)))
            throw new NotSupportedException("This player milestone supports one always-active background, with color grading fused into it.");
        if (_bridge.HasSpecialScene && !package.Document.AllowSpecialScene)
            throw new NotSupportedException("Official special scene requires explicit allowSpecialScene.");
        if (package.Document.Nodes.Any(n => n.Target == "background") && _bridge.Background == IntPtr.Zero)
            throw new InvalidOperationException("Native background target is unavailable.");
        return new Preparation(this, package, _bridge.Background, _bridge.GameCamera);
    }

    private BundleEntry Acquire(string path)
    {
        if (!_bundles.TryGetValue(path, out var entry))
        {
            entry = new(path, AssetBundle.LoadFromFileAsync(path)); _bundles.Add(path, entry);
            _report($"Presentation bundle loading: {System.IO.Path.GetFileName(path)}");
        }
        entry.References++; return entry;
    }
    private void Release(BundleEntry entry)
    {
        if (entry.References <= 0) throw new InvalidOperationException("Bundle lease released twice.");
        entry.References--;
    }
    private void Retire(MaterialResource[] resources, BundleEntry[] bundles)
        => _retired.Add((_frame + 2, resources, bundles));
    public void Collect()
    {
        _frame++;
        // C2D applies its requested material later. Don't free last frame's shader before that handoff.
        for (var i = _retired.Count - 1; i >= 0; i--)
            if (_retired[i].Due <= _frame)
            {
                var item = _retired[i];
                foreach (var r in item.Resources) { DestroyOwned(r.Material); foreach(var image in r.OwnedImages) { Object.Destroy(image); _images--; } }
                foreach (var b in item.Bundles) Release(b);
                _retired.RemoveAt(i);
            }
        // Pending loads cannot be canceled inside Unity; retain until complete, then unload.
        List<string>? dead = null;
        foreach (var pair in _bundles)
            if (pair.Value.References == 0 && pair.Value.LoadsComplete)
            {
                var bundle = pair.Value.Request.assetBundle;
                if (bundle != null) bundle.Unload(true);
                (dead ??= new()).Add(pair.Key);
            }
        if (dead != null) foreach (var path in dead) _bundles.Remove(path);
    }
    public void Dispose()
    {
        foreach (var item in _retired) foreach (var r in item.Resources) { DestroyOwned(r.Material); foreach(var image in r.OwnedImages) { Object.Destroy(image); _images--; } }
        _retired.Clear();
        foreach (var entry in _bundles.Values)
        {
            // During host unload preserve assets used by a deferred final C2D restore until Unity's cleanup.
            entry.UnloadAfterCompletion(_report);
        }
        _bundles.Clear();
    }
    private sealed class BundleEntry
    {
        public readonly string Path;
        public readonly AssetBundleCreateRequest Request;
        private readonly Dictionary<(string Path, IntPtr Type), AssetBundleRequest> _assets = new();
        public bool LoadsComplete => Request.isDone && _assets.Values.All(r => r.isDone);
        public int PendingLoads
        {
            get { var n = Request.isDone ? 0 : 1; foreach (var pair in _assets) if (!pair.Value.isDone) n++; return n; }
        }
        public int References;
        public BundleEntry(string path, AssetBundleCreateRequest request) => (Path, Request) = (path, request);
        public AssetBundleRequest Load(string path, Il2CppBase::Il2CppSystem.Type type)
        {
            var key = (path, type.Pointer);
            if (!_assets.TryGetValue(key, out var request))
            {
                var bundle = Request.assetBundle;
                if (bundle == null) throw new InvalidDataException($"Bundle failed to load: {Path}");
                // Unity 6's restored synchronous LoadAsset_Internal wrapper calls a broken
                // IL2CPP ReadOnlySpan marshaler. This overload invokes the game's compiled
                // async method instead; don't replace with LoadAsset/LoadAsset<T>.
                request = bundle.LoadAssetAsync(path, type);
                _assets.Add(key, request);
            }
            return request;
        }
        public void UnloadAfterCompletion(Action<string> report)
        {
            var unloaded = false;
            void Finish(AsyncOperation? _)
            {
                if (unloaded || !LoadsComplete) return;
                unloaded = true;
                try { if (Request.assetBundle != null) Request.assetBundle.Unload(false); }
                catch (Exception e) { report($"Presentation abandoned bundle cleanup: {e}"); }
            }
            foreach (var request in _assets.Values)
                if (!request.isDone) request.add_completed(DelegateSupport.ConvertDelegate<Il2CppBase::Il2CppSystem.Action<AsyncOperation>>(
                    (Action<AsyncOperation>)(operation => Finish(operation))));
            if (!Request.isDone) Request.add_completed(DelegateSupport.ConvertDelegate<Il2CppBase::Il2CppSystem.Action<AsyncOperation>>(
                (Action<AsyncOperation>)(operation => Finish(operation))));
            Finish(null);
        }
    }

    private sealed class Preparation : IEffectPreparation
    {
        private readonly UnityRenderBackend _owner;
        private readonly EffectPackage _package;
        private readonly IntPtr _background;
        private readonly Camera? _camera;
        private readonly Dictionary<string, BundleEntry> _owned = new();
        private readonly List<Material> _materials = new();
        private readonly List<Texture2D> _images = new();
        private MaterialEffect? _result;
        private bool _taken, _disposed, _requestedAssets;
        public string? Error { get; private set; }
        public Preparation(UnityRenderBackend owner, EffectPackage package, IntPtr background, Camera? camera)
        {
            (_owner, _package, _background, _camera) = (owner, package, background, camera);
            _owner._preparations++;
            try
            {
                foreach (var node in package.Document.Nodes.Where(n=>!SceneOpacityPlan.IsControl(n)))
                {
                    var path = EffectPackage.ResolveAsset(package.Root, node.Bundle);
                    if (!_owned.ContainsKey(path)) _owned.Add(path, owner.Acquire(path));
                }
            }
            catch { Dispose(); throw; }
        }
        public PreparationState Poll()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(Preparation));
            if (_result != null) return PreparationState.Ready;
            if (Error != null) return PreparationState.Failed;
            if (_owned.Values.Any(b => !b.Request.isDone)) return PreparationState.Pending;
            try
            {
                if (!_requestedAssets)
                {
                    foreach (var doc in _package.Document.Nodes.Where(n=>!SceneOpacityPlan.IsControl(n)))
                    {
                        var entry = _owned[EffectPackage.ResolveAsset(_package.Root, doc.Bundle)];
                        entry.Load(doc.Material, Il2CppType.Of<Material>());
                        foreach (var texture in doc.Textures.Values) if(!texture.StartsWith("file:", StringComparison.Ordinal)) entry.Load(texture, Il2CppType.Of<Texture>());
                    }
                    _requestedAssets = true;
                    _owner._report($"Presentation asset requests started: {_package.Document.Id}");
                }
                if (_owned.Values.Any(b => !b.LoadsComplete)) return PreparationState.Pending;
                var resources = new List<MaterialResource>();
                foreach (var doc in _package.Document.Nodes.Where(n=>!SceneOpacityPlan.IsControl(n)))
                {
                    var entry = _owned[EffectPackage.ResolveAsset(_package.Root, doc.Bundle)];
                    var asset = entry.Load(doc.Material, Il2CppType.Of<Material>()).asset?.TryCast<Material>();
                    if (asset == null || asset.shader == null || !asset.shader.isSupported)
                        throw new InvalidDataException($"Unsupported/missing material: {doc.Material}");
                    var material = _owner.Own(new Material(asset)); _materials.Add(material);
                    var textures = new Dictionary<string, Texture>();
                    var images = new List<Texture2D>();
                    foreach (var texture in doc.Textures)
                    {
                        Texture? value;
                        if(texture.Value.StartsWith("file:",StringComparison.Ordinal)) {
                            if(!_package.Document.Requires.Contains(AddonPackage.ImageCapability))throw new InvalidDataException("External images need a capability declaration.");
                            var path=EffectPackage.ResolveAsset(_package.Root,texture.Value.Substring(5));
                            if(new FileInfo(path).Length>16*1024*1024)throw new InvalidDataException("Image exceeds 16 MiB.");
                            var bytes=File.ReadAllBytes(path);ImageHeader.Validate(bytes);
                            var image=new Texture2D(2,2,TextureFormat.RGBA32,false);_images.Add(image);_owner._images++;images.Add(image);
                            if(!NativeImageLoader.Decode(image,bytes)||image.width>4096||image.height>4096)
                                throw new InvalidDataException("Invalid image or dimensions exceed 4096 pixels.");
                            image.wrapMode=TextureWrapMode.Clamp;value=image;
                        } else value = entry.Load(texture.Value, Il2CppType.Of<Texture>()).asset?.TryCast<Texture>();
                        if (value == null) throw new InvalidDataException($"Texture not found: {texture.Value}");
                        textures.Add(texture.Key, value);
                    }
                    resources.Add(new(doc.Id, doc.Target, material, textures, images.ToArray()));
                }
                _result = new MaterialEffect(_owner, _package, _background, _camera, resources.ToArray(), _owned.Values.ToArray());
                _owner._report($"Presentation materials validated: {_package.Document.Id}; nodes={resources.Count}");
                return PreparationState.Ready;
            }
            catch (Exception e) { Error = e.ToString(); return PreparationState.Failed; }
        }
        public IEffectNode Take()
        {
            if (_result == null || _taken || _disposed) throw new InvalidOperationException("Effect not ready or already taken.");
            _taken = true; return _result;
        }
        public void Dispose()
        {
            if (_disposed) return; _disposed = true;
            _owner._preparations--;
            if (_taken) return;
            foreach (var material in _materials) _owner.DestroyOwned(material);
            foreach(var image in _images) { Object.Destroy(image); _owner._images--; }
            foreach (var bundle in _owned.Values) _owner.Release(bundle);
        }
    }

    private sealed record MaterialResource(string Id, string Target, Material Material, Dictionary<string, Texture> Textures, Texture2D[] OwnedImages)
    {
        public readonly string PassName = "Presentation." + Id;
    }
    private sealed class MaterialEffect : IEffectNode
    {
        private readonly UnityRenderBackend _owner;
        private readonly IntPtr _target;
        private readonly Component? _targetObject;
        private readonly Camera? _camera;
        private readonly MaterialResource[] _resources;
        private readonly BundleEntry[] _bundles;
        private readonly (MaterialResource? Resource, int Id, ParameterKind Kind)[] _bindings;
        private readonly SceneOpacityPlan _scenePlan;
        private SceneOpacityValues _sceneValues=SceneOpacityValues.Visible;
        private readonly ParameterValue[] _last;
        private bool _hasLast, _active, _disposed, _handoff, _warnedBackbuffer, _reportedBinding;
        private long _bindingCheckFrame;
        private double _lastSeconds = double.NaN;
        private float _lastRate = float.NaN;
        private float _lastAspect = float.NaN;
        private ulong _lastRevision;
        private IntPtr _original, _assigned;
        private byte _originalOption;
        private Il2Cpp.CommandBufferHubFeature.RecordGraphAction? _callback;
        private Hub.Handle? _handle;
        private readonly bool _hasPost;
        private static readonly int TimeId = Shader.PropertyToID("_PresentationTime");
        private static readonly int BeatId = Shader.PropertyToID("_PresentationBeat");
        private static readonly int TickId = Shader.PropertyToID("_PresentationTick");
        private static readonly int SeedId = Shader.PropertyToID("_PresentationSeed");
        private static readonly int RateId = Shader.PropertyToID("_PresentationRate");
        private static readonly int AspectId = Shader.PropertyToID("_Aspect");
        private readonly bool[,] _uniforms;
        public MaterialEffect(UnityRenderBackend owner, EffectPackage package, IntPtr target, Camera? camera,
            MaterialResource[] resources, BundleEntry[] bundles)
        {
            (_owner, _target, _camera, _resources, _bundles) = (owner, target, camera, resources, bundles);
            _targetObject = target == IntPtr.Zero ? null : new Component(target);
            _hasPost = resources.Any(r => r.Target == "postprocess");
            _scenePlan=new SceneOpacityPlan(package.Document);
            _bindings = new (MaterialResource?, int, ParameterKind)[package.Timeline.Count];
            _last = new ParameterValue[_bindings.Length];
            for (var i = 0; i < _bindings.Length; i++)
            {
                var b = package.Timeline.Bindings[i];
                if(SceneOpacityPlan.IsControl(package.Document.Nodes.Single(n=>n.Id==b.Node)))continue;
                var resource = resources.Single(r => r.Id == b.Node);
                var mat = resource.Material;
                var id = Shader.PropertyToID(b.Property);
                if (!mat.HasProperty(id)) throw new InvalidDataException($"{b.Node}: shader has no {b.Property}.");
                var index = mat.shader.FindPropertyIndex(b.Property); var type = mat.shader.GetPropertyType(index);
                var valid = b.Kind switch
                {
                    ParameterKind.Float => type is ShaderPropertyType.Float or ShaderPropertyType.Range,
                    ParameterKind.Vector => type == ShaderPropertyType.Vector,
                    ParameterKind.Color => type == ShaderPropertyType.Color,
                    ParameterKind.Choice => type == ShaderPropertyType.Texture,
                    _ => false
                };
                if (!valid) throw new InvalidDataException($"{b.Node}.{b.Property}: shader parameter type mismatch.");
                if (b.Kind == ParameterKind.Choice)
                {
                    var ch = package.Document.Channels[i];
                    foreach (var choice in ch.Keys.Select(k => k.Choice).Prepend(ch.DefaultChoice))
                        if (choice == null || !resource.Textures.ContainsKey(choice))
                            throw new InvalidDataException($"{b.Property}: undefined texture choice {choice}.");
                }
                _bindings[i] = (resource, id, b.Kind);
            }
            _uniforms = new bool[resources.Length, 6];
            var ids = new[] { TimeId, BeatId, TickId, SeedId, RateId, AspectId };
            for (var i = 0; i < resources.Length; i++)
                for (var j = 0; j < ids.Length; j++) _uniforms[i, j] = resources[i].Material.HasProperty(ids[j]);
            _owner._effects++;
        }
        public void Apply(in FrameContext frame, ReadOnlySpan<ParameterValue> values)
        {
            _sceneValues=_scenePlan.Sample(frame.Seconds,values);
            if(_active&&_owner._bridge is ISceneOpacityBindings scene&&_scenePlan.Count>0)scene.ApplySceneOpacity(_sceneValues);
            if (_active && !_reportedBinding && _owner._frame >= _bindingCheckFrame && _assigned != IntPtr.Zero)
            {
                _reportedBinding = true;
                if (_targetObject != null)
                    _owner._report($"Presentation background binding: requestedOwned={OwnsBinding()}; calculatedMaterialMatches={_owner._bridge.CurrentMaterial(_target) == _assigned}");
                else _owner._report("Presentation background binding: target was destroyed.");
            }
            for (var i = 0; i < values.Length; i++)
            {
                if (_hasLast && _last[i] == values[i]) continue;
                var (r, id, kind) = _bindings[i]; var value = values[i]; var v = value.Number;
                if(r==null)continue;
                switch (kind)
                {
                    case ParameterKind.Float: r.Material.SetFloat(id, v.X); break;
                    case ParameterKind.Vector: r.Material.SetVector(id, new Vector4(v.X, v.Y, v.Z, v.W)); break;
                    case ParameterKind.Color: r.Material.SetColor(id, new Color(v.X, v.Y, v.Z, v.W)); break;
                    case ParameterKind.Choice: r.Material.SetTexture(id, r.Textures[value.Choice!]); break;
                }
                _last[i] = value;
            }
            _hasLast = true;
            var aspect=_camera!=null?_camera.aspect:(float)Screen.width/Math.Max(1,Screen.height);
            if (_lastSeconds == frame.Seconds && _lastRevision == frame.ClockRevision && _lastRate == frame.Playback.PlaybackRate && _lastAspect==aspect) return;
            _lastAspect=aspect;
            _lastSeconds = frame.Seconds; _lastRevision = frame.ClockRevision; _lastRate = frame.Playback.PlaybackRate;
            for (var i = 0; i < _resources.Length; i++)
            {
                var mat = _resources[i].Material;
                if (_uniforms[i, 0]) mat.SetFloat(TimeId, (float)frame.Seconds);
                if (_uniforms[i, 1]) mat.SetFloat(BeatId, (float)frame.Beat);
                if (_uniforms[i, 2]) mat.SetFloat(TickId, frame.VisualTick);
                if (_uniforms[i, 3]) mat.SetFloat(SeedId, frame.Seed);
                if (_uniforms[i, 4]) mat.SetFloat(RateId, frame.Playback.PlaybackRate);
                if (_uniforms[i, 5]) mat.SetFloat(AspectId, aspect);
            }
        }
        public void Reset(ResetReason reason) { _hasLast = false; _lastSeconds = double.NaN; /* No persistent history allocated in v1. */ }
        public void Activate(IEffectNode? previous)
        {
            if (_disposed || _active) throw new InvalidOperationException("Effect cannot activate twice.");
            var bg = _resources.SingleOrDefault(r => r.Target == "background");
            if (bg != null && _targetObject == null) throw new InvalidOperationException("Background target destroyed.");
            if (_hasPost && _camera == null) throw new InvalidOperationException("Game camera unavailable.");
            var old = previous as MaterialEffect;
            var beforeMaterial=_target!=IntPtr.Zero?_owner._bridge.RequestedMaterial(_target):IntPtr.Zero;
            var beforeOption=_target!=IntPtr.Zero?_owner._bridge.RequestedMaterialOption(_target):(byte)1;
            if(beforeMaterial==IntPtr.Zero&&_target!=IntPtr.Zero)beforeMaterial=_owner._bridge.CurrentMaterial(_target);
            try
            {
                if (_hasPost)
                {
                    _callback = DelegateSupport.ConvertDelegate<Il2Cpp.CommandBufferHubFeature.RecordGraphAction>(
                        (Action<RenderGraph, ContextContainer>)RecordGraph);
                    _handle = Hub.AddRenderGraph(_camera!, RenderPassEvent.AfterRenderingPostProcessing, _callback);
                    if (_handle == null) throw new InvalidOperationException("RenderGraph did not return a registration handle.");
                    _owner._registrations++;
                }
                if (bg != null)
                {
                    var requested = _owner._bridge.RequestedMaterial(_target);
                    var inherit = old != null && old._target == _target && old.OwnsBinding();
                    _original = inherit ? old!._original : requested != IntPtr.Zero ? requested : _owner._bridge.CurrentMaterial(_target);
                    // With no pending request, retain the effective material itself. There is no
                    // persistent request-option field beside it: +0x158 is a Transform pointer.
                    _originalOption = inherit ? old!._originalOption : requested != IntPtr.Zero ? _owner._bridge.RequestedMaterialOption(_target) : (byte)1;
                    if (requested == IntPtr.Zero && _owner._bridge.CurrentMaterial(_target) == IntPtr.Zero)
                        throw new InvalidOperationException("Original background material is not ready.");
                    _assigned = bg.Material.Pointer;
                    _owner._bridge.SetMaterial(_target, _assigned);
                    // C2D's calculated material updates later; handoff is based on its requested binding.
                }
                if(_owner._bridge is ISceneOpacityBindings scene)scene.ApplySceneOpacity(_sceneValues);
                if(old!=null&&old._target==_target)old._handoff=true;
                _active = true; _bindingCheckFrame = _owner._frame + 2;
            }
            catch
            {
                RemoveGraph();
                if (OwnsBinding())
                    _owner._bridge.SetMaterial(_target, beforeMaterial, beforeOption);
                if(_owner._bridge is ISceneOpacityBindings scene){scene.RestoreSceneOpacity();if(old!=null&&old._active)scene.ApplySceneOpacity(old._sceneValues);}
                throw;
            }
        }
        private void RecordGraph(RenderGraph graph, ContextContainer data)
        {
            if (_disposed || !_active) return;
            try
            {
                _owner._postCallbacks++;
                var resources = data.Get<UniversalResourceData>();
                if (resources.isActiveTargetBackBuffer)
                {
                    _owner._backbufferSkips++;
                    if (!_warnedBackbuffer) { _owner._report("Presentation PP skipped: camera has no readable intermediate color."); _warnedBackbuffer = true; }
                    return;
                }
                foreach (var node in _resources)
                {
                    if (node.Target != "postprocess") continue;
                    var source = resources.activeColorTexture;
                    var desc = graph.GetTextureDesc(ref source); desc.name = node.PassName; desc.clearBuffer = false;
                    var destination = graph.CreateTexture(ref desc);
                    var parameters = new GraphUtils.BlitMaterialParameters(source, destination, node.Material, 0);
                    GraphUtils.AddBlitPass(graph, parameters, desc.name);
                    _owner._passesRecorded++;
                    resources.cameraColor = destination;
                }
            }
            catch (Exception e)
            {
                _owner._renderErrors++;
                // Never let managed exceptions cross IL2CPP's renderer callback boundary.
                if (!_warnedBackbuffer) _owner._report($"Presentation RenderGraph disabled: {e.Message}");
                _warnedBackbuffer = true;
                // This node remains disposable; remove only our registration on next disposal.
                _active = false;
            }
        }
        public void Dispose()
        {
            if (_disposed) return; _disposed = true;
            _owner._effects--;
            try
            {
                if(_active&&!_handoff&&_owner._bridge is ISceneOpacityBindings scene)scene.RestoreSceneOpacity();
                RemoveGraph();
                if (!_handoff && OwnsBinding())
                    _owner._bridge.SetMaterial(_target, _original, _originalOption);
            }
            finally
            {
                _owner.Retire(_resources, _bundles);
            }
        }
        private void RemoveGraph()
        {
            if (_callback == null) return;
            try { if (_handle != null) { Hub.Remove(_handle); _owner._registrations--; } }
            catch (Exception e) { _owner._cleanupFailures++; _owner._report($"Presentation render registration cleanup: {e.Message}"); }
            finally { _callback = null; _handle = null; }
        }
        private bool OwnsBinding() => _assigned != IntPtr.Zero && _targetObject != null
            && (_owner._bridge.RequestedMaterial(_target) == _assigned
                || _owner._bridge.RequestedMaterial(_target) == IntPtr.Zero && _owner._bridge.CurrentMaterial(_target) == _assigned);
    }
}
