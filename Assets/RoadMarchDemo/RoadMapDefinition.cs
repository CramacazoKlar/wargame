using System;
using UnityEngine;

namespace CampaignDemo
{
    [CreateAssetMenu(menuName="Campaign Demo/Road Map",fileName="RoadMap")]
    public sealed class RoadMapDefinition : ScriptableObject
    {
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
        [Min(0.01f)] public float roadWidth=0.22f;
        public Color roadColor=new Color(0.90f,0.86f,0.72f);
    }
}
