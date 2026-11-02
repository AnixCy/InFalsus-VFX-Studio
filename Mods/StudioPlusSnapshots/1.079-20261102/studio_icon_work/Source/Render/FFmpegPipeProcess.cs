using System;using System.IO;using System.Diagnostics;using System.Collections.Concurrent;using System.Threading;using System.Text;using System.Globalization;
namespace InFalsusStudio.VideoRender {
 internal sealed class FFmpegPipeProcess:IDisposable {
  readonly BlockingCollection<byte[]> ready=new BlockingCollection<byte[]>(4);readonly ConcurrentQueue<byte[]> free=new ConcurrentQueue<byte[]>();readonly StringBuilder errors=new StringBuilder();readonly Process process;readonly Thread writer;volatile Exception failure;volatile bool disposed;internal long Written;
  internal static string Quote(string path){if(path.IndexOfAny(new[]{'"','\r','\n'})>=0)throw new ArgumentException("Invalid output path");return "\""+path+"\"";}
  internal static string Arguments(RenderJob j){string codec=j.Codec==1?"h264_nvenc":j.Codec==2?"libx265":"libx264";string quality=j.Codec==1?"-preset p5 -rc vbr -cq ":"-preset medium -crf ";return "-y -hide_banner -loglevel warning -nostdin -f rawvideo -pixel_format rgba -video_size "+j.Width+"x"+j.Height+" -framerate "+j.Fps+" -i pipe:0 -i "+Quote(j.TempAudio)+" -map 0:v:0 -map 1:a:0 -vf vflip,scale=in_range=full:out_range=tv:out_color_matrix=bt709,format=yuv420p -color_range tv -colorspace bt709 -color_primaries bt709 -color_trc bt709 -c:v "+codec+" "+quality+j.Crf+" -c:a aac -b:a 320k -t "+j.Seconds.ToString("0.#########",CultureInfo.InvariantCulture)+" -movflags +faststart "+Quote(j.TempVideo);}
  internal FFmpegPipeProcess(RenderJob j){for(int i=0;i<4;i++)free.Enqueue(new byte[checked(j.Width*j.Height*4)]);process=new Process{StartInfo=new ProcessStartInfo(j.Encoder,Arguments(j)){UseShellExecute=false,CreateNoWindow=true,RedirectStandardInput=true,RedirectStandardError=true}};try{process.ErrorDataReceived+=delegate(object sender,DataReceivedEventArgs e){if(e.Data==null)return;lock(errors){if(errors.Length>12000)errors.Remove(0,errors.Length-8000);errors.AppendLine(e.Data);}};process.Start();process.BeginErrorReadLine();writer=new Thread(Write){IsBackground=true,Name="InFalsus video encoder"};writer.Start();}catch{process.Dispose();ready.Dispose();throw;}}
  void Write(){try{foreach(byte[] bytes in ready.GetConsumingEnumerable()){process.StandardInput.BaseStream.Write(bytes,0,bytes.Length);Interlocked.Increment(ref Written);free.Enqueue(bytes);}process.StandardInput.Close();}catch(Exception e){if(!disposed)failure=e;}}
  internal bool Take(out byte[] bytes){ThrowIfFailed();return free.TryDequeue(out bytes);}
  internal void Submit(byte[] bytes){ThrowIfFailed();if(!ready.TryAdd(bytes))throw new IOException("Encoder frame queue overflow");}
  internal void Finish(){ready.CompleteAdding();}
  internal bool Complete{get{ThrowIfFailed();return !writer.IsAlive&&process.HasExited;}}
  internal string ErrorText{get{lock(errors)return errors.ToString();}}
  internal void ThrowIfFailed(){if(failure!=null)throw new IOException("FFmpeg pipe failed: "+ErrorText,failure);if(process.HasExited&&process.ExitCode!=0)throw new IOException("FFmpeg failed: "+ErrorText);}
  internal void CheckSuccess(long frames){ThrowIfFailed();process.WaitForExit();if(process.ExitCode!=0||Written!=frames)throw new IOException("Incomplete video: "+ErrorText);}
  public void Dispose(){if(disposed)return;disposed=true;try{if(!process.HasExited)process.Kill();}catch{}if(writer!=null)writer.Join(1500);process.Dispose();ready.Dispose();}
 }
}
