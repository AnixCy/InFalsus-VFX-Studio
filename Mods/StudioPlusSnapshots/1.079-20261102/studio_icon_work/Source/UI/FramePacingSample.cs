using System;
using System.Globalization;
namespace InFalsusStudio.NativeEditorUI {
 internal sealed class FramePacingSample {
  const int Capacity=4096;
  readonly double[] ends=new double[Capacity],durations=new double[Capacity],scratch=new double[Capacity];
  double now,publish;int first,count;
  internal double P99,Maximum;internal int SlowFrames;
  internal string Text="5s  P99 --  MAX --";
  internal void Reset(){if(count==0&&now==0)return;now=publish=0;first=count=0;P99=Maximum=0;SlowFrames=0;Text="5s  P99 --  MAX --";}
  internal bool Step(double seconds){
   if(double.IsNaN(seconds)||double.IsInfinity(seconds)||seconds<=0)return false;
   now+=seconds;publish+=seconds;
   while(count>0&&ends[first]<=now-5){first=(first+1)%Capacity;count--;}
   if(count==Capacity){first=(first+1)%Capacity;count--;}
   int at=(first+count)%Capacity;ends[at]=now;durations[at]=seconds*1000;count++;
   if(publish<.5)return false;publish=0;SlowFrames=0;Maximum=0;
   for(int i=0;i<count;i++){double ms=durations[(first+i)%Capacity];scratch[i]=ms;Maximum=Math.Max(Maximum,ms);if(ms>1000d/60)SlowFrames++;}
   Array.Sort(scratch,0,count);P99=scratch[Math.Max(0,(int)Math.Ceiling(count*.99)-1)];
   Text="5s  P99 "+P99.ToString("0.0",CultureInfo.InvariantCulture)+"ms   MAX "+Maximum.ToString("0.0",CultureInfo.InvariantCulture)+"ms   >16.7ms "+SlowFrames;
   return true;
  }
 }
}
