using System.Reflection;
using UnityEngine;

namespace InFalsus.Vfx.StudioPlus;

/// <summary>Use Studio+'s existing drawing components without sharing its chart input drafts.</summary>
internal sealed class StudioUiTheme
{
    const string ChineseFace = "Chiron Hei HK N", NumberFace = "Bai Jamjuree Medium";
    readonly object owner;
    readonly FieldInfo skinField, scaleField;
    readonly Action setup;
    readonly Action<Rect, string, float, Color, string, int> text;
    readonly Action<Rect, string, float, Color> paragraph;
    readonly Func<Rect, string, float, bool, bool, bool> button;
    readonly Func<Rect, string, string, int, int, int, string, float, int> hitIndex;
    readonly PaintInput paintInput;
    readonly Func<float, int> fontPixels;
    readonly Func<string, string, int, int> measure;
    readonly Func<string,string,int,float,string[]> wrap;
    delegate void PaintInput(Rect rect, string key, string value, string face, int pixels, int align,
        int padding, bool focused, TextEditor? editor, Color ink, float? anchorTop);
    GUIStyle? input;
    string? pointerKey;
    int pointerAnchor;
    float hostScale;
    internal float? LayoutScale;
    internal float BaseScale=>Mathf.Max(.8f,HostScale);
    internal float Scale => LayoutScale??BaseScale;
    float HostScale => hostScale;
    internal GUISkin Skin => (GUISkin)skinField.GetValue(owner)!;

    internal static StudioUiTheme? TryBind()
    {
        var type = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == "InfStudioVisualUI")
            ?.GetType("InFalsusStudio.NativeEditorUI.NativeEditorUIPlugin");
        var host = type?.GetField("I", StudioBridge.Flags)?.GetValue(null);
        return host is UnityEngine.Object obj && obj && (bool)type!.GetField("ready", StudioBridge.Flags)!.GetValue(host)!
            ? new StudioUiTheme(type, host) : null;
    }

    StudioUiTheme(Type type, object host)
    {
        owner = host;
        T Bind<T>(string name) where T : Delegate => (T)Delegate.CreateDelegate(typeof(T), host,
            type.GetMethod(name, StudioBridge.Flags) ?? throw new MissingMethodException(type.FullName, name));
        setup = Bind<Action>("SetupSkin");
        text = Bind<Action<Rect, string, float, Color, string, int>>("UiText");
        paragraph = Bind<Action<Rect, string, float, Color>>("UiParagraph");
        button = Bind<Func<Rect, string, float, bool, bool, bool>>("FlatButton");
        paintInput = Bind<PaintInput>("PaintInput");
        hitIndex = Bind<Func<Rect, string, string, int, int, int, string, float, int>>("InputHitIndex");
        fontPixels = Bind<Func<float, int>>("FontPixels");
        skinField = type.GetField("skin", StudioBridge.Flags)!;
        scaleField = type.GetField("scale", StudioBridge.Flags)!;
        hostScale = Mathf.Max(.25f, (float)scaleField.GetValue(owner)!);
        var raster = type.Assembly.GetType("InFalsusStudio.NativeEditorUI.UiFontRaster")!;
        measure = (Func<string, string, int, int>)Delegate.CreateDelegate(typeof(Func<string, string, int, int>),
            raster.GetMethod("Measure", StudioBridge.Flags)!);
        wrap=(Func<string,string,int,float,string[]>)Delegate.CreateDelegate(typeof(Func<string,string,int,float,string[]>),raster.GetMethod("Wrap",StudioBridge.Flags)!);
    }

    internal void Use() { hostScale = Mathf.Max(.25f, (float)scaleField.GetValue(owner)!); setup(); }
    internal bool HasLiveHost => owner is UnityEngine.Object obj && obj;
    internal bool CheckTextMetrics() => measure("0.95", NumberFace, 18) > 0 && measure("演出参数", ChineseFace, 16) > 0;
    float DesignSize(float pixels) => pixels * Scale / HostScale;
    internal static Color ColorOf(string hex) { ColorUtility.TryParseHtmlString(hex, out var color); return color; }
    internal static void Fill(Rect rect, string hex)
    {
        if (Event.current.type != EventType.Repaint) return;
        var old = GUI.color;
        try { GUI.color = old * ColorOf(hex); GUI.DrawTexture(rect, Texture2D.whiteTexture); }
        finally { GUI.color = old; }
    }
    internal void Label(Rect rect, string value, float size = 16, string color = "#a6c3d1", bool numeric = false, int align = 0)
        => text(rect, value, DesignSize(size), ColorOf(color), numeric ? NumberFace : ChineseFace, align);
    internal string Fit(string value,float width,float size=13)
    {
        int pixels=fontPixels(DesignSize(size));if(measure(value,ChineseFace,pixels)<=width)return value;
        int lo=0,hi=value.Length;while(lo<hi){int mid=(lo+hi+1)/2;if(measure(value.Substring(0,mid)+"…",ChineseFace,pixels)<=width)lo=mid;else hi=mid-1;}
        if(lo>0&&char.IsHighSurrogate(value[lo-1]))lo--;return value.Substring(0,lo)+"…";
    }
    internal bool Button(Rect rect, string value, bool active = false, bool boxed = true, float size = 15)
    {
        bool pressed=button(rect,value,DesignSize(size),active,boxed);
        if(pressed)GUI.FocusControl(null);
        return pressed;
    }
    internal void Paragraph(Rect rect, string value, float size = 13, string color = "#7c9dad")
        => paragraph(rect, value, DesignSize(size), ColorOf(color));
    internal float ParagraphHeight(string value,float width,float size=13)
    {
        int pixels=fontPixels(DesignSize(size));return Math.Max(1,wrap(value,ChineseFace,pixels,width).Length)*(pixels+3);
    }
    internal void Panel(Rect rect)
    {
        Fill(rect, "#0c202bfa");
        Fill(new Rect(rect.x, rect.y, Mathf.Max(1, HostScale), rect.height), "#48636f");
    }
    internal void Rule(Rect rect) { rect.height = Mathf.Max(1, rect.height); Fill(rect, "#2c4652"); }
    internal bool Toggle(Rect rect, string title, bool enabled)
    {
        bool pressed = GUI.Button(rect, GUIContent.none, GUIStyle.none);
        Label(rect, title);
        var track = new Rect(rect.xMax - 32 * Scale, rect.center.y - 8 * Scale, 32 * Scale, 16 * Scale);
        Fill(track, enabled ? "#6aacb4" : "#375665");
        Fill(new Rect(track.x + Scale, track.y + Scale, track.width - 2 * Scale, track.height - 2 * Scale), "#0c202b");
        Fill(new Rect(enabled ? track.xMax - 11 * Scale : track.x + 3 * Scale, track.y + 3 * Scale, 8 * Scale, 10 * Scale),
            enabled ? "#bcf7f5" : "#577582");
        return pressed ? !enabled : enabled;
    }

    internal string Number(Rect rect,string key,string value)=>Field(rect,key,value,true);
    internal string Text(Rect rect,string key,string value)=>Field(rect,key,value,false);
    string Field(Rect rect, string key, string value,bool numeric)
    {
        string face=numeric?NumberFace:ChineseFace;int align=numeric?2:0;
        if (input == null) {
            input = new GUIStyle(Skin.textField) { border = new RectOffset(), margin = new RectOffset(),
                padding = new RectOffset(5, 5, 0, 0), alignment = TextAnchor.MiddleRight, fixedHeight = 0 };
            foreach (var state in new[] { input.normal, input.hover, input.active, input.focused,
                         input.onNormal, input.onHover, input.onActive, input.onFocused }) {
                state.background = null; state.textColor = Color.clear;
            }
        }
        int pixels = fontPixels(DesignSize(19)), padding = Mathf.RoundToInt(5 * Scale);
        input.fontSize = pixels; input.padding.left = input.padding.right = padding;
        input.alignment=numeric?TextAnchor.MiddleRight:TextAnchor.MiddleLeft;
        bool wasFocused = GUI.GetNameOfFocusedControl() == key;
        var e = Event.current; var kind = e.type; var mouse = e.mousePosition; bool shift = e.shift;
        var caret = Skin.settings.cursorColor; var selection = Skin.settings.selectionColor;
        string next;
        try {
            Skin.settings.cursorColor = Skin.settings.selectionColor = Color.clear;
            GUI.SetNextControlName(key); next = GUI.TextField(rect, value, input);
        } finally { Skin.settings.cursorColor = caret; Skin.settings.selectionColor = selection; }
        bool focused = GUI.GetNameOfFocusedControl() == key;
        var editor = focused ? (TextEditor)GUIUtility.GetStateObject(typeof(TextEditor), GUIUtility.keyboardControl) : null;
        if (focused && kind == EventType.MouseDown && rect.Contains(mouse)) {
            if (!wasFocused || e.clickCount > 1) editor!.SelectAll();
            else {
                editor!.cursorIndex = hitIndex(rect, next, face, pixels, align, padding, key, mouse.x);
                if (!shift) editor.selectIndex = editor.cursorIndex;
            }
            pointerKey = key; pointerAnchor = editor!.selectIndex;
        } else if (focused && kind == EventType.MouseDrag && pointerKey == key) {
            editor!.selectIndex = pointerAnchor;
            editor.cursorIndex = hitIndex(rect, next, face, pixels, align, padding, key, mouse.x);
        }
        if (kind == EventType.MouseUp && pointerKey == key) pointerKey = null;
        Fill(rect, "#091923");
        Fill(new Rect(rect.x, rect.yMax - Scale, rect.width, Scale), focused ? "#b4efef" : "#4b7481");
        paintInput(rect, key, next, face, pixels, align, padding, focused, editor, ColorOf("#d3edf0"), null);
        return next;
    }
}
