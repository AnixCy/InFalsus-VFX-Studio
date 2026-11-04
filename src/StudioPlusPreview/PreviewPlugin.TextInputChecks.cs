using HarmonyLib;
using UnityEngine;

namespace InFalsus.Vfx.StudioPlus;
public sealed partial class PreviewPlugin
{
    void CheckTextDelete(Action<bool,string> check)
    {
        var previous=Model;bool view=Bridge.AuthoringView;var oldKey=selectedKey;
        var host=StudioShortcuts.TryBind()??throw new InvalidOperationException("Native shortcuts unavailable.");
        var table=host.Table;var type=table.GetType();var bindings=type.GetMethod("Snapshot",StudioBridge.Flags)!.Invoke(table,null);
        var route=host.Host.GetType().GetMethod("RouteHotkey",StudioBridge.Flags)!;
        try {
            Model=new PreviewModel(folder,Application.unityVersion,QualitySettings.activeColorSpace.ToString());
            var key=Model.AddKey("adjust/_Exposure",1);selectedKey=key;
            var chord=type.Assembly.GetType("InFalsusStudio.NativeEditorUI.HotkeyChord")!.GetMethod("From",StudioBridge.Flags)!
                .Invoke(null,new object[]{new Event{type=EventType.KeyDown,keyCode=KeyCode.X}});
            object?[] assign={"delete",chord,null};
            if(!(bool)type.GetMethod("Assign",StudioBridge.Flags)!.Invoke(table,assign)!)throw new InvalidOperationException((string?)assign[2]);
            Bridge.SetView(true);
            var prefix=typeof(Hooks).GetMethod(nameof(Hooks.NativeShortcuts))!;
            harmony!.Unpatch(route,prefix);
            try {
                var original=new Event{type=EventType.KeyDown,keyCode=KeyCode.Backspace};object?[] originalArgs={original,false};
                check((bool)route.Invoke(host.Host,originalArgs)!&&original.type==EventType.Used,"unpatched native router reproduces swallowed Backspace after delete is rebound to X");
            }finally{harmony.Patch(route,new HarmonyMethod(prefix){priority=Priority.First});}
            check(Harmony.GetPatchInfo(route).Prefixes.Any(p=>p.owner==harmony!.Id&&p.PatchMethod.Name==nameof(Hooks.NativeShortcuts)),"native hotkey filter is integrated with authoring/text ownership");
            var editor=new TextEditor{text="Feedback",cursorIndex=8,selectIndex=8};
            Event Key(KeyCode code,bool control=false)=>code is KeyCode.Backspace or KeyCode.Delete?Event.KeyboardEvent(code==KeyCode.Backspace?"backspace":"delete"):new(){type=EventType.KeyDown,keyCode=code,modifiers=control?EventModifiers.Control:EventModifiers.None};
            void Native(Event e){object?[] args={e,false};check(!(bool)route.Invoke(host.Host,args)!&&e.type==EventType.KeyDown,"native chart filter leaves authoring text key available: "+e.keyCode);}
            var backspace=Key(KeyCode.Backspace);Native(backspace);RouteWorkspaceEvent(backspace,true);
            bool edited=editor.HandleKeyEvent(backspace);check(edited&&editor.text=="Feedbac","Unity text editing deletes backward after chart delete is rebound to X: handled="+edited+", text="+editor.text+", cursor="+editor.cursorIndex+", selection="+editor.selectIndex);
            editor.cursorIndex=editor.selectIndex=0;var delete=Key(KeyCode.Delete);Native(delete);RouteWorkspaceEvent(delete,true);
            check(editor.HandleKeyEvent(delete)&&editor.text=="eedbac","Unity text editing deletes forward after chart delete is rebound to X");
            editor.cursorIndex=1;editor.selectIndex=4;backspace=Key(KeyCode.Backspace);Native(backspace);
            check(editor.HandleKeyEvent(backspace)&&editor.text=="eac","Backspace deletes a selected text range");
            editor.SelectAll();delete=Key(KeyCode.Delete);Native(delete);
            check(editor.HandleKeyEvent(delete)&&editor.text=="","Delete clears selected filter text");
            var x=Key(KeyCode.X);Native(x);RouteWorkspaceEvent(x,true);Update();
            check(x.type==EventType.KeyDown&&Model.Keys.Any(k=>k.Id==key),"rebound X remains available as text and cannot delete animation keys while text is focused");
            RouteWorkspaceEvent(Key(KeyCode.X),false);Update();
            check(!Model.Keys.Any(k=>k.Id==key),"rebound X still deletes an animation key outside text input");
        }finally{type.GetMethod("Restore",StudioBridge.Flags)!.Invoke(table,new[]{bindings});Model=previous;selectedKey=oldKey;Bridge.SetView(view);actions.Clear();}
    }
}
