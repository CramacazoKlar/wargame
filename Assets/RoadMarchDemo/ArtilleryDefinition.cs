using UnityEngine;
namespace CampaignDemo
{
    [CreateAssetMenu(menuName="Campaign Demo/Artillery",fileName="Artillery")]
    public sealed class ArtilleryDefinition : ScriptableObject
    {
        [Min(.01f)] public float range=8;
        [Min(.01f)] public float blastRadius=.65f;
        [Min(0)] public float cooldownSeconds=20;
        [Min(0)] public float delaySeconds=2;
        [Tooltip("地图外支援/非战役模式的单轮总伤害预算；战役炮兵用CampaignRules中的当前兵力系数。")] [Min(0)] public float damage=1.4f;
        public bool friendlyFire=true;
        public bool requireVision=true;
    }
}


