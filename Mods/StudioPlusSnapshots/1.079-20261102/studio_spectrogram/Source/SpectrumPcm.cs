using System;
using System.Collections.Generic;
using UnityEngine;
namespace InFalsusStudio.Spectrogram {
 // Unity AudioClip calls stay on the main thread. Immutable page buffers go to workers.
 internal sealed class SpectrumPcm {
  internal const int BlockFrames=16384;const long Limit=32L*1024*1024;
  sealed class Block {internal float[] Data;internal long Used;}
  readonly AudioClip clip;readonly int channels,frames;readonly Dictionary<int,Block> blocks=new Dictionary<int,Block>();long serial,bytes;
  internal double ReadMilliseconds;
  static readonly Func<AudioClip,float[],int,bool> GetData=(Func<AudioClip,float[],int,bool>)Delegate.CreateDelegate(typeof(Func<AudioClip,float[],int,bool>),typeof(AudioClip).GetMethod("GetData",new[]{typeof(float[]),typeof(int)}));
  internal SpectrumPcm(AudioClip audio){clip=audio;channels=audio.channels;frames=audio.samples;}
  internal bool Fill(long start,int count,System.Diagnostics.Stopwatch budget){long end=start+count;int first=(int)(Math.Max(0,start)/BlockFrames),last=(int)((Math.Min(frames,end)-1)/BlockFrames);for(int key=first;key<=last;key++){Block block;if(blocks.TryGetValue(key,out block)){block.Used=++serial;continue;}if(budget.Elapsed.TotalMilliseconds>=1)return false;int at=key*BlockFrames,take=Math.Min(BlockFrames,frames-at);var data=new float[take*channels];var watch=System.Diagnostics.Stopwatch.StartNew();if(!GetData(clip,data,at))throw new InvalidOperationException("Audio PCM is not readable.");watch.Stop();ReadMilliseconds=Math.Max(ReadMilliseconds,watch.Elapsed.TotalMilliseconds);blocks.Add(key,new Block{Data=data,Used=++serial});bytes+=(long)data.Length*4;}return true;}
  internal float[] Copy(long start,int count){var output=new float[checked(count*channels)];long first=Math.Max(0,start),end=Math.Min(frames,start+count);while(first<end){int key=(int)(first/BlockFrames),within=(int)(first%BlockFrames),take=(int)Math.Min(end-first,BlockFrames-within);Block b=blocks[key];b.Used=++serial;Array.Copy(b.Data,within*channels,output,(int)(first-start)*channels,take*channels);first+=take;}return output;}
  internal void Trim(){while(bytes>Limit){int key=-1;long oldest=long.MaxValue;foreach(var p in blocks)if(p.Value.Used<oldest){key=p.Key;oldest=p.Value.Used;}if(key<0)break;bytes-=(long)blocks[key].Data.Length*4;blocks.Remove(key);}}
  internal void Clear(){blocks.Clear();bytes=0;}
 }
}
