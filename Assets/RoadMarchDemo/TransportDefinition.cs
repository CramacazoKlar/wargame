using UnityEngine;
namespace CampaignDemo
{
    [CreateAssetMenu(menuName="Campaign Demo/Transport Type",fileName="TransportType")]
    public sealed class TransportDefinition : ScriptableObject
    {
        [Tooltip("班、排、连、营、团、旅、师、军对应的满状态装载量。暂用测试值，可分别调整。")]
        public float[] capacityByEchelon={100,300,900,2700,8100,24300,72900,218700};
        [Min(0)] public float buildCost=100;
        [Tooltip("班级基础费用；每升一级乘此倍率。")]
        [Min(1)] public float buildCostPerEchelon=3.3f;
        public static float ScaledBuildCost(float squadCost,float multiplier,ArmyEchelon echelon)
        {int level=(int)echelon;return level<0||level>7?float.PositiveInfinity:Mathf.Ceil(Mathf.Max(0,squadCost)*Mathf.Pow(Mathf.Max(1,multiplier),level));}
        public float BuildCost(ArmyEchelon echelon){return ScaledBuildCost(buildCost,buildCostPerEchelon,echelon);}
        [Min(.1f)] public float buildSeconds=5;
        [Min(.01f)] public float loadingRadius=.6f;
        public float Capacity(ArmyEchelon echelon){int i=(int)echelon;return capacityByEchelon==null||i>=capacityByEchelon.Length?0:Mathf.Max(0,capacityByEchelon[i]);}
    }
    [System.Serializable] public sealed class ResourceCollectorSettings
    {
        [Min(.1f)] public float health=500;
        [Min(0)] public float defense=10;
        [Min(0)] public float buildCost=100;
        [Min(.1f)] public float collectionInterval=5;
        [Min(0)] public float amountPerInterval=50;
        [Min(0)] public float storageLimit=500;
        public Vector2 footprint=new Vector2(.3f,.3f);
    }
}
