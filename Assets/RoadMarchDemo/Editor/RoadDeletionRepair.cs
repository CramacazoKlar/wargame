using System;
using System.IO;
using UnityEditor;
using UnityEngine;
namespace CampaignDemo.Editor
{
    [InitializeOnLoad]
    public static class RoadDeletionRepair
    {
        [Serializable] sealed class Baseline { public Vector2[] nodes;public RoadMapDefinition.Connection[] connections; }
        static RoadDeletionRepair(){EditorApplication.update+=Repair;}
        static void Repair()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode||EditorApplication.isCompiling)return;
            EditorApplication.update-=Repair;
            const string file="Assets/RoadMarchDemo/Editor/RoadDeleteBaseline.json";
            if(!File.Exists(file))return;var original=JsonUtility.FromJson<Baseline>(File.ReadAllText(file));
            var current=AssetDatabase.LoadAssetAtPath<RoadMapDefinition>("Assets/Game/Data/StitchedRoadMap.asset");
            if(current==null||current.nodes==null||current.connections==null||current.nodes.Length!=original.nodes.Length-1||current.connections.Length!=original.connections.Length)return;
            int removed=-1;
            for(int i=0;i<original.nodes.Length;i++)if(i>=current.nodes.Length||current.nodes[i]!=original.nodes[i]){removed=i;break;}
            if(removed<0)return;
            for(int i=removed;i<current.nodes.Length;i++)if(current.nodes[i]!=original.nodes[i+1])return;
            for(int i=0;i<current.connections.Length;i++)if(current.connections[i].a!=original.connections[i].a||current.connections[i].b!=original.connections[i].b)return;
            Undo.RecordObject(current,"Repair deleted road node indices");
            current.RemapAfterRemovedNode(removed);EditorUtility.SetDirty(current);AssetDatabase.SaveAssets();SceneView.RepaintAll();
            Debug.Log("ROAD_DELETE_REPAIRED: deleted node "+removed+", connected edges removed and later indices remapped.");
        }
    }
}
