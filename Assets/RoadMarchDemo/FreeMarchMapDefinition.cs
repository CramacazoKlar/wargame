using System;
using UnityEngine;
namespace CampaignDemo
{
    [CreateAssetMenu(menuName="Campaign Demo/Free March Map",fileName="FreeMarchMap")]
    public sealed class FreeMarchMapDefinition : ScriptableObject
    {
        [Serializable] public sealed class Region
        { public string name="Region"; public TerrainKind terrain=TerrainKind.Blocked; public Vector2[] polygon=new Vector2[0]; }
        public Rect bounds=new Rect(-12,-8,24,16);
        public TerrainKind defaultTerrain=TerrainKind.Ground;
        [Tooltip("Later regions override earlier regions.")] public Region[] regions=new Region[0];
        [NonSerialized] int revision;
        public int Revision { get { return revision; } }
        // Call after procedural edits to bounds, terrain or polygon arrays.
        public void MarkDirty() { unchecked { revision++; } }
        void OnValidate() { MarkDirty(); }
        void OnEnable() { MarkDirty(); }
    }
}
