using System;
using System.Threading;
using InFalsusStudio.Core;
namespace InFalsusStudio.Gameplay {
 // WSOLA and resampling run off the real-time mixer; only prepared frames cross this SPSC ring.
 internal sealed class GameplayMusicPcm:IDisposable {
  const int Capacity=32768;readonly float[] ring=new float[Capacity*2];readonly PcmSample source;readonly double rate,sourceMs;readonly int outputHz;readonly Thread worker;readonly ManualResetEvent ready=new ManualResetEvent(false);volatile bool running=true;long read,write;internal Exception Error;
  internal GameplayMusicPcm(PcmSample pcm,double rate,double sourceMs,int outputHz){source=pcm;this.rate=rate;this.sourceMs=Math.Max(0,sourceMs);this.outputHz=outputHz;worker=new Thread(Produce){IsBackground=true,Name="IFS gameplay music preparation"};worker.Start();}
  internal bool Ready {get{return Interlocked.Read(ref write)>=4096||Error!=null;}}
  internal bool Read(out float left,out float right){long r=read;if(r>=Interlocked.Read(ref write)){left=right=0;return false;}int i=(int)(r%Capacity)*2;left=ring[i];right=ring[i+1];Interlocked.Exchange(ref read,r+1);return true;}
  void Produce(){try{
   if(source==null){while(running){FillSilence();Thread.Sleep(1);}return;}
   WsolaTimeStretch stretch=null;float[] chunk=null,next=null;long streamBase=0;int chunkFrames=2048;
   if(Math.Abs(rate-1)>1e-9){stretch=new WsolaTimeStretch(source.Data,source.Channels,source.Frequency,rate);stretch.ResetOutputPosition((int)Math.Min(stretch.OutputFrames,sourceMs*.001*source.Frequency/rate));chunk=new float[chunkFrames*source.Channels];next=new float[(chunkFrames-1)*source.Channels];stretch.Read(chunk);}
   double pos=stretch==null?sourceMs*.001*source.Frequency:0,step=(double)source.Frequency/outputHz;
   while(running){long w=write;if(w-Interlocked.Read(ref read)>=Capacity-1){Thread.Sleep(1);continue;}float l,r;
    if(stretch==null){l=source.At(pos,0,false);r=source.At(pos,1,false);}
    else{
     while(pos-streamBase>=chunkFrames-1){float lastL=chunk[(chunkFrames-1)*source.Channels],lastR=chunk[(chunkFrames-1)*source.Channels+(source.Channels==1?0:1)];streamBase+=chunkFrames-1;stretch.Read(next);for(int c=0;c<source.Channels;c++)chunk[c]=c==0?lastL:lastR;Array.Copy(next,0,chunk,source.Channels,next.Length);}
     int at=(int)(pos-streamBase);float f=(float)(pos-streamBase-at);l=chunk[at*source.Channels]*(1-f)+chunk[(at+1)*source.Channels]*f;int rc=source.Channels==1?0:1;r=chunk[at*source.Channels+rc]*(1-f)+chunk[(at+1)*source.Channels+rc]*f;
    }
    int index=(int)(w%Capacity)*2;ring[index]=l;ring[index+1]=r;Interlocked.Exchange(ref write,w+1);pos+=step;
   }
  }catch(Exception e){Error=e;}finally{ready.Set();}}
  void FillSilence(){while(running&&write-Interlocked.Read(ref read)<Capacity-1){long w=write;int i=(int)(w%Capacity)*2;ring[i]=ring[i+1]=0;Interlocked.Exchange(ref write,w+1);}}
  public void Dispose(){running=false;if(worker!=null)worker.Join();ready.Dispose();}
 }
}
