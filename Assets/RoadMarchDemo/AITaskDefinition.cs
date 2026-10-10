using UnityEngine;
namespace CampaignDemo {
 public enum AIArrivalAction { Stop,Hold,OccupyPoint,EnterCity }
 public enum AIMissingMemberAction { Hold,Cancel }
 public enum AITaskKind { Guard,Patrol,Reinforce,Artillery }
 [CreateAssetMenu(menuName="Campaign Demo/AI Task")]
 public sealed class AITaskDefinition:ScriptableObject {
  public AITaskKind kind;
  public bool requireActivation;
  public AIArrivalAction arrivalAction=AIArrivalAction.Hold;
  public AIMissingMemberAction missingMemberAction=AIMissingMemberAction.Cancel;
  public bool useRoad=true;
  public Vector2[] waypoints=new Vector2[0];
  [Min(.01f)] public float arrivalDistance=.08f;
  [Min(0)] public float reactionSeconds=1;
  [Min(0)] public float clearContactSeconds=1;
  [Min(.1f)] public float retrySeconds=2;
 }
}

