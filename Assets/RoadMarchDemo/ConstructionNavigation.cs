using System;
using System.Collections.Generic;
using UnityEngine;
namespace CampaignDemo
{
    // Local terrain-aware route planning, independent of camera and drawing input.
    public static class ConstructionNavigation
    {
        struct Candidate:IComparable<Candidate>{public int index;public float priority;public long order;public int CompareTo(Candidate other){int c=priority.CompareTo(other.priority);return c!=0?c:order.CompareTo(other.order);}}
        public static List<RoadPoint> Plan(FreeMarchMapRuntime map,TerrainAccess access,Vector3 from,Vector3 to,float cellSize,int limit)
        {
            var a=new RoadPoint(from.x,from.y);var b=new RoadPoint(to.x,to.y);if(map==null||!map.CanTraverse(a,a,access)||!map.CanTraverse(b,b,access))return null;if(map.CanTraverse(a,b,access))return new List<RoadPoint>{a,b};
            float step=Mathf.Max(.01f,cellSize);var bounds=map.definition.bounds;var lower=map.transform.TransformPoint(new Vector3(bounds.xMin,bounds.yMin));var upper=map.transform.TransformPoint(new Vector3(bounds.xMax,bounds.yMax));int columns=Mathf.CeilToInt((upper.x-lower.x)/step),rows=Mathf.CeilToInt((upper.y-lower.y)/step);if(columns<=0||rows<=0)return null;
            Func<Vector3,int> index=p=>Mathf.Clamp(Mathf.FloorToInt((p.y-lower.y)/step),0,rows-1)*columns+Mathf.Clamp(Mathf.FloorToInt((p.x-lower.x)/step),0,columns-1);int start=index(from),goal=index(to);if(start==goal)return null;
            Func<int,RoadPoint> point=i=>i==start?a:i==goal?b:new RoadPoint(lower.x+(i%columns+.5f)*step,lower.y+(i/columns+.5f)*step);
            var open=new SortedSet<Candidate>();var costs=new Dictionary<int,float>{{start,0}};var parents=new Dictionary<int,int>();var closed=new HashSet<int>();long sequence=0;open.Add(new Candidate{index=start,priority=(float)RoadPoint.Distance(a,b),order=sequence++});
            while(open.Count>0&&closed.Count<limit)
            {
                var current=open.Min;open.Remove(current);int at=current.index;if(!closed.Add(at))continue;if(at==goal){var reverse=new List<RoadPoint>();for(int i=goal;;i=parents[i]){reverse.Add(point(i));if(i==start)break;}reverse.Reverse();return Simplify(reverse,map,access);}
                int x=at%columns,y=at/columns;for(int dy=-1;dy<=1;dy++)for(int dx=-1;dx<=1;dx++){if(dx==0&&dy==0)continue;int nx=x+dx,ny=y+dy;if(nx<0||nx>=columns||ny<0||ny>=rows)continue;int next=ny*columns+nx;if(closed.Contains(next))continue;var p=point(at);var q=point(next);if(!map.CanTraverse(p,q,access))continue;float cost=costs[at]+(float)RoadPoint.Distance(p,q);if(costs.TryGetValue(next,out var prior)&&cost>=prior)continue;costs[next]=cost;parents[next]=at;open.Add(new Candidate{index=next,priority=cost+(float)RoadPoint.Distance(q,b),order=sequence++});}
            }
            return null;
        }
        static List<RoadPoint> Simplify(List<RoadPoint> path,FreeMarchMapRuntime map,TerrainAccess access){var result=new List<RoadPoint>{path[0]};int i=0;while(i<path.Count-1){int next=path.Count-1;while(next>i+1&&!map.CanTraverse(path[i],path[next],access))next--;result.Add(path[next]);i=next;}return result;}
    }
}
