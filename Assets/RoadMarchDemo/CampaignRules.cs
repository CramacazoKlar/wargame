using UnityEngine;
namespace CampaignDemo
{
    [CreateAssetMenu(menuName="Campaign Demo/Campaign Rules",fileName="CampaignRules")]
    public sealed class CampaignRules:ScriptableObject
    {
        [Header("统一兵力与火力；示例值，正式平衡在资产中修改")]
        public float[] troopStrength={7,21,63,189,567,1701,5103,15309};
        public float[] transportEquivalents={1,3,9,27,81,243,729,2187};
        public float[] passengerCapacity={7,21,63,189,567,1701,5103,15309};
        public float[] cargoCapacity={10000,30000,90000,270000,810000,2430000,7290000,21870000};
        [Min(0)] public float lossPerStrengthPerSecond=.02f;
        [Range(0,1)] public float engineerOrdinaryAttackMultiplier=.1f;
        [Min(.0001f)] public float integrationStep=.01f;
        [InspectorName("后续目标顺序权重倍率"),Tooltip("按接敌先后递减，再乘目标当前等效兵力；不低于顺序权重下限。"),Range(.001f,1)] public float distanceDecay=2f/3f;
        [InspectorName("接敌顺序权重下限"),Tooltip("后排顺序权重不再无限递减；默认第三位及以后为4/9。"),Range(.001f,1)] public float contactWeightFloor=4f/9f;
        [Min(0)] public float vehicleHealthPerEquivalent=100;
        [Header("移动、朝向、包抄")]
        [Min(1)] public float movingIncomingMultiplier=1.2f;
        [Header("脱离交战惩罚；确定值，不使用随机数")]
        [Range(.05f,.10f)] public float retreatLossFraction=.075f;
        [Min(0)] public float retreatMinimumSpeed=.05f;
        [Range(0,1)] public float retreatAwayDotThreshold=.25f;
        [Min(.01f)] public float retreatEncounterResetSeconds=5;
        [Min(0)] public float infantryFacingLingerSeconds=2;
        [Range(0,180)] public float frontHalfAngle=60;
        [Range(0,180)] public float rearStartsAngle=135;
        [Min(1)] public float infantrySideMultiplier=1.2f,infantryRearMultiplier=1.4f;
        [Min(1)] public float tankSideMultiplier=2,tankRearMultiplier=3;
        [Range(1,180)] public float directionSeparationDegrees=45;
        [Min(0)] public float encirclementIncrement=.15f;
        [Min(1)] public float encirclementCap=2;
        [Header("炮击总预算；当前兵力与落弹时区域人数") ]
        [Min(0)] public float artilleryDamagePerStrength=.2f;
        [Range(0,1)] public float shellMaximumLossFraction=.15f;
        [Header("工事")]
        [Min(.01f)] public float fortHealth=100;
        [Range(.001f,1)] public float fortMovementMultiplier=.5f;
        [Min(1)] public float fortAttackMultiplier=1.3f;
        [HideInInspector, Range(0,1)] public float fortArtilleryProtection=.5f;
        [Range(0,1)] public float shellFortDamageShare=.5f;
        [Min(1)] public int fortLevelCount=3;
        [Min(1)] public float fortLevelHealthFactor=2,fortLevelAttackFactor=1.1f;
        [HideInInspector, Range(0,1)] public float fortLevelProtectionIncrement=.15f,fortMaximumProtection=.9f;
        [Min(1)] public float engineerFortDamageMultiplier=3;
        [Min(.01f)] public float constructionRange=.8f;
        [Min(.01f)] public float warehouseCaptureRadius=.7f,warehouseInteractionRadius=.8f;
        [Min(.01f)] public float warehouseCaptureSeconds=5;
        [Min(0)] public float warehouseBuildCost=20000;
        [Min(.01f)] public float warehouseBuildRadius=.25f;
        [Min(.01f)] public float transportInteractionRadius=.8f;
        [Min(.01f)] public float placementPreviewRadius=.3f;
        [Min(1)] public int unloadSearchSamples=24,unloadDirections=8;
        [Min(.01f)] public float unloadFirstRadius=.18f,unloadRingStep=.12f;
        public float At(float[] table,ArmyEchelon echelon){int i=(int)echelon;return table!=null&&i<table.Length?Mathf.Max(0,table[i]):0;}
    }
}





