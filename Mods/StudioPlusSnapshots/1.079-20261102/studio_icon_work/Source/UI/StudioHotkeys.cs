using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace InFalsusStudio.NativeEditorUI {
 internal struct HotkeyChord:IEquatable<HotkeyChord> {
  internal KeyCode Key;internal bool Ctrl,Alt,Shift;
  internal HotkeyChord(KeyCode key,bool ctrl=false,bool alt=false,bool shift=false){Key=key==KeyCode.RightAlt?KeyCode.LeftAlt:key;Ctrl=ctrl;Alt=alt&&Key!=KeyCode.LeftAlt;Shift=shift;}
  internal static bool MouseKey(KeyCode key){return key>=KeyCode.Mouse2&&key<=KeyCode.Mouse6;}
  internal static bool MouseEvent(Event e){return (e.type==EventType.MouseDown||e.type==EventType.MouseUp)&&e.button>=2&&e.button<=6;}
  internal static HotkeyChord From(Event e){return new HotkeyChord(MouseEvent(e)?(KeyCode)((int)KeyCode.Mouse0+e.button):e.keyCode,e.control||e.command,e.alt,e.shift);}
  internal Event Event(EventType type){return new Event{type=type,keyCode=Key,modifiers=(Ctrl?EventModifiers.Control:0)|(Alt?EventModifiers.Alt:0)|(Shift?EventModifiers.Shift:0)};}
  public bool Equals(HotkeyChord b){return Key==b.Key&&Ctrl==b.Ctrl&&Alt==b.Alt&&Shift==b.Shift;}
  public override bool Equals(object b){return b is HotkeyChord&&Equals((HotkeyChord)b);}public override int GetHashCode(){return (int)Key+(Ctrl?1024:0)+(Alt?2048:0)+(Shift?4096:0);}
  public override string ToString(){if(Key==KeyCode.None)return "未绑定";string key=Key.ToString();if(key.StartsWith("Alpha"))key=key.Substring(5);if(Key==KeyCode.LeftAlt)key="Alt";if(Key==KeyCode.Space)key="Space";if(Key==KeyCode.Mouse2)key="鼠标中键";else if(MouseKey(Key))key="鼠标侧键 "+((int)Key-(int)KeyCode.Mouse2);return (Ctrl?"Ctrl+":"")+(Alt?"Alt+":"")+(Shift?"Shift+":"")+key;}
  internal static bool Modifier(KeyCode key){return key==KeyCode.None||key==KeyCode.LeftControl||key==KeyCode.RightControl||key==KeyCode.LeftShift||key==KeyCode.RightShift||key==KeyCode.LeftAlt||key==KeyCode.RightAlt||key==KeyCode.LeftCommand||key==KeyCode.RightCommand||key==KeyCode.AltGr;}
  internal static bool Allowed(HotkeyChord c){if(c.Key==KeyCode.None)return !c.Ctrl&&!c.Alt&&!c.Shift;return Enum.IsDefined(typeof(KeyCode),c.Key)&&(!Modifier(c.Key)||c.Key==KeyCode.LeftAlt)&&((int)c.Key<(int)KeyCode.Mouse0||MouseKey(c.Key))&&c.Key!=KeyCode.Escape&&c.Key!=KeyCode.Return&&c.Key!=KeyCode.KeypadEnter&&c.Key!=KeyCode.F11&&c.Key!=KeyCode.LeftArrow&&c.Key!=KeyCode.RightArrow&&c.Key!=KeyCode.UpArrow&&c.Key!=KeyCode.DownArrow;}
 }
 internal sealed class HotkeyAction {
  internal readonly string Id,Name,Section;internal readonly HotkeyChord Default;internal readonly HotkeyChord[] Aliases;
  internal HotkeyAction(string id,string name,string section,HotkeyChord key,params HotkeyChord[] aliases){Id=id;Name=name;Section=section;Default=key;Aliases=aliases;}
 }
 internal sealed class StudioHotkeys {
  internal static readonly HotkeyAction[] Actions={
   new HotkeyAction("open","打开谱面 / 歌曲","工程",new HotkeyChord(KeyCode.O,true)),
   new HotkeyAction("save","保存","工程",new HotkeyChord(KeyCode.S,true)),
   new HotkeyAction("saveAs","另存为","工程",new HotkeyChord(KeyCode.S,true,false,true)),
   new HotkeyAction("previewHold","按住预览 · 松开返回","播放与视图",new HotkeyChord(KeyCode.Space)),
   new HotkeyAction("previewToggle","播放 / 暂停预览 · 不返回","播放与视图",new HotkeyChord(KeyCode.Q)),
   new HotkeyAction("gameplayStart", "内置游玩（Esc 退出）", "播放与视图", new HotkeyChord(KeyCode.P)),
   new HotkeyAction("grid","网格线开关","播放与视图",new HotkeyChord(KeyCode.Tab)),
   new HotkeyAction("overlap","切换重叠候选","编辑",new HotkeyChord(KeyCode.H)),
   new HotkeyAction("ui","显示 / 隐藏界面","播放与视图",new HotkeyChord(KeyCode.F1)),
   new HotkeyAction("undo","撤销","编辑",new HotkeyChord(KeyCode.Z,true)),
   new HotkeyAction("redo","重做","编辑",new HotkeyChord(KeyCode.Y,true),new HotkeyChord(KeyCode.Z,true,false,true)),
   new HotkeyAction("copy","复制","编辑",new HotkeyChord(KeyCode.C,true)),
   new HotkeyAction("cut","剪切","编辑",new HotkeyChord(KeyCode.X,true)),
   new HotkeyAction("paste","粘贴","编辑",new HotkeyChord(KeyCode.V,true)),
   new HotkeyAction("duplicate","复制副本","编辑",new HotkeyChord(KeyCode.D,true)),
   new HotkeyAction("all","全选","编辑",new HotkeyChord(KeyCode.A,true)),
   new HotkeyAction("delete","删除","编辑",new HotkeyChord(KeyCode.Delete),new HotkeyChord(KeyCode.Backspace)),
   new HotkeyAction("mirror","镜像","编辑",new HotkeyChord(KeyCode.M)),
   new HotkeyAction("handles","按住显示 / 拖动把手","编辑",new HotkeyChord(KeyCode.LeftAlt)),
   new HotkeyAction("skyNode","SkyArea 节点编辑切换","编辑",new HotkeyChord(KeyCode.N)),
   new HotkeyAction("endpoint","时间 / 位置拖动切换","编辑",new HotkeyChord(KeyCode.T,false,true)),
   new HotkeyAction("widthIncrease","增加 Tap / Hold 键宽","编辑",new HotkeyChord(KeyCode.I)),
   new HotkeyAction("widthDecrease","减小 Tap / Hold 键宽","编辑",new HotkeyChord(KeyCode.O)),
   new HotkeyAction("rangeSelect","段落选择","编辑",new HotkeyChord(KeyCode.R,true)),
   new HotkeyAction("select","选择工具","放置工具",new HotkeyChord(KeyCode.V)),
   new HotkeyAction("tap","放置 Tap","放置工具",new HotkeyChord(KeyCode.Alpha1)),
   new HotkeyAction("hold","放置 Hold","放置工具",new HotkeyChord(KeyCode.Alpha2)),
   new HotkeyAction("sky","放置 SkyArea","放置工具",new HotkeyChord(KeyCode.Alpha3)),
   new HotkeyAction("flickLeft","放置左 Flick","放置工具",new HotkeyChord(KeyCode.Alpha4)),
   new HotkeyAction("flickRight","放置右 Flick","放置工具",new HotkeyChord(KeyCode.Alpha5))
  };
  readonly Dictionary<string,HotkeyChord> bindings=new Dictionary<string,HotkeyChord>();
  internal StudioHotkeys(){Reset();}
  internal HotkeyChord this[string id]{get{return bindings[id];}}
  internal void Reset(){foreach(var a in Actions)bindings[a.Id]=a.Default;}
  internal void Restore(IDictionary<string,HotkeyChord> values){bindings.Clear();foreach(var pair in values)bindings[pair.Key]=pair.Value;}
  internal Dictionary<string,HotkeyChord> Snapshot(){return new Dictionary<string,HotkeyChord>(bindings);}
  internal HotkeyAction Find(HotkeyChord chord){
   if(chord.Key==KeyCode.None)return null;
   var exact=Actions.FirstOrDefault(a=>bindings[a.Id].Equals(chord)||(bindings[a.Id].Equals(a.Default)&&a.Aliases.Contains(chord)));if(exact!=null)return exact;
   // Alt exposes edit handles. Endpoint and linked-node toggles remain
   // available while it is held; explicitly bound chords still win above.
   foreach(string id in new[]{"endpoint","skyNode"}){
    var toggle=bindings[id];
    if(chord.Alt&&!toggle.Alt&&chord.Key==toggle.Key&&chord.Ctrl==toggle.Ctrl&&chord.Shift==toggle.Shift)return Actions.First(a=>a.Id==id);
   }return null;
  }
  internal static bool Native(HotkeyChord chord){return Actions.Any(a=>a.Default.Equals(chord)||a.Aliases.Contains(chord));}
  internal bool Assign(string id,HotkeyChord chord,out string error){
   error=null;if(!bindings.ContainsKey(id)){error="未知操作。";return false;}if(!HotkeyChord.Allowed(chord)){error="该按键用于基础交互或窗口操作，请换一个组合。";return false;}
   if(chord.Key==KeyCode.None){bindings[id]=chord;return true;}
   var action=Actions.First(a=>a.Id==id);var candidate=new HashSet<HotkeyChord>(new[]{chord}.Concat(chord.Equals(action.Default)?action.Aliases:new HotkeyChord[0]));
   var conflict=Actions.FirstOrDefault(a=>a.Id!=id&&(candidate.Contains(bindings[a.Id])||(bindings[a.Id].Equals(a.Default)&&a.Aliases.Any(candidate.Contains))));
   if(conflict!=null){error="与“"+conflict.Name+"”冲突，当前设置未修改。";return false;}bindings[id]=chord;return true;
  }
  internal bool DefaultsConflict(out string error){
   error=null;var seen=new Dictionary<HotkeyChord,string>();foreach(var a in Actions){var active=new[]{bindings[a.Id]}.Concat(bindings[a.Id].Equals(a.Default)?a.Aliases:new HotkeyChord[0]);foreach(var key in active){if(key.Key==KeyCode.None)continue;if(!HotkeyChord.Allowed(key)||seen.ContainsKey(key)){error="热键配置包含冲突或保留按键。";return true;}seen[key]=a.Id;}}return false;
  }
 }
}
