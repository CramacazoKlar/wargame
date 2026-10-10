using UnityEngine;
namespace CampaignDemo {
 [CreateAssetMenu(menuName="Campaign Demo/AI Behavior Profile")]
 public sealed class AIBehaviorProfile:ScriptableObject {
  [Header("可选能力；基础配置全部关闭")]
  public bool patrol,reinforcement,artillery,cityRelief,recovery,supply,enemyMemory;
  [Header("数字参数；也可以使用任务自身参数")]
  public bool useProfileTiming=true;
  [Min(.02f)] public float decisionSeconds=.25f;
  [Min(0)] public float reactionSeconds=1,clearContactSeconds=1;
  [Min(.1f)] public float retrySeconds=2,enemyMemorySeconds=60;
  [Min(1)] public int maximumFailedCommands=3;
  public bool Allows(AITaskKind kind){return kind==AITaskKind.Guard||kind==AITaskKind.Patrol&&patrol||kind==AITaskKind.Reinforce&&reinforcement||kind==AITaskKind.Artillery&&artillery;}
 }
}
