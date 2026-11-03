using HarmonyLib;
using InFalsus.Presentation;
using InFalsus.Presentation.Authoring;
using Newtonsoft.Json;
using UnityEngine;
using NVector4=System.Numerics.Vector4;

namespace InFalsus.Vfx.StudioPlus;

public sealed partial class PreviewPlugin
{
    void CheckShortcutsAndStarts(Action<bool,string> check,string output)
    {
        shortcuts=StudioShortcuts.TryBind();check(shortcuts!=null,"shortcuts bind to the live Studio+ hotkey table");
        var table=shortcuts!.Table;var tableType=table.GetType();
        var snapshot=tableType.GetMethod("Snapshot",StudioBridge.Flags)!.Invoke(table,null)!;
        var previousModel=Model;var previousKey=selectedKey;bool previousView=Bridge.AuthoringView;
        var chartHistory=Bridge.Owner!.History;
        var model=new PreviewModel(folder,Application.unityVersion,QualitySettings.activeColorSpace.ToString(),legacy:true);
        string id="adjust/_Fade";
        float Sample(double time){model.Sample(time);return model.Values[model.Catalog.Parameters.ToList().FindIndex(p=>p.Id==id)].Number.X;}
        Event Key(KeyCode code,bool ctrl=false,bool shift=false,EventType type=EventType.KeyDown)=>
            new(){type=type,keyCode=code,modifiers=(ctrl?EventModifiers.Control:0)|(shift?EventModifiers.Shift:0)};
        void Send(Event e,bool text=false){RouteWorkspaceEvent(e,text);Update();}
        void Bind(string action,KeyCode key,bool ctrl=false) {
            var chordType=tableType.Assembly.GetType("InFalsusStudio.NativeEditorUI.HotkeyChord")!;
            var chord=chordType.GetMethod("From",StudioBridge.Flags)!.Invoke(null,new object[]{Key(key,ctrl)});
            object?[] args={action,chord,null};
            if(!(bool)tableType.GetMethod("Assign",StudioBridge.Flags)!.Invoke(table,args)!)throw new Exception((string?)args[2]);
        }
        try {
            tableType.GetMethod("Reset",StudioBridge.Flags)!.Invoke(table,null);
            Model=model;selectedKey="";Bridge.SetView(true);Bridge.SeekSeconds(0);
            var info=Harmony.GetPatchInfo(typeof(InFalsusStudio.StudioBootstrap).GetMethod("OnGUI",StudioBridge.Flags)!);
            int own=info.Prefixes.Single(p=>p.owner==harmony!.Id&&p.PatchMethod.Name==nameof(Hooks.GuiBefore)).priority;
            check(info.Prefixes.Where(p=>p.owner!=harmony!.Id).All(p=>p.priority<own),
                "authoring GUI gate runs before the native shortcut capture prefix");
            var k=model.UpdateAnimatedParameter(id,new ParameterValue(new NVector4(1,0,0,0)),10,null,true);model.Commit();
            check(model.Keys.Length==2&&Math.Abs(Sample(5)-.5)<.0001,"first automatic animation includes a zero-time default anchor and interpolates into its first target");
            Send(Key(KeyCode.Z,true));check(model.Keys.Length==0,"shared undo shortcut removes the animated edit and its anchor together");
            Send(Key(KeyCode.Y,true));check(model.Keys.Length==2&&model.Keys.Any(x=>x.Id==k),"shared Ctrl+Y redo restores stable animated key identities");
            Send(Key(KeyCode.Z,true));Send(Key(KeyCode.Z,true,true));
            check(model.Keys.Length==2,"shared Ctrl+Shift+Z alias performs redo");
            Bind("undo",KeyCode.U,true);long revision=model.Session.Revision;
            Send(Key(KeyCode.Z,true));check(model.Session.Revision==revision,"old undo chord stops triggering after a live chart-editor rebind");
            Send(Key(KeyCode.U,true));check(model.Keys.Length==0,"authoring undo immediately follows the shared custom binding");
            Send(Key(KeyCode.Y,true));revision=model.Session.Revision;
            Send(Key(KeyCode.U,true),true);check(model.Session.Revision==revision,"numeric text focus retains text editing shortcuts");
            check(shortcuts.Find(Key(KeyCode.S,true))=="save"&&shortcuts.Find(Key(KeyCode.S,true,true))=="saveAs"&&shortcuts.Find(Key(KeyCode.O,true))=="open",
                "save save-as and open resolve through the same chart action table");
            selectedKey=k;Send(Key(KeyCode.Delete));check(!model.Keys.Any(x=>x.Id==k),"shared delete removes the selected animation key");
            Send(Key(KeyCode.U,true));check(model.Keys.Any(x=>x.Id==k),"shared undo restores a deleted animation key");
            int before=model.Keys.Length;
            model.UpdateAnimatedParameter("adjust/_Chroma",new ParameterValue(new NVector4(.02f,0,0,0)),5,null,true);model.Cancel();
            check(model.Keys.Length==before&&!model.Keys.Any(x=>x.ParameterId=="adjust/_Chroma"),"canceling a first animation discards both target and default anchor");
            var manual=model.AddKey("adjust/_Exposure",8);
            check(model.Keys.Any(x=>x.ParameterId=="adjust/_Exposure"&&x.Seconds==0),"manual first key insertion also creates the default anchor");
            model.Undo();check(!model.Keys.Any(x=>x.Id==manual||x.ParameterId=="adjust/_Exposure"),"one undo removes both keys of a first manual insertion");
            Bind("previewToggle",KeyCode.F6);
            Send(Key(KeyCode.Q));check(Bridge.Snapshot().Transport==TransportState.Frozen,"old preview chord is inactive after rebinding");
            Send(Key(KeyCode.F6));Send(Key(KeyCode.F6));
            check(Bridge.Snapshot().Transport==TransportState.Running&&!Bridge.Transport!.Playing,"custom preview chord starts silent transport once despite key repeat");
            Send(Key(KeyCode.F6,type:EventType.KeyUp));Send(Key(KeyCode.F6));Send(Key(KeyCode.F6,type:EventType.KeyUp));
            check(Bridge.Snapshot().Transport==TransportState.Frozen,"custom preview chord toggles pause after release");
            Bridge.SeekSeconds(3);Send(Key(KeyCode.Space));Send(Key(KeyCode.Space));
            check(heldPreviewKey==KeyCode.Space&&Bridge.Snapshot().Transport==TransportState.Running,"shared hold-preview binding plays without requiring a chart");
            Send(Key(KeyCode.Space,type:EventType.KeyUp));
            check(!heldPreviewKey.HasValue&&Bridge.Snapshot().Transport==TransportState.Frozen&&Math.Abs(Bridge.Snapshot().ChartSeconds-3)<.001,
                "hold-preview release pauses and returns to its exact starting time");
            Send(Key(KeyCode.Space));OnApplicationFocus(false);
            check(!heldPreviewKey.HasValue&&Bridge.Snapshot().Transport==TransportState.Frozen,"focus loss safely stops held preview");
            Bridge.SeekSeconds(7);RouteWorkspaceEvent(Key(KeyCode.Space),false);OnApplicationFocus(false);Update();
            check(!heldPreviewKey.HasValue&&Bridge.Snapshot().Transport==TransportState.Frozen&&Math.Abs(Bridge.Snapshot().ChartSeconds-7)<.001,
                "focus loss before queued preview starts cancels it without seeking or later playing");
            RouteWorkspaceEvent(Key(KeyCode.Space),false);ShowDecision(PendingDecision.OpenProject);Update();
            check(!heldPreviewKey.HasValue&&Bridge.Snapshot().Transport==TransportState.Frozen&&actions.Count==0,
                "opening an unsaved confirmation cancels pending held-preview input");
            ResolveDecision(DecisionChoice.Cancel);
            check(ReferenceEquals(chartHistory,Bridge.Owner.History),"authoring shortcuts never replace the chart undo history");
            CheckProjectStartFixture(check,output);
        } finally {
            tableType.GetMethod("Restore",StudioBridge.Flags)!.Invoke(table,new[]{snapshot});
            StopHeldPreview();playbackKeysDown.Clear();actions.Clear();Bridge.SeekSeconds(0);Bridge.SetView(previousView);
            Model=previousModel;selectedKey=previousKey;
        }
    }
    void CheckProjectStartFixture(Action<bool,string> check,string output)
    {
        var args=Environment.GetCommandLineArgs();int at=Array.IndexOf(args,"-vfx-project-fixture");if(at<0)return;
        string file=args[at+1];var original=File.ReadAllBytes(file);var data=Decode(File.ReadAllText(file));
        var store=new ProjectFileStore(p=>JsonConvert.SerializeObject(new ProjectFile{Project=p,BackgroundPath=data.BackgroundPath,AudioPath=data.AudioPath},Json),
            text=>Decode(text).Project,PreviewModel.Capabilities);
        var opened=store.Open(file);var fixture=new PreviewModel(folder,Application.unityVersion,QualitySettings.activeColorSpace.ToString(),legacy:true);
        fixture.Replace(opened.Session);var keys=fixture.Keys.ToArray();int missing=fixture.MissingAnimationStarts;
        check(missing>0,"read-only fixture reproduces continuous tracks without starting anchors");
        check(fixture.RepairAnimationStarts()==missing&&fixture.MissingAnimationStarts==0,"explicit repair fills all missing continuous starts in one transaction");
        bool ramps=true;
        foreach(var first in keys.GroupBy(k=>k.ParameterId).Select(g=>g.OrderBy(k=>k.Seconds).First())) {
            if(PreviewModel.IsSwitch(first.ParameterId)||first.Seconds<=0||first.Ease==Easing.Hold)continue;
            int index=fixture.Catalog.Parameters.ToList().FindIndex(p=>p.Id==first.ParameterId);
            var parameter=fixture.Catalog.Parameters[index];fixture.Sample(first.Seconds*.5);
            var expected=NVector4.Lerp(parameter.DefaultValue.Number,first.Value.Number,.5f);
            ramps&=NVector4.Distance(fixture.Values[index].Number,expected)<.0001;
        }
        check(ramps,"fixture parameters interpolate from their defaults before the original first target");
        check(keys.All(k=>fixture.Keys.Any(n=>n.Id==k.Id&&n.Seconds==k.Seconds&&n.Value==k.Value&&n.Ease==k.Ease)),
            "repair preserves every original key identity time value and outgoing easing");
        fixture.Undo();check(fixture.MissingAnimationStarts==missing&&fixture.Keys.Select(k=>k.Id).SequenceEqual(keys.Select(k=>k.Id)),
            "one undo restores the exact original fixture key sequence");
        fixture.Redo();string repaired=Path.Combine(output,"animation-starts-fixed.vfx.json");
        store.Save(fixture.Session,repaired,fixture.Session.Revision);
        check(store.Open(repaired).Session.Snapshot().Tracks.Sum(t=>t.KeyIds.Length)==keys.Length+missing,"repaired project copy saves and reopens with all animation keys");
        check(File.ReadAllBytes(file).SequenceEqual(original),"fixture repair never writes to the user's original project");
    }
}
