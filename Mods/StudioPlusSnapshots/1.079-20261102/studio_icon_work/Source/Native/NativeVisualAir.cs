using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using InFalsusStudio;
using InFalsusStudio.Core;
namespace InFalsusStudio.NativeVisual {
// Native V5 prefab geometry and SkyArea vertex contract. No chart mutation.
internal static class NativeAir {
 private const float NativeTrackLength=20.181811f,NativeAirWidth=1.62f;
 private const string BracketMaterial="Untitled Project_PBR_UV-Color-Mesh_2024-01-04_09.14.33";
 private static readonly Dictionary<object,HashSet<int>> tails=new Dictionary<object,HashSet<int>>();
 private sealed class StripSpan {internal double Start,End;internal StripSpan(double start,double end){Start=start;End=end;}}
 private static readonly Dictionary<object,Dictionary<int,StripSpan>> spans=new Dictionary<object,Dictionary<int,StripSpan>>();
 internal static void Validate(){
  foreach(var name in new[]{"SkyAt","FlickRange"})if(NativeVisualPlugin.T("Core.ChartMath").GetMethod(name,NativeVisualPlugin.F)==null)throw new Exception("Air chart interface changed: "+name);
  foreach(var name in new[]{"GroupOf","Automatic","Joins"})if(NativeVisualPlugin.T("Core.SkyGroups").GetMethod(name,NativeVisualPlugin.F)==null)throw new Exception("Sky group interface changed: "+name);
  foreach(var name in new[]{"UpdateConnections","SkyPoint","FlickZ","Edge","IsSelected"})if(NativeVisualPlugin.T("NoteRenderer").GetMethod(name,NativeVisualPlugin.F)==null)throw new Exception("Air renderer interface changed: "+name);
  if(NativeVisualPlugin.T("Core.SkyConnections").GetField("Starts",NativeVisualPlugin.F)==null)throw new Exception("Sky group head interface changed");
 }
 internal static void ValidateAssets(){
  foreach(var name in new[]{"flick","plane","bracket"})if(!NativeVisualPlugin.Meshes.ContainsKey(name))throw new Exception("Native air mesh missing: "+name);
  foreach(var name in new[]{"FlickV5 Left","FlickV5 Right","Mat.001","Mat.001 R","SkyArea Variant","SkySafeShadowPrefabMat","FlickShadowPrefabMat",BracketMaterial})NativeVisualPlugin.Mat(name);
  foreach(var name in new[]{"flicknote-baseshort","flicknoter-baseshort"})if(!NativeVisualPlugin.Textures.ContainsKey(name))throw new Exception("Native Flick short atlas missing: "+name);
 }
 internal static Range[] Capture(object o,string body){return new[]{new Range(NativeVisualPlugin.Get(o,body)),new Range(NativeVisualPlugin.Get(o,"edges")),new Range(NativeVisualPlugin.Get(o,"grid"))};}
 internal static void EnsureConnections(object o,object events){if(!tails.ContainsKey(o))Connections(o,events);}
 internal static void Dispose(object o){tails.Remove(o);spans.Remove(o);groupConnections.Remove(o);}
 private static int Root(Dictionary<int,int> parents,int id){int parent=parents[id];if(parent!=id)parents[id]=Root(parents,parent);return parents[id];}
 sealed class GroupConnections {internal InFalsusStudio.Core.SpcEvent[] Notes;internal HashSet<int> Tails,Heads;internal Dictionary<int,StripSpan> Spans;}
 static readonly Dictionary<object,Dictionary<string,GroupConnections>> groupConnections=new Dictionary<object,Dictionary<string,GroupConnections>>();
 internal static void Connections(object o,object events){
  var groups=((IEnumerable)events).Cast<object>().GroupBy(n=>InFalsusStudio.TimeGroups.TimingGroups.NameOf((InFalsusStudio.Core.SpcEvent)n)).ToArray();
  var allTails=new HashSet<int>();var allSpans=new Dictionary<int,StripSpan>();
  Dictionary<string,GroupConnections> cache;if(!groupConnections.TryGetValue(o,out cache))groupConnections[o]=cache=new Dictionary<string,GroupConnections>();var live=new HashSet<string>();
  foreach(var group in groups){live.Add(group.Key);var sky=group.Cast<InFalsusStudio.Core.SpcEvent>().Where(n=>n.Kind==InFalsusStudio.Core.EventKind.SkyArea).ToArray();GroupConnections g;
   if(!cache.TryGetValue(group.Key,out g)||!InFalsusStudio.Performance.EventValues.Same(sky,g.Notes)){ConnectionsSingle(o,sky);g=new GroupConnections{Notes=sky,Tails=tails[o],Heads=InFalsusStudio.Core.SkyGroups.Heads(sky),Spans=spans[o]};cache[group.Key]=g;}
   allTails.UnionWith(g.Tails);foreach(var pair in g.Spans)allSpans[pair.Key]=pair.Value;
  }
  foreach(string key in cache.Keys.Where(k=>!live.Contains(k)).ToArray())cache.Remove(key);
  tails[o]=allTails;spans[o]=allSpans;
 }
 internal static bool UpdateConnections(InFalsusStudio.NoteRenderer renderer,Dictionary<int,InFalsusStudio.Core.SpcEvent> before,Dictionary<int,InFalsusStudio.Core.SpcEvent> after){
  var changes=after.Values.Where(n=>n.Kind==InFalsusStudio.Core.EventKind.SkyArea).ToArray();if(changes.Length==0)return true;
  Dictionary<string,GroupConnections> cache;HashSet<int> allTails;Dictionary<int,StripSpan> allSpans;
  if(!groupConnections.TryGetValue(renderer,out cache)||!tails.TryGetValue(renderer,out allTails)||!spans.TryGetValue(renderer,out allSpans))return false;
  var heads=(InFalsusStudio.Core.SkyConnections)NativeVisualPlugin.Get(renderer,"connections");if(heads==null)return false;
  foreach(var note in changes)if(InFalsusStudio.TimeGroups.TimingGroups.NameOf(note)!=InFalsusStudio.TimeGroups.TimingGroups.NameOf(before[note.SourceId]))return false;
  var names=changes.Select(InFalsusStudio.TimeGroups.TimingGroups.NameOf).Distinct().ToArray();if(names.Any(name=>!cache.ContainsKey(name)))return false;
  foreach(string name in names){var previous=cache[name];var next=previous.Notes.Select(n=>after.ContainsKey(n.SourceId)?after[n.SourceId]:n).ToArray();
   bool geometry=changes.Where(n=>InFalsusStudio.TimeGroups.TimingGroups.NameOf(n)==name).Any(n=>Enumerable.Range(0,11).Any(i=>i!=7&&i!=8&&n.Get(i,-1)!=before[n.SourceId].Get(i,-1)));
   if(!geometry){previous.Notes=next;continue;}
   ConnectionsSingle(renderer,next);var updated=new GroupConnections{Notes=next,Tails=tails[renderer],Spans=spans[renderer],Heads=InFalsusStudio.Core.SkyGroups.Heads(next)};
   allTails.ExceptWith(previous.Tails);allTails.UnionWith(updated.Tails);heads.Starts.ExceptWith(previous.Heads);heads.Starts.UnionWith(updated.Heads);
   foreach(var n in previous.Notes)allSpans.Remove(n.SourceId);foreach(var p in updated.Spans)allSpans[p.Key]=p.Value;cache[name]=updated;
  }
  tails[renderer]=allTails;spans[renderer]=allSpans;return true;
 }
 private static void ConnectionsSingle(object o,object events){
  // Query the editor's existing rules; explicit groups and automatic joins are
  // read solely to set shader end flags. Source IDs and chart order stay intact.
  var sky=((IEnumerable)events).Cast<InFalsusStudio.Core.SpcEvent>().Where(n=>n.Kind==InFalsusStudio.Core.EventKind.SkyArea).ToArray();
  var type=NativeVisualPlugin.T("Core.SkyGroups");var group=type.GetMethod("GroupOf",NativeVisualPlugin.F);var automatic=type.GetMethod("Automatic",NativeVisualPlugin.F);var joins=type.GetMethod("Joins",NativeVisualPlugin.F);
  var endByGroup=new Dictionary<int,double>();var automaticStarts=new Dictionary<long,List<InFalsusStudio.Core.SpcEvent>>();var result=new HashSet<int>();
  foreach(var n in sky){int g=InFalsusStudio.Core.SkyGroups.GroupOf(n);bool auto=InFalsusStudio.Core.SkyGroups.Automatic(n);double end=n.EndMs;
   if(!auto&&g>=0){double previous;if(!endByGroup.TryGetValue(g,out previous)||end>previous)endByGroup[g]=end;}
   if(auto){long bucket=(long)Math.Floor(n.TimeMs/2);List<InFalsusStudio.Core.SpcEvent> row;if(!automaticStarts.TryGetValue(bucket,out row)){row=new List<InFalsusStudio.Core.SpcEvent>();automaticStarts.Add(bucket,row);}row.Add(n);}
  }
  foreach(var n in sky){int id=n.SourceId,g=InFalsusStudio.Core.SkyGroups.GroupOf(n);bool auto=InFalsusStudio.Core.SkyGroups.Automatic(n),last=true;double end=n.EndMs;
   if(!auto&&g>=0)last=end>=endByGroup[g]-.000001;
   if(auto){long bucket=(long)Math.Floor(end/2);for(long b=bucket-1;b<=bucket+1&&last;b++){List<InFalsusStudio.Core.SpcEvent> row;if(automaticStarts.TryGetValue(b,out row))foreach(var next in row)if(!System.Object.ReferenceEquals(n,next)&&InFalsusStudio.Core.SkyGroups.Joins(n,next)){last=false;break;}}}
   if(last)result.Add(id);
  }
  tails[o]=result;
  // Use a single longitudinal shader coordinate for each connected group.
  // Resetting distance to zero at every SPC segment made internal boundaries
  // look like illuminated heads/tails, even though their bracket was hidden.
  var parents=new Dictionary<int,int>();var explicitRoot=new Dictionary<int,int>();
  foreach(var n in sky){int id=n.SourceId;parents[id]=id;}
  foreach(var n in sky){int id=n.SourceId,g=InFalsusStudio.Core.SkyGroups.GroupOf(n);bool auto=InFalsusStudio.Core.SkyGroups.Automatic(n);
   if(!auto&&g>=0){int previous;if(explicitRoot.TryGetValue(g,out previous))parents[Root(parents,id)]=Root(parents,previous);else explicitRoot[g]=id;}
   if(auto){long bucket=(long)Math.Floor(n.EndMs/2);for(long b=bucket-1;b<=bucket+1;b++){List<InFalsusStudio.Core.SpcEvent> row;if(automaticStarts.TryGetValue(b,out row))foreach(var next in row)if(!System.Object.ReferenceEquals(n,next)&&InFalsusStudio.Core.SkyGroups.Joins(n,next))parents[Root(parents,next.SourceId)]=Root(parents,id);}}
  }
  var byRoot=new Dictionary<int,StripSpan>();var byId=new Dictionary<int,StripSpan>();
  foreach(var n in sky){int id=n.SourceId,root=Root(parents,id);double start=n.TimeMs,end=n.EndMs;StripSpan span;if(!byRoot.TryGetValue(root,out span)){span=new StripSpan(start,end);byRoot.Add(root,span);}else{span.Start=Math.Min(span.Start,start);span.End=Math.Max(span.End,end);}byId[id]=span;}
  spans[o]=byId;
 }
 private static bool First(object o,object note){var c=InFalsusStudio.Performance.RendererAccess.Connections((NoteRenderer)o);return c!=null&&c.Starts.Contains(((SpcEvent)note).SourceId);}
 private static Vector3 SkyPoint(object o,double x,double time){return InFalsusStudio.Performance.FastScrollHooks.SkyPoint((NoteRenderer)o,x,time);}
 private static SkyRange RangeAt(object n,double time){return ChartMath.SkyAt((SpcEvent)n,time);}
 internal static void Sky(object o,object note,Range[] state){
  var n=(SpcEvent)note;var renderer=(NoteRenderer)o;bool first=First(o,note);var p=InFalsusStudio.Performance.RendererAccess.Profile(renderer);float half=p.centralHalfWidth;
  double startMs=n.TimeMs,endMs=n.EndMs;
  if(state[0].Any&&InFalsusStudio.Performance.PreviewFrustum.Surface((NoteRenderer)o,state[0],false)){
   var s=Hooks.Copy(o,state[0],"sky-"+n.SourceId,NativeVisualPlugin.Mat("SkyArea Variant"));float scale=NativeTrackLength/Mathf.Max(.01f,p.stageFar);
   Dictionary<int,StripSpan> byId;StripSpan span=null;if(spans.TryGetValue(o,out byId))byId.TryGetValue(n.SourceId,out span);
   double stripStart=span==null?startMs:span.Start,stripEnd=span==null?endMs:span.End;float stripZ=NativeVisualPlugin.Z(o,stripStart),length=Mathf.Max(.00001f,Mathf.Abs(NativeVisualPlugin.Z(o,stripEnd)-stripZ)*scale);
   // _lA._aB (0x535670): UV0 = absolute normalized X / note fraction;
   // UV1 = normalized left-edge distance / distance along strip;
   // UV2 = normalized width / whole visual length. UV2.x is NOT world width.
   if(!s.ReusedSource||s.SkyStart!=stripStart||s.SkyEnd!=stripEnd||s.SkyScale!=scale){
   IList<Vector2> original=s.UsingPreviewCopy?(IList<Vector2>)s.PreviewUvs:s.SourceUvs??s.Mesh.uv;IList<Vector3> positions=s.UsingPreviewCopy?(IList<Vector3>)s.PreviewVertices:s.SourceVertices??s.Mesh.vertices;var u0=s.SkyUv0;var u1=s.SkyUv1;var u2=s.SkyUv2;u0.Clear();u1.Clear();u2.Clear();var widths=new Dictionary<float,float>();
   for(int i=0;i<positions.Count;i++){
    double time=startMs+original[i].y*400d;float width;
    if(!widths.TryGetValue(original[i].y,out width)){width=(float)RangeAt(note,time).Width;widths.Add(original[i].y,width);}
    float fraction=(float)((time-stripStart)/Math.Max(.00001,stripEnd-stripStart));
    u0.Add(new Vector2((positions[i].x/half+1)*.5f,fraction));
    // Interpolate physical edge distance, not side fraction times varying width.
    u1.Add(new Vector2(original[i].x*Mathf.Max(0,width),Mathf.Abs(positions[i].z-stripZ)*scale));
    u2.Add(new Vector2(Mathf.Max(0,width),length));
   }
   s.Mesh.SetUVs(0,u0);s.Mesh.SetUVs(1,u1);s.Mesh.SetUVs(2,u2);
    s.SkyStart=stripStart;s.SkyEnd=stripEnd;s.SkyScale=scale;
   }
   float progress=-NativeVisualPlugin.Z(o,0)*scale;
   s.Material.SetFloat("_VisualStart",stripZ*scale+progress);s.Material.SetFloat("_VisualLength",length);s.Material.SetFloat("_VisualChartProgress",progress);
   HashSet<int> last;bool isLast=tails.TryGetValue(o,out last)&&last.Contains(n.SourceId);
   if(!s.SkyMaterialReady||s.SkyMaterialFirst!=first)s.Material.SetFloat("_IsFirstOfGroup",first?1:0);
   if(!s.SkyMaterialReady||s.SkyMaterialLast!=isLast)s.Material.SetFloat("_IsLastOfGroup",isLast?1:0);
   s.SkyMaterialFirst=first;s.SkyMaterialLast=isLast;
   // Exact SafeAreaColorDetails defaults passed by SkySafeArea._KZ.
   // Its dot alpha/noise is left intact. The shader adapter converts only
   // the native Gamma-space RGB output for Studio's Linear framebuffer,
   // with B2 center opacity 0.38, center Gamma white mix 0.36,
   // and output saturation 1.6 in the paired shader bundle.
   bool ghost=(bool)NativeVisualPlugin.Get(o,"ghost");
   if(ghost||!s.SkyMaterialReady||s.SkyMaterialGhost!=ghost){
   s.Material.SetFloat("_TrackVisualLength",NativeTrackLength);s.Material.SetVector("_MissingRange",new Vector4(0,1,0,0));
   s.Material.SetVector("_Color",new Vector4(.688345373f,.627936959f,.971698105f,1));
   s.Material.SetVector("_EdgeCenterColor",new Vector4(1.35792816f,1.15355456f,4.23709488f,1));
   s.Material.SetVector("_EdgeGlowColor",new Vector4(.36470589f,.196078435f,.850980401f,0));
   s.Material.SetVector("_FrontBackColor",new Vector4(.496224105f,.441527218f,.773584902f,.129411772f));
   if(ghost)foreach(var key in new[]{"_Color","_EdgeCenterColor","_EdgeGlowColor","_FrontBackColor"}){var color=s.Material.GetVector(key);color.w*=.35f;s.Material.SetVector(key,color);}
   s.SkyMaterialReady=true;s.SkyMaterialGhost=ghost;
   }
   InFalsusStudio.Gameplay.SkyGameplayPalette.Apply(s.Material,n.SourceId,ghost);
   NativePresentation.DimSelected(o,note,s);
   // Remove only this note's legacy overlays, preserving the chart grid,
   // cursor and beat lines drawn outside this scope.
   foreach(var r in state)Hooks.Remove(r);
   Selection(o,note,startMs,endMs);
  }else{
   // A zero-duration SkyArea keeps its line (no fill mesh), but replaces
   // the grid bracket if one was emitted by the editor.
   if(state[0].Any){foreach(var r in state)Hooks.Remove(r);}else Hooks.Remove(state[2]);
   Selection(o,note,startMs,endMs);
  }
  if(first&&(!(bool)NativeVisualPlugin.Get(o,"hidePast")||Convert.ToDouble(NativeVisualPlugin.Get(o,"now"))<startMs)){
   var range=RangeAt(note,startMs);Bracket(o,SkyPoint(o,range.Left,startMs),false,p);Bracket(o,SkyPoint(o,range.Right,startMs),true,p);
  }
 }
 private static Vector3 SelectionPoint(StudioProfile p,ScrollTimeline scroll,double now,double x,double time){return new Vector3(StageSpace.AirX(p,x),p.skyHeight+.006f,(float)(scroll.Delta(time,now)*p.UnitsPerMs));}
 private static void Selection(object o,object note,double start,double end){
  var n=(SpcEvent)note;if(!InFalsusStudio.Performance.RendererAccess.Selected((NoteRenderer)o,n.SourceId))return;
  double now=(double)NativeVisualPlugin.Get(o,"now");if((bool)NativeVisualPlugin.Get(o,"hidePast"))start=Math.Max(start,now);
  var p=(StudioProfile)NativeVisualPlugin.Get(o,"p");var scroll=(ScrollTimeline)NativeVisualPlugin.Get(o,"scroll");
  foreach(double time in new[]{start,end}){var r=ChartMath.SkyAt(n,time);NativePresentation.SelectedOutlineLine(o,SelectionPoint(p,scroll,now,r.Left,time),SelectionPoint(p,scroll,now,r.Right,time));}
  int segments=Mathf.Clamp(Mathf.CeilToInt((float)(end-start)/24),1,128);
  for(int i=0;i<segments;i++){double a=start+(end-start)*i/segments,b=start+(end-start)*(i+1)/segments;var ra=ChartMath.SkyAt(n,a);var rb=ChartMath.SkyAt(n,b);
   NativePresentation.SelectedOutlineLine(o,SelectionPoint(p,scroll,now,ra.Left,a),SelectionPoint(p,scroll,now,rb.Left,b));
   NativePresentation.SelectedOutlineLine(o,SelectionPoint(p,scroll,now,ra.Right,a),SelectionPoint(p,scroll,now,rb.Right,b));
  }
 }
 private static void Bracket(object o,Vector3 position,bool right,object p){
  float near=NativeVisualPlugin.N(p,"stageNear"),far=NativeVisualPlugin.N(p,"stageFar");var camera=(Camera)NativeVisualPlugin.Get(o,"camera");if(position.z<near||position.z>far||camera.WorldToScreenPoint(position).z<=camera.nearClipPlane)return;
  var src=NativeVisualPlugin.Meshes["bracket"];float scale=2*NativeVisualPlugin.N(p,"centralHalfWidth")/NativeAirWidth;
  if(InFalsusStudio.Performance.PreviewFrustum.Playing){var source=src.bounds;var center=source.center;center.x*=right?-1:1;var bounds=new Bounds(position+center*scale,source.size*Mathf.Abs(scale));bounds.Expand(.0002f);if(!InFalsusStudio.Performance.PreviewFrustum.Visible(camera,bounds))return;}
  var s=NativeVisualPlugin.Visuals(o).Take("skyhead",NativeVisualPlugin.Mat(BracketMaterial));
  var vertices=src.vertices;for(int i=0;i<vertices.Length;i++)vertices[i]=position+new Vector3((right?-1:1)*vertices[i].x,vertices[i].y,vertices[i].z)*scale;
  var triangles=src.triangles;if(right)Flip(triangles);NativeTrack.ClipMesh(s.Mesh,vertices,src.uv,triangles,near,far);
  Ghost(o,s,NativeVisualPlugin.Mat(BracketMaterial));
 }
 internal static void Flick(object o,object note,Range[] state){
  var p=NativeVisualPlugin.Get(o,"p");var range=NativeVisualPlugin.T("Core.ChartMath").GetMethod("FlickRange",NativeVisualPlugin.F).Invoke(null,new[]{note});var air=NativeVisualPlugin.T("StageSpace").GetMethod("AirX",NativeVisualPlugin.F);
  float xl=(float)air.Invoke(null,new[]{p,NativeVisualPlugin.Get(range,"Left")}),xr=(float)air.Invoke(null,new[]{p,NativeVisualPlugin.Get(range,"Right")});bool right=Convert.ToDouble(NativeVisualPlugin.Call(note,"Get",4,0d))!=4;
  float width=Mathf.Max(.001f,(float)NativeVisualPlugin.P(range,"Width"));float scale=2*NativeVisualPlugin.N(p,"centralHalfWidth")/NativeAirWidth;
  float z=(float)NativeVisualPlugin.Call(o,"FlickZ",note),y=NativeVisualPlugin.N(p,"skyHeight")+.019f,origin=right?xl:xr,sign=right?-1:1;
  // Native prefab v5-3 is the yellow head (root rotated 180 degrees),
  // v5-3R the green head. Asset suffixes refer to prefab space, not SPC's
  // direction flag: SPC 4 uses green / right-raised, SPC 16 yellow / left-raised.
  var cap=NativeVisualPlugin.Meshes["flick"];var capMaterial=NativeVisualPlugin.Mat(right?"Mat.001":"Mat.001 R");var c=NativeVisualPlugin.Visuals(o).Take("flickcap",capMaterial);var vertices=cap.vertices;
  c.NoteId=(int)NativeVisualPlugin.Get(note,"SourceId");
  // Use the prefab's separate bevelled solid head, native atlas UV and
  // local offset, rather than extending it across the complete note.
  for(int i=0;i<vertices.Length;i++)vertices[i]=new Vector3(origin+sign*(vertices[i].x+(right?-.799000025f:-.797340989f))*scale,y+(vertices[i].y-cap.bounds.min.y)*scale,z+vertices[i].z*scale);
  var indices=cap.triangles;if(right)Flip(indices);NativeTrack.ClipMesh(c.Mesh,vertices,cap.uv,indices,NativeVisualPlugin.N(p,"stageNear"),NativeVisualPlugin.N(p,"stageFar"));Ghost(o,c,capMaterial);
  var body=NativeVisualPlugin.Meshes["plane"];var material=NativeVisualPlugin.Mat(right?"FlickV5 Left":"FlickV5 Right");var b=NativeVisualPlugin.Visuals(o).Take("flickbody",material);vertices=body.vertices;
  // FlickNoteV3._N (0x472890) moves only the far two vertices. The short
  // atlas is selected below width .49; the native shader scales only Bar.
  float originalWidth=body.vertices[1].x-body.vertices[2].x;
  for(int i=0;i<vertices.Length;i++){
   if(i==2||i==3)vertices[i].x+=(1-width)*originalWidth;
   vertices[i]=new Vector3(origin+sign*(vertices[i].x+(right?-.791000009f:-.788999975f))*scale,y+(vertices[i].y-body.bounds.min.y)*scale,z);
  }
  indices=body.triangles;if(!right)Flip(indices);NativeTrack.ClipMesh(b.Mesh,vertices,body.uv,indices,NativeVisualPlugin.N(p,"stageNear"),NativeVisualPlugin.N(p,"stageFar"));
  b.Material.SetFloat("_Scale",width);b.Material.SetFloat("_Opacity",(bool)NativeVisualPlugin.Get(o,"ghost")?.35f:1);
  b.Material.SetTexture("_Base",width<.49f?NativeVisualPlugin.Textures[right?"flicknote-baseshort":"flicknoter-baseshort"]:material.GetTexture("_Base"));
  Ghost(o,b,material);NativePresentation.DimSelected(o,note,b);NativePresentation.DimSelected(o,note,c);
  foreach(var r in state)Hooks.Remove(r);
  // Original Draw() still needs the source triangles to register air picks
  // and ground projections. Removal is deferred until MeshBatch.Commit.
  if((bool)NativeVisualPlugin.Call(o,"IsSelected",(int)NativeVisualPlugin.Get(note,"SourceId"))){Outline(o,b);Outline(o,c);}
 }
 private sealed class OutlineEdge{internal Vector3 A,B,Normal;internal int Count;internal bool Sharp;}
 internal static void Outline(object o,Slot slot){
  var v=slot.Mesh.vertices;var t=slot.Mesh.triangles;var edges=new Dictionary<ulong,OutlineEdge>();
  // Preserve the existing F5 weld exactly, but format each vertex once.
  var keys=new string[v.Length];var ids=new int[v.Length];var canonical=new Dictionary<string,int>(StringComparer.Ordinal);
  for(int i=0;i<v.Length;i++){keys[i]=v[i].ToString("F5");int id;if(!canonical.TryGetValue(keys[i],out id)){id=canonical.Count;canonical.Add(keys[i],id);}ids[i]=id;}
  for(int i=0;i<t.Length;i+=3){var normal=Vector3.Cross(v[t[i+1]]-v[t[i]],v[t[i+2]]-v[t[i]]).normalized;for(int k=0;k<3;k++){
   int ia=t[i+k],ib=t[i+(k+1)%3];var a=v[ia];var b=v[ib];if(string.CompareOrdinal(keys[ia],keys[ib])>0){var swap=a;a=b;b=swap;}
   uint lo=(uint)Math.Min(ids[ia],ids[ib]),hi=(uint)Math.Max(ids[ia],ids[ib]);ulong key=((ulong)lo<<32)|hi;
   OutlineEdge e;if(!edges.TryGetValue(key,out e)){e=new OutlineEdge{A=a,B=b,Normal=normal};edges.Add(key,e);}else if(Mathf.Abs(Vector3.Dot(e.Normal,normal))<.95f)e.Sharp=true;e.Count++;
  }}
  foreach(var e in edges.Values)if(e.Count==1||e.Sharp)NativePresentation.SelectedOutlineLine(o,e.A,e.B);
 }
 internal static void HeadPicks(object o){
  // Keep every original pick triangle. Extend only the solid native head,
  // which occupies pixels outside the original flat Flick silhouette.
  if((bool)NativeVisualPlugin.Get(o,"ghost"))return;
  var hitType=NativeVisualPlugin.T("NoteRenderer+HitTriangle");var hits=(IList)NativeVisualPlugin.Get(o,"hits");
  foreach(var s in NativeVisualPlugin.Visuals(o).Slots.Values.Where(s=>s.Used&&s.NoteId>=0&&s.Object.name=="NativeVisual flickcap")){
   var v=s.Mesh.vertices;var t=s.Mesh.triangles;
   for(int i=0;i<t.Length;i+=3){var hit=Activator.CreateInstance(hitType);NativeVisualPlugin.Set(hit,"A",v[t[i]]);NativeVisualPlugin.Set(hit,"B",v[t[i+1]]);NativeVisualPlugin.Set(hit,"C",v[t[i+2]]);NativeVisualPlugin.Set(hit,"Id",s.NoteId);NativeVisualPlugin.Set(hit,"Air",true);hits.Add(hit);}
  }
 }
 private static void Flip(int[] triangles){for(int i=0;i<triangles.Length;i+=3){int k=triangles[i];triangles[i]=triangles[i+1];triangles[i+1]=k;}}
 private static void Ghost(object o,Slot s,Material original){
  float alpha=(bool)NativeVisualPlugin.Get(o,"ghost")?.35f:1;
  foreach(var key in new[]{"_BaseColor","_Color"})if(s.Material.HasProperty(key)){var color=original.GetColor(key);color.a*=alpha;s.Material.SetColor(key,color);}
 }
}
}
