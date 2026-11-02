using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using InFalsusStudio.Core;
using InFalsusStudio.TimeGroups;
using InFalsusStudio.NativeEditorUI;
using InFalsusStudio.NativeVisual;
using UnityEngine;
namespace InFalsusStudio.Performance {
 internal static class P1PlayerProbe {
  internal static bool Requested {get{return Environment.GetCommandLineArgs().Contains("-visualui-p1-probe");}}
  static string Arg(string name,string fallback){var args=Environment.GetCommandLineArgs();int i=Array.IndexOf(args,name);return i>=0&&i+1<args.Length?args[i+1]:fallback;}
  static void Set(object o,string name,object value){o.GetType().GetField(name,TimingGroups.F).SetValue(o,value);}
  internal static IEnumerator Run(){
   string root=Arg("-visualui-p1-probe","");Directory.CreateDirectory(root);yield return null;yield return null;yield return null;
   StudioBootstrap boot=UnityEngine.Object.FindFirstObjectByType<StudioBootstrap>();
   if(boot==null){File.WriteAllText(Path.Combine(root,"ERROR.txt"),"StudioBootstrap missing");Application.Quit(2);yield break;}
   Application.runInBackground=true;QualitySettings.vSyncCount=0;Application.targetFrameRate=120;
   Set(boot,"showReleaseNotes",false);Set(boot,"guiShowReleaseNotes",false);Set(boot,"showUi",true);Set(boot,"showSongHud",true);Set(boot,"showGrid",false);Set(boot,"showNotes",true);Set(boot,"hidePast",true);Set(boot,"autoCursor",true);Set(boot,"showFeedback",true);
   var ui=NativeEditorUIPlugin.I;var transport=(StudioTransport)GroupRuntime.Get(boot,"transport");
   File.WriteAllText(Path.Combine(root,"environment.txt"),"Unity="+Application.unityVersion+"\r\nGPU="+SystemInfo.graphicsDeviceName+"\r\nAPI="+SystemInfo.graphicsDeviceType+"\r\nResolution="+Screen.width+"x"+Screen.height+"\r\nFrameTimingFeature="+FrameTimingManager.IsFeatureEnabled()+"\r\nGpuTimerFrequency="+FrameTimingManager.GetGpuTimerFrequency()+"\r\nProfiling=actual isolated Player; real DSP playback clock, no song music clip; native key sounds/feedback retained\r\nTimingCounters=asynchronous/delayed; profiler overhead included\r\nAB=same build, P1 optimizations disabled/enabled; previous UI/source-cache fixes enabled in both\r\n");
   var plain=new StringBuilder("chart(120,4)\n");for(int i=0;i<128;i++)plain.Append("tap(").Append(i*500).Append(",1,").Append(i%4+1).Append(")\n");
   var dense=new StringBuilder("chart(120,4)\n");for(int i=0;i<8000;i++)dense.Append(i%3==0?"track(":"tap(").Append(i*50).Append(i%3==0?",1)\n":",1,1)\n");
   string realPath=Arg("-visualui-p1-chart","");var charts=new[]{plain.ToString(),File.ReadAllText(realPath),dense.ToString()};string[] names={"ordinary","large","dense-track"};
   // Each chart: paused/playback, timing panel closed/open, and all FX modes.
   bool[] plays={false,false,true,true,true,true};bool[] panels={false,true,false,true,true,true};HitEffectStyle[] styles={HitEffectStyle.Simple,HitEffectStyle.Simple,HitEffectStyle.Simple,HitEffectStyle.None,HitEffectStyle.Simple,HitEffectStyle.Full};
   for(int chart=0;chart<charts.Length;chart++){
    var doc=SpcDocument.Parse(charts[chart]);byte[] before=doc.ToBytes();transport.Pause();typeof(StudioBootstrap).GetMethod("LoadDocument",TimingGroups.F).Invoke(boot,new object[]{doc});
    for(int mode=0;mode<2;mode++)for(int scenario=0;scenario<plays.Length;scenario++){
     P1Switches.Scroll=mode==1;P1Switches.Rendering=mode==1;transport.Pause();transport.Seek(30000);ui.formal=false;ui.panel=panels[scenario]?"时序":"";Set(boot,"showInspector",panels[scenario]);NativeVisualPlugin.HitStyle.Value=styles[scenario];
     if(plays[scenario])transport.Play();
     int warmFrames=int.Parse(Arg("-visualui-p1-warm",chart==2?"2":"8"),CultureInfo.InvariantCulture);for(int warm=0;warm<warmFrames;warm++)yield return null;
     string name=names[chart]+"_"+(mode==0?"reference":"optimized")+"_"+(plays[scenario]?"play":"pause")+"_"+(panels[scenario]?"timing":"closed")+"_"+styles[scenario];
     File.WriteAllText(Path.Combine(root,"progress.txt"),name);P1Profiler.Begin(name,root,double.Parse(Arg("-visualui-p1-seconds","1.6"),CultureInfo.InvariantCulture));while(P1Profiler.Active)yield return null;transport.Pause();
     if(!before.SequenceEqual(doc.ToBytes())){File.WriteAllText(Path.Combine(root,"ERROR.txt"),"Chart source changed during "+name);Application.Quit(3);yield break;}
    }
   }
   yield return P1PlayerAcceptance.Run(boot,root);
   P1Switches.Scroll=true;P1Switches.Rendering=true;File.WriteAllText(Path.Combine(root,"DONE.txt"),"36 actual Player cases completed; sources unchanged. No keyboard/mouse acceptance claimed.");Application.Quit(0);
  }
 }
}
