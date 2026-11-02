using System;
using System.IO;
using System.Collections;
using System.Globalization;
using UnityEngine;
using InFalsusStudio;
using InFalsusStudio.Core;
namespace InFalsusStudio.NativeEditorUI {
 public sealed partial class NativeEditorUIPlugin {
  static readonly Func<AudioClip,float[],int,bool> audioGetData=(Func<AudioClip,float[],int,bool>)Delegate.CreateDelegate(typeof(Func<AudioClip,float[],int,bool>),typeof(AudioClip).GetMethod("GetData",new[]{typeof(float[]),typeof(int)}));
  OggAudioEncoder audioOggEncoder;
  bool audioExportBusy,audioExportCancel; string audioExportFolder; string audioExportMessage="使用导出音频时，请将偏移设为 0。"; float audioExportProgress;
  void DrawProjectAudioExport(){
   Rect button=new Rect(28*scale,160*scale,(string.IsNullOrEmpty(audioExportFolder)||audioExportBusy?405:267)*scale,40*scale);
   if(FlatButton(button,audioExportBusy?"取消导出":"按当前偏移导出音频",16)){
    if(audioExportBusy)audioExportCancel=true;else Queue(StartAudioExport);
   }
   if(!audioExportBusy&&!string.IsNullOrEmpty(audioExportFolder)&&FlatButton(new Rect(305*scale,160*scale,128*scale,40*scale),"打开文件夹",15)){
    try{using(System.Diagnostics.Process.Start(ProjectFolderStartInfo(audioExportFolder))){} }catch(Exception ex){audioExportMessage="打开文件夹失败："+ex.Message;}
   }
   string message=audioExportBusy?"正在导出 "+Mathf.RoundToInt(audioExportProgress*100)+"%":audioExportMessage;
   var row=new Rect(28*scale,205*scale,405*scale,28*scale);
   UiText(row,UiFontRaster.Ellipsize(T(message),CNFace,FontPixels(13),row.width),13,Css("#a6c3d1"));
  }
  void StartAudioExport(){
   try{
    if(audioExportBusy||!(bool)Call(boot,"CommitAllInputFieldsNow"))return;
    var transport=Transport;var clip=transport.Clip;if(clip==null)throw new InvalidOperationException("请先加载音频。");
    if(transport.Playing)throw new InvalidOperationException("请先暂停预览。");
    double offset=transport.AudioOffsetMs;if(double.IsNaN(offset)||double.IsInfinity(offset))throw new InvalidOperationException("音频偏移无效。");
    if(offset==0){audioExportMessage="偏移为 0，无需导出重复文件。";return;}
    var project=Get(boot,"songProject") as SongFolderProject;string chart=Val<string>(boot,"loadedPath"),source=project!=null?project.AudioPath:Val<string>(boot,"loadedAudioPath");
    string folder=project!=null?project.Root:(!string.IsNullOrEmpty(chart)?Path.GetDirectoryName(Path.GetFullPath(chart)):null);
    if(string.IsNullOrEmpty(folder)||!Directory.Exists(folder))throw new InvalidOperationException("请先保存谱面或打开歌曲工程。");
    if(string.IsNullOrEmpty(source)||!File.Exists(source))throw new InvalidOperationException("当前音频文件路径不可用。");
    long shift=checked((long)Math.Round(offset*clip.frequency/1000,MidpointRounding.AwayFromZero));
    if(shift>=clip.samples)throw new InvalidOperationException("正偏移不小于音频长度，无法生成空音频。");
    long frames=checked((long)clip.samples-shift);long bytes=checked(frames*clip.channels*4L);
    if(frames<=0||bytes/2+44>256L*1024*1024||clip.channels<1||clip.frequency<1)throw new InvalidOperationException("导出文件超过 256 MiB 或音频格式无效。");
    string off=Math.Abs(offset).ToString("0.########",CultureInfo.InvariantCulture);string stem=Path.GetFileNameWithoutExtension(source)+"_offset_"+(offset>0?"p":"m")+off+"ms";
    string encoder=videoPreferences.FindEncoder();if(string.IsNullOrEmpty(encoder))throw new InvalidOperationException("未找到 FFmpeg，请在视频渲染设置中指定 FFmpeg。");
    string output=Path.Combine(folder,stem+".ogg");for(int i=1;File.Exists(output);i++)output=Path.Combine(folder,stem+"_"+i+".ogg");
    audioExportBusy=true;audioExportCancel=false;audioExportProgress=0;StartCoroutine(ExportAudio(clip,transport,project,chart,source,offset,shift,frames,bytes,output,project==null?-1:project.ActiveDifficulty));
   }catch(Exception ex){audioExportMessage="导出失败："+ex.Message;}
  }
  bool ExportCurrent(AudioClip clip,StudioTransport tr,SongFolderProject project,string chart,string source,double offset,int difficulty){
   if(audioExportCancel||boot==null||Transport!=tr||tr.Clip!=clip||tr.Playing||tr.AudioOffsetMs!=offset||Get(boot,"songProject")!=project||Val<string>(boot,"loadedPath")!=chart)return false;
   if(project!=null)return project.ActiveDifficulty==difficulty&&project.AudioPath==source;
   return Val<string>(boot,"loadedAudioPath")==source;
  }
  static void WriteWaveHeader(BinaryWriter w,int frequency,int channels,int data){
   w.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));w.Write(36+data);w.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));w.Write(16);w.Write((short)3);w.Write((short)channels);w.Write(frequency);w.Write(frequency*channels*4);w.Write((short)(channels*4));w.Write((short)32);w.Write(System.Text.Encoding.ASCII.GetBytes("data"));w.Write(data);
  }
  IEnumerator ExportAudio(AudioClip clip,StudioTransport tr,SongFolderProject project,string chart,string source,double offset,long shift,long frames,long bytes,string output,int difficulty){
   string temp=Path.Combine(Path.GetDirectoryName(output),".infstudio-audio-"+Guid.NewGuid().ToString("N")+".tmp");FileStream stream=null;BinaryWriter writer=null;string error=null;bool success=false;long done=0;string encoded=temp+".ogg";
   try{
    try{stream=new FileStream(temp,FileMode.CreateNew,FileAccess.Write,FileShare.None);writer=new BinaryWriter(stream);WriteWaveHeader(writer,clip.frequency,clip.channels,(int)bytes);}catch(Exception ex){error=ex.Message;}
    while(error==null&&done<frames){
     try{
      if(!ExportCurrent(clip,tr,project,chart,source,offset,difficulty))throw new OperationCanceledException("导出已取消或工程、难度、音频、偏移发生变化。");
      int take=(int)Math.Min(8192,frames-done);long first=done+shift;int silence=(int)Math.Min(take,Math.Max(0,-first));
      for(int i=0;i<silence*clip.channels;i++)writer.Write(0f);
      int read=take-silence;if(read>0){var samples=new float[read*clip.channels];if(!audioGetData(clip,samples,(int)Math.Max(0,first)))throw new IOException("读取音频数据失败。");foreach(float sample in samples)writer.Write(float.IsNaN(sample)||float.IsInfinity(sample)?0f:sample);}
      done+=take;audioExportProgress=.9f*(float)((double)done/frames);
     }catch(Exception ex){error=ex.Message;}
     yield return null;
    }
    if(writer!=null){try{writer.Flush();writer.Close();writer=null;stream=null;}catch(Exception ex){error=ex.Message;}}
    if(error==null){try{
      if(!ExportCurrent(clip,tr,project,chart,source,offset,difficulty))throw new OperationCanceledException("工程或偏移已变化。");
      using(var f=File.OpenRead(temp))using(var r=new BinaryReader(f)){if(f.Length!=bytes+44||new string(r.ReadChars(4))!="RIFF"||r.ReadInt32()!=bytes+36||new string(r.ReadChars(4))!="WAVE")throw new IOException("WAV 文件校验失败。");}
      audioOggEncoder=new OggAudioEncoder(videoPreferences.FindEncoder(),temp,encoded);audioExportProgress=.94f;
    }catch(Exception ex){error=ex.Message;}}
    while(error==null&&audioOggEncoder!=null&&!audioOggEncoder.HasExited){try{if(!ExportCurrent(clip,tr,project,chart,source,offset,difficulty))throw new OperationCanceledException("导出已取消或工程、难度、音频、偏移发生变化。");}catch(Exception ex){error=ex.Message;}yield return null;}
    if(error==null){try{if(!ExportCurrent(clip,tr,project,chart,source,offset,difficulty))throw new OperationCanceledException("工程或偏移已变化。");audioOggEncoder.CheckSuccess();using(var f=File.OpenRead(encoded))using(var r=new BinaryReader(f)){if(f.Length<64||new string(r.ReadChars(4))!="OggS")throw new IOException("OGG 文件校验失败。");}File.Move(encoded,output);success=true;audioExportProgress=1;audioExportFolder=Path.GetDirectoryName(output);audioExportMessage="导出完成："+output+"。使用导出音频时偏移设为 0。";}catch(Exception ex){error=ex.Message;}}
   }finally{if(audioOggEncoder!=null){audioOggEncoder.Dispose();audioOggEncoder=null;}if(writer!=null)writer.Dispose();else if(stream!=null)stream.Dispose();foreach(string temporary in new[]{temp,encoded})if(File.Exists(temporary))try{File.Delete(temporary);}catch{}audioExportBusy=false;if(!success)audioExportMessage="导出未完成："+(error??"已取消");}
  }
  void CancelOffsetAudioExport(){audioExportCancel=true;if(audioOggEncoder!=null){audioOggEncoder.Dispose();audioOggEncoder=null;}}
  internal sealed class OggAudioEncoder:IDisposable {
   readonly System.Diagnostics.Process process;readonly System.Text.StringBuilder errors=new System.Text.StringBuilder();bool disposed;
   internal static string Arguments(string input,string output){return "-hide_banner -loglevel warning -nostdin -n -i "+InFalsusStudio.VideoRender.FFmpegPipeProcess.Quote(input)+" -map 0:a:0 -vn -map_metadata -1 -c:a libvorbis -q:a 10 -f ogg "+InFalsusStudio.VideoRender.FFmpegPipeProcess.Quote(output);}
   internal OggAudioEncoder(string exe,string input,string output){process=new System.Diagnostics.Process{StartInfo=new System.Diagnostics.ProcessStartInfo(exe,Arguments(input,output)){UseShellExecute=false,CreateNoWindow=true,RedirectStandardError=true}};try{process.ErrorDataReceived+=delegate(object sender,System.Diagnostics.DataReceivedEventArgs e){if(e.Data==null)return;lock(errors){if(errors.Length>12000)errors.Remove(0,errors.Length-8000);errors.AppendLine(e.Data);}};process.Start();process.BeginErrorReadLine();}catch{process.Dispose();throw;}}
   internal bool HasExited{get{return disposed||process.HasExited;}}
   internal void CheckSuccess(){if(disposed)throw new OperationCanceledException("OGG export cancelled.");process.WaitForExit();if(process.ExitCode!=0){lock(errors)throw new IOException("OGG 编码失败："+errors);}}
   public void Dispose(){if(disposed)return;disposed=true;try{if(!process.HasExited){process.Kill();process.WaitForExit(1000);}}catch{}process.Dispose();}
  }
 }
}
