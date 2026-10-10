using System;
using System.Collections.Generic;
using UnityEngine;

namespace CampaignDemo {
 // Display-only deduplication. Full movement records and death markers stay intact.
 public static class ReplayTacticalCleanup {
  struct Span {public Vector3 a,b;public Span(Vector3 start,Vector3 end){a=start;b=end;}}
  public static void Build(ReplaySnapshot snapshot,float width) {
   float tolerance=Mathf.Max(.01f,width*.85f),step=Mathf.Max(.02f,width*.5f),cell=tolerance*4;
   var occupied=new Dictionary<long,List<Span>>();
   for(int armyIndex=snapshot.armies.Count-1;armyIndex>=0;armyIndex--) {
    var army=snapshot.armies[armyIndex];var output=new List<List<Vector3>>();
    for(int routeIndex=army.tacticalRoutes.Count-1;routeIndex>=0;routeIndex--) {
     var source=army.tacticalRoutes[routeIndex];var samples=new List<Vector3>();
     if(source.Count==0)continue;samples.Add(source[0]);
     for(int i=1;i<source.Count;i++){float length=Vector3.Distance(source[i-1],source[i]);if(length<.00001f)continue;int count=Mathf.Max(1,Mathf.CeilToInt(length/step));for(int j=1;j<=count;j++)samples.Add(Vector3.Lerp(source[i-1],source[i],(float)j/count));}
     var keep=new bool[Mathf.Max(0,samples.Count-1)];
     // Later traversals win within each unit, including a return along the same road.
     for(int i=keep.Length-1;i>=0;i--){var span=new Span(samples[i],samples[i+1]);if(Overlaps(span,occupied,tolerance,cell))continue;keep[i]=true;Insert(span,occupied,tolerance,cell);}
     List<Vector3> run=null;
     for(int i=0;i<keep.Length;i++){if(!keep[i]){run=null;continue;}if(run==null){run=new List<Vector3>{samples[i]};output.Add(run);}Append(run,samples[i+1]);}
    }
    army.tacticalRoutes.Clear();army.tacticalRoutes.AddRange(output);
   }
  }
  // Each section keeps its starting heading as zero; small turns do not reset it.
  public static void Polish(ReplaySnapshot snapshot,float width){foreach(var army in snapshot.armies)foreach(var route in army.tacticalRoutes)PolishRoute(route,width);}
  public static void PolishRoute(List<Vector3> route,float width){
   if(route.Count<3)return;
   // Heading sections classify bends only; render the original curved route.
   if(StraightSections(route).Count<3)return;
   if(route.Count<3)return;
   float length=0;for(int i=1;i<route.Count;i++)length+=Vector3.Distance(route[i-1],route[i]);
   float remaining=length*.25f;int first=0;
   while(first<route.Count-1){float leg=Vector3.Distance(route[first],route[first+1]);if(remaining<leg){route[first]=Vector3.MoveTowards(route[first],route[first+1],remaining);break;}remaining-=leg;first++;}
   if(first>0)route.RemoveRange(0,first);
  }
  public static List<Vector3> StraightSections(IList<Vector3> route){
   var result=new List<Vector3>();if(route.Count==0)return result;result.Add(route[0]);
   Vector3 baseline=Vector3.zero;bool haveBaseline=false;
   for(int i=1;i<route.Count;i++){var direction=route[i]-route[i-1];direction.z=0;if(direction.sqrMagnitude<.00000001f)continue;
    if(!haveBaseline){baseline=direction.normalized;haveBaseline=true;continue;}
    // Inclusive 30-degree threshold; retain the initial heading until a real split.
    if(Vector3.Angle(baseline,direction)>30.0001f){var corner=route[i-1];if((result[result.Count-1]-corner).sqrMagnitude>.00000001f)result.Add(corner);baseline=direction.normalized;}
   }
   var end=route[route.Count-1];if((result[result.Count-1]-end).sqrMagnitude>.00000001f)result.Add(end);
   return result;
  }

  static void Append(List<Vector3> points,Vector3 p){int n=points.Count;if(n>1){var a=points[n-2];var b=points[n-1];var d=p-a;float t=Vector3.Dot(b-a,d)/Mathf.Max(.00000001f,d.sqrMagnitude);if(t>=0&&t<=1&&(a+d*t-b).sqrMagnitude<.00000001f){points[n-1]=p;return;}}points.Add(p);}
  static long Key(int x,int y){return ((long)x<<32)^(uint)y;}
  static void Bounds(Span s,float margin,float cell,out int x0,out int x1,out int y0,out int y1){x0=Mathf.FloorToInt((Mathf.Min(s.a.x,s.b.x)-margin)/cell);x1=Mathf.FloorToInt((Mathf.Max(s.a.x,s.b.x)+margin)/cell);y0=Mathf.FloorToInt((Mathf.Min(s.a.y,s.b.y)-margin)/cell);y1=Mathf.FloorToInt((Mathf.Max(s.a.y,s.b.y)+margin)/cell);}
  static void Insert(Span span,Dictionary<long,List<Span>> grid,float tolerance,float cell){Bounds(span,tolerance,cell,out int x0,out int x1,out int y0,out int y1);for(int x=x0;x<=x1;x++)for(int y=y0;y<=y1;y++){long key=Key(x,y);if(!grid.TryGetValue(key,out var list)){list=new List<Span>();grid.Add(key,list);}list.Add(span);}}
  static bool Overlaps(Span s,Dictionary<long,List<Span>> grid,float tolerance,float cell){Bounds(s,0,cell,out int x0,out int x1,out int y0,out int y1);var d=s.b-s.a;float length=d.magnitude;if(length<.00001f)return true;var dir=d/length;for(int x=x0;x<=x1;x++)for(int y=y0;y<=y1;y++){if(!grid.TryGetValue(Key(x,y),out var spans))continue;foreach(var other in spans){var od=other.b-other.a;float ol=od.magnitude;if(ol<.00001f||Mathf.Abs(Vector3.Dot(dir,od/ol))<.97f)continue;float a=Vector3.Dot(other.a-s.a,dir),b=Vector3.Dot(other.b-s.a,dir);float overlap=Mathf.Min(length,Mathf.Max(a,b))-Mathf.Max(0,Mathf.Min(a,b));if(overlap<=Mathf.Min(length,ol)*.15f)continue;var mid=(other.a+other.b)*.5f;var normal=mid-s.a-dir*Vector3.Dot(mid-s.a,dir);if(normal.sqrMagnitude<=tolerance*tolerance)return true;}}return false;}
 }
}
