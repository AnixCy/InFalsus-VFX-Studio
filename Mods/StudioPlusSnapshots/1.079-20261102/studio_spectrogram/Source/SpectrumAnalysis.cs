using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
namespace InFalsusStudio.Spectrogram {
 internal sealed class SpectrumAnalysis:IDisposable {
  sealed class RawJob {
   internal int Page,Rows,Size;internal volatile bool NeedsPcm,Computing;
   internal CancellationTokenSource Cancel;internal Task<SpectrumRaw> Task;
   internal readonly TaskCompletionSource<float[]> Pcm=new TaskCompletionSource<float[]>(TaskCreationOptions.RunContinuationsAsynchronously);
   internal void Stop(){Cancel.Cancel();Pcm.TrySetCanceled();}
  }
  sealed class OutputJob {internal int Bins,Page;internal Task<SpectrumTile> Task;}
  internal AudioClip Clip;internal readonly int Rate,Frames,Channels,Hop,TotalRows,TotalPages,Workers;
  internal int Bins,First,Last;internal string Error;internal bool ViewReady;
  internal readonly SpectrumCache Disk;internal int Completed {get{return done.Count(k=>(int)(k>>32)==Size);}}
  internal int Size;
  internal long RawBytes,OutputBytes;internal double ReadMilliseconds {get{return pcm.ReadMilliseconds;}}
  internal bool Busy {get{return jobs.Count+outputs.Count>0;}}
  readonly SpectrumPcm pcm;readonly CancellationTokenSource cancel=new CancellationTokenSource();
  readonly Dictionary<long,SpectrumRaw> raw=new Dictionary<long,SpectrumRaw>();readonly Dictionary<long,SpectrumTile> tiles=new Dictionary<long,SpectrumTile>();
  readonly Dictionary<long,RawJob> jobs=new Dictionary<long,RawJob>();readonly Dictionary<long,OutputJob> outputs=new Dictionary<long,OutputJob>();readonly HashSet<long> done=new HashSet<long>();
  long serial;bool disposed;int previousFirst=-1,scanBase,scanSelected;
  const long RawLimit=96L*1024*1024,OutputLimit=64L*1024*1024;
  internal SpectrumAnalysis(AudioClip clip,int bins=256,string folder=null,string audio=null){Clip=clip;Rate=clip.frequency;Frames=clip.samples;Channels=clip.channels;Bins=SpectrumMath.ValidBins(bins);Hop=SpectrumMath.Hop(Rate);TotalRows=(Frames+Hop-1)/Hop;TotalPages=(TotalRows+255)/256;Workers=Math.Max(1,Math.Min(4,Environment.ProcessorCount-2));Size=SpectrumMath.WindowSize(Rate,Bins);pcm=new SpectrumPcm(clip);Disk=new SpectrumCache(folder??System.IO.Path.GetTempPath(),audio,Frames,Channels,Rate);}
  internal static long Key(int kind,int page){return ((long)kind<<32)|(uint)page;}
  internal int Rows(int page){return Math.Min(257,TotalRows-page*256);}
  internal double PageMilliseconds {get{return 256d*Hop*1000/Rate;}}
  internal void Select(int bins){Bins=SpectrumMath.ValidBins(bins);}
  internal void Window(double ms){if(double.IsNaN(ms)||double.IsInfinity(ms)||ms<=0)ms=20;double target=Math.Min(65536,Rate*Math.Max(.1,Math.Min(1000,ms))/1000);int n=32;while(n<target)n*=2;if(n>32&&target/(n/2)<n/target)n/=2;Size=n;}
  internal long LayerKey(int bins,int page,int size=0){return ((long)(size==0?Size:size)<<40)|((long)bins<<24)|(uint)page;}
  internal SpectrumTile Get(int page){return Get(Bins,page);}
  internal SpectrumTile Get(int bins,int page){SpectrumTile value;if(tiles.TryGetValue(LayerKey(bins,page),out value)){value.Used=++serial;return value;}return null;}
  SpectrumRaw Raw(int size,int page){SpectrumRaw value;if(raw.TryGetValue(Key(size,page),out value)){value.Used=++serial;return value;}return null;}
  void Store(SpectrumRaw value){long key=Key(value.Size,value.Page);SpectrumRaw old;if(raw.TryGetValue(key,out old))RawBytes-=old.Bytes;value.Used=++serial;raw[key]=value;RawBytes+=value.Bytes;done.Add(key);}
  void Store(SpectrumTile value){long key=LayerKey(value.Bins,value.Page,value.Size);SpectrumTile old;if(tiles.TryGetValue(key,out old))OutputBytes-=old.Data.Length;value.Used=++serial;tiles[key]=value;OutputBytes+=value.Data.Length;}
  internal void Tick(int first,int last,bool enabled=true){
   if(disposed)return;Drain();if(!enabled||Error!=null)return;First=Math.Max(0,Math.Min(TotalPages-1,first));Last=Math.Max(First,Math.Min(TotalPages-1,last));
   int behind=(int)Math.Ceiling(4000/PageMilliseconds),ahead=(int)Math.Ceiling(Math.Max(8000,(Last-First+1)*PageMilliseconds*2)/PageMilliseconds);int low=Math.Max(0,First-behind),high=Math.Min(TotalPages-1,Last+ahead);
   if(previousFirst>=0&&Math.Abs(First-previousFirst)>2){foreach(var job in jobs.Values)if(job.Page<low||job.Page>high)job.Stop();}previousFirst=First;
   // Display jobs precede background jobs, so raw analysis cannot starve texture production.
   for(int p=First;p<=Last;p++){WantOutput(256,p);WantOutput(Bins,p);}
   bool basic=true;for(int p=First;p<=Last;p++)if(Get(256,p)==null){basic=false;break;}
   for(int p=First;p<=Last;p++)WantRaw(Size,p);
   if(basic)for(int p=First;p<=Last;p++)WantRaw(Size,p);
   if(basic){for(int p=Last+1;p<=high;p++){WantOutput(256,p);WantOutput(Bins,p);}for(int p=First-1;p>=low;p--){WantOutput(256,p);WantOutput(Bins,p);}}
   for(int p=Last+1;p<=high;p++)WantRaw(Size,p);
   for(int p=First-1;p>=low;p--)WantRaw(Size,p);
   if(basic)for(int p=First;p<=high;p++)WantRaw(Size,p);
   Background(Size,ref scanBase);if(basic)Background(Size,ref scanSelected);
   var watch=System.Diagnostics.Stopwatch.StartNew();foreach(var job in jobs.Values.OrderBy(j=>j.Page>=First&&j.Page<=Last?0:j.Page>Last?1:2)){
    if(!job.NeedsPcm||job.Pcm.Task.IsCompleted||job.Cancel.IsCancellationRequested)continue;long start=(long)job.Page*256*Hop-job.Size/2;int count=(job.Rows-1)*Hop+job.Size;if(!pcm.Fill(start,count,watch))break;job.Pcm.TrySetResult(pcm.Copy(start,count));if(watch.Elapsed.TotalMilliseconds>=1)break;
   }pcm.Trim();Trim(low,high);
  }
  void Background(int size,ref int scan){for(int n=0;n<8&&jobs.Count+outputs.Count<Workers;n++){int page=scan;scan=(scan+1)%TotalPages;if(!done.Contains(Key(size,page)))WantRaw(size,page);}}
  void WantRaw(int size,int page){long key=Key(size,page);if(raw.ContainsKey(key)||jobs.ContainsKey(key)||jobs.Count+outputs.Count>=Workers)return;var job=new RawJob{Page=page,Rows=Rows(page),Size=size,Cancel=CancellationTokenSource.CreateLinkedTokenSource(cancel.Token)};var token=job.Cancel.Token;
   job.Task=Task.Run(async ()=>{string identity=await Disk.Identity.ConfigureAwait(false);token.ThrowIfCancellationRequested();var value=Disk.Read(identity,page,job.Rows,size,Hop,Rate);if(value!=null)return value;job.NeedsPcm=true;var data=await job.Pcm.Task.ConfigureAwait(false);token.ThrowIfCancellationRequested();job.Computing=true;value=SpectrumMath.Analyze(page,job.Rows,data,Channels,Rate,size,Hop,token);token.ThrowIfCancellationRequested();Disk.Write(identity,value,Hop,Rate);return value;},token);jobs.Add(key,job);
  }
  void WantOutput(int bins,int page){long key=LayerKey(bins,page);if(tiles.ContainsKey(key)||outputs.ContainsKey(key)||jobs.Count+outputs.Count>=Workers)return;var value=Raw(Size,page);if(value==null)return;var token=cancel.Token;outputs.Add(key,new OutputJob{Bins=bins,Page=page,Task=Task.Run(()=>SpectrumMath.Resample(value,Rate,bins,token),token)});}
  void Drain(){foreach(var key in jobs.Keys.ToArray()){var job=jobs[key];if(!job.Task.IsCompleted)continue;if(job.Task.IsFaulted){var e=job.Task.Exception.GetBaseException();if(!(e is OperationCanceledException))Error=e.Message;}else if(!job.Task.IsCanceled)Store(job.Task.Result);job.Cancel.Dispose();jobs.Remove(key);}foreach(var key in outputs.Keys.ToArray()){var job=outputs[key];if(!job.Task.IsCompleted)continue;if(job.Task.IsFaulted){var e=job.Task.Exception.GetBaseException();if(!(e is OperationCanceledException))Error=e.Message;}else if(!job.Task.IsCanceled)Store(job.Task.Result);outputs.Remove(key);}}
  bool Protected(int page,int low,int high){return page>=low&&page<=high;}
  void Trim(int low,int high){while(RawBytes>RawLimit){var victim=raw.Where(p=>!Protected(p.Value.Page,low,high)).OrderBy(p=>p.Value.Used).FirstOrDefault();if(victim.Value==null)break;RawBytes-=victim.Value.Bytes;raw.Remove(victim.Key);}while(OutputBytes>OutputLimit){var victim=tiles.Where(p=>!Protected(p.Value.Page,First,Last)).OrderBy(p=>p.Value.Used).FirstOrDefault();if(victim.Value==null)break;OutputBytes-=victim.Value.Data.Length;tiles.Remove(victim.Key);}}
  internal void EnsureRange(int first,int last){
   for(int page=Math.Max(0,first);page<=Math.Min(TotalPages-1,last);page++){if(Get(page)!=null)continue;int size=Size;var value=Raw(size,page);if(value==null){string identity=Disk.Identity.GetAwaiter().GetResult();value=Disk.Read(identity,page,Rows(page),size,Hop,Rate);if(value==null){long start=(long)page*256*Hop-size/2;int count=(Rows(page)-1)*Hop+size;while(!pcm.Fill(start,count,System.Diagnostics.Stopwatch.StartNew())){}value=SpectrumMath.Analyze(page,Rows(page),pcm.Copy(start,count),Channels,Rate,size,Hop,cancel.Token);Disk.Write(identity,value,Hop,Rate);}Store(value);}Store(SpectrumMath.Resample(value,Rate,Bins,cancel.Token));}pcm.Trim();Trim(Math.Max(0,first),Math.Min(TotalPages-1,last));
  }
  internal void ClearCpuForChecks(){raw.Clear();tiles.Clear();RawBytes=OutputBytes=0;}
  public void Dispose(){if(disposed)return;disposed=true;cancel.Cancel();foreach(var j in jobs.Values){j.Stop();j.Task.ContinueWith(t=>{var e=t.Exception;},TaskContinuationOptions.OnlyOnFaulted);}foreach(var j in outputs.Values)j.Task.ContinueWith(t=>{var e=t.Exception;},TaskContinuationOptions.OnlyOnFaulted);jobs.Clear();outputs.Clear();raw.Clear();tiles.Clear();done.Clear();pcm.Clear();RawBytes=OutputBytes=0;Clip=null;cancel.Dispose();}
 }
}
