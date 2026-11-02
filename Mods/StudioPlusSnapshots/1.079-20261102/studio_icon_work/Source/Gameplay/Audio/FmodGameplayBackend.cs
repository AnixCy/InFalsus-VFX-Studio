using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using F=InFalsusStudio.Gameplay.FmodNative;
namespace InFalsusStudio.Gameplay {
 internal sealed class FmodGameplayBackend:IDisposable {
  [DllImport("kernel32",CharSet=CharSet.Unicode,SetLastError=true)]static extern IntPtr LoadLibraryW(string file);
  static IntPtr library;
  F.System system;F.DSP generator;F.Channel channel;F.DSP_READ_CALLBACK readCallback;F.DSP_GETCLOCK_FUNC clockFunction;F.DSP_SHOULDIPROCESS_CALLBACK processCallback;Thread control;volatile bool alive,stopping;readonly float[] scratch=new float[65536];
  internal readonly GameplayClockBridge Clock=new GameplayClockBridge();internal GameplayTransport Transport;internal GameplayAudioScheduler Scheduler;internal GameplayMusicPcm Music;internal int Frequency,BufferCount;internal uint BlockFrames;internal string Output;internal volatile string Failure;internal volatile float MusicVolume=1;internal long CallbackCount;internal Action<long,float[],int,int> ObserveBlock;
  internal static void Check(F.RESULT result,string name){if(result!=F.RESULT.OK)throw new InvalidOperationException("FMOD "+name+": "+result);}
  internal FmodGameplayBackend(int block,string runtimePath,bool silent=false){
   try{
    if(library==IntPtr.Zero){library=LoadLibraryW(runtimePath);if(library==IntPtr.Zero)throw new IOException("Cannot load gameplay audio runtime: "+runtimePath+" ("+Marshal.GetLastWin32Error()+")");}
    Check(F.Factory.System_Create(out system),"create");uint version;Check(system.getVersion(out version),"version");if(version!=0x20309)throw new InvalidOperationException("Expected FMOD 2.03.09; got "+version.ToString("x"));
    if(silent)Check(system.setOutput(F.OUTPUTTYPE.NOSOUND),"silent output");
    Check(system.setSoftwareFormat(0,F.SPEAKERMODE.STEREO,0),"format");Check(system.setDSPBufferSize((uint)block,3),"buffer");Check(system.init(128,F.INITFLAGS.NORMAL,IntPtr.Zero),"init");F.SPEAKERMODE mode;int raw;Check(system.getSoftwareFormat(out Frequency,out mode,out raw),"actual format");Check(system.getDSPBufferSize(out BlockFrames,out BufferCount),"actual buffer");F.OUTPUTTYPE output;system.getOutput(out output);Output=output.ToString();
    readCallback=Read;processCallback=ShouldProcess;var desc=new F.DSP_DESCRIPTION{pluginsdkversion=110,name=new byte[32],version=0x10000,numinputbuffers=0,numoutputbuffers=1,read=readCallback,shouldiprocess=processCallback};var name=System.Text.Encoding.ASCII.GetBytes("IFS gameplay PCM and clock");Array.Copy(name,desc.name,name.Length);
    Check(system.createDSP(ref desc,out generator),"create generator");Check(system.playDSP(generator,default(F.ChannelGroup),false,out channel),"start generator");channel.setVolume(1);channel.setPriority(0);Check(system.update(),"update");alive=true;control=new Thread(Control){IsBackground=true,Name="IFS gameplay audio control"};control.Start();
   }catch{Dispose();throw;}
  }
  F.RESULT ShouldProcess(ref F.DSP_STATE state,bool idle,uint length,F.CHANNELMASK mask,int channels,F.SPEAKERMODE mode){return F.RESULT.OK;}
  F.RESULT Read(ref F.DSP_STATE state,IntPtr input,IntPtr output,uint length,int inputChannels,ref int outputChannels){
   try{
    int channels=outputChannels;if(channels<=0){channels=2;outputChannels=2;}int count=checked((int)length*channels);if(count>scratch.Length){Failure="Gameplay audio block exceeds prepared capacity";return F.RESULT.ERR_DSP_SILENCE;}
    if(clockFunction==null)clockFunction=state.functions.getclock;ulong clock;uint offset,block;Check(clockFunction(ref state,out clock,out offset,out block),"DSP clock");long first=checked((long)clock+offset);Clock.Publish(first,Stopwatch.GetTimestamp(),Frequency,(int)length);Array.Clear(scratch,0,count);
    var tr=Transport;var pcm=Music;var schedule=Scheduler;
    if(!stopping&&tr!=null&&tr.Playing&&pcm!=null){double musicStart=tr.StartFrame+Math.Max(0,-(tr.Entry+tr.Offset))*Frequency/(1000*tr.Rate);for(int f=0;f<(int)length;f++){if(first+f<musicStart)continue;float l,r;if(!pcm.Read(out l,out r)){if(schedule!=null)Interlocked.Increment(ref schedule.Diagnostics.Underruns);}for(int c=0;c<channels;c++)scratch[f*channels+c]=(c%2==0?l:r)*MusicVolume;}}
    if(!stopping&&tr!=null&&tr.Playing&&schedule!=null)schedule.Mix(scratch,(int)length,channels,Frequency,first);
    for(int i=0;i<count;i++){float v=scratch[i],a=Math.Abs(v);if(a>.95f)scratch[i]=Math.Sign(v)*(.95f+.04f*(1-1/(1+(a-.95f)*25)));}
    var observer=ObserveBlock;if(observer!=null)observer(first,scratch,(int)length,channels);
    Marshal.Copy(scratch,0,output,count);Interlocked.Increment(ref CallbackCount);return F.RESULT.OK;
   }catch(Exception e){Failure=e.GetType().Name+": "+e.Message;if(Scheduler!=null)Interlocked.Increment(ref Scheduler.Diagnostics.CallbackErrors);return F.RESULT.ERR_DSP_SILENCE;}
  }
  void Control(){while(alive){try{var result=system.update();if(result!=F.RESULT.OK){Failure="FMOD update: "+result;break;}}catch(Exception e){Failure=e.Message;break;}Thread.Sleep(1);}}
  public void Dispose(){stopping=true;alive=false;if(control!=null&&control!=Thread.CurrentThread){control.Join();control=null;}if(channel.hasHandle()){channel.stop();channel.clearHandle();}if(generator.hasHandle()){generator.release();generator.clearHandle();}if(system.hasHandle()){system.close();system.release();system.clearHandle();}if(Music!=null){Music.Dispose();Music=null;}Scheduler=null;Transport=null;GC.KeepAlive(readCallback);GC.KeepAlive(clockFunction);GC.KeepAlive(processCallback);}
 }
}
