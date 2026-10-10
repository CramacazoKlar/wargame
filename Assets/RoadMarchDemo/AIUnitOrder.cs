using UnityEngine;
namespace CampaignDemo {
 public enum AITaskLife { Active,Completed,Cancelled,Failed }
 public enum AIUnitState { Waiting,Holding,Marching,Reacting,Engaging,Deploying,FireSupport,Failed,Unavailable,Responding }
 [RequireComponent(typeof(BattleUnit))]
 public sealed class AIUnitOrder:MonoBehaviour {
  public AITaskDefinition task;
  public string taskKey;
  public AIBehaviorProfile profileOverride;
  public BattleUnit[] requiredUnits=new BattleUnit[0];
  public RecoveryPoint arrivalCity;
  public AITaskLife life;
  public string finishReason;
  internal AITaskReservations reservations;internal int failures,generation;internal int capabilitySignature=-1;
  public string RuntimeTaskId {get{return "unit-order-"+GetInstanceID()+"-"+generation;}}
  [SerializeField] AIUnitState state;
  [SerializeField] string lastCommandReason;
  public AIUnitState State {get{return state;} internal set{state=value;}}
  [SerializeField] string decisionReason;
  public string DecisionReason {get{return decisionReason;} internal set{decisionReason=value;}}
  public string LastCommandReason {get{return lastCommandReason;} internal set{lastCommandReason=value;}}
  public int WaypointIndex {get;internal set;}
  public int SubmittedCommands {get;internal set;}
  internal bool activated,routeIssued;
  internal float contactTime,quietTime,retry;
  public void Activate(){activated=true;}
  [ContextMenu("Restart configured AI task")] public void RestartConfiguredTask(){ResetTask(task);}
  public void ResumeTask(){routeIssued=false;contactTime=quietTime=0;State=AIUnitState.Waiting;}
  public void ResetTask(AITaskDefinition next){if(reservations!=null)reservations.Release(RuntimeTaskId);generation++;life=AITaskLife.Active;finishReason="";failures=0;task=next;WaypointIndex=0;activated=false;routeIssued=false;contactTime=quietTime=retry=0;State=AIUnitState.Waiting;LastCommandReason="";}
 }
}





