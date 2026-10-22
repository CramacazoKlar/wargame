using UnityEngine;
namespace CampaignDemo
{
 [CreateAssetMenu(menuName="Campaign Demo/策划/参战整补成长",fileName="UnitExperiencePlanning")]
 public sealed class UnitExperiencePlanning:ScriptableObject
 {
  [Range(1,10)]public int maximumLevel=4;
  [Range(0,1)]public float infantryOutputPerLevel=.10f;
  [Range(0,.09f)]public float infantryReductionPerLevel=.06f;
  [Range(0,1)]public float heavyOutputPerLevel=.06f;
  [Range(0,.09f)]public float heavyReductionPerLevel=.04f;
 }
}
