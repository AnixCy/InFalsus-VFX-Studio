using System;
using System.Collections.Generic;
using UnityEngine;
namespace InFalsusStudio.Performance {
 internal static class PreviewMeshClip {
  internal static bool Enabled=true;internal static long DirectTriangles;
  struct Vertex {internal Vector3 P;internal Vector2 U;internal Vertex(Vector3 p,Vector2 u){P=p;U=u;}}
  sealed class Buffers {internal readonly List<Vector3> Vertices=new List<Vector3>();internal readonly List<Vector2> Uvs=new List<Vector2>();internal readonly List<int> Indices=new List<int>();internal readonly List<Vertex> A=new List<Vertex>(5),B=new List<Vertex>(5);internal bool Busy;}
  [ThreadStatic]static Buffers buffers;
  static void Clip(List<Vertex> input,List<Vertex> output,float z,bool lower){output.Clear();if(input.Count==0)return;var a=input[input.Count-1];bool ai=lower?a.P.z>=z:a.P.z<=z;foreach(var b in input){bool bi=lower?b.P.z>=z:b.P.z<=z;if(ai!=bi){float f=(z-a.P.z)/(b.P.z-a.P.z);output.Add(new Vertex(Vector3.LerpUnclamped(a.P,b.P,f),Vector2.LerpUnclamped(a.U,b.U,f)));}if(bi)output.Add(b);a=b;ai=bi;}}
  internal static bool Try(Mesh mesh,Vector3[] vertices,Vector2[] uv,int[] indices,float near,float far){
   if(!Enabled)return false;if(buffers==null)buffers=new Buffers();var b=buffers;if(b.Busy)return false;b.Busy=true;
   try{var v=b.Vertices;var u=b.Uvs;var t=b.Indices;v.Clear();u.Clear();t.Clear();
    for(int i=0;i<indices.Length;i+=3){int ia=indices[i],ib=indices[i+1],ic=indices[i+2];float za=vertices[ia].z,zb=vertices[ib].z,zc=vertices[ic].z;
     if(za>=near&&za<=far&&zb>=near&&zb<=far&&zc>=near&&zc<=far){int start=v.Count;v.Add(vertices[ia]);v.Add(vertices[ib]);v.Add(vertices[ic]);u.Add(uv[ia]);u.Add(uv[ib]);u.Add(uv[ic]);t.Add(start);t.Add(start+1);t.Add(start+2);DirectTriangles++;continue;}
     if(za<near&&zb<near&&zc<near||za>far&&zb>far&&zc>far)continue;
     b.A.Clear();b.A.Add(new Vertex(vertices[ia],uv[ia]));b.A.Add(new Vertex(vertices[ib],uv[ib]));b.A.Add(new Vertex(vertices[ic],uv[ic]));Clip(b.A,b.B,near,true);Clip(b.B,b.A,far,false);if(b.A.Count<3)continue;int first=v.Count;
     foreach(var point in b.A){v.Add(point.P);u.Add(point.U);}for(int j=1;j<b.A.Count-1;j++){t.Add(first);t.Add(first+j);t.Add(first+j+1);}
    }
    mesh.Clear();mesh.SetVertices(v);mesh.SetUVs(0,u);mesh.SetTriangles(t,0,false);mesh.RecalculateNormals();mesh.RecalculateBounds();return true;
   }finally{b.Busy=false;}
  }
 }
}
