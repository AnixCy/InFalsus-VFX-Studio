using System;
using System.Collections.Concurrent;
using System.Threading;
namespace InFalsusStudio.Spectrogram {
 internal sealed class SpectrumTile {
  internal int Page,Rows,Bins,Size;internal byte[] Data;internal long Used;
 }
 internal sealed class SpectrumRaw {
  internal int Page,Rows,Size,Count;internal ushort[] Power;internal long Used;
  internal long Bytes {get{return (long)Power.Length*2;}}
 }
 internal static class SpectrumMath {
  internal const int Bins=256,RowsPerPage=256,Version=5;
  internal static readonly int[] OutputSizes={64,128,256,512,1024,2048};
  internal static int ValidBins(int n){return n>=2&&n<=2048&&(n&(n-1))==0?n:Bins;}
  internal static int Hop(int rate){int n=1;while(n<rate/375.0)n<<=1;return Math.Max(32,n);}
  // Short time support takes priority over oversampled frequency columns.
  internal static int WindowSize(int rate,int bins){double top=Math.Min(20000,rate*.5);double required=Math.Max(rate*.010,.35*rate*ValidBins(bins)/Math.Max(1,top-20));int n=1;while(n<required)n<<=1;return n;}
  internal static double Frequency(int column,int rate,int bins=Bins){return 20+(Math.Min(20000,rate*.5)-20)*(column+.5)/bins;}
  internal static double AudioToZ(double audio,double offset,double now,double units){return (audio-offset-now)*units;}
  static readonly double[] Unpack=MakeUnpack();
  static double[] MakeUnpack(){var a=new double[65536];for(int i=1;i<a.Length;i++)a[i]=Math.Pow(10,(i/65535d*132-120)/10);return a;}
  static ushort Pack(double power){if(power<=1e-12)return 0;return (ushort)Math.Round(Math.Max(0,Math.Min(1,(10*Math.Log10(power)+120)/132))*65535);}
  sealed class Plan {
   internal readonly int Size;internal readonly int[] Reverse;internal readonly double[] Window,Cos,Sin;internal readonly double Norm;
   internal Plan(int size){Size=size;Reverse=new int[size];Window=new double[size];Cos=new double[size/2];Sin=new double[size/2];int bits=0;for(int n=size;n>1;n>>=1)bits++;double sum=0;for(int i=0;i<size;i++){int v=i,rev=0;for(int j=0;j<bits;j++){rev=(rev<<1)|(v&1);v>>=1;}Reverse[i]=rev;Window[i]=.5-.5*Math.Cos(2*Math.PI*i/(size-1));sum+=Window[i];}Norm=4/(sum*sum);for(int i=0;i<Cos.Length;i++){double angle=-2*Math.PI*i/size;Cos[i]=Math.Cos(angle);Sin[i]=Math.Sin(angle);}}
   internal void Fft(double[] re,double[] im){for(int i=0;i<Size;i++){int j=Reverse[i];if(i<j){double v=re[i];re[i]=re[j];re[j]=v;v=im[i];im[i]=im[j];im[j]=v;}}for(int len=2;len<=Size;len<<=1){int half=len/2,step=Size/len;for(int at=0;at<Size;at+=len)for(int j=0;j<half;j++){int a=at+j,b=a+half,k=j*step;double x=re[b]*Cos[k]-im[b]*Sin[k],y=re[b]*Sin[k]+im[b]*Cos[k];re[b]=re[a]-x;im[b]=im[a]-y;re[a]+=x;im[a]+=y;}}}
  }
  static readonly ConcurrentDictionary<int,Plan> Plans=new ConcurrentDictionary<int,Plan>();
  internal static SpectrumRaw Analyze(int page,int rows,float[] pcm,int channels,int rate,int size,int hop,CancellationToken cancel){
   var plan=Plans.GetOrAdd(size,n=>new Plan(n));int count=Math.Min(size/2+1,(int)Math.Ceiling(Math.Min(20000,rate*.5)*size/rate)+2);var result=new SpectrumRaw{Page=page,Rows=rows,Size=size,Count=count,Power=new ushort[checked(rows*count)]};var re=new double[size];var im=new double[size];var power=new double[count];double norm=plan.Norm/channels;
   for(int row=0;row<rows;row++){if((row&7)==0)cancel.ThrowIfCancellationRequested();Array.Clear(power,0,count);for(int c=0;c<channels;c++){long start=(long)row*hop*channels+c;for(int i=0;i<size;i++){float value=pcm[start+(long)i*channels];re[i]=(float.IsNaN(value)||float.IsInfinity(value)?0:value)*plan.Window[i];im[i]=0;}plan.Fft(re,im);for(int k=1;k<count;k++)power[k]+=(re[k]*re[k]+im[k]*im[k])*norm;}for(int k=0;k<count;k++)result.Power[row*count+k]=Pack(power[k]);}return result;
  }
  // Area integration of the piecewise linear power spectrum, identical for every band.
  internal static SpectrumTile Resample(SpectrumRaw raw,int rate,int bins,CancellationToken cancel){
   bins=ValidBins(bins);var tile=new SpectrumTile{Page=raw.Page,Rows=raw.Rows,Size=raw.Size,Bins=bins,Data=new byte[raw.Rows*bins]};var power=new double[raw.Count];var integral=new double[raw.Count];var edges=new double[bins+1];double max=Math.Min(20000,rate*.5);for(int x=0;x<=bins;x++)edges[x]=Math.Max(1,Math.Min(raw.Count-1,(20+(max-20)*x/bins)*raw.Size/rate));
   for(int row=0;row<raw.Rows;row++){if((row&31)==0)cancel.ThrowIfCancellationRequested();for(int k=0;k<raw.Count;k++)power[k]=Unpack[raw.Power[row*raw.Count+k]];integral[0]=0;for(int k=1;k<raw.Count;k++)integral[k]=integral[k-1]+(power[k-1]+power[k])*.5;for(int x=0;x<bins;x++){double width=edges[x+1]-edges[x],energy=width>1e-9?(Area(power,integral,edges[x+1])-Area(power,integral,edges[x]))/width:power[(int)edges[x]];double db=10*Math.Log10(Math.Max(1e-12,energy));tile.Data[row*bins+x]=(byte)Math.Round(Math.Max(0,Math.Min(1,(db+80)/80))*255);}}return tile;
  }
  static double Area(double[] p,double[] sum,double at){int k=Math.Min(p.Length-2,(int)at);double t=at-k;return sum[k]+p[k]*t+(p[k+1]-p[k])*t*t*.5;}
 }
}
