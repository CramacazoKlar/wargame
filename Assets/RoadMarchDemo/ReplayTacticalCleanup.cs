using System;
using System.Collections.Generic;
using UnityEngine;
namespace CampaignDemo {
 // Display-only loop erasure. Each unit has its own index; raw movement is never edited.
 public static class ReplayTacticalCleanup {
  const double Epsilon=0.000001;
  sealed class Edge {public Vector3 a,b;public int index;public bool alive=true;}
  sealed class Index {
   readonly double cell;readonly Dictionary<long,List<Edge>> buckets=new Dictionary<long,List<Edge>>();
   public readonly List<Edge> edges=new List<Edge>();
   public Index(IList<Vector3> points){float minX=points[0].x,maxX=minX,minY=points[0].y,maxY=minY;foreach(var p in points){minX=Math.Min(minX,p.x);maxX=Math.Max(maxX,p.x);minY=Math.Min(minY,p.y);maxY=Math.Max(maxY,p.y);}cell=Math.Max(.05,Math.Max(maxX-minX,maxY-minY)/64);}
   IEnumerable<long> Cells(Vector3 a,Vector3 b){int x0=(int)Math.Floor((Math.Min(a.x,b.x)-Epsilon)/cell),x1=(int)Math.Floor((Math.Max(a.x,b.x)+Epsilon)/cell),y0=(int)Math.Floor((Math.Min(a.y,b.y)-Epsilon)/cell),y1=(int)Math.Floor((Math.Max(a.y,b.y)+Epsilon)/cell);for(int y=y0;y<=y1;y++)for(int x=x0;x<=x1;x++)yield return ((long)x<<32)^(uint)y;}
   public void Add(Vector3 a,Vector3 b){var edge=new Edge{a=a,b=b,index=edges.Count};edges.Add(edge);foreach(long key in Cells(a,b)){if(!buckets.TryGetValue(key,out var list)){list=new List<Edge>();buckets.Add(key,list);}list.Add(edge);}}
   public void Trim(int keep){for(int i=edges.Count-1;i>=keep;i--){edges[i].alive=false;edges.RemoveAt(i);}}
   public IEnumerable<Edge> Query(Vector3 a,Vector3 b){var seen=new HashSet<Edge>();foreach(long key in Cells(a,b))if(buckets.TryGetValue(key,out var list))foreach(var edge in list)if(edge.alive&&seen.Add(edge))yield return edge;}
  }
  public static void Build(ReplaySnapshot snapshot,float width){
   foreach(var army in snapshot.armies){var source=new List<List<Vector3>>();
    foreach(var route in army.tacticalRoutes){if(route.Count==0)continue;if(source.Count>0&&Same(source[source.Count-1][source[source.Count-1].Count-1],route[0]))source[source.Count-1].AddRange(route);else source.Add(new List<Vector3>(route));}
    army.tacticalRoutes.Clear();foreach(var route in source){var clean=EraseLoops(route);if(clean.Count>1)army.tacticalRoutes.Add(clean);}
   }
  }
  public static List<Vector3> EraseLoops(IList<Vector3> source){
   var input=new List<Vector3>();foreach(var p in source)Append(input,p);var path=new List<Vector3>();if(input.Count==0)return path;
   var index=new Index(input);path.Add(input[0]);
   for(int inputIndex=1;inputIndex<input.Count;inputIndex++){
    var target=input[inputIndex];
    while(!Same(path[path.Count-1],target)){
     int count=path.Count;var from=path[count-1];
     // Retracing the adjacent segment shortens it rather than keeping two overlapping strokes.
     if(count>1){var previous=path[count-2];var direction=from-previous;double length=direction.sqrMagnitude;double cross=Cross(direction,target-from);double dot=Vector3.Dot(direction,target-from);
      if(length>Epsilon*Epsilon&&Math.Abs(cross)<=Epsilon*Math.Sqrt(length)&&dot<0){double t=Vector3.Dot(target-previous,direction)/length;
       index.Trim(count-2);path.RemoveAt(count-1);
       if(t>0){path.Add(target);index.Add(previous,target);break;}continue;
      }
     }
     Edge hit=null;double first=double.MaxValue;
     foreach(var edge in index.Query(from,target)){if(edge.index>=count-2)continue;if(!Intersection(from,target,edge.a,edge.b,out double t))continue;
      if(t<first-Epsilon||(Math.Abs(t-first)<=Epsilon&&(hit==null||edge.index<hit.index))){first=t;hit=edge;}
     }
     if(hit==null){path.Add(target);index.Add(from,target);break;}
     var crossing=Vector3.Lerp(from,target,(float)first);int keep=hit.index+1;
     index.Trim(hit.index);path.RemoveRange(keep,path.Count-keep);
     if(!Same(path[path.Count-1],crossing)){var start=path[path.Count-1];path.Add(crossing);index.Add(start,crossing);}
     // Continue the unconsumed segment; it may cross an earlier part again.
    }
   }return path;
  }
  static bool Intersection(Vector3 a,Vector3 b,Vector3 c,Vector3 d,out double t){
   var u=b-a;var v=d-c;var w=c-a;double denominator=Cross(u,v);t=0;
   if(Math.Abs(denominator)>Epsilon*Math.Sqrt((double)u.sqrMagnitude*v.sqrMagnitude)){double along=Cross(w,v)/denominator,other=Cross(w,u)/denominator;if(along<=Epsilon||along>1+Epsilon||other<-Epsilon||other>1+Epsilon)return false;t=Math.Max(0,Math.Min(1,along));return true;}
   double length=u.sqrMagnitude;if(length<=Epsilon*Epsilon||Math.Abs(Cross(w,u))>Epsilon*Math.Sqrt(length))return false;
   double s=Vector3.Dot(c-a,u)/length,e=Vector3.Dot(d-a,u)/length;double start=Math.Max(0,Math.Min(s,e)),end=Math.Min(1,Math.Max(s,e));if(end<start-Epsilon||end<=Epsilon)return false;t=start>Epsilon?start:end;return true;
  }
  static double Cross(Vector3 a,Vector3 b){return (double)a.x*b.y-(double)a.y*b.x;}
  static bool Same(Vector3 a,Vector3 b){return (a-b).sqrMagnitude<Epsilon*Epsilon;}
  static void Append(List<Vector3> points,Vector3 p){if(points.Count>0&&Same(points[points.Count-1],p))return;if(points.Count>1){var a=points[points.Count-2];var b=points[points.Count-1];var d=p-a;double length=d.sqrMagnitude;if(length>Epsilon*Epsilon){double t=Vector3.Dot(b-a,d)/length;if(t>=0&&t<=1&&Math.Abs(Cross(d,b-a))<=Epsilon*Math.Sqrt(length)){points[points.Count-1]=p;return;}}}points.Add(p);}
  public static void Polish(ReplaySnapshot snapshot,float width){foreach(var army in snapshot.armies)foreach(var route in army.tacticalRoutes)PolishRoute(route,width);}
  public static void PolishRoute(List<Vector3> route,float width){var smooth=ReplayDrawing.Round(route,Mathf.Max(.005f,width*.4f));route.Clear();route.AddRange(smooth);}
  public static List<Vector3> StraightSections(IList<Vector3> route){return new List<Vector3>(route);}
  public static bool DrawHead(ReplayArmy army,List<Vector3> route){for(int i=army.tacticalRoutes.Count-1;i>=0;i--)if(army.tacticalRoutes[i].Count>1)return ReferenceEquals(army.tacticalRoutes[i],route)&&Same(route[route.Count-1],army.finalPosition);return false;}
 }
}
