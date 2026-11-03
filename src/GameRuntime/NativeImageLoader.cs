extern alias UnityCore;
using System.Runtime.InteropServices;
using Il2CppInterop.Runtime;
using UnityCore::UnityEngine;

namespace InFalsus.Presentation.Game;

internal static unsafe class NativeImageLoader
{
    // Verified against this target's restored LoadImage wrapper: Texture native ptr,
    // ref ManagedSpanWrapper { void* begin; int length; }, bool. The restored public
    // ReadOnlySpan overload calls an unavailable GetPinnableReference; do not use it.
    [StructLayout(LayoutKind.Sequential)] struct ByteSpan {public IntPtr Begin;public int Length;}
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]delegate byte LoadCall(IntPtr texture,ref ByteSpan bytes,byte markNonReadable);
    static readonly LoadCall Load=IL2CPP.ResolveICall<LoadCall>("UnityEngine.ImageConversion::LoadImage_Injected");
    internal static bool Decode(Texture2D texture,byte[] bytes)
    {
        fixed(byte* pointer=bytes){var span=new ByteSpan{Begin=(IntPtr)pointer,Length=bytes.Length};return Load(texture.m_CachedPtr,ref span,1)!=0;}
    }
}
