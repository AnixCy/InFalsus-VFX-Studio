using System;
using System.IO;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace InFalsusStudio.NativeVisual {
public sealed partial class NativeVisualPlugin {
 private IEnumerator SelfTest(){
  yield return null;yield return null;
  var outDir=Arg("-nativevisual-output",Path.Combine(folder,"Tests"));Directory.CreateDirectory(outDir);if(File.Exists(Path.Combine(outDir,"ERROR.txt")))File.Delete(Path.Combine(outDir,"ERROR.txt"));
  try{
   if(!Ready)throw new Exception("Adapter not ready");
   var boot=UnityEngine.Object.FindFirstObjectByType(T("StudioBootstrap"));
   var doc=T("Core.SpcDocument").GetMethod("Load",F).Invoke(null,new object[]{Arg("-nativevisual-test","")});
   var before=(byte[])Call(doc,"ToBytes");Call(boot,"LoadDocument",doc);
   Call(Get(boot,"transport"),"Seek",double.Parse(Arg("-nativevisual-now","58000"),System.Globalization.CultureInfo.InvariantCulture));
   Set(boot,"showUi",false);Set(boot,"showNotes",true);Set(boot,"showGrid",false);Set(boot,"showSongHud",false);Set(boot,"showCenterCombo",false);Set(boot,"hidePast",true);Set(boot,"autoCursor",true);
   bool editRegression=Arg("-nativevisual-edit-regression",null)!=null;
   if(editRegression){Set(boot,"showGrid",true);Set(boot,"hidePast",false);Set(boot,"showAirShadows",true);}
   bool presentationReview=Arg("-nativevisual-presentation-review",null)!=null;
   Call(boot,"RebuildGeometry");Call(boot,"LateUpdate");Call(Get(boot,"stage"),"Build",Get(boot,"Profile"));
   if(presentationReview){Set(boot,"selectedId",5);var selection=(ISet<int>)Get(boot,"selection");selection.Add(2);selection.Add(4);selection.Add(7);Call(boot,"LateUpdate");}
   var notes=Get(boot,"notes");var camera=(Camera)boot.GetType().GetProperty("ViewCamera",F).GetValue(boot,null);
   var hitsA=HitSnapshot(notes);var hitSetA=HitSet(notes);var picksA=PickSnapshot(notes,camera);var groundPicksA=PickSnapshot(notes,camera,1);
      Capture(camera,Path.Combine(outDir,"native.png"));
   var projectionsA=BatchSnapshot(Get(notes,"shadows"));
   if(presentationReview){
    var combo=Get(boot,"floorCombo");Call(combo,"Draw",10,true,N(Get(boot,"Profile"),"centralHalfWidth"));Capture(camera,Path.Combine(outDir,"combo10_selected.png"));
    var mesh=(Mesh)Get(combo,"mesh");var verts=mesh.vertices;var edgeColors=(List<Color>)Get(Get(notes,"edges"),"Colors");File.WriteAllText(Path.Combine(outDir,"presentation-checks.txt"),"combo_10_advance="+Mathf.Abs((verts[4].x+verts[5].x-verts[0].x-verts[1].x)*.5f).ToString("R")+"\r\ncombo_shader="+((Material)Get(combo,"material")).shader.name+"\r\nselected_outline_color="+PresentationHooks.SelectedColor.ToString("R")+"\r\norange_outline_vertices="+edgeColors.Count(c=>c.r>.95f&&Mathf.Abs(c.g-.55f)<.01f&&c.b<.05f));
    var outline=Visuals(notes).Slots.Values.FirstOrDefault(s=>s.Used&&s.Object.name=="NativeVisual selectionoutline");File.AppendAllText(Path.Combine(outDir,"presentation-checks.txt"),"\r\noutline_mesh="+(outline==null?"none":outline.Mesh.vertexCount+" vertices / "+outline.Mesh.triangles.Length+" indices / color "+outline.Material.GetColor("_Color")));
   }
   if(editRegression){
    File.WriteAllLines(Path.Combine(outDir,"editing-checks.txt"),EditingChecks(notes,doc,camera));
    bool oldCenter=CenterEnabled.Value;CenterEnabled.Value=false;Call(boot,"LateUpdate");Capture(camera,Path.Combine(outDir,"center_off.png"));CenterEnabled.Value=oldCenter;Call(boot,"LateUpdate");
   }
   if(Arg("-nativevisual-visibility",null)!=null){
    var renderers=Visuals(notes).Slots.Values.Select(s=>s.Object.GetComponent<MeshRenderer>()).Where(r=>r.gameObject.activeInHierarchy&&r.enabled).ToArray();
    File.WriteAllLines(Path.Combine(outDir,"mesh-screen.txt"),Visuals(notes).Slots.Values.Where(s=>s.Object.activeInHierarchy).Select(s=>{var points=s.Mesh.vertices.Select(v=>camera.WorldToViewportPoint(s.Object.transform.TransformPoint(v))).ToArray();return s.Object.name+" queue="+s.Material.renderQueue+" vertices="+points.Length+" viewport="+(points.Length==0?"empty":points.Min(v=>v.x)+","+points.Min(v=>v.y)+","+points.Max(v=>v.x)+","+points.Max(v=>v.y));}));
    foreach(var r in renderers)r.enabled=false;Capture(camera,Path.Combine(outDir,"native_empty.png"));foreach(var r in renderers)r.enabled=true;
   }
   bool startup=Enabled.Value;Enabled.Value=false;Call(boot,"RebuildGeometry");Call(boot,"LateUpdate");Call(Get(boot,"stage"),"Build",Get(boot,"Profile"));Post(camera);
   notes=Get(boot,"notes");
   var hitsB=HitSnapshot(notes);var hitSetB=HitSet(notes);var picksB=PickSnapshot(notes,camera);var groundPicksB=PickSnapshot(notes,camera,1);var projectionsB=BatchSnapshot(Get(notes,"shadows"));Capture(camera,Path.Combine(outDir,"original.png"));
   var after=(byte[])Call(doc,"ToBytes");var baselineWatch=System.Diagnostics.Stopwatch.StartNew();for(int i=0;i<20;i++)Call(boot,"LateUpdate");baselineWatch.Stop();
   Enabled.Value=startup;Call(boot,"RebuildGeometry");Call(boot,"LateUpdate");Call(Get(boot,"stage"),"Build",Get(boot,"Profile"));Post(camera);
   notes=Get(boot,"notes");
   var watch=System.Diagnostics.Stopwatch.StartNew();for(int i=0;i<20;i++)Call(boot,"LateUpdate");watch.Stop();
   var report=new[]{"chart_bytes_unchanged="+before.SequenceEqual(after),"hit_geometry_unchanged="+(hitsA==hitsB),"pick_ids_unchanged="+(picksA==picksB),"native_materials_supported="+Materials.Values.All(m=>m.shader.isSupported),"render_notes="+notes.GetType().GetProperty("RenderedNotes",F).GetValue(notes,null),"hit_triangles="+((IList)Get(notes,"hits")).Count,"adapter_ready_after_render="+Ready,"mean_frame_update_ms="+(watch.Elapsed.TotalMilliseconds/20).ToString("F3",System.Globalization.CultureInfo.InvariantCulture),"original_mean_frame_update_ms="+(baselineWatch.Elapsed.TotalMilliseconds/20).ToString("F3",System.Globalization.CultureInfo.InvariantCulture)};
   File.WriteAllLines(Path.Combine(outDir,"regression.txt"),report);Logger.LogInfo("Self test: "+string.Join("; ",report));
   if(editRegression)File.AppendAllText(Path.Combine(outDir,"editing-checks.txt"),"ground_projections_match_original="+(projectionsA==projectionsB)+"\r\noriginal_pick_geometry_retained="+hitSetB.IsSubsetOf(hitSetA)+"\r\nground_pick_ids_unchanged="+(groundPicksA==groundPicksB)+"\r\n");
   var profile=Get(boot,"Profile");File.WriteAllLines(Path.Combine(outDir,"profile.txt"),profile.GetType().GetFields(F).Where(f=>f.FieldType==typeof(float)).Select(f=>f.Name+"="+f.GetValue(profile)).Concat(new[]{"UnitsPerMs="+P(profile,"UnitsPerMs")}));
   File.WriteAllLines(Path.Combine(outDir,"note-z.txt"),((IEnumerable)Call(doc,"ReadEvents")).Cast<object>().Where(n=>Get(n,"Kind").ToString()=="Tap"||Get(n,"Kind").ToString()=="Hold").Select(n=>"id="+Get(n,"SourceId")+" time="+P(n,"TimeMs")+" z="+Z(notes,P(n,"TimeMs"))));
   File.WriteAllLines(Path.Combine(outDir,"note-screen.txt"),((IEnumerable)Call(doc,"ReadEvents")).Cast<object>().Where(n=>Get(n,"Kind").ToString()=="Tap").Select(n=>{int lane=(int)n.GetType().GetProperty("Lane").GetValue(n,null);float z=Z(notes,P(n,"TimeMs"));var a=(Vector3)T("StageSpace").GetMethod("FloorBound",F).Invoke(null,new object[]{profile,lane,(float)P(n,"GroundWidth"),false,z,0f});var b=(Vector3)T("StageSpace").GetMethod("FloorBound",F).Invoke(null,new object[]{profile,lane,(float)P(n,"GroundWidth"),true,z,0f});var v=camera.WorldToViewportPoint((a+b)*.5f);return Get(n,"SourceId")+","+v.x.ToString(System.Globalization.CultureInfo.InvariantCulture)+","+v.y.ToString(System.Globalization.CultureInfo.InvariantCulture);}));
   File.WriteAllText(Path.Combine(outDir,"fog.txt"),"enabled="+RenderSettings.fog+" mode="+RenderSettings.fogMode+" density="+RenderSettings.fogDensity+" start="+RenderSettings.fogStartDistance+" end="+RenderSettings.fogEndDistance);
  }catch(Exception e){Enabled.Value=true;File.WriteAllText(Path.Combine(outDir,"ERROR.txt"),e.ToString());Logger.LogError(e);}
  if(Arg("-nativevisual-edit-regression",null)!=null){
   var boot=UnityEngine.Object.FindFirstObjectByType(T("StudioBootstrap"));Set(boot,"showReleaseNotes",false);Set(boot,"guiShowReleaseNotes",false);Set(boot,"showUi",false);Set(boot,"showSongHud",true);Set(boot,"showGrid",true);Call(boot,"RebuildGeometry");Call(boot,"LateUpdate");
   if(Arg("-nativevisual-presentation-review",null)!=null){Set(boot,"selectedId",5);var selection=(ISet<int>)Get(boot,"selection");selection.Add(2);selection.Add(4);selection.Add(7);Call(boot,"LateUpdate");}
   yield return null;yield return new WaitForEndOfFrame();
   var capture=Type.GetType("UnityEngine.ScreenCapture, UnityEngine.ScreenCaptureModule",true).GetMethod("CaptureScreenshot",new[]{typeof(string),typeof(int)});capture.Invoke(null,new object[]{Path.Combine(outDir,"hud.png"),1});
   yield return null;yield return null;yield return null;
  }
  if(Arg("-nativevisual-ui-test",null)!=null){var boot=UnityEngine.Object.FindFirstObjectByType(T("StudioBootstrap"));Set(boot,"showUi",true);Set(boot,"showSongHud",true);Set(boot,"showGrid",true);Call(boot,"RebuildGeometry");yield break;}
  yield return null;Application.Quit();
 }
 private static string HitSnapshot(object notes){var r=new System.Text.StringBuilder();foreach(var h in (IList)Get(notes,"hits")){foreach(var n in new[]{"A","B","C"}){var v=(Vector3)Get(h,n);foreach(var f in new[]{v.x,v.y,v.z})r.Append(BitConverter.ToUInt32(BitConverter.GetBytes(f),0).ToString("X8")).Append('|');}r.Append(Get(h,"Id")).Append('|').Append(Get(h,"Air")).Append('|');}return r.ToString();}
 private static HashSet<string> HitSet(object notes){return new HashSet<string>(((IList)Get(notes,"hits")).Cast<object>().Select(h=>Get(h,"Id")+"|"+Get(h,"Air")+"|"+((Vector3)Get(h,"A")).ToString("R")+"|"+((Vector3)Get(h,"B")).ToString("R")+"|"+((Vector3)Get(h,"C")).ToString("R")));}
 private static string BatchSnapshot(object b){return string.Join(";",((List<Vector3>)Get(b,"Vertices")).Select(v=>v.ToString("R")).ToArray())+"/"+string.Join(",",((List<int>)Get(b,"Triangles")).Select(i=>i.ToString()).ToArray());}
 private static IEnumerable<string> EditingChecks(object notes,object doc,Camera camera){
  var hits=((IList)Get(notes,"hits")).Cast<object>().ToArray();var p=Get(notes,"p");
  foreach(var n in ((IEnumerable)Call(doc,"ReadEvents")).Cast<object>().Where(n=>(bool)n.GetType().GetProperty("IsNote",F).GetValue(n,null))){
   int id=(int)Get(n,"SourceId");string kind=Get(n,"Kind").ToString();yield return "note="+id+" kind="+kind+" pick_triangles="+hits.Count(h=>(int)Get(h,"Id")==id);
   if(kind=="Tap"){
    var slot=Visuals(notes).Slots.Values.FirstOrDefault(s=>s.Used&&s.Object.name=="NativeVisual ground-"+id);if(slot==null)continue;
    float expected=Z(notes,P(n,"TimeMs"));float actual=slot.Mesh.vertices.Min(v=>v.z);yield return "tap_front_anchor="+id+" error="+Mathf.Abs(expected-actual).ToString("R");
   }
  }
  foreach(var slot in Visuals(notes).Slots.Values.Where(s=>s.Used&&s.Object.name=="NativeVisual flickcap")){
   var v=slot.Mesh.vertices;var tri=slot.Mesh.triangles;int total=0,matched=0;
   for(int i=0;i<tri.Length;i+=3){var point=(v[tri[i]]+v[tri[i+1]]+v[tri[i+2]])/3;var screen=camera.WorldToScreenPoint(point);if(screen.z<=0)continue;var ids=(IList)Call(notes,"PickAll",camera.ScreenPointToRay(screen),2);total++;if(ids.Contains(slot.NoteId))matched++;}
   yield return "flick_head="+slot.Material.name+" native_head_pickable_faces="+matched+"/"+total;
  }
 }
 private static string PickSnapshot(object notes,Camera camera,int mode=0){var r=new System.Text.StringBuilder();for(int y=1;y<18;y++)for(int x=1;x<32;x++){var ray=camera.ViewportPointToRay(new Vector3(x/32f,y/18f,0));foreach(var id in (IList)Call(notes,"PickAll",ray,mode))r.Append(id).Append(',');r.Append(';');}return r.ToString();}
 private static void Capture(Camera camera,string path){
  var rt=new RenderTexture(1280,720,24,RenderTextureFormat.ARGB32);rt.Create();var target=camera.targetTexture;var active=RenderTexture.active;var rect=camera.rect;
  camera.targetTexture=rt;camera.rect=new Rect(0,0,1,1);camera.Render();RenderTexture.active=rt;
  var image=new Texture2D(1280,720,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1280,720),0,0);image.Apply();File.WriteAllBytes(path,image.EncodeToPNG());camera.targetTexture=target;camera.rect=rect;RenderTexture.active=active;Destroy(image);rt.Release();Destroy(rt);
 }
}
}
