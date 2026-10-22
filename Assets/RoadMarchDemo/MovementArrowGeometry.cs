using System;
using System.Collections.Generic;
using UnityEngine;
namespace CampaignDemo {
 // Pure geometry. Polygons are used only for outline clipping, never for a fill mesh.
 public static class MovementArrowGeometry {
  public sealed class Input {public string id,owner;public uint orderRevision;public Vector2? detectionPosition;public Vector2 start,direction;public float radius;public List<Vector2> route;}
  public sealed class Ribbon {public List<Vector2> polygon;public Vector2 tip,wingLeft,wingRight;public int tailEdge;}
  public struct Edge {public Vector2 a,b;public Edge(Vector2 from,Vector2 to){a=from;b=to;}}
  const float Epsilon=.00001f;
  public static float Radius(float baseRadius,int echelon){return Mathf.Max(.001f,baseRadius)*Mathf.Pow(1.2f,Mathf.Clamp(echelon,0,7));}
  public static Vector2 Perpendicular(Vector2 direction){var d=direction.sqrMagnitude>Epsilon?direction.normalized:Vector2.right;return new Vector2(-d.y,d.x);}
  public static List<List<int>> Groups(IList<Input> arrows,float taper=1.15f){var parent=new int[arrows.Count];for(int i=0;i<parent.Length;i++)parent[i]=i;Func<int,int> root=null;root=i=>parent[i]==i?i:parent[i]=root(parent[i]);
   var traces=new DetectionTrace[arrows.Count];for(int i=0;i<arrows.Count;i++)traces[i]=Trace(arrows[i],taper);
   for(int i=0;i<arrows.Count;i++)for(int j=i+1;j<arrows.Count;j++)if(arrows[i].owner==arrows[j].owner&&(Vector2.Distance(arrows[i].start,arrows[j].start)<=arrows[i].radius+arrows[j].radius||HitsCircle(traces[i],arrows[j].start,arrows[j].radius)||HitsCircle(traces[j],arrows[i].start,arrows[i].radius))){int a=root(i),b=root(j);if(a!=b)parent[b]=a;}
   var result=new List<List<int>>();var lookup=new Dictionary<int,List<int>>();for(int i=0;i<arrows.Count;i++){int r=root(i);if(!lookup.TryGetValue(r,out var group)){group=new List<int>();lookup[r]=group;result.Add(group);}group.Add(i);}return result;
  }
  sealed class DetectionTrace {public Vector2[] points;public float[] widths;}
  static DetectionTrace Trace(Input arrow,float taper){var trace=new DetectionTrace{points=new Vector2[33],widths=new float[33]};Vector2 target=arrow.route!=null&&arrow.route.Count>0?arrow.route[arrow.route.Count-1]:arrow.start;float chord=Vector2.Distance(arrow.start,target);Vector2 heading=arrow.direction.sqrMagnitude>Epsilon?arrow.direction.normalized:(target-arrow.start).normalized;Vector2 c1=arrow.start+heading*chord*.45f,c2=target-(target-arrow.start).normalized*chord*.24f;float total=0;var arc=new float[33];for(int i=0;i<=32;i++){float t=i/32f,u=1-t;trace.points[i]=u*u*u*arrow.start+3*u*u*t*c1+3*u*t*t*c2+t*t*t*target;if(i>0)total+=Vector2.Distance(trace.points[i-1],trace.points[i]);arc[i]=total;}for(int i=0;i<=32;i++)trace.widths[i]=arrow.radius*Mathf.Pow(1-arc[i]/Mathf.Max(.001f,total),Mathf.Max(.2f,taper));return trace;}
  static bool HitsCircle(DetectionTrace trace,Vector2 center,float radius){for(int i=1;i<trace.points.Length;i++)if(DistanceToSegment(center,trace.points[i-1],trace.points[i])<=radius+Mathf.Max(trace.widths[i-1],trace.widths[i]))return true;return false;}
  public static void SharedOrigin(IList<Input> arrows,IList<int> group,out Vector2 start,out Vector2 direction,out float radius){start=Vector2.zero;direction=Vector2.zero;float weight=0;foreach(int i in group){float w=arrows[i].radius;start+=arrows[i].start*w;direction+=arrows[i].direction.normalized*w;weight+=w;}start/=Mathf.Max(Epsilon,weight);if(direction.sqrMagnitude<Epsilon)direction=arrows[group[0]].direction;direction=direction.normalized;if(direction.sqrMagnitude<Epsilon)direction=Vector2.right;
   var normal=Perpendicular(direction);float low=float.MaxValue,high=float.MinValue;foreach(int i in group){var a=arrows[i];var n=Perpendicular(a.direction);foreach(float sign in new[]{-1f,1f}){float value=Vector2.Dot(a.start+n*a.radius*sign-start,normal);low=Mathf.Min(low,value);high=Mathf.Max(high,value);}}
   start+=normal*(low+high)*.5f;radius=Mathf.Max(.001f,(high-low)*.5f);
  }
  static float Length(IList<Vector2> points){float result=0;for(int i=1;i<points.Count;i++)result+=Vector2.Distance(points[i-1],points[i]);return result;}
  static Vector2 At(IList<Vector2> points,float distance){for(int i=1;i<points.Count;i++){float d=Vector2.Distance(points[i-1],points[i]);if(distance<=d)return Vector2.Lerp(points[i-1],points[i],d>0?distance/d:0);distance-=d;}return points[points.Count-1];}
  public static Ribbon Build(Input input,Vector2 sharedStart,Vector2 sharedDirection,float sharedRadius,bool merged,float taper=1.15f,float headSize=.2f,int samples=48){
   if(input.route==null||input.route.Count<2)return null;var route=new List<Vector2>();route.Add(input.start);foreach(var p in input.route)if((p-route[route.Count-1]).sqrMagnitude>Epsilon*Epsilon)route.Add(p);if(route.Count<2)return null;
   float originalLength=Length(route);if(originalLength<.001f)return null;int count=Mathf.Clamp(samples,16,96);var centers=new List<Vector2>();
   // The visual is defined by start, destination and the accepted order's initial direction.
   // Use a cubic spine rather than copying road corners into the arrow envelope.
   Vector2 source=merged?sharedStart:input.start,target=route[route.Count-1];
   Vector2 heading=merged?sharedDirection:input.direction;float chord=Vector2.Distance(source,target);
   if(chord<.001f)return null;if(heading.sqrMagnitude<Epsilon)heading=(target-source).normalized;
   Vector2 c1=source+heading.normalized*chord*.45f,c2=target-(target-source).normalized*chord*.24f;
   for(int i=0;i<=count;i++){float t=i/(float)count,u=1-t;centers.Add(u*u*u*source+3*u*u*t*c1+3*u*t*t*c2+t*t*t*target);}
   float total=Length(centers),headLength=Mathf.Min(Mathf.Max(.03f,headSize),total*.28f),bodyLength=total-headLength;var tip=route[route.Count-1];Vector2 finalDirection=(tip-At(centers,bodyLength)).normalized;if(finalDirection.sqrMagnitude<Epsilon)finalDirection=input.direction.normalized;var finalNormal=Perpendicular(finalDirection);float rootRadius=merged?sharedRadius:input.radius;
   var left=new List<Vector2>();var right=new List<Vector2>();var firstNormal=Perpendicular(merged?sharedDirection:input.direction);for(int i=0;i<=count;i++){float distance=bodyLength*i/count,t=distance/Mathf.Max(.001f,total);Vector2 center=At(centers,distance),before=At(centers,Mathf.Max(0,distance-.035f)),after=At(centers,Mathf.Min(total,distance+.035f));var normal=i==0?firstNormal:Perpendicular(after-before);float width=rootRadius*Mathf.Pow(1-t,Mathf.Max(.2f,taper));left.Add(center+normal*width);right.Add(center-normal*width);}
   var basePoint=At(centers,bodyLength);float neck=rootRadius*Mathf.Pow(headLength/total,Mathf.Max(.2f,taper)),wing=Mathf.Max(neck*1.6f,Mathf.Min(headLength*.7f,rootRadius*.65f));var ribbon=new Ribbon{polygon=new List<Vector2>(),tip=tip,wingLeft=basePoint+finalNormal*wing,wingRight=basePoint-finalNormal*wing};ribbon.polygon.AddRange(left);ribbon.polygon.Add(ribbon.wingLeft);ribbon.polygon.Add(tip);ribbon.polygon.Add(ribbon.wingRight);for(int i=right.Count-1;i>=0;i--)ribbon.polygon.Add(right[i]);ribbon.tailEdge=ribbon.polygon.Count-1;return ribbon;
  }
  static float Cross(Vector2 a,Vector2 b){return a.x*b.y-a.y*b.x;}
  public static float DistanceToSegment(Vector2 p,Vector2 a,Vector2 b){var d=b-a;float t=d.sqrMagnitude>Epsilon*Epsilon?Mathf.Clamp01(Vector2.Dot(p-a,d)/d.sqrMagnitude):0;return Vector2.Distance(p,a+d*t);}
  // 0 outside, 1 strict interior, 2 boundary. Shared edges keep a single owner.
  static int Classify(Vector2 p,List<Vector2> polygon){bool inside=false;for(int i=0,j=polygon.Count-1;i<polygon.Count;j=i++){var a=polygon[j];var b=polygon[i];if(DistanceToSegment(p,a,b)<Epsilon*3)return 2;if((a.y>p.y)!=(b.y>p.y)&&p.x<(b.x-a.x)*(p.y-a.y)/(b.y-a.y)+a.x)inside=!inside;}return inside?1:0;}
  static void Cuts(Vector2 a,Vector2 b,Vector2 c,Vector2 d,List<float> cuts){var r=b-a;var s=d-c;float cross=Cross(r,s);if(Mathf.Abs(cross)>Epsilon*Epsilon){float t=Cross(c-a,s)/cross,u=Cross(c-a,r)/cross;if(t>Epsilon&&t<1-Epsilon&&u>=-Epsilon&&u<=1+Epsilon)cuts.Add(t);}else if(Mathf.Abs(Cross(c-a,r))<Epsilon&&r.sqrMagnitude>Epsilon*Epsilon){float t=Vector2.Dot(c-a,r)/r.sqrMagnitude,u=Vector2.Dot(d-a,r)/r.sqrMagnitude;if(t>Epsilon&&t<1-Epsilon)cuts.Add(t);if(u>Epsilon&&u<1-Epsilon)cuts.Add(u);}}
  public static List<Edge> UnionOutline(IList<Ribbon> ribbons){var output=new List<Edge>();var cuts=new List<float>();for(int p=0;p<ribbons.Count;p++){var ribbon=ribbons[p];var polygon=ribbon.polygon;for(int e=0;e<polygon.Count;e++){if(e==ribbon.tailEdge)continue;Vector2 a=polygon[e],b=polygon[(e+1)%polygon.Count];if((a-b).sqrMagnitude<Epsilon*Epsilon)continue;cuts.Clear();cuts.Add(0);cuts.Add(1);
    for(int q=0;q<ribbons.Count;q++)if(q!=p){var other=ribbons[q].polygon;for(int f=0;f<other.Count;f++)Cuts(a,b,other[f],other[(f+1)%other.Count],cuts);}cuts.Sort();
    for(int i=1;i<cuts.Count;i++){if(cuts[i]-cuts[i-1]<Epsilon)continue;Vector2 from=Vector2.Lerp(a,b,cuts[i-1]),to=Vector2.Lerp(a,b,cuts[i]),mid=(from+to)*.5f;bool covered=false;for(int q=0;q<ribbons.Count;q++)if(q!=p){int classification=Classify(mid,ribbons[q].polygon);if(classification==1||(classification==2&&q<p)){covered=true;break;}}if(!covered)output.Add(new Edge(from,to));}
   }}return output;
  }
  // Stitch exposed edges into open polylines. No operation closes the tail.
  public static List<List<Vector2>> Chains(IList<Edge> edges){var result=new List<List<Vector2>>();var used=new bool[edges.Count];var lookup=new Dictionary<Vector2Int,List<int>>();Func<Vector2,Vector2Int> key=p=>new Vector2Int(Mathf.RoundToInt(p.x*10000),Mathf.RoundToInt(p.y*10000));for(int i=0;i<edges.Count;i++)foreach(var p in new[]{edges[i].a,edges[i].b}){var k=key(p);if(!lookup.TryGetValue(k,out var list)){list=new List<int>();lookup[k]=list;}list.Add(i);}
   for(int i=0;i<edges.Count;i++){if(used[i])continue;used[i]=true;var chain=new List<Vector2>{edges[i].a,edges[i].b};for(int pass=0;pass<2;pass++){if(pass==1)chain.Reverse();for(int n=0;n<edges.Count;n++){var tail=chain[chain.Count-1];if(!lookup.TryGetValue(key(tail),out var candidates))break;int next=-1;foreach(int candidate in candidates)if(!used[candidate]){next=candidate;break;}if(next<0)break;used[next]=true;var edge=edges[next];chain.Add((edge.a-tail).sqrMagnitude<(edge.b-tail).sqrMagnitude?edge.b:edge.a);}}result.Add(chain);}return result;
  }
 }
}


