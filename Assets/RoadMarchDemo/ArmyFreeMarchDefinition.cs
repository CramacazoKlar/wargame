using UnityEngine;
namespace CampaignDemo
{
    [CreateAssetMenu(menuName="Campaign Demo/Free March Army Type",fileName="FreeMarchArmy")]
    public sealed class ArmyFreeMarchDefinition : ScriptableObject
    {
        [Range(0,100)] public float mode2ReductionPercent=25;
        public TerrainSpeedPenalty[] terrainReductions=new TerrainSpeedPenalty[0];
        public TerrainAccess allowedTerrain=TerrainAccess.Ground|TerrainAccess.Road|TerrainAccess.Grass|TerrainAccess.Forest;
    }
}

