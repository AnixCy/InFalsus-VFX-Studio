using System;
namespace InFalsusStudio.NativeEditorUI {
 internal static class InputTextSelection {
  static int Category(char ch){return char.IsWhiteSpace(ch)?0:char.IsLetterOrDigit(ch)||ch=='_'?1:2;}
  internal static int[] Word(string text,int index){
   text=text??"";if(text.Length==0)return new[]{0,0};index=Math.Max(0,Math.Min(text.Length-1,index));int kind=Category(text[index]),start=index,end=index+1;
   while(start>0&&Category(text[start-1])==kind)start--;while(end<text.Length&&Category(text[end])==kind)end++;
   return new[]{start,end};
  }
 }
}
