using System.Collections.Generic;
using InFalsusStudio.NativeVisual;
using UnityEngine;
namespace InFalsusStudio.Performance {
 internal static class PreviewMeshUpload {
  internal static bool Enabled=true;
  internal static bool Copy(NoteRenderer renderer,Range range,Slot slot){
   if(!Enabled||!PreviewFrustum.Playing)return false;
   var batch=(MeshBatch)range.Batch;int count=batch.Vertices.Count-range.V,indices=batch.Triangles.Count-range.I;
   // Sky source is planar triangle soup. Other topology retains the general path.
   if(count==0||indices!=count||count%3!=0)return false;
   for(int i=0;i<count;i++)if(batch.Triangles[range.I+i]-range.V!=i)return false;
   if(slot.PreviewVertices==null){slot.PreviewVertices=new List<Vector3>();slot.PreviewUvs=new List<Vector2>();slot.PreviewNormals=new List<Vector3>();slot.PreviewIndices=new List<int>();slot.PreviewColors=new List<Color>();}
   var v=slot.PreviewVertices;var uv=slot.PreviewUvs;var normals=slot.PreviewNormals;var triangles=slot.PreviewIndices;var colors=slot.PreviewColors;
   bool topology=slot.Mesh.vertexCount!=count||triangles.Count!=count;
   v.Clear();uv.Clear();normals.Clear();
   Vector3 min=batch.Vertices[range.V],max=min;
   for(int i=0;i<count;i++){var point=batch.Vertices[range.V+i];v.Add(point);uv.Add(batch.Uvs[range.V+i]);min=Vector3.Min(min,point);max=Vector3.Max(max,point);}
   for(int i=0;i<count;i+=3){var normal=Vector3.Cross(v[i+1]-v[i],v[i+2]-v[i]).normalized;normals.Add(normal);normals.Add(normal);normals.Add(normal);}
   if(topology){slot.Mesh.Clear();triangles.Clear();colors.Clear();for(int i=0;i<count;i++){triangles.Add(i);colors.Add(Color.white);}}
   slot.Mesh.SetVertices(v);slot.Mesh.SetUVs(0,uv);slot.Mesh.SetNormals(normals);
   if(topology){slot.Mesh.SetColors(colors);slot.Mesh.SetTriangles(triangles,0,false);}
   slot.Mesh.bounds=new Bounds((min+max)*.5f,max-min);slot.UsingPreviewCopy=true;slot.ReusedSource=false;
   return true;
  }
 }
}
