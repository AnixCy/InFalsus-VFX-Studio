using System;
using System.Linq;
using UnityEngine;
using InFalsusStudio.Core;
using InFalsusStudio.TimeGroups;
namespace InFalsusStudio.NativeEditorUI {
 public sealed partial class NativeEditorUIPlugin {
  bool popupTimingExpanded;string popupTimingValue="";string[] popupTimingNames=new string[0];
  internal static string TimingGroupCaption(string name){return name==null?"混合 timinggroup":name==""?"默认group":name;}
  void DrawPopupTimingGroup(){
   var row=new Rect(0,popupY,360*scale,42*scale);popupY+=42*scale;Fill(new Rect(0,row.yMax-scale,row.width,scale),Css("#2c4652"));
   UiText(new Rect(0,row.y,120*scale,row.height),"timinggroup",15,Css("#a6c3d1"));
   if(FlatButton(new Rect(129*scale,row.y+6*scale,231*scale,30*scale),TimingGroupCaption(popupTimingValue)+"  ▾",14,popupTimingExpanded))Queue(delegate{popupTimingExpanded=!popupTimingExpanded;});
   if(!popupTimingExpanded)return;
   var history=popupHistory;var ids=(int[])popupIds.Clone();
   foreach(string name in popupTimingNames){
    string target=name;
    if(FlatButton(new Rect(12*scale,popupY+3*scale,348*scale,32*scale),TimingGroupCaption(target),14,popupTimingValue==target))Queue(delegate{AssignPopupTimingGroup(history,ids,target);});
    popupY+=38*scale;
   }
  }
  void AssignPopupTimingGroup(EditHistory history,int[] ids,string target){
   if(!(bool)Call(boot,"CommitAllInputFieldsNow"))return;
   RefreshFilterNotes();var selected=Val<System.Collections.Generic.HashSet<int>>(boot,"selection").Where(i=>filterNotes.ContainsKey(i)).OrderBy(i=>i).ToArray();
   if(!ReferenceEquals(boot.History,history)||!ids.SequenceEqual(selected)||ids.Any(i=>!filterNotes.ContainsKey(i)))throw new InvalidOperationException(T("源语句已变化，请重新选择。"));
   var model=TimingGroups.Model(history.Document);
   if(target!=""&&model.Find(target)==null)throw new ArgumentException("目标 timinggroup 已不存在，请重新选择。");
   if(ids.Any(i=>model.Of(i)!=target))GroupChange(history,document=>TimingGroups.AssignNotes(document,ids,target));
   GroupRuntime.Activate(boot,target);popupTimingExpanded=false;Set(boot,"noteForm",null);GUI.FocusControl(null);
  }
 }
}
