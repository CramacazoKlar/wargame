using System;
using System.Collections.Generic;
using UnityEngine;
namespace CampaignDemo {
 [Serializable] public struct ReplayTimedPoint {public float time,radius;public Vector3 position;public bool observe;}
 public static class QuarterReplay {
  public static void Capture(ReplayArmy army,float time,Vector3 position,float radius,bool observe,bool force=false){var h=army.timeline;var p=new ReplayTimedPoint{time=time,position=position,radius=radius,observe=observe};if(h.Count==0){h.Add(p);return;}var last=h[h.Count-1];if(Mathf.Abs(last.time-time)<.000001f){h[h.Count-1]=p;return;}if(force||time-last.time>=.25f||(position-last.position).sqrMagnitude>=.0025f||observe!=last.observe||radius!=last.radius)h.Add(p);}
  // Clip by the battle clock, interpolating the boundary without joining separate on-map segments.
  public static void BuildTactical(ReplayArmy army,float cutoff){army.tacticalRoutes.Clear();foreach(var segment in army.movementSegments){var route=new List<Vector3>();for(int i=0;i<segment.Count;i++){var p=segment[i];if(p.time<cutoff)continue;if(route.Count==0&&i>0&&segment[i-1].time<cutoff){var before=segment[i-1];route.Add(Vector3.Lerp(before.position,p.position,Mathf.InverseLerp(before.time,p.time,cutoff)));}if(route.Count==0||(route[route.Count-1]-p.position).sqrMagnitude>.00000001f)route.Add(p.position);}if(route.Count>=2)army.tacticalRoutes.Add(route);}}
  public static Vector3 At(ReplayArmy army,float time){var h=army.timeline;if(h.Count==0)return army.finalPosition;if(time<=h[0].time)return h[0].position;for(int i=1;i<h.Count;i++)if(h[i].time>=time){var a=h[i-1];var b=h[i];return Vector3.Lerp(a.position,b.position,Mathf.InverseLerp(a.time,b.time,time));}return h[h.Count-1].position;}
  public static void Build(ReplaySnapshot snapshot){snapshot.observationCutoff=snapshot.elapsed*.25f;foreach(var army in snapshot.armies){army.observation.Clear();army.routes.Clear();army.hasSummaryStart=false;ReplayVisionTrail trail=null;var h=army.timeline;
    for(int i=0;i<h.Count;i++){var p=h[i];if(p.time>snapshot.observationCutoff){if(i>0&&h[i-1].observe&&trail!=null){var end=At(army,snapshot.observationCutoff);if(trail.points.Count==0||(trail.points[trail.points.Count-1]-end).sqrMagnitude>.00000001f)trail.points.Add(end);}break;}
     if(!p.observe){trail=null;continue;}if(trail==null||trail.radius!=p.radius){trail=new ReplayVisionTrail{radius=p.radius};army.observation.Add(trail);}trail.points.Add(p.position);
    }
    if(h.Count==0||army.dead&&army.deathTime<=snapshot.observationCutoff)continue;
    army.summaryStart=At(army,snapshot.observationCutoff);army.hasSummaryStart=true;if((army.finalPosition-army.summaryStart).sqrMagnitude>.0001f)army.routes.Add(new List<Vector3>{army.summaryStart,army.finalPosition});
   }}
  public static List<Vector3> Polygon(Vector3 start,Vector3 end,ArmyEchelon echelon){var points=new List<Vector3>();float length=Vector3.Distance(start,end);if(length<.01f)return points;float width=length*(.09f+.02f*Mathf.Clamp((int)echelon,0,4));float dip=length*.06f;var control=(start+end)*.5f+Vector3.down*(dip*2);float stop=Mathf.Clamp(1-width*1.15f/length,.66f,.94f);var right=new List<Vector3>();
   for(int i=0;i<=48;i++){float t=i/48f*stop;var p=Point(start,control,end,t);var d=Tangent(start,control,end,t);var n=new Vector3(-d.y,d.x);float half=width*(1-.7f*t/stop)*.5f;points.Add(p+n*half);right.Add(p-n*half);}
   var b=Point(start,control,end,stop);var tangent=Tangent(start,control,end,stop);var normal=new Vector3(-tangent.y,tangent.x);points.Add(b+normal*width*.39f);points.Add(end);points.Add(b-normal*width*.39f);for(int i=right.Count-1;i>=0;i--)points.Add(right[i]);return points;
  }
  static Vector3 Point(Vector3 a,Vector3 c,Vector3 b,float t){return (1-t)*(1-t)*a+2*(1-t)*t*c+t*t*b;}
  static Vector3 Tangent(Vector3 a,Vector3 c,Vector3 b,float t){return (2*(1-t)*(c-a)+2*t*(b-c)).normalized;}
  static float Cross(Vector3 a,Vector3 b,Vector3 c){return (b.x-a.x)*(c.y-a.y)-(b.y-a.y)*(c.x-a.x);}
  public static Mesh Mesh(List<Vector3> p){var mesh=new Mesh();if(p.Count<3)return mesh;var indices=new List<int>();for(int i=0;i<p.Count;i++)indices.Add(i);float area=0;for(int i=0;i<p.Count;i++){var a=p[i];var b=p[(i+1)%p.Count];area+=a.x*b.y-b.x*a.y;}if(area<0)indices.Reverse();var triangles=new List<int>();int guard=p.Count*p.Count;
   while(indices.Count>2&&guard-->0){bool clipped=false;for(int i=0;i<indices.Count;i++){int a=indices[(i+indices.Count-1)%indices.Count],b=indices[i],c=indices[(i+1)%indices.Count];if(Cross(p[a],p[b],p[c])<=.000000001f)continue;bool inside=false;foreach(int j in indices){if(j==a||j==b||j==c)continue;if(Cross(p[a],p[b],p[j])>=0&&Cross(p[b],p[c],p[j])>=0&&Cross(p[c],p[a],p[j])>=0){inside=true;break;}}if(inside)continue;triangles.Add(a);triangles.Add(b);triangles.Add(c);indices.RemoveAt(i);clipped=true;break;}if(!clipped)break;}
   mesh.SetVertices(p);mesh.SetTriangles(triangles,0);mesh.RecalculateBounds();return mesh;
  }
 }
}
