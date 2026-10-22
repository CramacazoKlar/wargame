using System;
using UnityEditor;
using UnityEngine;
namespace CampaignDemo.Editor
{
    public static class RoadDeletionVerification
    {
        public static void Run()
        {
            var map=ScriptableObject.CreateInstance<RoadMapDefinition>();RoadMapDefinition clone=null;
            try
            {
                map.nodes=new[]{new Vector2(0,0),new Vector2(1,0),new Vector2(2,0),new Vector2(3,0)};map.connections=new[]{new RoadMapDefinition.Connection(0,1),new RoadMapDefinition.Connection(1,2),new RoadMapDefinition.Connection(2,3)};map.spawnNodes=new[]{0,1,3};int version=map.Revision;map.RemoveNode(1);
                if(map.nodes.Length!=3||map.connections.Length!=1||map.connections[0].a!=1||map.connections[0].b!=2)throw new Exception("Indices not safely remapped");
                if(map.spawnNodes.Length!=2||map.spawnNodes[1]!=2||map.Revision==version)throw new Exception("Spawn or revision not updated");
                var original=AssetDatabase.LoadAssetAtPath<RoadMapDefinition>("Assets/Game/Data/StitchedRoadMap.asset");clone=UnityEngine.Object.Instantiate(original);clone.RemoveNode(402);Validate(clone);
                map.RemoveNode(0);Validate(map);map.RemoveNode(map.nodes.Length-1);Validate(map);
                bool rejected=false;try{map.RemoveNode(999);}catch(ArgumentOutOfRangeException){rejected=true;}if(!rejected)throw new Exception("Invalid deletion not rejected");
                Debug.Log("ROAD_DELETE_VERIFY_PASS: node 402, first/last nodes, spawn indices and invalid input checked.");EditorApplication.Exit(0);
            }
            catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
            finally{UnityEngine.Object.DestroyImmediate(map);if(clone!=null)UnityEngine.Object.DestroyImmediate(clone);}
        }
        static void Validate(RoadMapDefinition data){var nodes=new RoadPoint[data.nodes.Length];for(int i=0;i<nodes.Length;i++)nodes[i]=new RoadPoint(data.nodes[i].x,data.nodes[i].y);var edges=new RoadEdge[data.connections.Length];for(int i=0;i<edges.Length;i++)edges[i]=new RoadEdge(data.connections[i].a,data.connections[i].b);new RoadNetwork(nodes,edges);}
    }
}
