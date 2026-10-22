using UnityEngine;
namespace CampaignDemo {
 [CreateAssetMenu(menuName="Campaign Demo/策划/固定雷达")]
 public sealed class RadarDefinition:ScriptableObject {
  [Min(0)]public float visionRadius=10;
  [Min(1)]public float health=63;
  [Min(0)]public float buildCost=20000;
  [Tooltip("工兵在附近安放，由最近己方仓库或据点支付。"),Min(.1f)]public float deploymentDistance=1;
  [Min(.1f)]public float minimumSpacing=.75f;
 }
}
