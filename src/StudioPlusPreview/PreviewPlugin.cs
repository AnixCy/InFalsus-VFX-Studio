using System.Reflection;
using BepInEx;
using BepInEx.Unity.Mono;
using HarmonyLib;
using InFalsus.Presentation;
using InFalsus.Presentation.Authoring;
using InFalsusStudio;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using UnityEngine;
using UnityEngine.Rendering;
using UColor = UnityEngine.Color;

namespace InFalsus.Vfx.StudioPlus;

[BepInPlugin("local.infalsus.vfx.preview", "InFalsus VFX Studio Preview", "0.1.2")]
[BepInDependency("local.infalsusstudio.visualui", "1.0.7")]
public sealed partial class PreviewPlugin : BaseUnityPlugin
{
    internal static PreviewPlugin? Instance;
    internal readonly StudioBridge Bridge = new();
    internal PreviewModel? Model;
    internal PreviewSurface? Surface;
    readonly Queue<Action> actions = new();
    Harmony? harmony;
    string folder = "", imagePath = "", savedImagePath = "", projectFile = "";
    ProjectFileVersion? fileVersion;
    string status = "实时预览 · 回车应用 · Esc 取消";
    bool previewEnabled = true, stopped, actionRunning;
    internal bool TestMode;
    internal int RenderedFrames;
    internal double CurrentSeconds;
    static readonly JsonSerializerSettings Json = new() {
        Formatting = Formatting.Indented, MaxDepth = 32, TypeNameHandling = TypeNameHandling.None,
        Converters = { new StringEnumConverter { AllowIntegerValues = false } }
    };
    sealed class ProjectFile
    {
        public int FormatVersion { get; set; } = 1;
        public AuthoringProjectDocument Project { get; set; } = null!;
        public string BackgroundPath { get; set; } = "";
    }
    bool Dirty => Model != null && (Model.Session.IsDirty || Model.IsEditing || textParameter != null || imagePath != savedImagePath);
    internal void Awake()
    {
        Instance = this;
        TestMode = Environment.GetCommandLineArgs().Contains("-vfx-self-check");
        if (TestMode) AudioListener.volume = 0;
        folder = Path.GetDirectoryName(GetType().Assembly.Location)!;
        harmony = new Harmony("local.infalsus.vfx.preview");
        void Patch(string method, string? prefix = null, string? postfix = null) {
            var target = typeof(StudioBootstrap).GetMethod(method, StudioBridge.Flags)
                ?? throw new MissingMethodException(typeof(StudioBootstrap).FullName, method);
            harmony.Patch(target, prefix == null ? null : new HarmonyMethod(typeof(Hooks).GetMethod(prefix)!) { priority = Priority.First },
                postfix == null ? null : new HarmonyMethod(typeof(Hooks).GetMethod(postfix)!) { priority = Priority.Last });
        }
        try {
            Patch("OnGUI", prefix: nameof(Hooks.GuiBefore), postfix: nameof(Hooks.Draw));
            harmony.Patch(typeof(StudioBootstrap).GetMethod("OnGUI", StudioBridge.Flags),
                finalizer: new HarmonyMethod(typeof(Hooks).GetMethod(nameof(Hooks.GuiFinally))!));
            Patch("WorkspaceBlocksInput", postfix: nameof(Hooks.Block));
            Patch("ChromeBlocks", postfix: nameof(Hooks.Block));
            Patch("HandleSceneEvent", prefix: nameof(Hooks.Scene));
            Patch("HandleKeyboard", prefix: nameof(Hooks.Keys));
            Patch("HandleCommittedInput", prefix: nameof(Hooks.TextInput));
            harmony.Patch(typeof(StudioTransport).GetMethod("Seek"), postfix: new HarmonyMethod(typeof(Hooks).GetMethod(nameof(Hooks.Seek))!));
            if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("VFX_WORK_ROOT"))) {
                var getter = typeof(StudioBootstrap).GetProperty("PreferencesPath", StudioBridge.Flags)?.GetGetMethod(true);
                if (getter != null) harmony.Patch(getter, prefix: new HarmonyMethod(typeof(Hooks).GetMethod(nameof(Hooks.Preferences))!));
            }
            RenderPipelineManager.beginCameraRendering += BeforeCamera;
            Application.wantsToQuit += MayQuit;
            Logger.LogInfo("VFX parameter editor initialized; preview uses the Studio display clock.");
            if (TestMode) StartCoroutine(RunChecks());
        } catch (Exception e) { harmony.UnpatchSelf(); stopped = true; Logger.LogError(e); }
    }
    void Update()
    {
        // A veto by another subscriber must not leave authorization for a later attempt.
        if (quitPermit && Time.frameCount > quitPermitExpiresAfter) quitPermit = false;
        // Native file dialogs may pump window messages. Never re-enter the action queue.
        if (stopped || actionRunning || quitPermit) return;
        if (!Bridge.Owner) {
            Bridge.Owner = UnityEngine.Object.FindFirstObjectByType<StudioBootstrap>();
            if (Bridge.Owner && Bridge.Owner!.ViewCamera) {
                try {
                    Surface?.Dispose(); Model?.Cancel();
                    Model = new PreviewModel(folder, Application.unityVersion, QualitySettings.activeColorSpace.ToString());
                    Surface = new PreviewSurface(Bridge.Owner.ViewCamera, folder);
                } catch (Exception e) { Error(e); stopped = true; }
            } else Bridge.Owner = null;
        }
        while (actions.Count != 0) {
            var action = actions.Dequeue();
            actionRunning = true;
            try { action(); }
            catch (Exception e) { Error(e); }
            finally { actionRunning = false; }
            if (decision != PendingDecision.None) { actions.Clear(); break; }
        }
    }
    void BeforeCamera(ScriptableRenderContext context, Camera camera)
    {
        if (stopped || Model == null || Surface == null || !Bridge.Owner || camera != Bridge.Owner!.ViewCamera) return;
        Surface.Enabled = previewEnabled;
        if (!previewEnabled) return;
        try { CurrentSeconds = Surface.Draw(Model, Bridge.Snapshot(), Bridge.Exporting).Seconds; RenderedFrames++; }
        catch (Exception e) { Error(e); previewEnabled = false; Surface.Enabled = false; }
    }
    void Queue(Action action)
    {
        if (BlocksHostInput) return;
        actions.Enqueue(() => { FinishText(); action(); drafts.Clear(); });
    }
    void Error(Exception e)
    {
        while (e is TargetInvocationException && e.InnerException != null) e = e.InnerException;
        status = e is AuthoringEditException edit && edit.Code == AuthoringEditError.FileConflict
            ? "文件已存在或被其他程序修改，请另存为其他文件。" : e.Message;
        if (decision != PendingDecision.None) decisionError = status;
        Logger.LogWarning("VFX: " + e);
    }
    ProjectFileStore Store() => new(p => JsonConvert.SerializeObject(new ProjectFile { Project = p, BackgroundPath = imagePath }, Json),
        text => Decode(text).Project, PreviewModel.Capabilities);
    static ProjectFile Decode(string text)
    {
        var p = JsonConvert.DeserializeObject<ProjectFile>(text, Json);
        if (p == null || p.FormatVersion != 1 || p.Project == null || p.BackgroundPath == null)
            throw new InvalidDataException("不是受支持的演出参数工程。");
        return p;
    }
    void SaveProject() => TrySaveProject();
    bool TrySaveProject()
    {
        if (Model == null) return false;
        FinishText();
        Model.Commit();
        string target = projectFile.Length == 0 ? saveDialog("演出工程.vfx.json") : projectFile;
        if (target.Length == 0) return false;
        var result = Store().Save(Model.Session, target, Model.Session.Revision, fileVersion);
        projectFile = target; fileVersion = result.FileVersion; savedImagePath = imagePath;
        status = "工程已保存。";
        return true;
    }
    void SaveAs()
    {
        if (Model == null) return;
        Model.Commit(); var target = saveDialog("演出工程.vfx.json"); if (target.Length == 0) return;
        var result = Store().Save(Model.Session, target, Model.Session.Revision,
            string.Equals(target, projectFile, StringComparison.OrdinalIgnoreCase) ? fileVersion : null);
        projectFile = target; fileVersion = result.FileVersion; savedImagePath = imagePath; status = "工程已另存。";
    }
    void LoadProject()
    {
        if (Model == null || Surface == null) return;
        if (Dirty) { ShowDecision(PendingDecision.OpenProject); return; }
        OpenProjectFile();
    }
    void OpenProjectFile()
    {
        if (Model == null || Surface == null) return;
        var file = openDialog("打开演出参数工程", "json"); if (file.Length == 0) return;
        if (new FileInfo(file).Length > 4*1024*1024) throw new InvalidDataException("工程超过 4 MiB。");
        var data = Decode(File.ReadAllText(file));
        var opened = Store().Open(file);
        Texture2D? image = null;
        try {
            if (data.BackgroundPath.Length != 0) image = Surface.PrepareImage(data.BackgroundPath);
            Model.Replace(opened.Session);
        } catch { if (image) Destroy(image); throw; }
        Surface.ApplyImage(image); imagePath = savedImagePath = data.BackgroundPath;
        projectFile = file; fileVersion = opened.FileVersion; drafts.Clear(); status = "工程已打开。";
    }
    void ChooseImage()
    {
        if (Model == null || Surface == null) return;
        var path = openDialog("选择背景图片", "png,jpg,jpeg"); if (path.Length == 0) return;
        var image = Surface.PrepareImage(path);
        Surface.ApplyImage(image); imagePath = Path.GetFullPath(path);
        Model.UpdateParameter("background/_PatternMix", new ParameterValue(new System.Numerics.Vector4(0,0,0,0)));
        Model.Commit(); status = "背景已载入；参数修改实时预览。";
    }
    void OnDestroy()
    {
        stopped = true; RenderPipelineManager.beginCameraRendering -= BeforeCamera;
        Application.wantsToQuit -= MayQuit; harmony?.UnpatchSelf();
        Model?.Cancel(); Surface?.Dispose(); ReleaseStyle();
        if (Instance == this) Instance = null;
    }
    public static class Hooks
    {
        public static void GuiBefore(StudioBootstrap __instance, out Event? __state)
        {
            __state=null; var p=Instance; var e=Event.current;
            if(p?.Bridge.Owner!=__instance||p.Model==null||e==null)return;
            if((p.stopped||p.Bridge.Exporting)&&!p.BlocksHostInput)return;
            if(p.BlocksHostInput) {
                if(e.isMouse||e.isKey||e.type==EventType.ScrollWheel) {
                    __state=e; Event.current=new Event(e){type=EventType.Ignore};
                }
                return;
            }
            bool ownText=p.OwnsText;
            if(ownText&&e.type==EventType.MouseDown&&!p.Captures(e.mousePosition)&&GUIUtility.hotControl==0) {
                GUI.FocusControl(null); ownText=false;
            }
            bool mouse=e.isMouse||e.type==EventType.ScrollWheel;
            if(!(mouse&&(p.Captures(e.mousePosition)||p.sliderEditing||ownText))&&!(e.isKey&&ownText))return;
            if(e.type==EventType.KeyDown&&ownText&&(e.control||e.command)&&e.keyCode==KeyCode.S) {
                p.Queue(p.SaveProject); e.Use();
            }
            // Host controls still receive the same GUI tree, without consuming the overlay's input.
            __state=e; Event.current=new Event(e){type=EventType.Ignore};
        }
        public static void Draw(StudioBootstrap __instance, Event? __state)
        {
            if(__state!=null)Event.current=__state;
            if (Instance?.Bridge.Owner != __instance) return;
            try { Instance.DrawEditor(); }
            catch (ExitGUIException) { throw; }
            catch (Exception e) { Instance.Error(e); }
        }
        public static Exception? GuiFinally(Exception? __exception, Event? __state)
        { if(__state!=null)Event.current=__state; return __exception; }
        public static void Block(Vector2 __0, ref bool __result) { if (Instance != null) __result |= Instance.BlocksHostInput || Instance.Captures(__0); }
        public static bool Scene(Event __0) => Instance == null || !(Instance.BlocksHostInput || Instance.Captures(__0.mousePosition));
        public static bool Keys(Event __0)
        {
            var p = Instance;
            if (p?.BlocksHostInput == true) return false;
            if (p == null || !p.OwnsText) return true;
            if (__0.type == EventType.KeyDown && (__0.control || __0.command) && __0.keyCode == KeyCode.S) {
                p.Queue(p.SaveProject); __0.Use();
            }
            return false;
        }
        public static bool TextInput() => Instance == null || !(Instance.BlocksHostInput || Instance.OwnsText);
        public static void Seek(StudioTransport __instance) => Instance?.Bridge.Seeked(__instance);
        public static bool Preferences(ref string __result)
        {
            string root = Environment.GetEnvironmentVariable("VFX_WORK_ROOT")!;
            Directory.CreateDirectory(root); __result = Path.Combine(root,"presentation.v09.json"); return false;
        }
    }
}
