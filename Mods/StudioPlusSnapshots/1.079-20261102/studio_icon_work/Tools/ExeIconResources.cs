using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
public static class ExeIconResources {
 public sealed class Entry {public string Type,Name;public ushort Language;public byte[] Data;public string Key{get{return Type+"/"+Name+"/"+Language;}}public string Hash{get{return Hex(Data);}}}
 public sealed class Section {public string Name,Hash;public int Size;}
 delegate bool Types(IntPtr module,IntPtr type,IntPtr param);
 delegate bool Names(IntPtr module,IntPtr type,IntPtr name,IntPtr param);
 delegate bool Languages(IntPtr module,IntPtr type,IntPtr name,ushort language,IntPtr param);
 [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)]static extern IntPtr LoadLibraryEx(string file,IntPtr reserved,uint flags);
 [DllImport("kernel32.dll")]static extern bool FreeLibrary(IntPtr module);
 [DllImport("kernel32.dll",EntryPoint="EnumResourceTypesW",SetLastError=true)]static extern bool EnumTypes(IntPtr module,Types callback,IntPtr param);
 [DllImport("kernel32.dll",EntryPoint="EnumResourceNamesW",SetLastError=true)]static extern bool EnumNames(IntPtr module,IntPtr type,Names callback,IntPtr param);
 [DllImport("kernel32.dll",EntryPoint="EnumResourceLanguagesW",SetLastError=true)]static extern bool EnumLanguages(IntPtr module,IntPtr type,IntPtr name,Languages callback,IntPtr param);
 [DllImport("kernel32.dll",EntryPoint="FindResourceExW",SetLastError=true)]static extern IntPtr Find(IntPtr module,IntPtr type,IntPtr name,ushort language);
 [DllImport("kernel32.dll",SetLastError=true)]static extern IntPtr LoadResource(IntPtr module,IntPtr resource);
 [DllImport("kernel32.dll",SetLastError=true)]static extern uint SizeofResource(IntPtr module,IntPtr resource);
 [DllImport("kernel32.dll")]static extern IntPtr LockResource(IntPtr loaded);
 [DllImport("kernel32.dll",EntryPoint="BeginUpdateResourceW",CharSet=CharSet.Unicode,SetLastError=true)]static extern IntPtr Begin(string file,bool deleteExisting);
 [DllImport("kernel32.dll",EntryPoint="UpdateResourceW",SetLastError=true)]static extern bool Update(IntPtr handle,IntPtr type,IntPtr name,ushort language,byte[] data,uint size);
 [DllImport("kernel32.dll",EntryPoint="EndUpdateResourceW",SetLastError=true)]static extern bool End(IntPtr handle,bool discard);
 static string Id(IntPtr value){return (ulong)value.ToInt64()<=65535?"#"+value.ToInt64():Marshal.PtrToStringUni(value);}
 static IntPtr Id(string value,out bool allocated){allocated=!value.StartsWith("#",StringComparison.Ordinal);return allocated?Marshal.StringToHGlobalUni(value):new IntPtr(int.Parse(value.Substring(1)));}
 static string Hex(byte[] bytes){using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-","");}
 public static Entry[] Read(string file){
  IntPtr module=LoadLibraryEx(file,IntPtr.Zero,0x22);if(module==IntPtr.Zero)throw new Win32Exception();
  var result=new List<Entry>();Exception failure=null;
  try{
   Types types=(m,t,p)=>{
    Names names=(mm,tt,n,pp)=>{
     Languages languages=(mmm,ttt,nn,language,ppp)=>{
      try{IntPtr resource=Find(module,ttt,nn,language);if(resource==IntPtr.Zero)throw new Win32Exception();uint size=SizeofResource(module,resource);IntPtr data=LockResource(LoadResource(module,resource));if(data==IntPtr.Zero&&size>0)throw new Win32Exception();var bytes=new byte[size];if(size>0)Marshal.Copy(data,bytes,0,(int)size);result.Add(new Entry{Type=Id(ttt),Name=Id(nn),Language=language,Data=bytes});return true;}catch(Exception e){failure=e;return false;}
     };bool ok=EnumLanguages(module,tt,n,languages,IntPtr.Zero);GC.KeepAlive(languages);if(!ok&&failure==null)failure=new Win32Exception();return ok;
    };bool success=EnumNames(module,t,names,IntPtr.Zero);GC.KeepAlive(names);if(!success&&failure==null)failure=new Win32Exception();return success;
   };bool done=EnumTypes(module,types,IntPtr.Zero);GC.KeepAlive(types);if(!done&&failure==null)failure=new Win32Exception();if(failure!=null)throw failure;return result.ToArray();
  }finally{FreeLibrary(module);}
 }
 static void Write(IntPtr handle,string type,string name,ushort language,byte[] bytes){
  bool a,b;IntPtr t=Id(type,out a),n=Id(name,out b);try{if(!Update(handle,t,n,language,bytes,bytes==null?0:(uint)bytes.Length))throw new Win32Exception();}finally{if(a)Marshal.FreeHGlobal(t);if(b)Marshal.FreeHGlobal(n);}
 }
 public static void Replace(string exe,string ico){
  var entries=Read(exe);var groups=entries.Where(e=>e.Type=="#14").ToArray();if(groups.Length==0)groups=new[]{new Entry{Type="#14",Name="#1",Language=0}};
  byte[] source=File.ReadAllBytes(ico);var images=new List<byte[]>();byte[] group;
  int first=entries.Where(e=>e.Type=="#3"&&e.Name.StartsWith("#",StringComparison.Ordinal)).Select(e=>int.Parse(e.Name.Substring(1))).DefaultIfEmpty(0).Max()+1;
  using(var input=new BinaryReader(new MemoryStream(source)))using(var buffer=new MemoryStream())using(var output=new BinaryWriter(buffer)){
   if(input.ReadUInt16()!=0||input.ReadUInt16()!=1)throw new InvalidDataException("Not an ICO");int count=input.ReadUInt16();if(count<1||count>256||first+count>65535)throw new InvalidDataException("Invalid icon count");
   output.Write((ushort)0);output.Write((ushort)1);output.Write((ushort)count);
   for(int i=0;i<count;i++){
    byte[] format=input.ReadBytes(8);if(format.Length!=8)throw new InvalidDataException();uint size=input.ReadUInt32(),offset=input.ReadUInt32();if((ulong)offset+size>(ulong)source.Length)throw new InvalidDataException("Icon exceeds file");var image=new byte[size];Array.Copy(source,offset,image,0,size);images.Add(image);output.Write(format);output.Write(size);output.Write((ushort)(first+i));
   }group=buffer.ToArray();
  }
  IntPtr update=Begin(exe,false);if(update==IntPtr.Zero)throw new Win32Exception();
  try{
   foreach(var old in entries.Where(e=>e.Type=="#3"))Write(update,old.Type,old.Name,old.Language,null);
   foreach(ushort language in groups.Select(e=>e.Language).Distinct())for(int i=0;i<images.Count;i++)Write(update,"#3","#"+(first+i),language,images[i]);
   foreach(var item in groups)Write(update,item.Type,item.Name,item.Language,group);
   if(!End(update,false))throw new Win32Exception();update=IntPtr.Zero;
  }finally{if(update!=IntPtr.Zero)End(update,true);}
  var after=Read(exe);var unchanged=entries.Where(e=>e.Type!="#3"&&e.Type!="#14").ToDictionary(e=>e.Key,e=>e.Hash);
  var check=after.Where(e=>e.Type!="#3"&&e.Type!="#14").ToArray();if(check.Length!=unchanged.Count||check.Any(e=>!unchanged.ContainsKey(e.Key)||unchanged[e.Key]!=e.Hash))throw new InvalidDataException("Non-icon resources changed");
  foreach(var item in groups){if(!after.Single(e=>e.Key==item.Key).Data.SequenceEqual(group))throw new InvalidDataException("Group icon mismatch");}
  foreach(ushort language in groups.Select(e=>e.Language).Distinct())for(int i=0;i<images.Count;i++)if(!after.Single(e=>e.Type=="#3"&&e.Name=="#"+(first+i)&&e.Language==language).Data.SequenceEqual(images[i]))throw new InvalidDataException("Icon image mismatch");
 }
 public static Section[] Sections(string exe){
  byte[] data=File.ReadAllBytes(exe);using(var reader=new BinaryReader(new MemoryStream(data))){reader.BaseStream.Position=0x3c;int pe=reader.ReadInt32();reader.BaseStream.Position=pe+6;int count=reader.ReadUInt16();reader.BaseStream.Position=pe+20;int optional=reader.ReadUInt16();var result=new List<Section>();
   for(int i=0;i<count;i++){reader.BaseStream.Position=pe+24+optional+40*i;string name=Encoding.ASCII.GetString(reader.ReadBytes(8)).TrimEnd('\0');reader.BaseStream.Position+=8;int size=reader.ReadInt32(),offset=reader.ReadInt32();var bytes=new byte[size];Array.Copy(data,offset,bytes,0,size);result.Add(new Section{Name=name,Size=size,Hash=Hex(bytes)});}return result.ToArray();
  }
 }
}
