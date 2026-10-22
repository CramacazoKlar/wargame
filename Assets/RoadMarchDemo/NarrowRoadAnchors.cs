using System;
using System.Collections.Generic;
using UnityEngine;
namespace CampaignDemo {
 // Supplemental exact centreline nodes keep coarse A* grids connected across narrow bridges.
 public sealed class NarrowRoadAnchors {
  public readonly List<RoadPoint> points=new List<RoadPoint>();
  public readonly Dictionary<int,List<int>> links=new Dictionary<int,List<int>>();
  readonly int first;readonly Func<Vector3,int> cell;readonly int columns,rows;
  void Join(int a,int b){if(!links.TryGetValue(a,out var left))links[a]=left=new List<int>();if(!left.Contains(b))left.Add(b);if(!links.TryGetValue(b,out var right))links[b]=right=new List<int>();if(!right.Contains(a))right.Add(a);}
  int Add(Vector3 p){int id=first+points.Count;points.Add(new RoadPoint(p.x,p.y));int index=cell(p),x=index%columns,y=index/columns;for(int dy=-1;dy<=1;dy++)for(int dx=-1;dx<=1;dx++){int xx=x+dx,yy=y+dy;if(xx>=0&&yy>=0&&xx<columns&&yy<rows)Join(id,yy*columns+xx);}return id;}
  public NarrowRoadAnchors(FreeMarchMapRuntime map,float step,int columns,int rows,Func<Vector3,int> cell){this.columns=columns;this.rows=rows;this.cell=cell;first=columns*rows;var roads=map.GetComponent<RoadMapRuntime>();if(roads==null||roads.definition==null||!map.automaticRoadTerrain||roads.definition.roadWidth>=step*1.1f)return;var data=roads.definition;var nodes=new Dictionary<int,int>();Func<int,int> node=i=>{if(!nodes.TryGetValue(i,out var id))nodes[i]=id=Add(roads.transform.TransformPoint(data.nodes[i]));return id;};
   foreach(var edge in data.connections){if(edge.a<0||edge.b<0||edge.a>=data.nodes.Length||edge.b>=data.nodes.Length)continue;Vector3 a=roads.transform.TransformPoint(data.nodes[edge.a]),b=roads.transform.TransformPoint(data.nodes[edge.b]);int previous=node(edge.a),end=node(edge.b),count=Mathf.Max(1,Mathf.CeilToInt(Vector3.Distance(a,b)/Mathf.Max(.05f,step)));for(int i=1;i<=count;i++){int next=i==count?end:Add(Vector3.Lerp(a,b,i/(float)count));Join(previous,next);previous=next;}}
  }
 }
}
