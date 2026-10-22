using System;
using UnityEngine;

namespace CampaignDemo
{
    [CreateAssetMenu(menuName="Campaign Demo/Road Map",fileName="RoadMap")]
    public sealed class RoadMapDefinition : ScriptableObject
    {
        [NonSerialized] int revision;
        public int Revision {get{return revision;}}
        public void MarkDirty(){unchecked{revision++;}}
        void OnValidate(){MarkDirty();}
        void OnEnable(){MarkDirty();}
        public void RemoveNode(int index)
        {
            if(nodes==null||index<0||index>=nodes.Length)throw new ArgumentOutOfRangeException("index");
            var remaining=new Vector2[nodes.Length-1];Array.Copy(nodes,0,remaining,0,index);Array.Copy(nodes,index+1,remaining,index,nodes.Length-index-1);nodes=remaining;
            RemapAfterRemovedNode(index);
        }
        public void RemapAfterRemovedNode(int index)
        {
            var links=new System.Collections.Generic.List<Connection>();
            foreach(var link in connections??new Connection[0])if(link.a!=index&&link.b!=index)links.Add(new Connection(link.a>index?link.a-1:link.a,link.b>index?link.b-1:link.b));connections=links.ToArray();
            var spawns=new System.Collections.Generic.List<int>();foreach(var node in spawnNodes??new int[0])if(node!=index)spawns.Add(node>index?node-1:node);spawnNodes=spawns.ToArray();MarkDirty();
        }
        [Serializable] public struct Connection
        { public int a,b; public Connection(int a,int b){this.a=a;this.b=b;} }
        [Header("Map art - optional, independent of navigation")]
        public Sprite background;
        public Vector2 backgroundSize=new Vector2(24,16);
        public Vector2 backgroundCenter;
        [Header("Road geometry in local XY coordinates")]
        public Vector2[] nodes;
        public Connection[] connections;
        [Header("Spawn points")]
        public int[] spawnNodes={0,4,8};
        [Tooltip("Optional free-position spawns. When non-empty these replace Spawn Nodes; armies may start off-road.")]
        public Vector2[] spawnPositions;
        [Header("Road appearance")]
        public bool showRoads=true;
        [Min(0.01f)] public float roadWidth=0.5f;
        public Color roadColor=new Color(0.90f,0.86f,0.72f);
    }
}
