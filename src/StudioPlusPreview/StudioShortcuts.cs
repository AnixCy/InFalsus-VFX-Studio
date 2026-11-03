using System.Reflection;
using UnityEngine;

namespace InFalsus.Vfx.StudioPlus;

// Resolve actions against the live chart-editor table, including aliases and rebinding.
// Do not keep a second configuration or call the chart action dispatcher.
internal sealed class StudioShortcuts
{
    internal readonly object Host, Table;
    readonly MethodInfo fromEvent, find, label;
    readonly FieldInfo actionId;
    StudioShortcuts(Type type, object host)
    {
        Host=host;Table=type.GetField("hotkeys",StudioBridge.Flags)!.GetValue(host)!;
        var chord=type.Assembly.GetType("InFalsusStudio.NativeEditorUI.HotkeyChord")!;
        fromEvent=chord.GetMethod("From",StudioBridge.Flags)!;
        find=Table.GetType().GetMethod("Find",StudioBridge.Flags)!;
        actionId=find.ReturnType.GetField("Id",StudioBridge.Flags)!;
        label=type.GetMethod("HotkeyLabel",StudioBridge.Flags)!;
    }
    internal static StudioShortcuts? TryBind()
    {
        var type=AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a=>a.GetName().Name=="InfStudioVisualUI")
            ?.GetType("InFalsusStudio.NativeEditorUI.NativeEditorUIPlugin");
        var host=type?.GetField("I",StudioBridge.Flags)?.GetValue(null);
        return host is UnityEngine.Object obj&&obj&&(bool)type!.GetField("ready",StudioBridge.Flags)!.GetValue(host)!
            ?new StudioShortcuts(type,host):null;
    }
    internal string? Find(Event e)
    {
        var action=find.Invoke(Table,new[]{fromEvent.Invoke(null,new object[]{e})});
        return action==null?null:(string)actionId.GetValue(action)!;
    }
    internal string Label(string id)=>(string)label.Invoke(Host,new object[]{id})!;
}
