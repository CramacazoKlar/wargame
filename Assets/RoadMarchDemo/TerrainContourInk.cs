using System;
using System.Collections.Generic;
using UnityEngine;
namespace CampaignDemo {
 // A decorative scalar height field. Simulation, traversal and combat never read it.
 public sealed class TerrainContourInk {
  sealed class Hill {public RoadPoint[] polygon;public Rect bounds;public float peak,depth,shoulder;public bool forest;}
  struct Road {public Vector2 a,b;}
  struct EdgePoint {public int key;public Vector2 point;}
  struct Link {public EdgePoint a,b;}
  public struct Stroke {public Vector2 a,b;public bool forest;}
  readonly List<Hill> hills=new List<Hill>();readonly List<Hill> waters=new List<Hill>();readonly List<Road> roads=new List<Road>();
  readonly Rect bounds;readonly float roadClearance;readonly int seed;
  public readonly List<Stroke> strokes=new List<Stroke>();
  public int ForestStrokes {get;private set;}public int MountainStrokes {get;private set;}
  static Rect Bounds(Vector2[] points){float x0=float.MaxValue,y0=float.MaxValue,x1=float.MinValue,y1=float.MinValue;foreach(var p in points){x0=Mathf.Min(x0,p.x);y0=Mathf.Min(y0,p.y);x1=Mathf.Max(x1,p.x);y1=Mathf.Max(y1,p.y);}return Rect.MinMaxRect(x0,y0,x1,y1);}
  static float Boundary(Hill hill,Vector2 p){float best=float.MaxValue;for(int i=0;i<hill.polygon.Length;i++){var a=hill.polygon[i];var b=hill.polygon[(i+1)%hill.polygon.Length];var av=new Vector2((float)a.X,(float)a.Y);var d=new Vector2((float)(b.X-a.X),(float)(b.Y-a.Y));best=Mathf.Min(best,Vector2.Distance(p,av+d*Mathf.Clamp01(Vector2.Dot(p-av,d)/Mathf.Max(.000001f,d.sqrMagnitude))));}return best;}
  static bool Inside(Hill hill,Vector2 p){return hill.bounds.Contains(p)&&TerrainGeometry.Contains(hill.polygon,new RoadPoint(p.x,p.y));}
  public TerrainContourInk(FreeMarchMapRuntime terrain,RoadMapRuntime network,int seed){this.seed=seed;bounds=terrain.definition.bounds;roadClearance=network.definition.roadWidth*.5f+.10f;
   foreach(var region in terrain.definition.regions){if(region==null||region.polygon==null||region.polygon.Length<3)continue;bool forest=region.terrain==TerrainKind.Forest;bool mountain=region.terrain==TerrainKind.Mountain||region.terrain==TerrainKind.HighMountain;bool water=region.terrain==TerrainKind.Water;if(!forest&&!mountain&&!water)continue;var h=new Hill{polygon=Array.ConvertAll(region.polygon,p=>new RoadPoint(p.x,p.y)),bounds=Bounds(region.polygon),forest=forest,peak=forest?.9f:region.terrain==TerrainKind.HighMountain?5:3.3f,shoulder=forest?3.5f:8.5f};
    if(water){waters.Add(h);continue;}float deepest=0;for(int y=0;y<=14;y++)for(int x=0;x<=14;x++){var p=new Vector2(Mathf.Lerp(h.bounds.xMin,h.bounds.xMax,x/14f),Mathf.Lerp(h.bounds.yMin,h.bounds.yMax,y/14f));if(Inside(h,p))deepest=Mathf.Max(deepest,Boundary(h,p));}h.depth=Mathf.Max(.15f,deepest);hills.Add(h);
   }
   foreach(var e in network.definition.connections){Vector2 a=terrain.transform.InverseTransformPoint(network.transform.TransformPoint(network.definition.nodes[e.a])),b=terrain.transform.InverseTransformPoint(network.transform.TransformPoint(network.definition.nodes[e.b]));roads.Add(new Road{a=a,b=b});}
  }
  public float RoadDistance(Vector2 p){float best=float.MaxValue;foreach(var road in roads){var d=road.b-road.a;best=Mathf.Min(best,Vector2.Distance(p,road.a+d*Mathf.Clamp01(Vector2.Dot(p-road.a,d)/Mathf.Max(.000001f,d.sqrMagnitude))));}return best;}
  public bool RoadClear(Vector2 p){return RoadDistance(p)>roadClearance;}
  float Evaluate(Vector2 p,out bool forest){float height=0;forest=false;foreach(var h in hills){if(p.x<h.bounds.xMin-h.shoulder||p.x>h.bounds.xMax+h.shoulder||p.y<h.bounds.yMin-h.shoulder||p.y>h.bounds.yMax+h.shoulder)continue;bool inside=Inside(h,p);float distance=Boundary(h,p),boundary= h.forest?.30f:.08f;float factor=inside?boundary+(1-boundary)*Mathf.SmoothStep(0,1,distance/h.depth):boundary*Mathf.Pow(Mathf.Clamp01(1-distance/h.shoulder),2);float value=h.peak*factor;if(value>height){height=value;forest=h.forest;}}
   if(height<=0)return 0;foreach(var water in waters)if(Inside(water,p))return 0;
   // Roads lie at the same zero datum, with a smooth shoulder. This affects drawing only.
   float roadFactor=Mathf.SmoothStep(0,1,(RoadDistance(p)-roadClearance)/.65f);return height*roadFactor*(.97f+.03f*StaffMapCanvas.Noise(p.x*.6f,p.y*.6f,seed));
  }
  public float HeightAt(Vector2 p){bool forest;return Evaluate(p,out forest);}
  EdgePoint Crossing(int x,int y,int edge,int nx,int ny,float[] values,float level){int ax=x,ay=y,bx=x,by=y,key=0;switch(edge){case 0:bx++;key=y*(nx-1)+x;break;case 1:ax++;bx++;by++;key=(nx-1)*ny+y*nx+x+1;break;case 2:ay++;by++;bx++;key=(y+1)*(nx-1)+x;break;default:by++;key=(nx-1)*ny+y*nx+x;break;}float a=values[ay*nx+ax],b=values[by*nx+bx];var pa=new Vector2(bounds.xMin+ax*bounds.width/(nx-1),bounds.yMin+ay*bounds.height/(ny-1));var pb=new Vector2(bounds.xMin+bx*bounds.width/(nx-1),bounds.yMin+by*bounds.height/(ny-1));return new EdgePoint{key=key,point=Vector2.Lerp(pa,pb,(level-a)/(b-a))};}
  static void Join(Dictionary<int,List<int>> edges,int key,int index){if(!edges.TryGetValue(key,out var list))edges[key]=list=new List<int>();list.Add(index);}
  public void Draw(StaffMapCanvas canvas){strokes.Clear();ForestStrokes=MountainStrokes=0;int nx=641,ny=Mathf.Max(129,Mathf.RoundToInt((nx-1)*bounds.height/bounds.width)+1);var values=new float[nx*ny];float max=0;for(int y=0;y<ny;y++)for(int x=0;x<nx;x++){float h=HeightAt(new Vector2(bounds.xMin+x*bounds.width/(nx-1),bounds.yMin+y*bounds.height/(ny-1)));values[y*nx+x]=h;max=Mathf.Max(max,h);}
   var levels=new List<float>{.04f,.12f,.24f};for(float h=.48f;h<=max;h+=.36f)levels.Add(h);foreach(float level in levels){var links=new List<Link>();var edges=new Dictionary<int,List<int>>();var crossing=new int[4];for(int y=0;y<ny-1;y++)for(int x=0;x<nx-1;x++){float a=values[y*nx+x],b=values[y*nx+x+1],c=values[(y+1)*nx+x+1],d=values[(y+1)*nx+x];int n=0;if((a>=level)!=(b>=level))crossing[n++]=0;if((b>=level)!=(c>=level))crossing[n++]=1;if((d>=level)!=(c>=level))crossing[n++]=2;if((a>=level)!=(d>=level))crossing[n++]=3;if(n==0)continue;
     if(n==4&&((a+b+c+d)*.25f>=level)!=(a>=level)){crossing[0]=3;crossing[1]=0;crossing[2]=1;crossing[3]=2;}
     for(int i=0;i<n;i+=2){var link=new Link{a=Crossing(x,y,crossing[i],nx,ny,values,level),b=Crossing(x,y,crossing[i+1],nx,ny,values,level)};int index=links.Count;links.Add(link);Join(edges,link.a.key,index);Join(edges,link.b.key,index);}
    }
    var used=new bool[links.Count];for(int i=0;i<links.Count;i++){if(used[i])continue;int first=edges[links[i].a.key].Count==1?links[i].a.key:edges[links[i].b.key].Count==1?links[i].b.key:links[i].a.key;var points=new List<Vector2>();int current=first,chosen=i;for(int guard=0;guard<=links.Count;guard++){if(used[chosen])break;used[chosen]=true;var link=links[chosen];var from=link.a.key==current?link.a:link.b;var to=link.a.key==current?link.b:link.a;if(points.Count==0)points.Add(from.point);points.Add(to.point);current=to.key;if(current==first)break;chosen=-1;foreach(int next in edges[current])if(!used[next]){chosen=next;break;}if(chosen<0)break;}DrawChain(canvas,points,current==first,level);}
   }
  }
  void DrawChain(StaffMapCanvas canvas,List<Vector2> points,bool closed,float level){if(points.Count<3)return;if(closed)points.RemoveAt(points.Count-1);var smooth=new List<Vector2>();if(!closed)smooth.Add(points[0]);for(int i=0;i<(closed?points.Count:points.Count-1);i++){var a=points[i];var b=points[(i+1)%points.Count];smooth.Add(Vector2.Lerp(a,b,.25f));smooth.Add(Vector2.Lerp(a,b,.75f));}if(!closed)smooth.Add(points[points.Count-1]);else smooth.Add(smooth[0]);
   for(int i=1;i<smooth.Count;i++){var a=smooth[i-1];var b=smooth[i];int count=Mathf.Max(1,Mathf.CeilToInt(Vector2.Distance(a,b)/.04f));for(int j=0;j<count;j++){var from=Vector2.Lerp(a,b,j/(float)count);var to=Vector2.Lerp(a,b,(j+1f)/count);if(!RoadClear(from)||!RoadClear(to)||!RoadClear((from+to)*.5f))continue;bool forest;Evaluate((from+to)*.5f,out forest);var ink=StaffMapStyle.Hex(forest?0x6D7766:0x8A7B61);float alpha=level<=.24f?(forest?.24f:.28f):(forest?.44f:.53f);canvas.Line(from,to,canvas.bounds.width/2300,ink,alpha);strokes.Add(new Stroke{a=from,b=to,forest=forest});if(forest)ForestStrokes++;else MountainStrokes++;}}
  }
 }
}



