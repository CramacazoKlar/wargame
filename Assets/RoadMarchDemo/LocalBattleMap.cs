using System;
using System.Collections.Generic;
using UnityEngine;
namespace CampaignDemo {
 [ExecuteAlways] public sealed class LocalBattleMap:MonoBehaviour {
  [Serializable] public sealed class River {public Vector2[] points=new Vector2[0];[Min(.1f)]public float width=.8f;}
  public River[] rivers=new River[0];[Min(.05f)]public float roadWidth=.24f;
  public FreeMarchMapDefinition.Region[] baseRegions=new FreeMarchMapDefinition.Region[0];
  public FreeMarchMapRuntime terrain;public RoadMapRuntime roads;int hash;Material material;readonly List<GameObject> visuals=new List<GameObject>();
  public bool useSavedGeometry;public bool roadDefinitionIsAuthoritative;
  int GeometryHash(){int h=roadWidth.GetHashCode();foreach(var r in rivers){h=unchecked(h*397^r.width.GetHashCode());foreach(var p in r.points)h=unchecked(h*397^p.GetHashCode());}return h;}
  void OnEnable(){if(useSavedGeometry&&terrain!=null&&terrain.definition!=null&&terrain.definition.regions.Length>0){hash=GeometryHash();return;}Rebuild();}void OnValidate(){hash=0;}
  void Update(){if(roadDefinitionIsAuthoritative&&roads!=null&&roads.definition!=null)roadWidth=roads.definition.roadWidth;int h=GeometryHash();if(h!=hash){Rebuild();}}
  public void Rebuild(){if(terrain==null||roads==null||terrain.definition==null||roads.definition==null)return;
   hash=GeometryHash();
   var regions=new List<FreeMarchMapDefinition.Region>(baseRegions);foreach(var g in visuals)if(g!=null){if(Application.isPlaying)Destroy(g);else DestroyImmediate(g);}visuals.Clear();
   foreach(var r in rivers){for(int i=1;i<r.points.Length;i++){var a=r.points[i-1];var b=r.points[i];var normal=new Vector2(-(b-a).y,(b-a).x).normalized*r.width/2;regions.Add(new FreeMarchMapDefinition.Region{name="自动河道",terrain=TerrainKind.Water,polygon=new[]{a+normal,b+normal,b-normal,a-normal}});}foreach(var p in r.points){var cap=new Vector2[12];for(int i=0;i<12;i++)cap[i]=p+new Vector2(Mathf.Cos(i*Mathf.PI/6),Mathf.Sin(i*Mathf.PI/6))*r.width/2;regions.Add(new FreeMarchMapDefinition.Region{name="河道接合",terrain=TerrainKind.Water,polygon=cap});}}
   terrain.definition.regions=regions.ToArray();terrain.definition.MarkDirty();terrain.roadTerrainWidth=roadWidth;terrain.automaticRoadTerrain=true;terrain.waterMap=null;terrain.InvalidateGeometry();roads.definition.roadWidth=roadWidth;roads.definition.MarkDirty();

   var cartography=GetComponent<StaffMapCartography>();if(cartography==null)cartography=gameObject.AddComponent<StaffMapCartography>();cartography.roads=roads;cartography.terrain=terrain;cartography.Invalidate();
  }
  void DrawSymbols(){foreach(var region in baseRegions){if(region==null||region.polygon==null||region.polygon.Length<3)continue;var points=region.polygon;var polygon=new RoadPoint[points.Length];float x0=float.MaxValue,y0=float.MaxValue,x1=float.MinValue,y1=float.MinValue;for(int i=0;i<points.Length;i++){polygon[i]=new RoadPoint(points[i].x,points[i].y);x0=Mathf.Min(x0,points[i].x);y0=Mathf.Min(y0,points[i].y);x1=Mathf.Max(x1,points[i].x);y1=Mathf.Max(y1,points[i].y);}
   if(region.terrain==TerrainKind.Forest){var edge=new List<Vector2>(points);edge.Add(points[0]);Draw(edge.ToArray(),.018f,StaffMapStyle.Forest,1);int row=0;for(float y=y0+.18f;y<y1;y+=.36f,row++)for(float x=x0+.18f+(row%2)*.2f;x<x1;x+=.4f){var triangle=new[]{new Vector2(x,y+.11f),new Vector2(x-.1f,y-.08f),new Vector2(x+.1f,y-.08f),new Vector2(x,y+.11f)};bool inside=true;foreach(var t in triangle)if(!TerrainGeometry.Contains(polygon,new RoadPoint(t.x,t.y)))inside=false;if(inside)Draw(triangle,.016f,StaffMapStyle.Hex(0x526149),1);}}
   if(region.terrain==TerrainKind.Mountain||region.terrain==TerrainKind.HighMountain){int count=region.terrain==TerrainKind.HighMountain?11:6;var center=new Vector2((x0+x1)/2,(y0+y1)/2);for(int level=0;level<count;level++){float factor=Mathf.Lerp(1,.2f,level/(float)(count-1));var ring=new Vector2[65];for(int i=0;i<65;i++){float angle=i*Mathf.PI*2/64;var ray=new Vector2(Mathf.Cos(angle)*(x1-x0)/2,Mathf.Sin(angle)*(y1-y0)/2);var outer=center+ray;for(int k=0;k<8&&!TerrainGeometry.Contains(polygon,new RoadPoint(outer.x,outer.y));k++)outer=Vector2.Lerp(outer,center,.15f);ring[i]=Vector2.Lerp(center,outer,factor);}Draw(ring,level==0?.035f:.016f,StaffMapStyle.Hex(0x526149),1);}}
  }}
  void Draw(Vector2[] points,float width,Color color,int order){if(points.Length<2)return;if(material==null)material=new Material(Shader.Find("Sprites/Default"));var g=new GameObject("自动宽度地形");g.hideFlags=HideFlags.DontSave;g.transform.SetParent(transform,false);var line=g.AddComponent<LineRenderer>();line.sharedMaterial=material;line.positionCount=points.Length;for(int i=0;i<points.Length;i++)line.SetPosition(i,transform.TransformPoint(points[i]));line.startWidth=line.endWidth=width;line.startColor=line.endColor=color;line.sortingOrder=order;line.numCornerVertices=4;line.numCapVertices=4;visuals.Add(g);}
  void OnDestroy(){foreach(var g in visuals)if(g!=null){if(Application.isPlaying)Destroy(g);else DestroyImmediate(g);}if(material!=null){if(Application.isPlaying)Destroy(material);else DestroyImmediate(material);}}
 }
}

