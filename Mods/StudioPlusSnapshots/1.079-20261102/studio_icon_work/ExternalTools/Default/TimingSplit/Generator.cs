using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Web.Script.Serialization;
namespace InfStudio.External {
 public sealed class SplitPoint {public double time;public string[] endpoint;}
 public sealed class SplitData {public int sourceId,groupId;public string scope,source;public string[] tokens;public SplitPoint[] points;}
 public sealed class SplitRequest {public int version;public string tool;public SplitData split;public SplitData[] splits;}
 public static class TimingSplit {
  static string N(double number){if(double.IsNaN(number)||double.IsInfinity(number))throw new ArgumentException("Non-finite timing.");return number.ToString("0.##########",CultureInfo.InvariantCulture);}
  public static void Run(string requestPath,string outputPath){
   var json=new JavaScriptSerializer{MaxJsonLength=8000000};var request=json.Deserialize<SplitRequest>(File.ReadAllText(requestPath));var sources=request.splits??(request.split==null?new SplitData[0]:new[]{request.split});
   if(request.version!=1||request.tool!="TimingSplit"||sources.Length==0||sources.Any(s=>s==null||s.tokens==null||s.tokens.Length<10||s.points==null||s.points.Length<3||s.groupId<0)||sources.Sum(s=>s.points.Length-1)>4096)throw new ArgumentException("Invalid timing split request; select SkyAreas with internal timing events.");
   var lines=new List<string>();foreach(var s in sources){int open=s.source.IndexOf('('),close=s.source.IndexOf(')',open+1);if(open<0||close<open)throw new ArgumentException("Invalid source statement.");string suffix=s.source.Substring(close+1),indent=s.source.Substring(0,s.source.Length-s.source.TrimStart().Length);
   for(int i=0;i<s.points.Length-1;i++){
    var a=s.points[i];var b=s.points[i+1];if(a.endpoint==null||b.endpoint==null||a.endpoint.Length!=3||b.endpoint.Length!=3||b.time-a.time<.001)throw new ArgumentException("Zero/too short split interval.");
    var tokens=s.tokens.ToList();while(tokens.Count<=10)tokens.Add("");tokens[0]=i==0?s.tokens[0]:N(a.time);for(int k=0;k<3;k++){tokens[1+k]=a.endpoint[k];tokens[4+k]=b.endpoint[k];}tokens[7]="0";tokens[8]="0";tokens[9]=N(b.time-a.time);
    double old;tokens[10]=s.tokens.Length>10&&double.TryParse(s.tokens[10],NumberStyles.Float,CultureInfo.InvariantCulture,out old)&&old==s.groupId?s.tokens[10]:s.groupId.ToString(CultureInfo.InvariantCulture);
    if(double.Parse(tokens[9],CultureInfo.InvariantCulture)<=0)throw new ArgumentException("Rounded duration is zero.");lines.Add((i==0?indent:"")+"skyarea("+string.Join(",",tokens.ToArray())+")"+(i==0?suffix:""));
   }
   }
   File.WriteAllLines(outputPath,lines.ToArray(),new UTF8Encoding(false));File.WriteAllText(outputPath+".info.json",json.Serialize(new{segments=lines.Count,timings=0,message="Cut points sample both original eased edges; generated pieces use linear edges."}),new UTF8Encoding(false));
  }
 }
}
