using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
namespace InFalsusStudio.Spectrogram {
 internal sealed class SpectrumCache {
  const int Magic=0x35505349;const long Limit=1024L*1024*1024;
  readonly string root;internal readonly Task<string> Identity;internal int Hits,Writes;internal string Warning;
  internal SpectrumCache(string folder,string audio,int frames,int channels,int rate){root=Path.GetFullPath(Path.Combine(folder,"Cache","Spectrogram-v"+SpectrumMath.Version));Identity=Task.Run(()=>{
   try{if(string.IsNullOrEmpty(audio)||!File.Exists(audio))return null;var info=new FileInfo(audio);long length=info.Length,stamp=info.LastWriteTimeUtc.Ticks;string hash;using(var file=new FileStream(audio,FileMode.Open,FileAccess.Read,FileShare.ReadWrite))using(var sha=SHA256.Create())hash=Hex(sha.ComputeHash(file));info.Refresh();if(length!=info.Length||stamp!=info.LastWriteTimeUtc.Ticks)return null;string metadata=hash+":"+frames+":"+channels+":"+rate+":v"+SpectrumMath.Version;using(var sha=SHA256.Create())return Hex(sha.ComputeHash(Encoding.UTF8.GetBytes(metadata)));}catch(Exception e){Warning=e.Message;return null;}
  });Task.Run(()=>Prune());}
  static string Hex(byte[] bytes){return BitConverter.ToString(bytes).Replace("-","");}
  string PathFor(string identity,int size,int hop,int page){if(identity==null)return null;return Path.Combine(root,identity,"n"+size+"h"+hop,"p"+page+".spcache");}
  internal SpectrumRaw Read(string identity,int page,int rows,int size,int hop,int rate){string path=PathFor(identity,size,hop,page);if(path==null||!File.Exists(path))return null;try{
   using(var file=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read))using(var reader=new BinaryReader(file)){
    if(reader.ReadInt32()!=Magic||reader.ReadInt32()!=SpectrumMath.Version||reader.ReadInt32()!=page||reader.ReadInt32()!=rows||reader.ReadInt32()!=size||reader.ReadInt32()!=hop||reader.ReadInt32()!=rate)return null;int count=reader.ReadInt32();if(count!=Math.Min(size/2+1,(int)Math.Ceiling(Math.Min(20000,rate*.5)*size/rate)+2))return null;int bytes=checked(rows*count*2);if(file.Length!=32L+32+bytes)return null;var expected=reader.ReadBytes(32);var data=reader.ReadBytes(bytes);using(var sha=SHA256.Create())if(!sha.ComputeHash(data).SequenceEqual(expected))return null;var power=new ushort[rows*count];Buffer.BlockCopy(data,0,power,0,data.Length);System.Threading.Interlocked.Increment(ref Hits);return new SpectrumRaw{Page=page,Rows=rows,Size=size,Count=count,Power=power};}
   }catch(Exception e){Warning=e.Message;return null;}}
  internal void Write(string identity,SpectrumRaw tile,int hop,int rate){string path=PathFor(identity,tile.Size,hop,tile.Page);if(path==null)return;string temp=null;try{
   Directory.CreateDirectory(Path.GetDirectoryName(path));temp=path+"."+Guid.NewGuid().ToString("N")+".tmp";var data=new byte[tile.Power.Length*2];Buffer.BlockCopy(tile.Power,0,data,0,data.Length);using(var file=new FileStream(temp,FileMode.CreateNew,FileAccess.Write,FileShare.None))using(var writer=new BinaryWriter(file)){writer.Write(Magic);writer.Write(SpectrumMath.Version);writer.Write(tile.Page);writer.Write(tile.Rows);writer.Write(tile.Size);writer.Write(hop);writer.Write(rate);writer.Write(tile.Count);using(var sha=SHA256.Create())writer.Write(sha.ComputeHash(data));writer.Write(data);}if(File.Exists(path))File.Replace(temp,path,null);else File.Move(temp,path);temp=null;if(System.Threading.Interlocked.Increment(ref Writes)%64==0)Task.Run(()=>Prune());
   }catch(Exception e){Warning=e.Message;}finally{if(temp!=null)try{File.Delete(temp);}catch{}}}
  internal void Prune(){try{if(!Directory.Exists(root))return;var files=new DirectoryInfo(root).EnumerateFiles("*.spcache",SearchOption.AllDirectories).OrderBy(f=>f.LastWriteTimeUtc).ToArray();long bytes=files.Sum(f=>f.Length);foreach(var f in files){if(bytes<=Limit)break;long size=f.Length;try{f.Delete();bytes-=size;}catch{}}}catch(Exception e){Warning=e.Message;}}
 }
}
