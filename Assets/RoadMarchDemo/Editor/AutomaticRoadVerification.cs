using System;
using UnityEditor;
using UnityEngine;
namespace CampaignDemo.Editor
{
    public static class AutomaticRoadVerification
    {
        static int count;static void Check(bool value,string name){if(!value)throw new Exception(name);count++;}
        static FreeMarchMapDefinition.Region Box(TerrainKind kind,float x,float y,float w,float h){return new FreeMarchMapDefinition.Region{terrain=kind,polygon=new[]{new Vector2(x,y),new Vector2(x+w,y),new Vector2(x+w,y+h),new Vector2(x,y+h)}};}
        public static void Run()
        {
            var root=new GameObject("Automatic road test");var roadData=ScriptableObject.CreateInstance<RoadMapDefinition>();var terrainData=ScriptableObject.CreateInstance<FreeMarchMapDefinition>();
            try
            {
                var road=root.AddComponent<RoadMapRuntime>();road.definition=roadData;var map=root.AddComponent<FreeMarchMapRuntime>();map.definition=terrainData;terrainData.defaultTerrain=TerrainKind.Mountain;
                roadData.nodes=new[]{new Vector2(-2,0),new Vector2(0,0),new Vector2(0,2)};roadData.connections=new[]{new RoadMapDefinition.Connection(0,1),new RoadMapDefinition.Connection(1,2)};
                Check(map.TerrainAt(new RoadPoint(-1,0))==TerrainKind.Road,"road generated without manual Road regions");Check(map.TerrainAt(new RoadPoint(0,0))==TerrainKind.Road,"bend junction connected");Check(map.TerrainAt(new RoadPoint(-1,.2))==TerrainKind.Mountain,"outside corridor retains terrain");Check(map.CanTraverse(new RoadPoint(-1.5,0),new RoadPoint(-.5,0),TerrainAccess.Road),"road-only unit traverses automatic corridor");
                var rules=new[]{new TerrainSpeedPenalty{terrain=TerrainKind.Mountain,reductionPercent=60},new TerrainSpeedPenalty{terrain=TerrainKind.Road,reductionPercent=0}};Check(TerrainSpeedRules.Calculate(2,0,TerrainSpeedRules.TerrainReduction(map.TerrainAt(new RoadPoint(-1,0)),rules))==2,"automatic road uses road speed");
                terrainData.regions=new[]{Box(TerrainKind.Water,-1.1f,-1,.2f,2)};terrainData.MarkDirty();Check(map.TerrainAt(new RoadPoint(-1,0))==TerrainKind.Water,"water not overwritten");Check(!map.CanTraverse(new RoadPoint(-1.5,0),new RoadPoint(-.5,0),TerrainAccess.Road),"river still blocks road-only unit");
                terrainData.regions=new[]{Box(TerrainKind.Blocked,-1.1f,-1,.2f,2)};terrainData.MarkDirty();Check(map.TerrainAt(new RoadPoint(-1,0))==TerrainKind.Blocked,"blocked region not overwritten");
                terrainData.regions=new FreeMarchMapDefinition.Region[0];terrainData.MarkDirty();int version=map.Version;roadData.nodes[0]=new Vector2(-2,1);roadData.nodes[1]=new Vector2(0,1);roadData.MarkDirty();Check(map.TerrainAt(new RoadPoint(-1,1))==TerrainKind.Road&&map.TerrainAt(new RoadPoint(-1,0))==TerrainKind.Mountain,"moving nodes updates corridor");Check(map.Version!=version,"road edit invalidates display cache");
                map.automaticRoadTerrain=false;Check(map.TerrainAt(new RoadPoint(-1,1))==TerrainKind.Mountain,"automatic roads can be disabled");Check(terrainData.regions.Length==0,"manual terrain data stays untouched");
                Debug.Log("AUTOMATIC_ROAD_VERIFY_PASS "+count);EditorApplication.Exit(0);
            }
            catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
            finally{UnityEngine.Object.DestroyImmediate(root);UnityEngine.Object.DestroyImmediate(roadData);UnityEngine.Object.DestroyImmediate(terrainData);}
        }
    }
}
