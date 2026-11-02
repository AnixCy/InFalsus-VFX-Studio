using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using InFalsusStudio.Core;
namespace InFalsusStudio.TimeGroups {
 internal sealed class GroupSourceRow {
  internal int Id,Index;internal string Text,Editable;internal string[] Tokens;
 }
 internal static class GroupSourceSnapshot {
  static readonly FieldInfo source=typeof(SpcDocument).GetField("lines",TimingGroups.F);
  static readonly Type rowType=typeof(SpcDocument).GetNestedType("Line",TimingGroups.F);
  static readonly FieldInfo id=rowType.GetField("Id",TimingGroups.F),text=rowType.GetField("Text",TimingGroups.F);
  static readonly Regex Call=new Regex(@"^(\s*)([A-Za-z_][A-Za-z_0-9]*)(\s*)\((.*)\)(.*)$",RegexOptions.CultureInvariant);
  internal static GroupSourceRow[] Read(SpcDocument d){
   var list=(System.Collections.IList)source.GetValue(d);var rows=new GroupSourceRow[list.Count];
   for(int i=0;i<rows.Length;i++)rows[i]=new GroupSourceRow{Id=(int)id.GetValue(list[i]),Index=i,Text=(string)text.GetValue(list[i])};return rows;
  }
  internal static string Editable(GroupModel m,int id){
   GroupSourceRow row;if(!m.Source.TryGetValue(id,out row))return "";
   if(row.Editable==null){var call=Call.Match(row.Text);row.Editable=call.Success&&call.Groups[2].Value=="icp_event"?m.Document.EditableSourceLine(id):row.Text;}return row.Editable;
  }
  internal static string[] Tokens(GroupModel m,int id){
   GroupSourceRow row;if(!m.Source.TryGetValue(id,out row))return new string[0];
   if(row.Tokens==null){var call=Call.Match(Editable(m,id));row.Tokens=call.Success?call.Groups[4].Value.Split(','):new string[0];}return row.Tokens;
  }
 }
}
