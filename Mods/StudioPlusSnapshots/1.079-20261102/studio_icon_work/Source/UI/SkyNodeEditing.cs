using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using InFalsusStudio;
using InFalsusStudio.Core;
using InFalsusStudio.Performance;
using InFalsusStudio.TimeGroups;
using UnityEngine;
namespace InFalsusStudio.NativeEditorUI {
 public sealed partial class NativeEditorUIPlugin {
  sealed class SkyNode {internal SpcEvent A,B;internal double Center,Time;internal Vector2 Gui;}
  bool skyNodeMode,skyNodeDragging,skyNodeTime;float skyNodeNoticeUntil;int skyNodeControl,skyNodeButton,skyNodeCycle,skyNodeRevision=-1,skyNodeSelected=-1;object skyNodeHistory;string skyNodeGroup;
  Vector2 skyNodeDown;Vector3 skyNodePlane;SkyNode skyNodeActive;double skyNodeLastTime;
  bool skyNodePending,skyNodeHasDelta;Vector2 skyNodePendingPoint;EventModifiers skyNodePendingModifiers;double skyNodeAppliedDelta;int[] skyNodeIds;
  internal bool SkyNodeDragging(StudioBootstrap owner){return skyNodeDragging&&ReferenceEquals(boot,owner);}
  readonly List<SkyNode> skyNodes=new List<SkyNode>();
  readonly HashSet<int> skyNodeSelection=new HashSet<int>();
  void InitSkyNode(){
   harmony.Patch(Method("OnGUI"),prefix:new HarmonyMethod(typeof(SkyNodeHooks).GetMethod("Dispatch",F)){priority=Priority.First+1500});
   harmony.Patch(Method("DrawEditingOverlay"),postfix:new HarmonyMethod(typeof(SkyNodeHooks).GetMethod("Draw",F)));
   harmony.Patch(Method("LateUpdate"),prefix:new HarmonyMethod(typeof(SkyNodeHooks).GetMethod("Flush",F)){priority=Priority.First+600});
   foreach(string name in new[]{"CancelGesture","ResetEditingForDocument"})harmony.Patch(Method(name),postfix:new HarmonyMethod(typeof(SkyNodeHooks).GetMethod("Ended",F)));
  }
  void ToggleSkyNode(){
   if(boot==null||!boot.HasDocument||Transport.Playing||boot.History.InTransaction||(bool)Prop(boot,"WorkflowActive")||Val<PointPlacement>(boot,"pointPlacement").Pending)return;
   skyNodeMode=!skyNodeMode;skyNodeNoticeUntil=Time.unscaledTime+2;skyNodeRevision=-1;Set(boot,"status",skyNodeMode?"SkyArea 节点编辑已开启；H 切换重叠节点。":"SkyArea 节点编辑已关闭。");
  }
  void CollectSkyNodes(){
   if(!skyNodeMode||boot==null||!boot.HasDocument||!Val<bool>(boot,"showUi")||!Val<bool>(boot,"showNotes")||Transport.Playing)return;
   var history=boot.History;int selected=Val<int>(boot,"selectedId");var selectedIds=Val<HashSet<int>>(boot,"selection");
   if(!ReferenceEquals(skyNodeHistory,history)||skyNodeRevision!=history.Revision||skyNodeSelected!=selected||skyNodeGroup!=TimingGroups.Current||!skyNodeSelection.SetEquals(selectedIds)){
    skyNodes.Clear();skyNodeHistory=history;skyNodeRevision=history.Revision;skyNodeSelected=selected;skyNodeGroup=TimingGroups.Current;skyNodeSelection.Clear();skyNodeSelection.UnionWith(selectedIds);
    var model=TimingGroups.Model(history.Document);string group=TimingGroups.Current;
    var notes=boot.Events.Where(n=>n.Kind==EventKind.SkyArea&&ChartMath.InvalidReason(n)==null&&model.Of(n.SourceId)==group&&!TimingGroups.Hidden(n,boot.CurrentMs)).ToArray();
    var heads=new Dictionary<long,List<SpcEvent>>();var tails=new Dictionary<long,List<SpcEvent>>();
    foreach(var note in notes){long h=(long)Math.Floor(note.TimeMs/SkyGroups.TimeTolerance),t=(long)Math.Floor(note.EndMs/SkyGroups.TimeTolerance);List<SpcEvent> bucket;
     if(!heads.TryGetValue(h,out bucket))heads[h]=bucket=new List<SpcEvent>();bucket.Add(note);
     if(!tails.TryGetValue(t,out bucket))tails[t]=bucket=new List<SpcEvent>();bucket.Add(note);
    }
    var seen=new HashSet<long>();
    Action<SpcEvent,SpcEvent> add=(a,b)=>{
     if(!SkyGroups.Joins(a,b))return;long key=((long)(uint)a.SourceId<<32)|(uint)b.SourceId;if(!seen.Add(key))return;
     var tail=ChartMath.SkyAt(a,a.EndMs);var head=ChartMath.SkyAt(b,b.TimeMs);double center=(Math.Max(tail.Left,head.Left)+Math.Min(tail.Right,head.Right))*.5;
     skyNodes.Add(new SkyNode{A=a,B=b,Center=center,Time=a.EndMs});
    };
    foreach(var current in notes){if(!selectedIds.Contains(current.SourceId)&&current.SourceId!=selected)continue;
     long head=(long)Math.Floor(current.TimeMs/SkyGroups.TimeTolerance),tail=(long)Math.Floor(current.EndMs/SkyGroups.TimeTolerance);
     for(long offset=-1;offset<=1;offset++){List<SpcEvent> bucket;
      if(tails.TryGetValue(head+offset,out bucket))foreach(var other in bucket)add(other,current);
      if(heads.TryGetValue(tail+offset,out bucket))foreach(var other in bucket)add(current,other);
     }
    }
    skyNodes.Sort((a,b)=>{int n=a.Time.CompareTo(b.Time);if(n!=0)return n;n=a.A.SourceId.CompareTo(b.A.SourceId);return n!=0?n:a.B.SourceId.CompareTo(b.B.SourceId);});skyNodeCycle=0;
   }
   foreach(var node in skyNodes){float z=Convert.ToSingle(Call(boot,"Depth",node.Time));var q=boot.ViewCamera.WorldToScreenPoint(new Vector3(StageSpace.AirX(boot.Profile,node.Center),boot.Profile.skyHeight+.019f,z));node.Gui=q.z>boot.ViewCamera.nearClipPlane?new Vector2(q.x,Screen.height-q.y):new Vector2(-10000,-10000);}
  }
  SkyNode HitSkyNode(Vector2 pos){
   CollectSkyNodes();float radius=14*Convert.ToSingle(Prop(boot,"OverlayScale"));var near=skyNodes.Where(n=>(n.Gui-pos).sqrMagnitude<radius*radius).ToList();return near.Count==0?null:near[skyNodeCycle%near.Count];
  }
  bool NodePlane(Vector2 p,out Vector3 q){object[] args={p,true,0,Vector3.zero};bool ok=(bool)Method("PlanePoint").Invoke(boot,args);q=(Vector3)args[3];return ok;}
  void BeginSkyNode(SkyNode node,Event e){
   if(!(bool)Call(boot,"CommitAllInputFieldsNow")||!(bool)Call(boot,"CommitPendingNoteNow"))return;Vector3 p;if(!NodePlane(e.mousePosition,out p))return;
   skyNodeActive=node;skyNodeTime=EndpointDragMode.TimeOnly(boot);skyNodeDown=e.mousePosition;skyNodePlane=p;skyNodeLastTime=node.Time;skyNodeButton=e.button;boot.History.Begin();skyNodeDragging=true;GUIUtility.hotControl=skyNodeControl;
   skyNodePending=skyNodeHasDelta=false;skyNodeIds=new[]{node.A.SourceId,node.B.SourceId};DragPreview.PrepareLinked(boot,skyNodeIds);
   Set(boot,"status","节点联动："+node.A.SourceId+" → "+node.B.SourceId+"；时间差 "+(node.B.TimeMs-node.A.EndMs).ToString("0.###")+" ms。");
  }
  static void NodeEdit(SpcDocument doc,SkyNode node,bool time,double delta){
   var a=node.A;var b=node.B;
   if(time){double ae=a.EndMs+delta,bs=b.TimeMs+delta;AuthoringSafety.Duration(a.TimeMs,ae);AuthoringSafety.Duration(bs,b.EndMs);AuthoredNumber.Set(doc,a.SourceId,9,ae-a.TimeMs);AuthoredNumber.Set(doc,b.SourceId,0,bs);AuthoredNumber.Set(doc,b.SourceId,9,b.EndMs-bs);}
   else{var ar=ChartMath.SkyAt(a,a.EndMs);var br=ChartMath.SkyAt(b,b.TimeMs);RationalAirCoordinates.WriteEndpoint(doc,a,true,ar.Center+delta,ar.Width);RationalAirCoordinates.WriteEndpoint(doc,b,false,br.Center+delta,br.Width);}
   var changed=doc.ReadEvents().Where(n=>n.SourceId==a.SourceId||n.SourceId==b.SourceId).ToArray();if(changed.Length!=2)throw new InvalidOperationException("节点源物件已变化。");
   foreach(var n in changed){double at;string reason=AuthoringSafety.Reason(n)??ChartMath.InvalidReason(n);if(reason!=null)throw new ArgumentException(reason);if(ChartMath.SkyMinimumWidth(n,out at)<-1e-10)throw new ArgumentException("SkyArea 边缘交叉。");if(!AirOverflow.Enabled&&AirReachBounds.Violation(n)!=null)throw new ArgumentException("节点超出天空边界。");}
  }
  void PreviewSkyNode(Event e){
   if(!skyNodeDragging||!boot.History.InTransaction)return;skyNodePendingPoint=e.mousePosition;skyNodePendingModifiers=e.modifiers;skyNodePending=true;
  }
  void FlushSkyNode(){
   if(!skyNodePending)return;skyNodePending=false;if(!skyNodeDragging||boot==null||!boot.History.InTransaction)return;
   ApplySkyNode(skyNodePendingPoint,(skyNodePendingModifiers&EventModifiers.Alt)!=0);
  }
  void ApplySkyNode(Vector2 mouse,bool alt){
   if((mouse-skyNodeDown).sqrMagnitude<9&&!skyNodeHasDelta)return;Vector3 p;if(!NodePlane(mouse,out p))return;
   double delta;if(skyNodeTime){float z=Convert.ToSingle(Call(boot,"Depth",skyNodeActive.Time))+p.z-skyNodePlane.z;object[] args={z,skyNodeLastTime,0d};if(!(bool)Method("TimeFromDepth").Invoke(boot,args))return;double time=(double)Call(boot,"SnapTime",(double)args[2],alt);skyNodeLastTime=time;delta=time-skyNodeActive.Time;}
   else{double raw=skyNodeActive.Center+(p.x-skyNodePlane.x)/(2*boot.Profile.centralHalfWidth);double split=skyNodeActive.A.Get(5);delta=(double)Call(boot,"SnapX",raw,split,false,alt)-skyNodeActive.Center;}
   if(skyNodeHasDelta&&delta==skyNodeAppliedDelta)return;
   try{bool previous=rainInserting;rainInserting=true;try{DragPreview.PreviewLinked(boot,skyNodeIds,d=>NodeEdit(d,skyNodeActive,skyNodeTime,delta));skyNodeAppliedDelta=delta;skyNodeHasDelta=true;}finally{rainInserting=previous;}}
   catch(Exception ex){Set(boot,"status","节点位置无效："+(ex.InnerException??ex).Message);}
  }
  void EndSkyNode(bool cancel){
   if(!skyNodeDragging)return;if(!cancel)FlushSkyNode();skyNodePending=false;skyNodeDragging=false;
   if(boot!=null&&boot.History!=null&&boot.History.InTransaction){if(cancel)boot.History.Cancel();else boot.History.Commit();Call(boot,"AfterDocumentEdit");}
   if(GUIUtility.hotControl==skyNodeControl)GUIUtility.hotControl=0;skyNodeActive=null;skyNodeRevision=-1;
  }
  bool RouteSkyNode(Event e){
   if(skyNodeDragging){
    if(!Application.isFocused||e.rawType==EventType.KeyDown&&e.keyCode==KeyCode.Escape||e.rawType==EventType.MouseDown&&e.button==1){EndSkyNode(true);e.Use();return true;}
    if(e.rawType==EventType.MouseDrag||e.rawType==EventType.MouseMove){PreviewSkyNode(e);e.Use();return true;}
    if(e.rawType==EventType.MouseUp&&e.button==skyNodeButton){PreviewSkyNode(e);EndSkyNode(false);e.Use();return true;}
    if(e.rawType==EventType.MouseDown||e.rawType==EventType.MouseUp||e.rawType==EventType.ScrollWheel){e.Use();return true;}return false;
   }
   if(!skyNodeMode||formal||boot==null||!boot.HasDocument||Transport.Playing||boot.History.InTransaction||(bool)Prop(boot,"WorkflowActive")||Val<PointPlacement>(boot,"pointPlacement").Pending)return false;
   if(e.type==EventType.KeyDown&&hotkeys.Find(HotkeyChord.From(e))!=null&&hotkeys.Find(HotkeyChord.From(e)).Id=="overlap"&&!PopupOwnsTextFocus&&!GUI.GetNameOfFocusedControl().StartsWith("field",StringComparison.Ordinal)&&HitSkyNode(e.mousePosition)!=null){skyNodeCycle++;Set(boot,"status","切换共同节点候选 "+(skyNodeCycle+1)+"。");e.Use();return true;}
   if(e.rawType!=EventType.MouseDown||(e.button!=0&&!(e.button==2&&MiddleHandleBinding(hotkeys["handles"])))||e.shift||e.control||e.command||GUIUtility.hotControl!=0||!(bool)Call(boot,"AllowedCanvas",e.mousePosition))return false;
   var hit=HitSkyNode(e.mousePosition);if(hit==null)return false;BeginSkyNode(hit,e);e.Use();return true;
  }
  void DrawSkyNodes(){
   if(!skyNodeMode||boot==null||skyNodeDragging)return;CollectSkyNodes();var old=GUI.color;float s=Convert.ToSingle(Prop(boot,"OverlayScale"));var painted=new HashSet<SkyNode>();
   foreach(var n in skyNodes){if(painted.Contains(n)||!(bool)Call(boot,"AllowedCanvas",n.Gui))continue;var choices=skyNodes.Where(x=>(x.Gui-n.Gui).sqrMagnitude<196*s*s).ToList();foreach(var item in choices)painted.Add(item);int index=skyNodeCycle%choices.Count;var chosen=choices[index];GUI.color=new Color(1,.9f,.05f,1);GUI.DrawTexture(new Rect(chosen.Gui.x-6*s,chosen.Gui.y-6*s,12*s,12*s),Texture2D.whiteTexture);string text="节点 "+chosen.A.SourceId+" → "+chosen.B.SourceId+(choices.Count>1?"  "+(index+1)+"/"+choices.Count+"  重叠候选键切换":"");text=T(text);var style=new GUIStyle(GUI.skin.label){fontStyle=FontStyle.Bold,fontSize=Mathf.Max(12,Mathf.RoundToInt(16*s)),alignment=TextAnchor.MiddleLeft};style.normal.background=null;style.normal.textColor=new Color(1,.92f,.08f,1);float width=Mathf.Min(260*s,style.CalcSize(new GUIContent(text)).x+10*s);var rect=new Rect(chosen.Gui.x+8*s,chosen.Gui.y-22*s,width,24*s);GUI.color=Color.white;GUI.Label(new Rect(rect.x+5*s,rect.y,rect.width-10*s,rect.height),text,style);}GUI.color=old;
  }
  static class SkyNodeHooks {
   static void Flush(StudioBootstrap __instance){if(I!=null&&I.ready&&I.boot==__instance)I.FlushSkyNode();}
   static bool Dispatch(StudioBootstrap __instance,bool __runOriginal){
    if(!__runOriginal||I==null||!I.ready)return true;I.Bind(__instance);I.skyNodeControl=GUIUtility.GetControlID("InfStudio.SkyNode".GetHashCode(),FocusType.Passive);
    var e=Event.current;var type=e.rawType;bool captured=I.skyNodeDragging;bool handled=I.RouteSkyNode(e);
    // Captured motion only queues its latest coordinate. Layout, Repaint and
    // all press/release/cancel boundaries still retain the normal GUI tree.
    if(captured&&handled&&(type==EventType.MouseMove||type==EventType.MouseDrag)){I.authoringGuiPointer=e.mousePosition;I.authoringPointerValid=true;return false;}return true;
   }
   static void Draw(StudioBootstrap __instance){if(I!=null&&I.ready&&I.boot==__instance)I.DrawSkyNodes();}
   static void Ended(){if(I!=null&&I.skyNodeDragging){if(GUIUtility.hotControl==I.skyNodeControl)GUIUtility.hotControl=0;I.skyNodePending=false;I.skyNodeDragging=false;I.skyNodeActive=null;I.skyNodeRevision=-1;}}
  }
 }
}
