using UnityEngine;
namespace CampaignDemo
{
    [CreateAssetMenu(menuName="Campaign Demo/Combat Effect Profile",fileName="CombatEffectProfile")]
    public sealed class CombatEffectProfile : ScriptableObject
    {
        public GameObject firePrefab;
        public GameObject projectilePrefab;
        public GameObject impactPrefab;
        [Tooltip("普通持续攻击的显示间隔，不改变攻击频率或伤害。")] [Min(0)] public float directAttackInterval=.35f;
        [Min(.01f)] public float lifetime=.2f;
        [Min(.01f)] public float projectileTravelSeconds=.12f;
        [Min(.01f)] public float prefabScale=1;
        public bool scaleImpactByBlastRadius;
        [Tooltip("未提供Prefab时显示简单线条/落点圈；正式美术就绪后可关闭。")] public bool showPlaceholder=true;
        public Color color=new Color(1,.7f,.25f,.9f);
        [Min(.001f)] public float lineWidth=.025f;
        [Min(.01f)] public float directImpactRadius=.08f;
    }
}
