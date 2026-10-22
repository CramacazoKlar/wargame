using UnityEngine;
namespace CampaignDemo
{
    [CreateAssetMenu(menuName="Campaign Demo/Artillery",fileName="Artillery")]
    public sealed class ArtilleryDefinition : ScriptableObject
    {
        [Min(.01f)] public float range=8f;
        [Min(.01f)] public float blastRadius=.65f;
        [Min(0)] public float cooldownSeconds=20;
        [Min(0)] public float delaySeconds=2;
        [Header("持续炮击（秒）；按班、排、连等编制逐级增加")]
        [Min(0)] public float barrageBaseSeconds=1.5f;
        [Min(0)] public float barrageSecondsPerEchelon=2;
        [Min(0)] public float rocketBarrageBaseSeconds=2;
        [Min(0)] public float rocketBarrageSecondsPerEchelon=.5f;
        [Min(.1f)] public float shellIntervalSeconds=.9f;
        [Min(.1f)] public float rocketShellIntervalSeconds=.35f;
        public float BarrageDuration(UnitRole role,ArmyEchelon echelon){int rank=Mathf.Clamp((int)echelon,0,7);return Mathf.Max(0,role==UnitRole.RocketArtillery?rocketBarrageBaseSeconds+rank*rocketBarrageSecondsPerEchelon:barrageBaseSeconds+rank*barrageSecondsPerEchelon);}
        public int BarrageRounds(UnitRole role,ArmyEchelon echelon){float duration=BarrageDuration(role,echelon),interval=role==UnitRole.RocketArtillery?rocketShellIntervalSeconds:shellIntervalSeconds;return duration<=0?1:Mathf.Clamp(Mathf.CeilToInt(duration/Mathf.Max(.1f,interval))+1,2,128);}
        [Tooltip("地图外支援/非战役模式的单轮总伤害预算；战役炮兵用CampaignRules中的当前兵力系数。")] [Min(0)] public float damage=1.4f;
        public bool friendlyFire=true;
        [HideInInspector] public bool requireVision=false; // Legacy serialized value; firing no longer checks visibility.
    }
}



