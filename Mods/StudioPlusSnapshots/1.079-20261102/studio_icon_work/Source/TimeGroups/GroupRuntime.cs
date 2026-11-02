using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Linq.Expressions;
using HarmonyLib;
using InFalsusStudio.Core;
using InFalsusStudio.NativeVisual;
namespace InFalsusStudio.TimeGroups {
 internal static class GroupRuntime {
  internal static readonly BindingFlags F=TimingGroups.F;
  static GroupModel last;static string lastName;
  [ThreadStatic]static SpcEvent anchor;
  static readonly Dictionary<Type,Dictionary<string,FieldInfo>> runtimeFields=new Dictionary<Type,Dictionary<string,FieldInfo>>();
  static FieldInfo RuntimeField(object o,string name){var type=o.GetType();Dictionary<string,FieldInfo> fields;if(!runtimeFields.TryGetValue(type,out fields)){fields=new Dictionary<string,FieldInfo>();runtimeFields.Add(type,fields);}FieldInfo field;if(!fields.TryGetValue(name,out field)){field=type.GetField(name,F);if(field==null)throw new MissingFieldException(type.FullName,name);fields.Add(name,field);}return field;}
  static readonly Dictionary<Type,Dictionary<string,Func<object,object>>> getters=new Dictionary<Type,Dictionary<string,Func<object,object>>>();
  internal static object Get(object o,string name){var type=o.GetType();Dictionary<string,Func<object,object>> fields;if(!getters.TryGetValue(type,out fields))getters[type]=fields=new Dictionary<string,Func<object,object>>();Func<object,object> get;
   if(!fields.TryGetValue(name,out get)){var input=Expression.Parameter(typeof(object));var value=Expression.Field(Expression.Convert(input,type),RuntimeField(o,name));fields[name]=get=Expression.Lambda<Func<object,object>>(Expression.Convert(value,typeof(object)),input).Compile();}return get(o);
  }
  static readonly Dictionary<Type,Dictionary<string,Action<object,object>>> setters=new Dictionary<Type,Dictionary<string,Action<object,object>>>();
  static void Set(object o,string name,object value){var type=o.GetType();Dictionary<string,Action<object,object>> fields;if(!setters.TryGetValue(type,out fields))setters[type]=fields=new Dictionary<string,Action<object,object>>();Action<object,object> set;
   if(!fields.TryGetValue(name,out set))fields[name]=set=InFalsusStudio.Performance.CompatibleFieldWrites.Create(RuntimeField(o,name));set(o,value);
  }
  static HarmonyMethod H(string name){return new HarmonyMethod(typeof(GroupRuntime).GetMethod(name,F));}
  internal static void Install(Harmony harmony){
   foreach(string name in new[]{"Visible","DrawGround","DrawSky","DrawFlick"})harmony.Patch(typeof(NoteRenderer).GetMethod(name,F),H("Scope"),null,null,H("Restore"),null);
   var methods=typeof(NoteRenderer).GetMethods(F).Concat(typeof(NoteRenderer).GetNestedTypes(F).SelectMany(t=>t.GetMethods(F)));
   foreach(var method in methods)if(!method.IsStatic&&method.ReturnType==typeof(int)&&method.Name.Contains("Draw")&&method.GetParameters().Select(p=>p.ParameterType).SequenceEqual(new[]{typeof(SpcEvent),typeof(SpcEvent)}))harmony.Patch(method,H("Compare"));
   harmony.Patch(typeof(StudioBootstrap).GetMethod("RefreshDocument",F),null,H("Refreshed"));
   harmony.Patch(typeof(StudioBootstrap).GetMethod("get_EffectiveScroll",F),null,H("Effective"));
   harmony.Patch(typeof(StudioBootstrap).GetMethod("BeginGuiFrame",F),null,H("Frame"));
   harmony.Patch(typeof(StudioBootstrap).GetMethod("SetSelection",F),null,H("Selected"));
   harmony.Patch(typeof(StudioBootstrap).GetMethod("NoteAnchor",F),H("Anchor"),null,null,H("AnchorEnd"),null);
   harmony.Patch(typeof(StudioBootstrap).GetMethod("AfterDocumentEdit",F),null,H("Refreshed"));
  }
  internal static void Bind(StudioBootstrap boot){if(!ReferenceEquals(TimingGroups.Boot,boot)){TimingGroups.Boot=boot;last=null;lastName=null;}}
  internal static void Sync(StudioBootstrap boot){
   Bind(boot);if(boot.History==null)return;var model=TimingGroups.Model(boot.History.Document);string name=TimingGroups.Current;
   if(!model.Extended){last=model;lastName=name;return;}
   if(model.Events==null)boot.History.Document.ReadEvents();
   if(last!=model||lastName!=name){
    Set(boot,"beats",model.Beat(name));Set(boot,"authoringGrid",new AuthoringGrid(model.Beat(name)));last=model;lastName=name;
   }
  }
  internal static void Activate(StudioBootstrap boot,string name){Bind(boot);TimingGroups.Current=name;Sync(boot);}
  static void Refreshed(StudioBootstrap __instance,MethodBase __originalMethod){if(__originalMethod.Name=="RefreshDocument")last=null;Sync(__instance);}
  static void Frame(StudioBootstrap __instance){
   Sync(__instance);if(__instance.History==null||!InFalsusStudio.Performance.PreviewGuiSnapshots.Capture())return;var model=TimingGroups.Model(__instance.History.Document);if(!model.Extended)return;
   var timing=__instance.Events.Where(e=>e.Kind==EventKind.Chart||((e.Kind==EventKind.Bpm||e.Kind==EventKind.Track)&&TimingGroups.NameOf(e)==TimingGroups.Current)).OrderBy(e=>Math.Abs(e.TimeMs-__instance.CurrentMs)).Take(12).ToArray();
   Set(__instance,"guiTimingEvents",timing);
  }
  static void Selected(StudioBootstrap __instance){
   if(__instance.History==null)return;var selection=(System.Collections.IEnumerable)Get(__instance,"selection");if(!selection.Cast<object>().Any())return;
   int id=(int)Get(__instance,"selectedId");var note=__instance.Events.FirstOrDefault(n=>n.SourceId==id);var tag=TimingGroups.Tag(note);if(tag!=null&&tag.Model.Extended)Activate(__instance,tag.Name);
  }
  static void Effective(StudioBootstrap __instance,ref ScrollTimeline __result){
   Bind(__instance);if(__instance.History==null||(bool)Get(__instance,"linearTimeLayout"))return;
   var scoped=TimingGroups.Tag(anchor);if(scoped!=null&&scoped.Model.Extended&&scoped.Model.Errors.Count==0){__result=scoped.Model.Scroll(scoped.Name);return;}
   var model=TimingGroups.Model(__instance.History.Document);if(model.Extended&&model.Errors.Count==0){if(model.Events==null)__instance.History.Document.ReadEvents();__result=model.Scroll(TimingGroups.Current);}
  }
  static void Anchor(SpcEvent __0,out SpcEvent __state){__state=anchor;anchor=__0;}
  static Exception AnchorEnd(SpcEvent __state,Exception __exception){anchor=__state;return __exception;}
  internal static ScrollTimeline For(NoteRenderer renderer,SpcEvent note){
   var original=(ScrollTimeline)Get(renderer,"scroll");
   if(!ReferenceEquals(TimingGroups.Boot,null)&&(bool)Get(TimingGroups.Boot,"linearTimeLayout"))return original;
   var tag=TimingGroups.Tag(note);if(tag!=null&&tag.Model.Extended&&tag.Model.Errors.Count==0)return tag.Model.Scroll(tag.Name);
   if((bool)Get(renderer,"ghost")&&!ReferenceEquals(TimingGroups.Boot,null)&&TimingGroups.Boot.History!=null){var m=TimingGroups.Model(TimingGroups.Boot.History.Document);if(m.Extended&&m.Errors.Count==0)return m.Scroll(TimingGroups.Current);}
   return original;
  }
  static void Scope(NoteRenderer __instance,SpcEvent __0,out GroupProperties.RenderState __state){var tag=TimingGroups.Tag(__0);if(tag==null&&(ReferenceEquals(TimingGroups.Boot,null)||TimingGroups.Boot.History==null||!TimingGroups.Model(TimingGroups.Boot.History.Document).Extended)){__state=null;return;}__state=new GroupProperties.RenderState{Scroll=(ScrollTimeline)Get(__instance,"scroll"),HidePast=(bool)Get(__instance,"hidePast"),Cursor=(AutoplayCursor)Get(__instance,"visualCursor")};Set(__instance,"scroll",For(__instance,__0));if(TimingGroups.NoInput(__0)){Set(__instance,"hidePast",!TimingGroups.NoClip(__0));Set(__instance,"visualCursor",null);}}
  static Exception Restore(NoteRenderer __instance,GroupProperties.RenderState __state,Exception __exception){if(__state!=null){Set(__instance,"scroll",__state.Scroll);Set(__instance,"hidePast",__state.HidePast);Set(__instance,"visualCursor",__state.Cursor);}return __exception;}
  static bool Compare(object __instance,SpcEvent __0,SpcEvent __1,ref int __result){
   NoteRenderer renderer=__instance as NoteRenderer;if(renderer==null){var field=__instance.GetType().GetField("<>4__this",F);if(field!=null)renderer=field.GetValue(__instance) as NoteRenderer;}if(renderer==null)return true;
   int indexed;if(InFalsusStudio.Performance.RenderIndex.Compare(renderer,__0,__1,out indexed)){__result=indexed;return false;}
   if(TimingGroups.Tag(__0)==null&&TimingGroups.Tag(__1)==null)return true;
   double now=(double)Get(renderer,"now");double a=For(renderer,__0).Delta((__0.TimeMs+__0.EndMs)*.5,now),b=For(renderer,__1).Delta((__1.TimeMs+__1.EndMs)*.5,now);__result=b.CompareTo(a);return false;
  }
  internal static void Dispose(){last=null;lastName=null;anchor=null;TimingGroups.Boot=null;}
 }
}

