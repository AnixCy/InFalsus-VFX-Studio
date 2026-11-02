using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Web.Script.Serialization;
namespace InfStudio.External {
 public class Value {public string key,value;}
 public class Segment {public int start,end,path,ease;public double x1,x2;}
 public class Request {public int version,groupStart;public string tool;public Value[] values;public Segment[] segments;public double bpm,meter;public double[] grid;public int gridDivision;public string gridGroup;}
 public class Context {
  public readonly Request Request;public readonly string Root;
  readonly Dictionary<string,string> values;readonly List<Row> output=new List<Row>();
  public int Segments,Timings;public string Message="";
  class Row {public int Time,Order;public string Text;}
  public Context(Request r,string root){Request=r;Root=root;Need(r!=null&&r.version==1,"Unsupported tool request.");values=(r.values??new Value[0]).ToDictionary(v=>v.key,v=>v.value);}
  public static void Need(bool ok,string message){if(!ok)throw new ArgumentException(message);}
  public static bool Finite(double x){return !double.IsNaN(x)&&!double.IsInfinity(x);}
  public string S(string key,string fallback=""){string value;return values.TryGetValue(key,out value)?value:fallback;}
  public double D(string key,double fallback=0){double value;Need(double.TryParse(S(key,fallback.ToString(CultureInfo.InvariantCulture)),NumberStyles.Float,CultureInfo.InvariantCulture,out value)&&Finite(value),"Enter a finite number: "+key);return value;}
  public int I(string key,int fallback=0){double value=D(key,fallback);Need(value==Math.Truncate(value)&&value>=int.MinValue&&value<=int.MaxValue,"Enter an integer: "+key);return (int)value;}
  public bool B(string key,bool fallback=false){bool value;Need(bool.TryParse(S(key,fallback.ToString()),out value),"Enter true or false: "+key);return value;}
  public static int Round(double x){Need(Finite(x)&&x>=0&&x<=86400000,"Time must be within 0..86400000 ms.");return (int)Math.Round(x,MidpointRounding.AwayFromZero);}
  public static string N(double value){Need(Finite(value),"Invalid generated number.");return value.ToString("0.############",CultureInfo.InvariantCulture);}
  public void Range(out int start,out int end){start=I("start");end=I("end",1000);Need(start>=0&&end<=86400000&&end>start,"End must be after Start, within 0..86400000 ms.");}
  public Segment[] Captured(){var result=Request.segments??new Segment[0];Need(result.Length>0&&result.Length<=4096,"Capture selected SkyArea paths first (up to 4096 segments).");foreach(var s in result)Need(s.start>=0&&s.end>s.start&&s.end<=86400000&&Finite(s.x1)&&Finite(s.x2),"Invalid captured path.");return result;}
  public void Line(double t1,double t2,double x1,double x2,int minDuration=1,int ease=0){
   Need(Segments<16000,"More than 16000 blackline segments; lower Count or Quality.");
   int start=Round(t1),end=Round(t2);if(end<start){int swap=start;start=end;end=swap;double xx=x1;x1=x2;x2=xx;ease=ease==1?2:ease==2?1:0;}
   minDuration=Math.Max(1,minDuration);if(end-start<minDuration){if(start+minDuration<=86400000)end=start+minDuration;else start=end-minDuration;}
   Need(start>=0&&end>start&&end<=86400000,"A generated SkyArea must have positive duration.");Need(Finite(x1)&&Finite(x2)&&Math.Min(x1,x2)>=-20&&Math.Max(x1,x2)<=20,"Generated X is outside -20..20.");
   long id=(long)Request.groupStart+Segments;Need(Request.groupStart>=0&&id<=int.MaxValue,"No groupid range available.");
   int a=(int)Math.Round(x1*100,MidpointRounding.AwayFromZero),b=(int)Math.Round(x2*100,MidpointRounding.AwayFromZero);Need(ease>=0&&ease<=2,"Invalid easing.");
   int helper=Math.Max(0,start-1);output.Add(new Row{Time=helper,Order=0,Text="skyarea("+helper+",10000,100,1,10000,100,1,0,0,1,"+id+")"});
   output.Add(new Row{Time=start,Order=1,Text="skyarea("+start+","+a+",100,1,"+b+",100,1,"+ease+","+ease+","+(end-start)+","+id+")"});Segments++;
  }
  public void Timing(int time,double value,bool bpm,double meter){Need(time>=0&&time<=86400000&&Finite(value)&&(!bpm||value>0),"Invalid generated timing.");output.Add(new Row{Time=time,Order=0,Text=bpm?"bpm("+time+","+N(value)+","+N(meter)+")":"track("+time+","+N(value)+")"});Timings++;}
  public string[] Lines(){return output.OrderBy(r=>r.Time).ThenBy(r=>r.Order).Select(r=>r.Text).ToArray();}
  public List<int> Grid(int start,int end,int limit=10000){
   Need(Request.grid!=null&&Request.gridDivision>0,"No main editor grid supplied. Run from Studio 1.0.77 or later.");
   Need(Request.grid.Length<=limit,"Too many main-grid samples.");var times=new SortedSet<int>{start,end};
   foreach(double time in Request.grid){Need(Finite(time)&&time>=0&&time<=86400000,"Invalid main-grid sample.");int t=Round(time);if(t>=start&&t<=end)times.Add(t);}return times.ToList();
  }
  public static List<int> Sample(int start,int end,double bpm,int division,int limit=10000){
   Need(Finite(bpm)&&bpm>0&&division>=1&&division<=4096,"Invalid reference BPM or subdivision.");double step=60000/bpm/division;Need((end-start)/step<=limit,"Too many timing/grid samples.");var times=new SortedSet<int>{start,end};
   long first=(long)Math.Ceiling(start/step),last=(long)Math.Floor(end/step);for(long i=first;i<=last;i++){int time=Round(i*step);if(time>=start&&time<=end)times.Add(time);}return times.ToList();
  }
  public static double Ease(string kind,double x){
   switch(kind){case "SineIn":return 1-Math.Cos(x*Math.PI/2);case "SineOut":return Math.Sin(x*Math.PI/2);case "SineInOut":return (1-Math.Cos(Math.PI*x))/2;
    case "QuadIn":return x*x;case "QuadOut":return 1-(1-x)*(1-x);case "QuadInOut":return x<.5?2*x*x:1-Math.Pow(-2*x+2,2)/2;
    case "CubicIn":return x*x*x;case "CubicOut":return 1-Math.Pow(1-x,3);case "CubicInOut":return x<.5?4*x*x*x:1-Math.Pow(-2*x+2,3)/2;
    case "QuartIn":return Math.Pow(x,4);case "QuartOut":return 1-Math.Pow(1-x,4);case "QuartInOut":return x<.5?8*Math.Pow(x,4):1-Math.Pow(-2*x+2,4)/2;
    case "ExpoIn":return x==0?0:Math.Pow(2,10*x-10);case "ExpoOut":return x==1?1:1-Math.Pow(2,-10*x);case "ExpoInOut":return x==0?0:x==1?1:x<.5?Math.Pow(2,20*x-10)/2:(2-Math.Pow(2,-20*x+10))/2;
    case "Linear":return x;default:throw new ArgumentException("Unknown easing: "+kind);
   }
  }
  public static void Run(string requestPath,string outputPath,string root,string tool,Action<Context> generate){
   var json=new JavaScriptSerializer{MaxJsonLength=8000000};var request=json.Deserialize<Request>(File.ReadAllText(requestPath,Encoding.UTF8));Need(request!=null&&request.tool==tool,"Tool/request mismatch.");var c=new Context(request,root);generate(c);string[] lines=c.Lines();Need(lines.Length>0&&lines.Length<=32000,"No output, or output exceeds 32000 statements.");
   File.WriteAllText(outputPath,string.Join("\r\n",lines)+"\r\n",new UTF8Encoding(false));File.WriteAllText(outputPath+".info.json",json.Serialize(new {segments=c.Segments,timings=c.Timings,message=c.Message}),new UTF8Encoding(false));
  }
 }
}
