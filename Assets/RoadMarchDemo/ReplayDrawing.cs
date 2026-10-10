using System.Collections.Generic;
using UnityEngine;
namespace CampaignDemo {
 public static class ReplayDrawing {
  public static Color Blue=new Color(20/255f,56/255f,209/255f);public static Color Red=new Color(209/255f,20/255f,56/255f);
  public static Color Side(string owner,string blue){return owner==blue?Blue:Red;}
  public static void LayoutEnds(ReplaySnapshot snapshot){foreach(var army in snapshot.armies)army.displayPosition=army.finalPosition;}
  // Round corners locally without a Catmull-Rom overshoot into an untravelled region.
  public static List<Vector3> Round(IList<Vector3> path,float radius=.04f){var output=new List<Vector3>();if(path.Count==0)return output;output.Add(path[0]);for(int i=1;i<path.Count-1;i++){var b=path[i];float length=Mathf.Min(radius,Mathf.Min(Vector3.Distance(path[i-1],b),Vector3.Distance(b,path[i+1]))*.25f);var a=Vector3.MoveTowards(b,path[i-1],length);var c=Vector3.MoveTowards(b,path[i+1],length);output.Add(a);for(int j=1;j<=5;j++){float t=j/5f;output.Add((1-t)*(1-t)*a+2*(1-t)*t*b+t*t*c);}}if(path.Count>1)output.Add(path[path.Count-1]);return output;}
  public static Mesh Strip(IList<Vector3> path,float width,bool arrow){var vertices=new List<Vector3>();var triangles=new List<int>();if(path.Count<2)return new Mesh();for(int i=0;i<path.Count;i++){var direction=i==path.Count-1?path[i]-path[i-1]:path[i+1]-path[i];direction.z=0;var normal=new Vector3(-direction.y,direction.x,0).normalized*width*.5f;vertices.Add(path[i]+normal);vertices.Add(path[i]-normal);if(i>0){int n=i*2;triangles.Add(n-2);triangles.Add(n-1);triangles.Add(n);triangles.Add(n);triangles.Add(n-1);triangles.Add(n+1);}}
   if(arrow){var tip=path[path.Count-1];var forward=(tip-path[path.Count-2]).normalized;var normal=new Vector3(-forward.y,forward.x,0);int n=vertices.Count;vertices.Add(tip);vertices.Add(tip-forward*width*4+normal*width*2);vertices.Add(tip-forward*width*4-normal*width*2);triangles.Add(n);triangles.Add(n+1);triangles.Add(n+2);}
   var mesh=new Mesh();if(vertices.Count>65535)mesh.indexFormat=UnityEngine.Rendering.IndexFormat.UInt32;mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);mesh.RecalculateBounds();return mesh;
  }
 }
}
