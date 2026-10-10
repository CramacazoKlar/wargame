using System;
using UnityEngine;
namespace CampaignDemo
{
    public enum OwnershipState { Unoccupied, Occupied }
    [CreateAssetMenu(menuName="Campaign Demo/Ownership Map",fileName="OwnershipMap")]
    public sealed class OwnershipMapDefinition : ScriptableObject
    {
        [Serializable] public sealed class Region
        {
            public string name="Region";
            public Vector2[] polygon=new Vector2[0];
            public OwnershipState state=OwnershipState.Unoccupied;
            public string ownerId="A";
            public Color color=new Color(0.2f,0.65f,0.9f);
        }
        public Region[] regions=new Region[0];
        [NonSerialized] int revision;
        public int Revision { get { return revision; } }
        public void MarkDirty() { unchecked { revision++; } }
        void OnValidate() { MarkDirty(); }
        void OnEnable() { MarkDirty(); }
    }
}
