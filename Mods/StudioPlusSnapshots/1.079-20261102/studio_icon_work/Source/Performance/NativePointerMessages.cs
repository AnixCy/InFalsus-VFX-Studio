using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using BepInEx.Logging;
using UnityEngine;
namespace InFalsusStudio.Performance {
 // Runtime IMGUI performs native Layout dispatch BEFORE the managed OnGUI prefix.
 // Coalesce only this process's absolute mouse-move messages before that work.
 // Raw input, key/button/wheel messages and capture/focus ownership stay native.
 internal static class NativePointerMessages {
  delegate bool WindowVisitor(IntPtr window,IntPtr data);
  delegate IntPtr WindowSubclass(IntPtr window,uint message,IntPtr w,IntPtr l,UIntPtr id,UIntPtr data);
  [DllImport("kernel32.dll")]static extern uint GetCurrentThreadId();
  [DllImport("user32.dll")]static extern bool EnumThreadWindows(uint thread,WindowVisitor callback,IntPtr data);
  [DllImport("user32.dll")]static extern uint GetWindowThreadProcessId(IntPtr window,out uint process);
  [StructLayout(LayoutKind.Sequential)]struct ClientRect {internal int Left,Top,Right,Bottom;}
  [DllImport("user32.dll")]static extern bool GetClientRect(IntPtr window,out ClientRect rect);
  [DllImport("user32.dll",CharSet=CharSet.Unicode)]static extern int GetClassName(IntPtr window,StringBuilder text,int capacity);
  [DllImport("user32.dll")]static extern IntPtr SendMessage(IntPtr window,uint message,IntPtr w,IntPtr l);
  [DllImport("user32.dll",SetLastError=true)]static extern bool PostMessage(IntPtr window,uint message,IntPtr w,IntPtr l);
  [DllImport("comctl32.dll",SetLastError=true)]static extern bool SetWindowSubclass(IntPtr window,WindowSubclass callback,UIntPtr id,UIntPtr data);
  [DllImport("comctl32.dll")]static extern bool RemoveWindowSubclass(IntPtr window,WindowSubclass callback,UIntPtr id);
  [DllImport("comctl32.dll")]static extern IntPtr DefSubclassProc(IntPtr window,uint message,IntPtr w,IntPtr l);
  const uint MouseMove=0x200;
  const long FlushFlag=0x40000000;
  const long BoundaryFlag=0x20000000;
  static readonly UIntPtr HookId=new UIntPtr(0x49465032u);
  static readonly WindowSubclass callback=WindowMessage;
  static IntPtr window,pendingW,pendingL,queuedW,queuedL;static bool pending,queued,unavailable,quittingRegistered,shuttingDown;static uint generation;static float nextAttempt;static string failure;
  static IntPtr latestClientPoint;static bool clientPointValid;static int clientWidth,clientHeight;
  internal static bool Enabled=true;
  internal static bool PassiveMotion;
  internal static long Received,Delivered,FrameFlushes,BoundaryFlushes;
  internal static bool Attached {get{return window!=IntPtr.Zero;}}
  internal static bool HasPending {get{return pending||queued;}}
  static bool Boundary(uint message){return message>=0x201&&message<=0x20e||message>=0x100&&message<=0x109;}
  static void ClearPending(){pending=queued=false;generation++;}
  static IntPtr Drain(IntPtr target,bool boundary){
   if(!pending&&!queued)return IntPtr.Zero;
   if(!boundary){
    if(queued)return IntPtr.Zero;queuedW=pendingW;queuedL=pendingL;pending=false;queued=true;uint ticket=++generation;
    // A posted WM_MOUSEMOVE must re-enter Unity's OS message pump. Calling its
    // window procedure from Update changes input state without creating IMGUI events.
    var packed=new IntPtr(unchecked((long)(((ulong)ticket<<32)|(uint)queuedL.ToInt64())));
    if(!PostMessage(target,MouseMove,new IntPtr(queuedW.ToInt64()|FlushFlag),packed)){queued=false;unavailable=true;failure="Pointer message posting failed; native input restored.";}
    return IntPtr.Zero;
   }
   var w=pending?pendingW:queuedW;var l=pending?pendingL:queuedL;ClearPending();
   return SendMessage(target,MouseMove,new IntPtr(w.ToInt64()|FlushFlag|BoundaryFlag),l);
  }
  static IntPtr WindowMessage(IntPtr target,uint message,IntPtr w,IntPtr l,UIntPtr id,UIntPtr data){
   try{
    if(message==MouseMove&&(w.ToInt64()&FlushFlag)!=0){
     if((w.ToInt64()&BoundaryFlag)!=0){Delivered++;BoundaryFlushes++;return DefSubclassProc(target,message,new IntPtr(w.ToInt64()&~(FlushFlag|BoundaryFlag)),l);}
     uint ticket=(uint)((ulong)l.ToInt64()>>32);if(!queued||ticket!=generation)return IntPtr.Zero;
     var latestW=pending?pendingW:queuedW;var latestL=pending?pendingL:queuedL;pending=queued=false;Delivered++;FrameFlushes++;return DefSubclassProc(target,message,latestW,latestL);
    }
    if(message==0x82){ClearPending();window=IntPtr.Zero;RemoveWindowSubclass(target,callback,HookId);return DefSubclassProc(target,message,w,l);}
    if(!Enabled||unavailable){if(pending)Drain(target,true);return DefSubclassProc(target,message,w,l);}
    if(message==MouseMove){latestClientPoint=l;clientPointValid=true;pendingW=w;pendingL=l;pending=true;Received++;return IntPtr.Zero;}
    if(message>=0x201&&message<=0x209||message>=0x20b&&message<=0x20d){latestClientPoint=l;clientPointValid=true;}
    if(message==0x5){clientWidth=(ushort)(l.ToInt64()&65535);clientHeight=(ushort)((l.ToInt64()>>16)&65535);}
    // Never apply an old client-coordinate sample after focus/capture/size changes.
    if(message==0x8||message==0x3||message==0x5||message==0x215||message==0x2e0||message==0x2a2||message==0x2a3||message==0x1c&&w==IntPtr.Zero||message==0x6&&(w.ToInt64()&0xffff)==0){ClearPending();clientPointValid=false;}
    else if(Boundary(message))Drain(target,true);
   }catch(Exception e){ClearPending();unavailable=true;failure="Pointer message coalescing disabled: "+e.GetType().Name+": "+e.Message;}
   return DefSubclassProc(target,message,w,l);
  }
  static void Attach(){
   uint own=(uint)Process.GetCurrentProcess().Id,thread=GetCurrentThreadId();
   EnumThreadWindows(thread,delegate(IntPtr candidate,IntPtr data){
    uint pid;uint ownerThread=GetWindowThreadProcessId(candidate,out pid);if(pid!=own||ownerThread!=thread)return true;
    var name=new StringBuilder(128);GetClassName(candidate,name,128);if(name.ToString()!="UnityWndClass")return true;
    if(SetWindowSubclass(candidate,callback,HookId,UIntPtr.Zero)){window=candidate;ClearPending();ClientRect rect;if(GetClientRect(candidate,out rect)){clientWidth=rect.Right-rect.Left;clientHeight=rect.Bottom-rect.Top;}return false;}return true;
   },IntPtr.Zero);
  }
  internal static void Tick(ManualLogSource logger){
   if(Application.platform!=RuntimePlatform.WindowsPlayer||shuttingDown)return;
   if(!quittingRegistered){Application.quitting+=Shutdown;quittingRegistered=true;}
   if(failure!=null){if(logger!=null)logger.LogWarning(failure);failure=null;}
   if(unavailable)return;
   try{
    if(window==IntPtr.Zero){if(Time.realtimeSinceStartup<nextAttempt)return;nextAttempt=Time.realtimeSinceStartup+1;Attach();}
    // The queued sample uses the ordinary Windows message pump, not a direct
    // managed OnGUI call. Button/key boundaries flush before forwarding.
    if(window!=IntPtr.Zero&&pending){if(PassiveMotion)ClearPending();else Drain(window,false);}
   }catch(DllNotFoundException){unavailable=true;}catch(EntryPointNotFoundException){unavailable=true;}
  }
  internal static void RefreshGuiPointer(Event e){
   if(!Enabled||unavailable||!clientPointValid||e==null||(e.rawType!=EventType.Repaint&&e.rawType!=EventType.Layout)||clientWidth<=0||clientHeight<=0)return;
   // Menu hover and painting need the latest position, not a separate native input pass.
   // Client/render scaling also covers high-DPI and resized player windows.
   e.mousePosition=new Vector2((short)(latestClientPoint.ToInt64()&65535)*(Screen.width/(float)clientWidth),(short)((latestClientPoint.ToInt64()>>16)&65535)*(Screen.height/(float)clientHeight));
  }
  internal static void Dispose(){
   ClearPending();clientPointValid=false;try{if(window!=IntPtr.Zero)RemoveWindowSubclass(window,callback,HookId);}catch(DllNotFoundException){}catch(EntryPointNotFoundException){}window=IntPtr.Zero;
  }
  static void Shutdown(){shuttingDown=true;Dispose();}
 }
}
