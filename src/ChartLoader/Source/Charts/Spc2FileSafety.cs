using System.Buffers.Binary;
using System.Security.Cryptography;

namespace InFalsusChartLoader;

internal static class Spc2FileSafety
{
    static bool? supported;
    internal static bool Supported()
    {
        if(supported.HasValue)return supported.Value;
        string root=System.Diagnostics.Process.GetCurrentProcess().MainModule!.FileName!;
        using var stream=File.OpenRead(Path.Combine(Path.GetDirectoryName(root)!,"GameAssembly.dll"));
        using var hasher=SHA256.Create();string sha=Convert.ToHexString(hasher.ComputeHash(stream));
        return (supported=sha=="C7D0C835B88F8B2BDE577107ACE496A15913491588DF85BBD30D756E2375F44D").Value;
    }
    internal static bool IsSpc2(byte[] bytes)=>bytes is {Length:>=4}&&BinaryPrimitives.ReadUInt32LittleEndian(bytes)==0x32435053;
    internal static bool Validate(byte[] bytes,out string reason)
    {
        reason="invalid SPC2 chart";if(bytes.Length<48||bytes.Length>64*1024*1024)return false;
        ReadOnlySpan<byte> b=bytes;
        uint U(int at)=>BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(at));
        if(U(4)!=2||U(8)!=48||U(12)!=128||U(16)!=32)return false;
        uint notes=U(20),events=U(24),metadata=U(36);
        if(notes==0||notes>100000||events>100000||metadata<64||metadata%128!=0||48L+128L*notes+32L*events+metadata!=bytes.Length)return false;
        for(int at=28;at<=32;at+=4){float f=BitConverter.Int32BitsToSingle((int)U(at));if(!float.IsFinite(f)||f<=0)return false;}
        for(int i=0;i<notes;i++) {
            int at=48+i*128;
            if(BinaryPrimitives.ReadUInt64LittleEndian(b.Slice(at))!=(ulong)i||BinaryPrimitives.ReadUInt64LittleEndian(b.Slice(at+8))>=notes)return false;
            int side=(int)U(at+16),type=(int)U(at+20),start=(int)U(at+24),end=(int)U(at+28);
            if(side<1||side>4||type is not (1 or 2 or 4 or 5)||start<0||end<start||end>3600000||((type is 4 or 5)&&side!=4))return false;
            for(int k=0;k<4;k++){int p=at+32+k*12;float f=BitConverter.Int32BitsToSingle((int)U(p+8));if((int)U(p+4)<=0||!float.IsFinite(f)||Math.Abs(f)>2147)return false;}
        }
        reason=null;return true;
    }
}
