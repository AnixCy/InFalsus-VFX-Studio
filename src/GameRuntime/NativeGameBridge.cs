extern alias UnityCore;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using UnityCore::UnityEngine;

namespace InFalsus.Presentation.Game;

public sealed record ChartIdentity(string SongKey, int Difficulty, string ChartFingerprint);
public sealed class GameProfile
{
    public string Sha256 { get; set; } = "";
    public string UnityVersion { get; set; } = "";
    public int AudioClockRva { get; set; }
    public int AudioReadyRva { get; set; }
    public int MaterialGetRva { get; set; }
    public int MaterialSetRva { get; set; }
    public int PracticeStateRva { get; set; }
    public int PracticeSeekRva { get; set; }
    public string AudioClockPrefix { get; set; } = "";
    public string PracticeSeekPrefix { get; set; } = "";
}

/// <summary>Only presentation material/opacity setters write game objects. No transport/input/save writes.</summary>
public sealed unsafe partial class NativeGameBridge : IGameBindings
{
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate double ClockCall(IntPtr self, IntPtr method);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate byte BoolCall(IntPtr self, IntPtr method);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate IntPtr PtrCall(IntPtr self, IntPtr method);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void MaterialCall(IntPtr self, IntPtr material, byte option, IntPtr method);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate IntPtr DomainCall();
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate IntPtr AssembliesCall(IntPtr domain, out nuint count);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate IntPtr ImageCall(IntPtr assembly);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate nuint CountCall(IntPtr image);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate IntPtr ClassCall(IntPtr image, nuint index);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate IntPtr NameCall(IntPtr klass);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate IntPtr FieldCall(IntPtr klass, [MarshalAs(UnmanagedType.LPUTF8Str)] string name);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate nuint OffsetCall(IntPtr field);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void StaticCall(IntPtr field, out IntPtr value);

    public GameProfile Profile { get; }
    public IntPtr BaseAddress { get; }
    public IntPtr Scene { get; private set; }
    public IntPtr Background { get; private set; }
    public Camera? GameCamera { get; private set; }
    public bool HasSpecialScene { get; private set; }
    public string SceneControllerName { get; private set; } = "none";
    public bool CanRetainBackground { get; private set; }
    public ulong SeekSerial { get; private set; }
    public bool HasExactSeekSerial { get; set; }
    private readonly Dictionary<string, ChartIdentity> _catalog;
    private readonly ClockCall _clock;
    private readonly BoolCall _ready;
    private readonly PtrCall _material;
    private readonly MaterialCall _setMaterial;
    private readonly StaticCall _static;
    private readonly NameCall _className;
    private readonly IntPtr _sceneStatic, _engineStatic, _logicalStatic, _defaultSpecial;
    private IntPtr _lastScene, _lastAudio, _lastChart, _pausePointer;
    private IntPtr _controllerClass;
    private Component? _pause;
    private Component? _sceneObject, _backgroundObject;
    private long _logicalStamp, _practiceGeneration = -1, _generation;
    private ChartIdentity? _identity;

    public NativeGameBridge(string gameRoot, string profilesFile, string catalogFile)
    {
        this.gameRoot=Path.GetFullPath(gameRoot);
        if (!OperatingSystem.IsWindows() || IntPtr.Size != 8) throw new PlatformNotSupportedException("Windows x64 game adapter required.");
        var profiles = RuntimeDefaults.Profiles(profilesFile);
        using var stream = File.OpenRead(Path.Combine(gameRoot, "GameAssembly.dll"));
        using var sha = SHA256.Create();
        var hash = Convert.ToHexString(sha.ComputeHash(stream)).ToLowerInvariant();
        Profile = profiles.SingleOrDefault(p => p.Sha256 == hash)
            ?? throw new NotSupportedException($"Unrecognized GameAssembly: {hash}. Generate and review a new adapter profile.");
        BaseAddress = GetModuleHandleW("GameAssembly.dll");
        if (BaseAddress == IntPtr.Zero) throw new InvalidOperationException("GameAssembly is not loaded.");
        VerifyPrefix(Profile.AudioClockRva, Profile.AudioClockPrefix);
        if (Profile.PracticeStateRva != 0)
        {
            if (Marshal.PtrToStringAnsi(BaseAddress + Profile.PracticeStateRva + 232, 8) != "IFPRACT1")
                throw new InvalidOperationException("Practice state marker mismatch.");
            VerifyPrefix(Profile.PracticeSeekRva, Profile.PracticeSeekPrefix);
        }
        _catalog = File.Exists(catalogFile)?JsonSerializer.Deserialize<Dictionary<string, ChartIdentity>>(File.ReadAllText(catalogFile))!:new();
        _clock = At<ClockCall>(Profile.AudioClockRva); _ready = At<BoolCall>(Profile.AudioReadyRva);
        _material = At<PtrCall>(Profile.MaterialGetRva); _setMaterial = At<MaterialCall>(Profile.MaterialSetRva);
        _static = Export<StaticCall>("il2cpp_field_static_get_value");
        var fields = Export<FieldCall>("il2cpp_class_get_field_from_name");
        var offset = Export<OffsetCall>("il2cpp_field_get_offset");
        var classes = FindClasses("GameScene", "LogicalNotePlayer", "_VD", "_Dg", "_sD", "_xe");
        // _sD is abstract: every scene uses a concrete subclass. The ordinary no-op
        // controller is _E._xe; comparing with _sD misclassifies all normal play.
        _defaultSpecial = classes["_xe"];
        if (Export<NameCall>("il2cpp_class_get_parent")(_defaultSpecial) != classes["_sD"])
            throw new InvalidOperationException("Ordinary scene-controller hierarchy mismatch.");
        _className = Export<NameCall>("il2cpp_class_get_name");
        IntPtr Field(string type, string name)
        {
            var field = fields(classes[type], name);
            return field != IntPtr.Zero ? field : throw new MissingFieldException(type, name);
        }
        void Check(string type, string name, int expected)
        {
            if (offset(Field(type, name)) != (nuint)expected)
                throw new InvalidOperationException($"Layout mismatch: {type}.{name}.");
        }
        _sceneStatic = Field("GameScene", "_xn"); _engineStatic = Field("GameScene", "_DN");
        _logicalStatic = Field("LogicalNotePlayer", "_Qe");
        Check("GameScene", "_gN", 0x1e8); Check("GameScene", "_lN", 0x230);
        Check("GameScene", "backgroundObject", 0xd0); Check("GameScene", "pauseLayer", 0x140);
        Check("GameScene", "gameCamera", 0x110); Check("_VD", "_WEA", 0xd0);
        Check("_VD", "_VEA", 0xc0); Check("_VD", "_wEA", 0xc8);
        Check("LogicalNotePlayer", "_Ve", 0x48); Check("LogicalNotePlayer", "_We", 0x58);
        Check("_Dg", "_dvA", 0x30); Check("_Dg", "_CvA", 0x28);
    }

    public void NotifySeekCommitted() => SeekSerial++;
    public bool IsCommittedPracticeSeek(IntPtr scene) => Profile.PracticeStateRva != 0
        && scene != IntPtr.Zero && Marshal.ReadIntPtr(scene + 0x1e8) != IntPtr.Zero
        && Marshal.ReadByte(BaseAddress + Profile.PracticeStateRva + 1) != 0
        && Marshal.ReadByte(BaseAddress + Profile.PracticeStateRva + 3) == 0
        && Marshal.ReadByte(BaseAddress + Profile.PracticeStateRva + 184) == 0;

    public bool TrySample(out PlaybackSnapshot snapshot)
    {
        snapshot = default;
        CanRetainBackground = false;
        _static(_sceneStatic, out var scene); _static(_engineStatic, out var engine); _static(_logicalStatic, out var logical);
        if (scene == IntPtr.Zero || engine == IntPtr.Zero || logical == IntPtr.Zero) return TransportUnavailable(scene);
        // Unity validity is cached per scene, not reconstructed every frame.
        var state = Marshal.ReadInt32(scene + 0x230);
        var audio = Marshal.ReadIntPtr(scene + 0x1e8);
        if (state < 1 || state > 4 || audio == IntPtr.Zero || Marshal.ReadByte(audio + 0x28) == 0
            || _ready(audio, IntPtr.Zero) == 0) return TransportUnavailable(scene);
        var chart = Marshal.ReadIntPtr(logical + 0x48);
        var stamp = Marshal.ReadInt64(logical + 0x58);
        var practice = BaseAddress + Profile.PracticeStateRva;
        var generation = Profile.PracticeStateRva == 0 ? 0 : Marshal.ReadInt64(practice + 200);
        if (scene != _lastScene || audio != _lastAudio || chart != _lastChart || stamp != _logicalStamp
            || generation != _practiceGeneration)
        {
            Invalidate();
            var name = ReadString(chart);
            _catalog.TryGetValue(name, out _identity);
            if(packCatalog.TryGetValue(name,out var packed)) {
                string? actual=ImportedChartHash(name);
                _identity=actual!=null&&string.Equals(actual,packed.ChartFingerprint,StringComparison.OrdinalIgnoreCase)?packed:null;
            }
            _lastScene = scene; _lastAudio = audio; _lastChart = chart;
            _logicalStamp = stamp; _practiceGeneration = generation; _generation++;
            Scene = scene; Background = Marshal.ReadIntPtr(scene + 0xd0);
            _sceneObject = new Component(scene);
            _backgroundObject = Background == IntPtr.Zero ? null : new Component(Background);
            var transform = Marshal.ReadIntPtr(scene + 0x110);
            if (transform != IntPtr.Zero) GameCamera = new Component(transform).GetComponent<Camera>();
        }
        if (_identity == null) return false;
        var pause = Marshal.ReadIntPtr(scene + 0x140);
        if (pause != _pausePointer) { _pausePointer = pause; _pause = pause == IntPtr.Zero ? null : new Component(pause); }
        var ui = _pause != null && _pause.gameObject.activeSelf;
        var kind = Marshal.ReadInt32(audio + 0x30);
        var rate = *(float*)(audio + 0x48);
        var seconds = _clock(audio, IntPtr.Zero);
        var transport = kind == 2 ? TransportState.Running : kind == 1 ? TransportState.Frozen : TransportState.Stopped;
        if (transport == TransportState.Stopped) return TransportUnavailable(scene);
        var phase = state == 4 ? PlaybackPhase.Ending : transport == TransportState.Frozen ? PlaybackPhase.Paused
            : state < 3 || seconds < 0 || ui ? PlaybackPhase.Countdown : PlaybackPhase.Playing;
        var special = Marshal.ReadIntPtr(engine + 0xc8);
        var controller = special == IntPtr.Zero ? IntPtr.Zero : Marshal.ReadIntPtr(special);
        HasSpecialScene = controller != IntPtr.Zero && controller != _defaultSpecial;
        if (controller != _controllerClass)
        {
            _controllerClass = controller;
            SceneControllerName = controller == IntPtr.Zero ? "none" : Marshal.PtrToStringUTF8(_className(controller))!;
        }
        snapshot = new(_generation, _identity.SongKey, _identity.Difficulty, _identity.ChartFingerprint,
            seconds, *(double*)(engine + 0xd0), Marshal.ReadInt32(engine + 0xc0), rate, transport, phase, ui,
            Profile.PracticeStateRva != 0 && Marshal.ReadByte(practice + 1) != 0,
            Profile.PracticeStateRva != 0 && (Marshal.ReadByte(practice + 3) != 0 || Marshal.ReadByte(practice + 184) != 0),
            SeekSerial, HasExactSeekSerial, Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency);
        return snapshot.IsValid;
    }

    private bool TransportUnavailable(IntPtr scene)
    {
        // End-of-song/exit stops and releases audio before the scene's visual transition ends.
        // Retain only an already identified, unchanged target; this cannot start a new addon.
        CanRetainBackground = _identity != null && _lastScene != IntPtr.Zero
            && (scene == IntPtr.Zero || scene == _lastScene)
            && _sceneObject != null && _backgroundObject != null
            && _sceneObject.gameObject.activeInHierarchy && _backgroundObject.gameObject.activeInHierarchy
            && Marshal.ReadIntPtr(_lastScene + 0xd0) == Background;
        if (!CanRetainBackground) Invalidate();
        return false;
    }

    private void Invalidate()
    {
        RestoreSceneOpacity();
        _lastScene = _lastAudio = _lastChart = IntPtr.Zero; _identity = null;
        Scene = Background = IntPtr.Zero; GameCamera = null; _pause = null; _pausePointer = IntPtr.Zero;
        _sceneObject = _backgroundObject = null; CanRetainBackground = false;
    }
    public bool TargetAlive(IntPtr target) => target != IntPtr.Zero && new Component(target) != null;
    public IntPtr CurrentMaterial(IntPtr target) => _material(target, IntPtr.Zero);
    // _irA stores a material request at +0x40 and marks C2D dirty; _H is the later calculated +0x150.
    public IntPtr RequestedMaterial(IntPtr target) => Marshal.ReadIntPtr(target + 0x40);
    // Calculated._pHb is ValueTuple<Material,bool>; preserve the bool on restore as well as the pointer.
    public byte RequestedMaterialOption(IntPtr target) => Marshal.ReadByte(target + 0x48);
    public void SetMaterial(IntPtr target, IntPtr material, byte option = 1) => _setMaterial(target, material, option, IntPtr.Zero);
    public void VerifySeekPrefix() => VerifyPrefix(Profile.PracticeSeekRva, Profile.PracticeSeekPrefix);

    private void VerifyPrefix(int rva, string hex)
    {
        var expected = Convert.FromHexString(hex); var actual = new byte[expected.Length];
        if (rva <= 0 || actual.Length < 5) throw new InvalidDataException("Incomplete native profile.");
        Marshal.Copy(BaseAddress + rva, actual, 0, actual.Length);
        if (!actual.AsSpan().SequenceEqual(expected)) throw new InvalidOperationException($"Native prefix mismatch at {rva:x}.");
    }
    private T At<T>(int rva) where T : Delegate => Marshal.GetDelegateForFunctionPointer<T>(BaseAddress + rva);
    private T Export<T>(string name) where T : Delegate => Marshal.GetDelegateForFunctionPointer<T>(NativeLibrary.GetExport(BaseAddress, name));
    private Dictionary<string, IntPtr> FindClasses(params string[] names)
    {
        var wanted = new HashSet<string>(names); var found = new Dictionary<string, IntPtr>();
        var domain = Export<DomainCall>("il2cpp_domain_get")();
        var assemblies = Export<AssembliesCall>("il2cpp_domain_get_assemblies")(domain, out var count);
        var getImage = Export<ImageCall>("il2cpp_assembly_get_image");
        var getCount = Export<CountCall>("il2cpp_image_get_class_count");
        var getClass = Export<ClassCall>("il2cpp_image_get_class"); var getName = Export<NameCall>("il2cpp_class_get_name");
        for (nuint i = 0; i < count; i++)
        {
            var image = getImage(Marshal.ReadIntPtr(assemblies + checked((int)i * IntPtr.Size)));
            var classCount = getCount(image);
            for (nuint j = 0; j < classCount; j++)
            {
                var klass = getClass(image, j); var name = Marshal.PtrToStringUTF8(getName(klass))!;
                if (!wanted.Contains(name)) continue;
                if (!found.TryAdd(name, klass)) throw new InvalidOperationException($"Ambiguous native class: {name}");
            }
        }
        foreach (var name in names) if (!found.ContainsKey(name)) throw new TypeLoadException(name);
        return found;
    }
    private static string ReadString(IntPtr value)
    {
        if (value == IntPtr.Zero) return "";
        var length = Marshal.ReadInt32(value + 0x10);
        return length is > 0 and < 512 ? Marshal.PtrToStringUni(value + 0x14, length)! : "";
    }
    [DllImport("kernel32", CharSet = CharSet.Unicode)] private static extern IntPtr GetModuleHandleW(string name);
}
