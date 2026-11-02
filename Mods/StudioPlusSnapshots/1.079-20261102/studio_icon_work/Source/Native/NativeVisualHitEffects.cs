using System;
using System.IO;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;
namespace InFalsusStudio.NativeVisual {
// Original Sprite geometry, atlas pointers and animation key times are retained.
// Only the position/scale maps the native 0.8-wide stage to Studio's stage.
internal static class NativeHitEffects {
 [Serializable] internal sealed class Catalog { public float nativeTrackHalfWidth; public Sequence[] sequences; }
 [Serializable] internal sealed class Pose { public Vector3 scale,position; public Quaternion rotation; public bool flipX,flipY; }
 [Serializable] internal sealed class Frame { public float time; public string asset; [NonSerialized] public Sprite sprite; }
 [Serializable] internal sealed class Sequence {
  public string kind,sourceClip; public float sampleRate,duration; public Frame[] frames; public Pose[] poses; public Vector3 sourceAnchorLocal;
  internal Sprite At(double time){
   if(time<0||time>=duration)return null;int low=0,high=frames.Length;
   while(low<high){int mid=(low+high)/2;if(frames[mid].time<=time+0.000001)low=mid+1;else high=mid;}
   return low==0?null:frames[low-1].sprite;
  }
 }
 private sealed class Entry { internal object Note;internal string Kind;internal double Start,End,ActualEnd;internal int Lane; }
 private sealed class Pool {
  internal Transform Parent;internal int Layer,Used;internal readonly List<SpriteRenderer> Sprites=new List<SpriteRenderer>();
  internal readonly List<NativeSkyFeedback.Region> SkyRegions=new List<NativeSkyFeedback.Region>();
  internal object Source;internal Entry[] Entries=new Entry[0];internal double[] MaxEnds=new double[0];
  internal Entry[] Bursts=new Entry[30];internal double[] BurstAges=new double[30];
  internal Pool(object renderer){var obj=NativeVisualPlugin.Obj(NativeVisualPlugin.Get(renderer,"glow"));Parent=obj.transform.parent;Layer=obj.layer;}
  internal void Clear(){Used=0;}
  internal void End(){for(int i=Used;i<Sprites.Count;i++)Sprites[i].enabled=false;}
  internal void Hide(){Used=0;End();}
  internal void Dispose(){foreach(var r in Sprites)if(r)UnityEngine.Object.Destroy(r.gameObject);Sprites.Clear();}
  internal void Emit(Sequence seq,double age,int lane,Vector3 position,float factor,Quaternion surfaceRotation){
   var sprite=seq.At(age);if(!sprite||Used>=384)return;
   SpriteRenderer r;
   if(Used<Sprites.Count)r=Sprites[Used];else{
    var obj=new GameObject("NativeVisual native hit frame");obj.layer=Layer;obj.transform.SetParent(Parent,false);
    r=obj.AddComponent<SpriteRenderer>();r.sharedMaterial=NativeVisualPlugin.Mat("spritesdefault");
    r.shadowCastingMode=ShadowCastingMode.Off;r.receiveShadows=false;r.lightProbeUsage=LightProbeUsage.Off;r.reflectionProbeUsage=ReflectionProbeUsage.Off;Sprites.Add(r);
   }
   Used++;var pose=seq.poses[Mathf.Clamp(lane,0,seq.poses.Length-1)];
   r.enabled=true;r.sprite=sprite;r.color=Color.white;r.flipX=pose.flipX;r.flipY=pose.flipY;
   bool ground=seq.kind=="main"||seq.kind=="tap"||seq.kind=="hold";
   var correction=ground?GroundRetarget(pose.rotation,surfaceRotation*Vector3.right):surfaceRotation;
   r.transform.localPosition=position;r.transform.localRotation=correction*pose.rotation;r.transform.localScale=pose.scale*factor;
  }
 }
 private static readonly Dictionary<string,Sequence> Sequences=new Dictionary<string,Sequence>();
 private static readonly Dictionary<object,Pool> Pools=new Dictionary<object,Pool>();
 private static MethodInfo invalidReason,skyAt,flickRange,clamp,airX;
 private static float nativeHalf;
 internal static void Init(string folder){
  var catalog=ReadCatalog(Path.Combine(folder,"Effects/native-effects.bin"));
  if(catalog==null||catalog.sequences==null||catalog.nativeTrackHalfWidth<=0)throw new Exception("Native effect timeline missing");nativeHalf=catalog.nativeTrackHalfWidth;
  foreach(var seq in catalog.sequences){
   if(seq.frames==null||seq.frames.Length==0||seq.poses==null||seq.poses.Length==0)throw new Exception("Invalid native effect sequence "+seq.kind);
   foreach(var f in seq.frames)if(!string.IsNullOrEmpty(f.asset)){f.sprite=NativeVisualPlugin.Bundle.LoadAsset<Sprite>(f.asset);if(!f.sprite||!f.sprite.texture)throw new Exception("Native hit frame missing: "+f.asset);}
   Sequences.Add(seq.kind,seq);
  }
  foreach(var name in new[]{"main","tap","hold","flickleft","flickright"})if(!Sequences.ContainsKey(name))throw new Exception("Missing native hit clip "+name);
  var math=NativeVisualPlugin.T("Core.ChartMath");invalidReason=math.GetMethod("InvalidReason",NativeVisualPlugin.F);skyAt=math.GetMethod("SkyAt",NativeVisualPlugin.F);flickRange=math.GetMethod("FlickRange",NativeVisualPlugin.F);
  clamp=NativeVisualPlugin.T("Core.AirReachBounds").GetMethod("Clamp",NativeVisualPlugin.F);airX=NativeVisualPlugin.T("StageSpace").GetMethod("AirX",NativeVisualPlugin.F);
 }
 private static string String(BinaryReader r){int len=r.ReadInt32();if(len<0||len>2048)throw new Exception("Invalid native effect string");return System.Text.Encoding.UTF8.GetString(r.ReadBytes(len));}
 private static Vector3 Vector(BinaryReader r){return new Vector3(r.ReadSingle(),r.ReadSingle(),r.ReadSingle());}
 private static Catalog ReadCatalog(string path){using(var r=new BinaryReader(File.OpenRead(path))){
  if(r.ReadUInt32()!=0x314e5846)throw new Exception("Invalid native effect catalog");var c=new Catalog();c.nativeTrackHalfWidth=r.ReadSingle();int total=r.ReadInt32();if(total!=5)throw new Exception("Unexpected native effect count");c.sequences=new Sequence[total];
  for(int i=0;i<total;i++){
   var s=new Sequence();s.kind=String(r);s.sourceClip=String(r);s.sampleRate=r.ReadSingle();s.duration=r.ReadSingle();int frames=r.ReadInt32();if(frames<1||frames>1000)throw new Exception("Invalid native frame count");s.frames=new Frame[frames];
   for(int j=0;j<frames;j++)s.frames[j]=new Frame{time=r.ReadSingle(),asset=String(r)};
   int poses=r.ReadInt32();if(poses<1||poses>16)throw new Exception("Invalid native pose count");s.poses=new Pose[poses];
   for(int j=0;j<poses;j++){var p=new Pose();p.scale=Vector(r);p.position=Vector(r);p.rotation=new Quaternion(r.ReadSingle(),r.ReadSingle(),r.ReadSingle(),r.ReadSingle());p.flipX=r.ReadBoolean();p.flipY=r.ReadBoolean();s.poses[j]=p;}
   s.sourceAnchorLocal=Vector(r);c.sequences[i]=s;
  }
  return c;
 }}
 private static Pool For(object renderer){Pool p;if(!Pools.TryGetValue(renderer,out p)){p=new Pool(renderer);Pools.Add(renderer,p);}return p;}
 internal static void Invalidate(object renderer){Pool p;if(renderer!=null&&Pools.TryGetValue(renderer,out p))p.Source=null;}
 internal static void DrawCharacterBursts(object renderer,object events,double now,bool enabled,double lowerBound){
  if(!enabled||NativeJudgementPresentation.Character.Value==HitCharacter.None)return;
  var pool=For(renderer);Index(pool,events);Array.Clear(pool.Bursts,0,pool.Bursts.Length);
  int lo=0,hi=pool.MaxEnds.Length;while(lo<hi){int mid=(lo+hi)/2;if(pool.MaxEnds[mid]+500<now)lo=mid+1;else hi=mid;}
  for(int i=lo;i<pool.Entries.Length;i++){
   var e=pool.Entries[i];if(e.Start>now)break;if(e.Kind!="Tap"&&e.Kind!="Hold")continue;
   bool held=e.Kind=="Hold"&&now<e.ActualEnd;
   if(e.Kind=="Tap"?e.Start<lowerBound:e.ActualEnd<lowerBound)continue;
   double age=CharacterAge(now,e.Start,e.ActualEnd,e.Kind=="Hold");if(age<0||age>=500)continue;
   int width=e.Lane==0||e.Lane==5?1:Math.Max(1,Math.Min(5-e.Lane,(int)Math.Ceiling(NativeVisualPlugin.P(e.Note,"GroundWidth"))));
   int key=Math.Max(0,Math.Min(5,e.Lane))*5+width;var previous=pool.Bursts[key];
   // Track._iBA ignores retriggers during the first 50 ms of a pulse.
   bool previousHeld=previous!=null&&previous.Kind=="Hold"&&now<previous.ActualEnd;
   if(previous==null||held&&!previousHeld||held==previousHeld&&e.Start-previous.Start>50){pool.Bursts[key]=e;pool.BurstAges[key]=age;}
  }
  int used=0;for(int i=0;i<pool.Bursts.Length&&used<12;i++){var e=pool.Bursts[i];if(e==null)continue;int seed=(int)NativeVisualPlugin.Get(e.Note,"SourceId");NativeJudgementPresentation.Burst(renderer,e.Lane,i%5,pool.BurstAges[i],seed);used++;}
 }
 // Keep the contact envelope lit while held. The original shader scrolls its
 // backing/beam textures independently of _Progress; replaying the whole
 // envelope or reseeding each cycle makes the whole character layer flash.
 // Let the normal native tail finish after release; Tap timing is unchanged.
 internal static double CharacterAge(double now,double start,double end,bool hold){
  double age=now-start;if(!hold||age<0)return age;
  if(now<end)return Math.Min(age,50);
  return Math.Min(Math.Max(0,end-start),50)+now-end;
 }
 internal static Quaternion GroundRetarget(Quaternion nativePose,Vector3 targetAcross){
  // Match the source sprite's actual across vector to the judgement surface.
  // This also rotates the upright main flash, which previously stayed vertical
  // on both walls; source poses that already tilt are not tilted twice.
  return Quaternion.FromToRotation(nativePose*Vector3.right,targetAcross.normalized);
 }
 internal static float[] TrackPowers(object renderer,object events,double now,bool enabled,double lowerBound){
  if(InFalsusStudio.Gameplay.GameplaySession.Active)return InFalsusStudio.Gameplay.GameplaySession.Presentation.Powers();
  var result=new float[6];if(!enabled)return result;var pool=For(renderer);Index(pool,events);
  int lo=0,hi=pool.MaxEnds.Length;while(lo<hi){int mid=(lo+hi)/2;if(pool.MaxEnds[mid]+500<now)lo=mid+1;else hi=mid;}
  for(int i=lo;i<pool.Entries.Length;i++){
   var e=pool.Entries[i];if(e.Start>now)break;if(e.Kind!="Tap"&&e.Kind!="Hold")continue;
   if(e.Kind=="Tap"&&e.Start<lowerBound)continue;
   double end=e.Kind=="Hold"?e.ActualEnd:e.Start;float power=NativeTrackLines.Fade(now,end);if(power<=0)continue;
   int width=Math.Max(1,(int)Math.Ceiling(NativeVisualPlugin.P(e.Note,"GroundWidth")));
   int last=e.Lane==0||e.Lane==5?e.Lane:Math.Min(4,e.Lane+width-1);
   for(int lane=Math.Max(0,e.Lane);lane<=last;lane++)result[lane]=Math.Max(result[lane],power);
  }return result;
 }
 private static void Index(Pool pool,object events){
  if(ReferenceEquals(pool.Source,events))return;var entries=new List<Entry>();
  foreach(var note in (IEnumerable)events){
   if(InFalsusStudio.TimeGroups.TimingGroups.NoInput(note as InFalsusStudio.Core.SpcEvent))continue;
   string kind=NativeVisualPlugin.Get(note,"Kind").ToString();if(kind!="Tap"&&kind!="Hold"&&kind!="Flick"&&kind!="SkyArea")continue;
   if(!string.IsNullOrEmpty((string)invalidReason.Invoke(null,new[]{note})))continue;
   double start=NativeVisualPlugin.P(note,"TimeMs"),end=NativeVisualPlugin.P(note,"EndMs");
   if(kind=="Tap"||kind=="Flick")end=start+1300;
   entries.Add(new Entry{Note=note,Kind=kind,Start=start,ActualEnd=end,End=kind=="SkyArea"?end+300:end,Lane=kind=="Tap"||kind=="Hold"?(int)NativeVisualPlugin.P(note,"Lane"):0});
  }
  pool.Entries=entries.OrderBy(e=>e.Start).ToArray();pool.MaxEnds=new double[pool.Entries.Length];double max=double.NegativeInfinity;
  for(int i=0;i<pool.Entries.Length;i++){max=Math.Max(max,pool.Entries[i].End);pool.MaxEnds[i]=max;}pool.Source=events;
 }
 internal static void Draw(object renderer,object events,double now,bool enabled,object cursor,double playbackStart){
  var pool=For(renderer);pool.Clear();var glow=NativeVisualPlugin.Get(renderer,"glow");NativeVisualPlugin.Call(glow,"Clear");NativeVisualPlugin.Set(glow,"ClipX",false);
  NativeVisualPlugin.Set(renderer,"<EffectCount>k__BackingField",0);
  if(!enabled){pool.End();NativeVisualPlugin.Call(glow,"Commit");return;}
  Index(pool,events);var profile=NativeVisualPlugin.Get(renderer,"p");float scale=NativeVisualPlugin.N(profile,"centralHalfWidth")/nativeHalf;int count=0;
  // Prefix maximum end times prune inactive intervals without changing chart ordering.
  int lo=0,hi=pool.MaxEnds.Length;while(lo<hi){int mid=(lo+hi)/2;if(pool.MaxEnds[mid]<now)lo=mid+1;else hi=mid;}
  Entry activeSky=null,tailSky=null;
  for(int i=lo;i<pool.Entries.Length;i++){
   var e=pool.Entries[i];if(e.Start>now+300)break;if(e.End<now)continue;
   if(e.Kind=="SkyArea"){
    if(now<e.Start)continue;
    // Retail Track._gBA (676930/676961/676980) updates ONE material and
    // ONE particle grid. Later active notes replace its range; after release
    // Track.Update (66f614) fades that shared contact, not each old segment.
    if(now<e.ActualEnd)activeSky=e;
    else if(tailSky==null||e.ActualEnd>=tailSky.ActualEnd)tailSky=e;
    continue;
   }
   if(count>=128)continue;
   if(e.Kind=="Tap"||e.Kind=="Hold"){
    if(now<e.Start||(e.Kind=="Tap"&&e.Start<playbackStart))continue;if(e.Kind=="Hold"&&now>=e.End)continue;
    var pos=(Vector3)NativeVisualPlugin.Call(renderer,"Ground",e.Note,.5f,.02f);pos.y+=.012f;
    // Side lanes lie on inclined surfaces; the source animation retains its X rotation.
    var left=(Vector3)NativeVisualPlugin.Call(renderer,"Ground",e.Note,0f,.02f);var right=(Vector3)NativeVisualPlugin.Call(renderer,"Ground",e.Note,1f,.02f);
    var floor=Quaternion.FromToRotation(Vector3.right,(right-left).normalized);double age=(now-e.Start)*.001;
    if(e.Start>=playbackStart)pool.Emit(Sequences["main"],age,e.Lane,pos,scale,floor);
    if(e.Kind=="Tap")pool.Emit(Sequences["tap"],age,e.Lane,pos,scale,floor);
    else pool.Emit(Sequences["hold"],age%Sequences["hold"].duration,e.Lane,pos,scale,floor);
    if(age<Sequences["main"].duration||e.Kind=="Hold")count++;
   }else if(e.Kind=="Flick"){
    double contact=cursor==null?e.Start:Convert.ToDouble(NativeVisualPlugin.Call(cursor,"VisualContactTime",e.Note));double age=(now-contact)*.001;
    if(contact<playbackStart||age<0||age>=Sequences["flickleft"].duration)continue;
    bool right=Convert.ToDouble(NativeVisualPlugin.Call(e.Note,"Get",4,0d))==4;var range=flickRange.Invoke(null,new[]{e.Note});
    double x=(double)NativeVisualPlugin.Get(range,right?"Right":"Left");x=(double)clamp.Invoke(null,new object[]{x});
    var position=new Vector3((float)airX.Invoke(null,new[]{profile,(object)x}),NativeVisualPlugin.N(profile,"skyHeight")+.035f,.015f);
    var seq=Sequences[right?"flickright":"flickleft"];var pose=seq.poses[0];var anchor=seq.sourceAnchorLocal;
    // The source prefab offsets assume its default head width. Map the actual bright
    // contact point instead so arbitrary SPC Flick ranges do not shift the flare.
    if(pose.flipX)anchor.x=-anchor.x;if(pose.flipY)anchor.y=-anchor.y;
    position-=pose.rotation*Vector3.Scale(anchor,pose.scale)*scale;
    pool.Emit(seq,age,0,position,scale,Quaternion.identity);count++;
   }
  }
  var skyContact=activeSky??tailSky;
  if(skyContact!=null){
   double sample=Math.Min(now,skyContact.ActualEnd);var range=skyAt.Invoke(null,new[]{skyContact.Note,(object)sample});double left=(double)NativeVisualPlugin.Get(range,"Left"),right=(double)NativeVisualPlugin.Get(range,"Right");
   NativeSkyFeedback.Draw(renderer,left,right,NativeSkyFeedback.Opacity(now,skyContact.ActualEnd),now,skyContact.Start,skyContact.ActualEnd);count++;
  }
  pool.End();NativeVisualPlugin.Set(renderer,"<EffectCount>k__BackingField",count);NativeVisualPlugin.Call(glow,"Commit");
 }
 internal static void DrawGameplay(object renderer,double now,bool enabled){
  var session=InFalsusStudio.Gameplay.GameplaySession.Presentation;var engine=InFalsusStudio.Gameplay.GameplaySession.Engine;
  var pool=For(renderer);pool.Clear();var glow=NativeVisualPlugin.Get(renderer,"glow");NativeVisualPlugin.Call(glow,"Clear");NativeVisualPlugin.Set(glow,"ClipX",false);NativeVisualPlugin.Set(renderer,"<EffectCount>k__BackingField",0);
  bool playing=InFalsusStudio.Gameplay.GameplaySession.Phase==InFalsusStudio.Gameplay.GameplayPhase.Playing;
  if(!enabled||!playing||NativeVisualPlugin.HitStyle.Value==HitEffectStyle.None){pool.End();NativeVisualPlugin.Call(glow,"Commit");return;}
  int count=0;var profile=NativeVisualPlugin.Get(renderer,"p");float scale=NativeVisualPlugin.N(profile,"centralHalfWidth")/nativeHalf;bool full=NativeVisualPlugin.HitStyle.Value==HitEffectStyle.Full;
  foreach(var pulse in session.Pulses){var e=pulse.Note.Source;double age=(now-pulse.Time)*.001;if(age<0||age>1.5||count>=128)continue;count++;
   if(!full&&age<.34)NativeVisualPlugin.Call(renderer,e.Kind==InFalsusStudio.Core.EventKind.Flick?"Flick":"GroundTap",e,age);
   if(e.Kind==InFalsusStudio.Core.EventKind.Tap||e.Kind==InFalsusStudio.Core.EventKind.Hold){
    if(NativeJudgementPresentation.Character.Value!=HitCharacter.None)NativeJudgementPresentation.Burst(renderer,e.Lane,(int)Math.Ceiling(e.GroundWidth),age*1000,e.SourceId);
    if(!full)continue;var pos=(Vector3)NativeVisualPlugin.Call(renderer,"Ground",e,.5f,.02f);pos.y+=.012f;
    var left=(Vector3)NativeVisualPlugin.Call(renderer,"Ground",e,0f,.02f);var right=(Vector3)NativeVisualPlugin.Call(renderer,"Ground",e,1f,.02f);var rotation=Quaternion.FromToRotation(Vector3.right,(right-left).normalized);
    pool.Emit(Sequences["main"],age,e.Lane,pos,scale,rotation);if(e.Kind==InFalsusStudio.Core.EventKind.Tap)pool.Emit(Sequences["tap"],age,e.Lane,pos,scale,rotation);
   }else if(full){bool right=e.Get(4)==4;var range=InFalsusStudio.Core.ChartMath.FlickRange(e);var seq=Sequences[right?"flickright":"flickleft"];var pose=seq.poses[0];var anchor=seq.sourceAnchorLocal;if(pose.flipX)anchor.x=-anchor.x;if(pose.flipY)anchor.y=-anchor.y;
    var p=(InFalsusStudio.StudioProfile)profile;var position=new Vector3(InFalsusStudio.StageSpace.AirX(p,right?range.Right:range.Left),p.skyHeight+.035f,.015f)-pose.rotation*Vector3.Scale(anchor,pose.scale)*scale;pool.Emit(seq,age,0,position,scale,Quaternion.identity);
   }
  }
  foreach(var n in engine.Notes){var e=n.Source;if(e.TimeMs>now)break;if(e.Kind!=InFalsusStudio.Core.EventKind.Hold||now>e.EndMs||!engine.Holding(n,InFalsusStudio.Gameplay.GameplaySession.InputState))continue;
   if(count++>=128)break;if(!full)NativeVisualPlugin.Call(renderer,"GroundHold",e,now);
   var pos=(Vector3)NativeVisualPlugin.Call(renderer,"Ground",e,.5f,.02f);pos.y+=.012f;var left=(Vector3)NativeVisualPlugin.Call(renderer,"Ground",e,0f,.02f);var right=(Vector3)NativeVisualPlugin.Call(renderer,"Ground",e,1f,.02f);var rotation=Quaternion.FromToRotation(Vector3.right,(right-left).normalized);
   if(full)pool.Emit(Sequences["hold"],(now-e.TimeMs)*.001%Sequences["hold"].duration,e.Lane,pos,scale,rotation);
   NativeJudgementPresentation.Burst(renderer,e.Lane,(int)Math.Ceiling(e.GroundWidth),50,e.SourceId);
  }
  // SPC2 particle emission uses the union of all captured visible regions;
  // a disjoint group's bounding box must not light up the gap between members.
  pool.SkyRegions.Clear();double skyLeft=1,skyRight=0;
  foreach(var n in engine.Notes){var e=n.Source;if(e.TimeMs>now)break;if(e.Kind!=InFalsusStudio.Core.EventKind.SkyArea||now>=e.EndMs||!engine.SkyActive(e.SourceId)||InFalsusStudio.TimeGroups.TimingGroups.Hidden(e,now))continue;
   var range=InFalsusStudio.Core.ChartMath.SkyAt(e,now);pool.SkyRegions.Add(new NativeSkyFeedback.Region(range.Left,range.Right,e.TimeMs,e.EndMs));skyLeft=Math.Min(skyLeft,range.Left);skyRight=Math.Max(skyRight,range.Right);if(!full)NativeVisualPlugin.Call(renderer,"Sky",e,now,range.Left,range.Right);
  }
  if(pool.SkyRegions.Count>0){if(full)NativeSkyFeedback.Draw(renderer,skyLeft,skyRight,1,now,regions:pool.SkyRegions);count++;}
  pool.End();NativeVisualPlugin.Set(renderer,"<EffectCount>k__BackingField",count);NativeVisualPlugin.Set(glow,"ClipX",false);NativeVisualPlugin.Call(glow,"Commit");
 }
 internal static void Hide(object renderer){Pool pool;if(Pools.TryGetValue(renderer,out pool))pool.Hide();}
 internal static void Dispose(object renderer){Pool pool;if(Pools.TryGetValue(renderer,out pool)){pool.Dispose();Pools.Remove(renderer);}}
 internal static void DisposeAll(){foreach(var pool in Pools.Values)pool.Dispose();Pools.Clear();Sequences.Clear();}
 internal static string Report(){return string.Join("\r\n",Sequences.Values.Select(s=>s.kind+"="+s.sourceClip+"; source_fps="+s.sampleRate+"; duration="+s.duration+"; frames="+s.frames.Length).ToArray())+"\r\nactive_native_sprite_renderers="+Pools.Values.Sum(p=>p.Used);}
}
}
