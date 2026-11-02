using System;
using System.Collections.Generic;
using System.Linq;
namespace InfStudio.External {
 public static class Generator {
  public static void Run(string request,string output,string root){Context.Run(request,output,root,"GradientTiming",Generate);}
  static void Generate(Context c){
   int start,end;c.Range(out start,out end);double a=c.D("startValue"),b=c.D("endValue",1),reference=c.D("referenceBpm",138);bool bpm=c.S("eventType","track")=="bpm";
   Context.Need(reference>0,"Reference BPM must be positive.");if(bpm)Context.Need(a>0&&b>0,"BPM timing cannot contain zero or negative BPM. Use track for stop/reverse.");
   List<int> times;if(c.S("sampling","beat")=="beat")times=c.Grid(start,end);else{int splits=c.I("splitCount",32);Context.Need(splits>=1&&splits<=10000,"Split count must be 1..10000.");times=Enumerable.Range(0,splits+1).Select(i=>Context.Round(start+(end-start)*i/(double)splits)).Distinct().OrderBy(t=>t).ToList();}
   foreach(int time in times){double x=Context.Ease(c.S("easing","Linear"),(time-start)/(double)(end-start));double value=a+(b-a)*x;if(!bpm&&c.S("unit","multiplier")=="effectiveBpm")value/=reference;c.Timing(time,value,bpm,c.Request.meter>0?c.Request.meter:4);}
   c.Message="Generated "+times.Count+" "+(bpm?"BPM":"track")+" events, including the exact final value.";
  }
 }
}
