using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using InFalsusStudio.Core;
using InFalsusStudio.TimeGroups;
namespace InFalsusStudio.Performance {
 // A numeric drag cannot change row order, separators or BOM. Compare those
 // once, then maintain exact saved-text differences for only the changed rows.
 internal sealed class DragDirtyState {
  static readonly FieldInfo lines=typeof(SpcDocument).GetField("lines",TimingGroups.F);
  static readonly FieldInfo eol=lines.FieldType.GetGenericArguments()[0].GetField("Eol",TimingGroups.F);
  readonly Dictionary<int,string> saved=new Dictionary<int,string>();
  readonly HashSet<int> different=new HashSet<int>();
  readonly bool structure;
  internal DragDirtyState(SpcDocument original,byte[] bytes){
   if(bytes==null){structure=true;return;}
   var baseline=SpcDocument.FromBytes(bytes);
   if(original.HasUtf8Bom!=baseline.HasUtf8Bom||original.LineCount!=baseline.LineCount){structure=true;return;}
   var a=(IList)lines.GetValue(original);var b=(IList)lines.GetValue(baseline);
   for(int i=0;i<a.Count;i++){
    if(!Equals(eol.GetValue(a[i]),eol.GetValue(b[i]))){structure=true;return;}
    int id=original.LineIdAt(i);string text=baseline.SourceLine(baseline.LineIdAt(i));saved[id]=text;
    if(original.SourceLine(id)!=text)different.Add(id);
   }
  }
  internal bool Update(SpcDocument current,IEnumerable<int> changed){
   if(structure)return true;
   foreach(int id in changed){string text;if(!saved.TryGetValue(id,out text))return true;if(current.SourceLine(id)==text)different.Remove(id);else different.Add(id);}
   return different.Count!=0;
  }
 }
}
