using UnityEngine;
namespace CampaignDemo
{
    [CreateAssetMenu(menuName="Campaign Demo/AI Basic Commander Profile")]
    public sealed class AIBasicProfile : ScriptableObject
    {
        public bool attackCities=true, recover=true, produceResources=true, transportResources=true;
        public bool recruitInfantry=true;
        [Header("造兵目标；班为1，排为3，连为9")]
        [Min(0)] public float targetAttackStrength=6;
        [Min(1)] public float maximumArmyStrength=60;
        [Min(1)] public int maximumProductionJobs=2;
        public ArmyEchelon recruitmentEchelon=ArmyEchelon.Squad;
        [HideInInspector] public GameObject infantryPrefab;
        [Header("制造测试值；正式数值仍可调整")]
        [HideInInspector,Min(0)] public float infantrySquadCost=100;
        [HideInInspector,Min(1)] public float infantrySquadSeconds=30;
        [HideInInspector,Min(1)] public float infantryCostScale=3.3f, infantryTimeScale=3;
        [HideInInspector] public bool infantryRequiresFactory=false;
        [Header("独立作弊设置；默认关闭")]
        public bool enableCheats=false;
        [Range(1,10)] public float resourceIncomeMultiplier=1, productionSpeedMultiplier=1;
        public bool omniscientEnemyIntel=false;
        [Range(0,1)] public float defenseRatio=.4f;
        [Min(0)] public float minimumCityDefense=1, minimumAttackStrength=3;
        [Range(0,1)] public float deployHealth=.9f, retreatHealth=.4f, recoveryHealth=.9f, squadRetreatRatio=.6f;
        [Range(0,1)] public float expectedLossRatio=.4f;
        [Min(0)] public float productionReserve=100;
        public bool preferRoad=true, allowGroundFallback=true;
        [Min(.01f)] public float arrivalDistance=.12f;
        [Min(1)] public float gatheringSeconds=15, targetRetrySeconds=30;
        [Min(0)] public int maximumCollectors=2;
        [Min(1)] public float economyInterval=5, desiredCityStock=500;
    }
}
