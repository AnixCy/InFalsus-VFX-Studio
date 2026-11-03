using System.Collections;
using System.Security.Cryptography;
using InFalsus.Presentation;
using InFalsus.Presentation.Authoring;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using NVector4 = System.Numerics.Vector4;

namespace InFalsus.Vfx.StudioPlus;

public sealed partial class PreviewPlugin
{
    // Explicit, muted development command. Never runs in normal editing sessions.
    IEnumerator RunChecks()
    {
        var checks=new List<string>();var hashes=new Dictionary<string,string>();string? failure=null;
        for(int i=0;i<180&&(Surface==null||Model==null)&&!stopped;i++)yield return null;
        void Assert(bool value,string name){if(!value)throw new Exception(name);checks.Add(name);}
        try {
            Assert(Model!=null&&Surface!=null,"plugin bound to Studio camera");
            var theme=StudioUiTheme.TryBind();
            Assert(theme!=null&&theme.HasLiveHost,"flat UI delegates bind to the actual Studio+ component");
            Assert(theme!.CheckTextMetrics(),"Chinese and numeric labels use the host font rasterizer");
            Assert(!Dirty&&MayQuit(),"unchanged project exits without a confirmation");
            RenderStageForCheck();
            Assert(RenderedFrames>0,"stage camera rendering callback ran");
            var model=Model!;var surface=Surface!;int material=surface.Material.GetInstanceID();
            var snapshot=Bridge.Snapshot();
            snapshot=new PlaybackSnapshot(snapshot.SessionGeneration,"studio-preview",0,"",0,0,0,1,
                TransportState.Frozen,PlaybackPhase.Paused,true,false,false,30,true,1);
            surface.Draw(model,snapshot,false);string baseline=GpuHash(surface.Material);hashes["baseline"]=baseline;
            model.UpdateParameter("background/_Brightness",new ParameterValue(new NVector4(2,0,0,0)));
            surface.Draw(model,snapshot,false);string changed=GpuHash(surface.Material);hashes["modified"]=changed;
            Assert(changed!=baseline,"parameter override changes real GPU output");
            Assert(Math.Abs(surface.Material.GetFloat("_Brightness")-2)<.0001,"material receives preview value");
            surface.Draw(model,snapshot,true);
            Assert(GpuHash(surface.Material)==baseline,"committed-only render excludes temporary override");
            model.Cancel();surface.Draw(model,snapshot,false);
            Assert(GpuHash(surface.Material)==baseline,"cancel restores exact GPU baseline");
            model.UpdateParameter("background/_Brightness",new ParameterValue(new NVector4(1.7f,0,0,0)));
            model.UpdateParameter("background/_Brightness",new ParameterValue(new NVector4(2,0,0,0)));model.Commit();
            Assert(model.Session.CanUndo,"drag produces undo history");
            model.Undo();surface.Draw(model,snapshot,false);
            Assert(!model.Session.CanUndo&&GpuHash(surface.Material)==baseline,"one undo restores complete drag");
            model.Redo();surface.Draw(model,snapshot,false);
            Assert(GpuHash(surface.Material)==changed,"redo restores edited GPU output");
            model.Reset();surface.Draw(model,snapshot,false);
            Assert(GpuHash(surface.Material)==baseline,"reset restores defaults");
            var args=Environment.GetCommandLineArgs();int at=Array.IndexOf(args,"-vfx-check-output");
            string output=at>=0?args[at+1]:Path.Combine(folder,"checks.json");Directory.CreateDirectory(Path.GetDirectoryName(output)!);
            var imageFile=Path.Combine(Path.GetDirectoryName(output)!,"image-check.png");
            var imageProbe=new Texture2D(2,2,TextureFormat.RGBA32,false);
            imageProbe.SetPixels(new[]{Color.red,Color.green,Color.blue,Color.white});imageProbe.Apply();
            File.WriteAllBytes(imageFile,ImageConversion.EncodeToPNG(imageProbe));Destroy(imageProbe);
            surface.ApplyImage(surface.PrepareImage(imageFile));
            model.UpdateParameter("background/_PatternMix",new ParameterValue(new NVector4(0,0,0,0)));model.Commit();
            surface.Draw(model,snapshot,false);
            Assert(GpuHash(surface.Material)!=baseline,"validated local image reaches GPU material");
            surface.ApplyImage(null);model.Reset();surface.Draw(model,snapshot,false);
            Assert(GpuHash(surface.Material)==baseline,"clearing image restores procedural baseline");
            var file=Path.Combine(Path.GetDirectoryName(output)!,"saved-"+Guid.NewGuid().ToString("N")+".vfx.json");
            model.UpdateParameter("background/_Brightness",new ParameterValue(new NVector4(1.4f,0,0,0)));model.Commit();
            var saved=Store().Save(model.Session,file,model.Session.Revision);
            Assert(!model.Session.IsDirty,"save marks current artwork clean");
            model.UpdateParameter("background/_Brightness",new ParameterValue(new NVector4(1.6f,0,0,0)));model.Commit();
            Store().Save(model.Session,file,model.Session.Revision,saved.FileVersion);
            Assert(!model.Session.IsDirty,"verified overwrite uses the Mono atomic-replace path");
            var reopened=Store().Open(file);model.Replace(reopened.Session);surface.Draw(model,snapshot,false);
            Assert(Math.Abs(surface.Material.GetFloat("_Brightness")-1.6)<.0001,"saved project reopens through host JSON codec");
            model.Reset();surface.Draw(model,snapshot,false);
            var future=new PlaybackSnapshot(snapshot.SessionGeneration,"studio-preview",0,"",2,2,0,1,
                TransportState.Frozen,PlaybackPhase.Paused,true,false,false,31,true,3);
            surface.Draw(model,future,false);string futureHash=GpuHash(surface.Material);hashes["seek2s"]=futureHash;
            Assert(futureHash!=baseline,"paused seek updates time-driven GPU output");
            surface.Draw(model,future,false);
            Assert(GpuHash(surface.Material)==futureHash,"paused timestamp is deterministic");
            for(int i=0;i<20;i++) {
                model.UpdateParameter("background/_Brightness",new ParameterValue(new NVector4(1+i*.01f,0,0,0)));
                surface.Draw(model,future,false);
            }
            Assert(surface.Material.GetInstanceID()==material,"parameter updates reuse one material and bundle");model.Cancel();
            button=new Rect(60,60,100,32);
            bool blocked=(bool)typeof(InFalsusStudio.StudioBootstrap).GetMethod("WorkspaceBlocksInput",StudioBridge.Flags)!
                .Invoke(Bridge.Owner,new object[]{button.center})!;
            Assert(blocked,"editor panel blocks host authoring input");
            Assert(!Bridge.Transport!.Playing,"checks never started audio transport");
            CheckLifecycle(Assert,Path.GetDirectoryName(output)!);
            CheckTimelineAndEffects(Assert,Path.GetDirectoryName(output)!,hashes);
            CheckShortcutsAndStarts(Assert,Path.GetDirectoryName(output)!);
            CheckEffectInstances(Assert,Path.GetDirectoryName(output)!);
            CheckAuthoringWorkflow(Assert,Path.GetDirectoryName(output)!);
            CheckCurveEditing(Assert,Path.GetDirectoryName(output)!);
            CheckBackgroundControls(Assert,Path.GetDirectoryName(output)!);
            var report=new {ok=true,checks,hashes,unity=Application.unityVersion,colorSpace=QualitySettings.activeColorSpace.ToString(),
                graphics=SystemInfo.graphicsDeviceType.ToString(),uiVisuallyReviewed=false,audioPlayed=false,gameStarted=false};
            File.WriteAllText(output,JsonConvert.SerializeObject(report,Json));
            Logger.LogInfo("VFX checks passed: "+checks.Count);
        } catch(Exception e) {
            failure=e.ToString();Logger.LogError(e);
            var args=Environment.GetCommandLineArgs();int at=Array.IndexOf(args,"-vfx-check-output");
            if(at>=0)File.WriteAllText(args[at+1],JsonConvert.SerializeObject(new{ok=false,checks,hashes,error=failure},Json));
        }
        stopped=true;Surface?.Dispose();Surface=null;
        yield return null;
        if(failure==null) {
            // Finish through the actual asynchronous quit path, not a test-mode bypass.
            MayQuit();ResolveDecision(DecisionChoice.Discard);
        } else {
            quitPermit=true;quitPermitExpiresAfter=Time.frameCount+1;Application.Quit(2);
        }
    }
    void CheckLifecycle(Action<bool,string> assert,string output)
    {
        var savedSaveDialog=saveDialog;var savedOpenDialog=openDialog;var savedQuit=quitApplication;
        int quits=0;
        void Edit(float value) {
            Model!.UpdateParameter("background/_Brightness",new ParameterValue(new NVector4(value,0,0,0)));
            Model.Commit();
        }
        void Decide(DecisionChoice choice) { QueueDecision(choice);Update(); }
        try {
            quitApplication=()=>{quits++;assert(MayQuit(),"approved quit consumes a single permit");};
            Edit(1.25f);
            int before=decisionOpenings,callbacks=quitCallbacks;
            // Invoke the same managed dispatcher used by native Unity, without queueing process shutdown.
            var dispatch=typeof(Application).GetMethod("Internal_ApplicationWantsToQuit",StudioBridge.Flags)!;
            bool dispatcherVetoed=true;
            for(int i=0;i<3;i++)dispatcherVetoed&=!(bool)dispatch.Invoke(null,null)!;
            assert(dispatcherVetoed&&quitCallbacks>=callbacks+3,"Unity quit dispatcher reaches the nonblocking veto");
            bool allVetoed=true;
            for(int i=0;i<200;i++)allVetoed&=!MayQuit();
            assert(allVetoed,"200 repeated dirty quit requests are vetoed");
            assert(decision==PendingDecision.Quit&&decisionOpenings==before+1,"repeated close requests retain one confirmation");
            Queue(SaveProject);
            assert(actions.Count==0,"confirmation blocks underlying editor actions");
            bool blocked=false;Hooks.Block(Vector2.zero,ref blocked);
            assert(blocked&&!Hooks.Keys(new Event{type=EventType.KeyDown,keyCode=KeyCode.Space})&&!Hooks.TextInput(),
                "confirmation blocks host mouse keyboard and text handlers");
            QueueDecision(DecisionChoice.Cancel);QueueDecision(DecisionChoice.Cancel);
            assert(actions.Count==1,"confirmation buttons queue at most one resolution");Update();
            assert(decision==PendingDecision.None&&!BlocksHostInput&&Dirty,"cancel returns to editing without discarding changes");

            MayQuit();projectFile="";fileVersion=null;int saves=0;
            saveDialog=_=>{
                saves++;Update();Queue(SaveProject);
                assert(!MayQuit(),"close during a native picker is vetoed without another prompt");
                return "";
            };
            before=decisionOpenings;Decide(DecisionChoice.Save);
            assert(saves==1&&actions.Count==0&&decisionOpenings==before,"nested message pumping cannot reenter file actions");
            assert(decision==PendingDecision.Quit&&Dirty&&quits==0,"canceled save keeps confirmation and changes intact");

            var protectedFile=Path.Combine(output,"protected-"+Guid.NewGuid().ToString("N")+".json");
            File.WriteAllText(protectedFile,"external content");projectFile=protectedFile;fileVersion=null;
            Decide(DecisionChoice.Save);
            assert(decision==PendingDecision.Quit&&decisionError.Length>0&&quits==0&&File.ReadAllText(protectedFile)=="external content",
                "failed save keeps confirmation open and preserves external data");

            textParameter="background/_Brightness";textKey=textParameter+":0";drafts[textKey]="-";
            Decide(DecisionChoice.Save);
            assert(decision==PendingDecision.Quit&&quits==0&&Dirty,"invalid numeric draft cannot be discarded by save and quit");
            Decide(DecisionChoice.Cancel);
            assert(decision==PendingDecision.None&&textParameter!=null,"cancel quit preserves an invalid numeric draft");
            textParameter=textKey=null;drafts.Clear();textCommitQueued=false;

            var saveFile=Path.Combine(output,"quit-save-"+Guid.NewGuid().ToString("N")+".vfx.json");
            projectFile=saveFile;fileVersion=null;MayQuit();Decide(DecisionChoice.Save);
            assert(quits==1&&!Dirty&&decision==PendingDecision.None&&!quitPermit,
                "successful save authorizes exactly one quit and clears dirty state");
            var savedBytes=File.ReadAllBytes(saveFile);
            Edit(1.75f);assert(!MayQuit()&&decision==PendingDecision.Quit,"quit authorization does not leak into later edits");
            Decide(DecisionChoice.Discard);
            assert(quits==2&&Dirty&&File.ReadAllBytes(saveFile).SequenceEqual(savedBytes),"discard quit never overwrites the saved project");

            quitApplication=()=>{};MayQuit();Decide(DecisionChoice.Discard);
            assert(quitPermit&&BlocksHostInput,"a deferred quit temporarily gates editing");
            quitPermitExpiresAfter=Time.frameCount-1;Update();
            assert(!quitPermit&&!BlocksHostInput&&!MayQuit()&&decision==PendingDecision.Quit,
                "an unconsumed quit permit expires after another subscriber vetoes");
            Decide(DecisionChoice.Cancel);

            LoadProject();before=decisionOpenings;LoadProject();
            assert(decision==PendingDecision.OpenProject&&decisionOpenings==before,"open project reuses a single unsaved confirmation");
            int opens=0;var content=Model!.Session.Snapshot().ContentId;
            openDialog=(_,__)=>{
                opens++;Update();Queue(LoadProject);return "";
            };
            Decide(DecisionChoice.Discard);
            assert(opens==1&&actions.Count==0&&decision==PendingDecision.None&&Dirty&&Model.Session.Snapshot().ContentId==content,
                "canceled project picker preserves current artwork and cannot reenter the queue");
            LoadProject();openDialog=(_,__)=>throw new IOException("simulated picker failure");
            Decide(DecisionChoice.Discard);
            assert(decision==PendingDecision.OpenProject&&decisionError.Length>0&&Dirty,"picker errors preserve artwork and leave a usable confirmation");
            Decide(DecisionChoice.Cancel);
        } finally {
            saveDialog=savedSaveDialog;openDialog=savedOpenDialog;quitApplication=savedQuit;
            decision=PendingDecision.None;decisionQueued=false;quitPermit=false;actions.Clear();
        }
    }
    void RenderStageForCheck()
    {
        // Batch mode has no interactive repaint loop. Exercise the actual stage camera explicitly.
        var camera=Bridge.Owner!.ViewCamera;var target=new RenderTexture(96,96,24,RenderTextureFormat.ARGB32);target.Create();
        var oldTarget=camera.targetTexture;var oldRect=camera.rect;
        try {
            camera.targetTexture=target;camera.rect=new Rect(0,0,1,1);
            RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=target});
        } finally {camera.targetTexture=oldTarget;camera.rect=oldRect;target.Release();DestroyImmediate(target);}
    }
    static string GpuHash(Material material)
    {
        var cameraObject=new GameObject("VFX isolated GPU check camera");var camera=cameraObject.AddComponent<Camera>();
        camera.enabled=false;camera.orthographic=true;camera.orthographicSize=.5f;
        camera.transform.position=new Vector3(0,0,-2);camera.nearClipPlane=.1f;camera.farClipPlane=10;
        camera.cullingMask=1<<29;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.black;camera.allowHDR=false;
        camera.GetUniversalAdditionalCameraData().renderPostProcessing=false;
        var quad=new GameObject("VFX isolated GPU check plane",typeof(MeshFilter),typeof(MeshRenderer)){layer=29};
        var mesh=new Mesh();mesh.vertices=new[]{new Vector3(-.5f,-.5f,0),new Vector3(.5f,-.5f,0),new Vector3(.5f,.5f,0),new Vector3(-.5f,.5f,0)};
        mesh.uv=new[]{Vector2.zero,Vector2.right,Vector2.one,Vector2.up};mesh.triangles=new[]{0,2,1,0,3,2};mesh.RecalculateBounds();
        quad.GetComponent<MeshFilter>().sharedMesh=mesh;quad.GetComponent<MeshRenderer>().sharedMaterial=material;
        var target=new RenderTexture(96,96,24,RenderTextureFormat.ARGB32);target.Create();
        var pixels=new Texture2D(96,96,TextureFormat.RGBA32,false);var previous=RenderTexture.active;
        try {
            var request=new UniversalRenderPipeline.SingleCameraRequest{destination=target};
            if(!RenderPipeline.SupportsRenderRequest(camera,request))throw new NotSupportedException("Camera render request unsupported");
            RenderPipeline.SubmitRenderRequest(camera,request);
            RenderTexture.active=target;pixels.ReadPixels(new Rect(0,0,96,96),0,0,false);
            using var sha=SHA256.Create();return BitConverter.ToString(sha.ComputeHash(pixels.GetRawTextureData())).Replace("-","");
        } finally {
            RenderTexture.active=previous;target.Release();DestroyImmediate(target);DestroyImmediate(pixels);
            DestroyImmediate(quad);DestroyImmediate(mesh);DestroyImmediate(cameraObject);
        }
    }
}
