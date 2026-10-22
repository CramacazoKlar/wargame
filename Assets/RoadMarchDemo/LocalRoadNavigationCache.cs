using System;
using System.Collections.Generic;
using UnityEngine;
namespace CampaignDemo {
 // Geometry-only edges; order endpoints always use an uncached exact-position check.
 public sealed class LocalRoadNavigationCache {
  public struct Edge {public bool allowed;public float weight;}
  static readonly List<LocalRoadNavigationCache> maps=new List<LocalRoadNavigationCache>();
  readonly Dictionary<ulong,Edge> edges=new Dictionary<ulong,Edge>();
  FreeMarchMapRuntime map;int version,water;float step;TerrainAccess access;
  public NarrowRoadAnchors anchors;
  [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]static void Reset(){maps.Clear();}
  static int WaterVersion(RasterWaterMap map){if(map==null)return 0;unchecked{int h=(map.source==null?0:map.source.GetInstanceID())^map.localBounds.GetHashCode()^map.transform.localToWorldMatrix.GetHashCode();foreach(var bridge in map.bridges??Array.Empty<Rect>())h=h*397^bridge.GetHashCode();return h;}}
  public static LocalRoadNavigationCache For(FreeMarchMapRuntime map,TerrainAccess access,float step,int columns,int rows,Func<Vector3,int> index){int version=map.Version,water=WaterVersion(map.waterMap);maps.RemoveAll(c=>c.map==null);foreach(var c in maps)if(c.map==map&&c.access==access&&c.step==step){if(c.version!=version||c.water!=water)c.Initialize(map,access,step,columns,rows,index,version,water);return c;}if(maps.Count>=4)maps.RemoveAt(0);var result=new LocalRoadNavigationCache();result.Initialize(map,access,step,columns,rows,index,version,water);maps.Add(result);return result;}
  void Initialize(FreeMarchMapRuntime target,TerrainAccess allowed,float cell,int columns,int rows,Func<Vector3,int> index,int revision,int waterRevision){map=target;access=allowed;step=cell;version=revision;water=waterRevision;edges.Clear();anchors=new NarrowRoadAnchors(map,step,columns,rows,index);}
  static ulong Key(int a,int b){return ((ulong)(uint)Math.Min(a,b)<<32)|(uint)Math.Max(a,b);}
  public bool TryGet(int a,int b,out Edge edge)=>edges.TryGetValue(Key(a,b),out edge);
  public void Store(int a,int b,Edge edge){if(edges.Count<200000)edges[Key(a,b)]=edge;}
 }
}
