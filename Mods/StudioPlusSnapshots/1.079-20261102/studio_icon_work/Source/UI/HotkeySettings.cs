using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;
namespace InFalsusStudio.NativeEditorUI {
 public sealed partial class NativeEditorUIPlugin {
  [Serializable] sealed class HotkeyFile {public int version=2;public string bindings;}
  readonly StudioHotkeys hotkeys=new StudioHotkeys();ConfigEntry<string> hotkeyConfig;string capturingHotkey,hotkeyMessage="点击按键录入新组合，修改后自动保存。";bool hotkeyDispatching;float hotkeyPageHeight=2000;
  void InitHotkeys(){
   InitHandleHotkey();
   hotkeyConfig=Config.Bind("Hotkeys","Bindings","","Custom editor shortcuts. Edited from Settings > Operations > Hotkeys; no chart data.");
   try{LoadHotkeys(hotkeyConfig.Value);}catch(Exception e){hotkeys.Reset();hotkeyMessage="热键配置无效，已使用默认按键。";Logger.LogWarning("Hotkeys ignored: "+e.Message);}
   if(hotkeyMigrationNeeded){string migrationMessage=hotkeyMessage;if(PersistHotkeys(hotkeys.Snapshot())&&migrationMessage.StartsWith("保留自定义热键",StringComparison.Ordinal))hotkeyMessage=migrationMessage;}
   harmony.Patch(Method("OnGUI"),new HarmonyMethod(typeof(HotkeyHooks).GetMethod("Capture",F)){priority=Priority.First+1000});
  }
  bool hotkeyMigrationNeeded;
  void LoadHotkeys(string json){
   if(string.IsNullOrEmpty(json))return;if(json.Length>32768)throw new InvalidDataException("Hotkey settings too large");var file=JsonUtility.FromJson<HotkeyFile>(json);
   if(file==null||(file.version!=1&&file.version!=2)||string.IsNullOrEmpty(file.bindings))throw new InvalidDataException("Invalid hotkey settings");
   var rows=file.bindings.Split('|');if(rows.Length>64)throw new InvalidDataException("Too many hotkeys");var next=hotkeys.Snapshot();var seen=new HashSet<string>();var custom=new HashSet<string>();
   bool oldHeld=true;if(file.version==1)oldHeld=Config.Bind("Playback","HoldSpacePreview",false,"Legacy preview migration only; no runtime effect.").Value;
   foreach(var row in rows){var e=row.Split(':');int key,flags;if(e.Length!=3||!seen.Add(e[0])||!int.TryParse(e[1],out key)||!int.TryParse(e[2],out flags)||flags<0||flags>7)throw new InvalidDataException("Invalid hotkey entry");
    string id=e[0];var chord=new HotkeyChord((KeyCode)key,(flags&1)!=0,(flags&2)!=0,(flags&4)!=0);
    if(file.version==1&&id=="preview"){if(chord.Equals(new HotkeyChord(KeyCode.Space)))continue;id=oldHeld?"previewHold":"previewToggle";}
    else if(file.version==1&&id=="select"&&chord.Equals(new HotkeyChord(KeyCode.Q)))continue;
    if(!next.ContainsKey(id))continue;next[id]=chord;if(!chord.Equals(StudioHotkeys.Actions.First(x=>x.Id==id).Default))custom.Add(id);
   }
   var previous=hotkeys.Snapshot();hotkeys.Restore(StudioHotkeys.Actions.ToDictionary(x=>x.Id,x=>new HotkeyChord(KeyCode.None)));
   var conflicts=new List<string>();foreach(var action in StudioHotkeys.Actions.OrderByDescending(x=>custom.Contains(x.Id))){string error;if(!hotkeys.Assign(action.Id,next[action.Id],out error))conflicts.Add(action.Name);}
   if(conflicts.Count>0)hotkeyMessage="保留自定义热键；以下操作需重新绑定："+string.Join("、",conflicts.ToArray());
   hotkeyMigrationNeeded=file.version==1;
  }
  string SerializeHotkeys(){var payload=string.Join("|",StudioHotkeys.Actions.Select(a=>{var k=hotkeys[a.Id];return a.Id+":"+(int)k.Key+":"+((k.Ctrl?1:0)|(k.Alt?2:0)|(k.Shift?4:0));}).ToArray());var json=JsonUtility.ToJson(new HotkeyFile{bindings=payload});if(JsonUtility.FromJson<HotkeyFile>(json).bindings!=payload)throw new InvalidDataException("Hotkey serialization failed");return json;}
  static string PutHotkeys(string text,string value){
   string nl=text.Contains("\r\n")?"\r\n":"\n",line="Bindings = "+value;
   var section=Regex.Match(text,@"(?ms)^\[Hotkeys\][^\r\n]*(?:\r?\n|\z).*?(?=^\[|\z)");
   if(!section.Success)return text+(text.Length>0&&!text.EndsWith("\n",StringComparison.Ordinal)?nl:"")+nl+"[Hotkeys]"+nl+line+nl;
   string body=section.Value;var key=Regex.Match(body,@"(?m)^[ \t]*Bindings[ \t]*=[^\r\n]*");body=key.Success?body.Substring(0,key.Index)+line+body.Substring(key.Index+key.Length):body+(body.EndsWith("\n",StringComparison.Ordinal)?"":nl)+line+nl;
   return text.Substring(0,section.Index)+body+text.Substring(section.Index+section.Length);
  }
  bool PersistHotkeys(Dictionary<string,HotkeyChord> previous){
   string old=hotkeyConfig.Value;try{hotkeyConfig.Value=SerializeHotkeys();SharedPreferences.Store(hotkeyConfig);hotkeyMigrationNeeded=false;hotkeyMessage="热键已保存。";return true;}
   catch(Exception e){hotkeyConfig.Value=old;hotkeys.Restore(previous);hotkeyMessage="保存失败，保留原热键："+e.Message;Logger.LogWarning(hotkeyMessage);return false;}
  }
  void AssignHotkey(string id,HotkeyChord chord){if(spacePreview.Active)StopSpacePreview();previewKeysDown.Clear();var old=hotkeys.Snapshot();string error;if(!hotkeys.Assign(id,chord,out error)){hotkeyMessage=error;return;}PersistHotkeys(old);}
  void ResetHotkeys(){if(spacePreview.Active)StopSpacePreview();previewKeysDown.Clear();var old=hotkeys.Snapshot();hotkeys.Reset();PersistHotkeys(old);capturingHotkey=null;}
  string HotkeyLabel(string id){var a=StudioHotkeys.Actions.First(x=>x.Id==id);var k=hotkeys[id];return k.ToString()+(k.Equals(a.Default)&&a.Aliases.Length>0?" / "+string.Join(" / ",a.Aliases.Select(x=>x.ToString()).ToArray()):"");}
  void StartHotkeyCapture(string id){Queue(delegate{if(!(bool)Call(boot,"CommitAllInputFieldsNow"))return;ReleasePopupFocus();GUI.FocusControl(null);capturingHotkey=id;hotkeyMessage="按下新组合键；Esc 取消录入。";});}
  bool CaptureHotkey(Event e){
   if(capturingHotkey==null)return false;if(panel!="设置"||settingsTab!="热键"){capturingHotkey=null;return false;}
   bool mouse=HotkeyChord.MouseEvent(e);if(!mouse&&e.type!=EventType.KeyDown&&e.type!=EventType.KeyUp)return false;
   if(e.type==EventType.KeyDown||e.type==EventType.MouseDown){if(!mouse&&e.keyCode==KeyCode.Escape){capturingHotkey=null;hotkeyMessage="已取消录入。";}
    else if(mouse||!HotkeyChord.Modifier(e.keyCode)){string id=capturingHotkey;var chord=HotkeyChord.From(e);capturingHotkey=null;Queue(delegate{AssignHotkey(id,chord);});}}
   // Wait for Alt release, so Alt+another key can still be recorded as a chord.
   if(capturingHotkey!=null&&e.type==EventType.KeyUp&&(e.keyCode==KeyCode.LeftAlt||e.keyCode==KeyCode.RightAlt)){string id=capturingHotkey;var chord=HotkeyChord.From(e);capturingHotkey=null;Queue(delegate{AssignHotkey(id,chord);});}
   e.Use();return true;
  }
  internal bool NativeTextInputFocused {get{return GUIUtility.keyboardControl!=0&&numericControl==GUIUtility.keyboardControl&&GUI.GetNameOfFocusedControl()==numericKey;}}
  static bool ManagedNativeKey(HotkeyChord k){return StudioHotkeys.Native(k)||k.Key==KeyCode.Space||k.Key==KeyCode.F1||k.Key==KeyCode.M||k.Key==KeyCode.Q||k.Key==KeyCode.Delete||k.Key==KeyCode.Backspace||(k.Key>=KeyCode.Alpha1&&k.Key<=KeyCode.Alpha5)||(k.Ctrl&&new[]{KeyCode.S,KeyCode.O,KeyCode.Z,KeyCode.Y,KeyCode.A,KeyCode.C,KeyCode.X,KeyCode.V,KeyCode.D}.Contains(k.Key));}
  static void ConsumeGridTab(Event e){GUI.FocusControl(null);GUIUtility.keyboardControl=0;e.character='\0';e.keyCode=KeyCode.None;e.Use();}
  bool RouteHotkey(Event e,bool textFocused){
   if(InFalsusStudio.Gameplay.GameplaySession.HandleEvent(e))return true;if(hotkeyDispatching)return false;if(CaptureHotkey(e))return true;bool mouse=HotkeyChord.MouseEvent(e);if(!mouse&&e.type!=EventType.KeyDown&&e.type!=EventType.KeyUp)return false;bool release=e.type==EventType.KeyUp||e.type==EventType.MouseUp;
   var chord=HotkeyChord.From(e);HotkeyAction action=null;
   if(release){previewKeysDown.Remove(chord.Key);if(spacePreview.Active&&chord.Key==spacePreview.Key){StopSpacePreview();e.Use();return true;}if(hotkeys.Find(chord)!=null&&chord.Key==KeyCode.Tab){ConsumeGridTab(e);return true;}return false;}
   action=hotkeys.Find(chord);
   // Tab is the grid action even when IMGUI has already advanced focus to a
   // footer field. Clear both native Tab payloads, not only Event.type.
   if(action!=null&&action.Id=="grid"&&chord.Key==KeyCode.Tab){Queue(delegate{if(textFocused&&!(bool)Call(boot,"CommitAllInputFieldsNow"))return;Call(boot,"SetBeatGrid",!Val<bool>(boot,"showGrid"));});ConsumeGridTab(e);return true;}
   if(e.character=='\t'&&chord.Key==KeyCode.None&&hotkeys["grid"].Key==KeyCode.Tab){ConsumeGridTab(e);return true;}
   if(action!=null&&action.Id=="handles"){if(MiddleHandleBinding(hotkeys["handles"]))return false;if(!textFocused){e.Use();return true;}return false;}
   if(textFocused&&!release&&(action==null||(action.Id!="save"&&action.Id!="saveAs"))){if(chord.Ctrl&&chord.Key==KeyCode.S){e.Use();return true;}return false;}
   if(action!=null){
    if(action.Id=="gameplayStart"){if(EnableGameplayMode)Queue(delegate{InFalsusStudio.Gameplay.GameplaySession.Enter(boot);});e.Use();return true;}
    if(action.Id=="previewHold"||action.Id=="previewToggle"){HandlePreviewAction(action.Id,chord,e);return true;}
    if(action.Id=="widthIncrease"||action.Id=="widthDecrease"){int step=action.Id=="widthIncrease"?1:-1;Queue(delegate{ChangeGroundWidth(step);});e.Use();return true;}
    if(action.Id=="grid"){Command("SetBeatGrid",!Val<bool>(boot,"showGrid"));e.Use();return true;}
    if(action.Id=="overlap"){Queue(CycleOverlapCandidate);e.Use();return true;}
    if(action.Id=="skyNode"){ToggleSkyNode();e.Use();return true;}
    if(action.Id=="rangeSelect"){Command("BeginRangeSelection");e.Use();return true;}
    if(action.Id=="select"){Queue(delegate{Call(boot,"UseTool",Enum.Parse(Get(boot,"tool").GetType(),"Select"));});e.Use();return true;}
    var canonical=action.Default.Event(release?EventType.KeyUp:EventType.KeyDown);hotkeyDispatching=true;
    try{Call(boot,"HandleKeyboard",canonical,textFocused);}finally{hotkeyDispatching=false;e.Use();}return true;
   }
   // Once rebound, the old physical binding must not fall through to the
   // native handler and trigger its original hard-coded action as well.
   if(e.type==EventType.KeyDown&&ManagedNativeKey(chord)){e.Use();return true;}return false;
  }
  void DrawHotkeySettings(){
   MatchedLink("‹ 返回操作设置",delegate{Queue(delegate{settingsTab="操作";capturingHotkey=null;panelScroll=Vector2.zero;});});
   Rect info=ContentRow(96);UiParagraph(new Rect(0,info.y+8*scale,405*scale,88*scale),hotkeyMessage,14,Css("#a6c3d1"));
   string section=null;foreach(var a in StudioHotkeys.Actions){if(section!=a.Section){section=a.Section;MatchedSection(section);}Rect row=ContentRow(76);RowLine(row);
    UiText(new Rect(0,row.y,405*scale,32*scale),a.Name,16,Css("#a6c3d1"));bool capturing=capturingHotkey==a.Id;
    if(FlatButton(new Rect(0,row.y+34*scale,240*scale,34*scale),capturing?"请按键…":HotkeyLabel(a.Id),15,capturing)){string id=a.Id;StartHotkeyCapture(id);}
    if(FlatButton(new Rect(250*scale,row.y+34*scale,70*scale,34*scale),"默认",14)){string id=a.Id;var key=a.Default;Queue(delegate{capturingHotkey=null;AssignHotkey(id,key);});}
    if(FlatButton(new Rect(330*scale,row.y+34*scale,75*scale,34*scale),"清除",14)){string id=a.Id;Queue(delegate{capturingHotkey=null;AssignHotkey(id,new HotkeyChord(KeyCode.None));});}
   }
   MatchedSection("固定交互");Rect fixedRow=ContentRow(126);UiParagraph(new Rect(0,fixedRow.y+8*scale,405*scale,118*scale),"Ctrl 多选 · 方向键微调\nEsc 取消\nEnter 确认输入 · F11 恢复窗口\n文字输入框保留常规文字编辑按键。",13,Css("#7c9dad"));
   MatchedLink("恢复全部默认热键",delegate{Queue(ResetHotkeys);});hotkeyPageHeight=contentY/scale+12;
  }
  static class HotkeyHooks {
   static bool Capture(){if(I==null||!I.ready)return true;var e=Event.current;if(InFalsusStudio.Gameplay.GameplaySession.Active)return true;if(I.CaptureHotkey(e))return false;
    if(I.boot!=null&&(HotkeyChord.MouseEvent(e)||e.isKey)){
     bool focused=I.NativeTextInputFocused||I.PopupOwnsTextFocus||Get(I.boot,"hudField").ToString()!="None"||GUI.GetNameOfFocusedControl().StartsWith("field",StringComparison.Ordinal);
     if(I.RouteHotkey(e,focused))return false;
    }return true;}
  }
 }
}
