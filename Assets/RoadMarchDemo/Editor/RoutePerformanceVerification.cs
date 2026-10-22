using System;
using System.IO;
using System.Linq;
using System.Diagnostics;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace CampaignDemo.Editor {
 public static class RoutePerformanceVerification {
  public static void Run(){try{EditorSceneManager.OpenScene("Assets/Scenes/DoubleValley9x.unity");var map=UnityEngine.Object.FindObjectOfType<FreeMarchMapRuntime>();var road=map.GetComponent<RoadMapRuntime>();var access=TerrainAccess.Ground|TerrainAccess.Road|TerrainAccess.Grass|TerrainAccess.Forest|TerrainAccess.Mountain;var nodes=road.definition.nodes.Select(p=>road.transform.TransformPoint(p)).Where(p=>map.CanTraverse(new RoadPoint(p.x,p.y),new RoadPoint(p.x,p.y),access)).ToArray();if(nodes.Length<4)throw new Exception("Not enough navigable nodes");string report="";for(int pass=0;pass<2;pass++)for(int i=0;i<3;i++){var from=nodes[i*nodes.Length/3];var to=nodes.OrderByDescending(p=>(p-from).sqrMagnitude).First();var timer=Stopwatch.StartNew();var route=LocalRoadAStar.Plan(map,access,from,to,.2f,40000);timer.Stop();if(route!=null)for(int j=1;j<route.Count;j++)if(!map.CanTraverse(route[j-1],route[j],access))throw new Exception("Route crosses blocked terrain");report+=$"pass={pass} pair={i} distance={Vector2.Distance(from,to):0.00} ms={timer.Elapsed.TotalMilliseconds:0.00} points={(route==null?0:route.Count)} from={from} to={to}\n";}File.WriteAllText(Environment.GetEnvironmentVariable("REVIEW_BENCH_RESULT"),report);EditorApplication.Exit(0);}catch(Exception e){File.WriteAllText(Environment.GetEnvironmentVariable("REVIEW_BENCH_RESULT"),"FAIL "+e);EditorApplication.Exit(1);}}
 }
}
