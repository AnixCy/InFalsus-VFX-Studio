using System;
using System.Linq.Expressions;
using InFalsusStudio.Core;
using InFalsusStudio.TimeGroups;
namespace InFalsusStudio.Performance {
 internal static class RendererAccess {
  internal static Func<NoteRenderer,T> Field<T>(string name){var input=Expression.Parameter(typeof(NoteRenderer));return Expression.Lambda<Func<NoteRenderer,T>>(Expression.Field(input,typeof(NoteRenderer).GetField(name,TimingGroups.F)),input).Compile();}
  internal static readonly Func<NoteRenderer,StudioProfile> Profile=Field<StudioProfile>("p");
  internal static readonly Func<NoteRenderer,ScrollTimeline> Scroll=Field<ScrollTimeline>("scroll");
  internal static readonly Func<NoteRenderer,double> Now=Field<double>("now");
  internal static readonly Func<NoteRenderer,UnityEngine.Camera> Camera=Field<UnityEngine.Camera>("camera");
  internal static readonly Func<NoteRenderer,int,bool> Selected=SelectedCall();
  internal static readonly Func<NoteRenderer,SkyConnections> Connections=Field<SkyConnections>("connections");
  static Func<NoteRenderer,int,bool> SelectedCall(){var r=Expression.Parameter(typeof(NoteRenderer));var id=Expression.Parameter(typeof(int));return Expression.Lambda<Func<NoteRenderer,int,bool>>(Expression.Call(r,typeof(NoteRenderer).GetMethod("IsSelected",TimingGroups.F),id),r,id).Compile();}
 }
}
