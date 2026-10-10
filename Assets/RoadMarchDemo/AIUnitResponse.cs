using System.Collections.Generic;
using UnityEngine;
namespace CampaignDemo {
 [RequireComponent(typeof(BattleUnit),typeof(AIUnitOrder))]
 public abstract class AIUnitResponse:MonoBehaviour {
  [Min(.1f)] public float retrySeconds=2;
  [SerializeField] string responseState;
  public string ResponseState {get{return responseState;} protected set{responseState=value;}}
  public abstract int Priority {get;}
  protected float retry;
  protected bool Send(BattleAIController ai,AIUnitOrder order,BattleCommand command){bool accepted=ai.IssueCommand(order,command);if(!accepted)retry=retrySeconds;else order.State=AIUnitState.Responding;return accepted;}
  protected bool Move(BattleAIController ai,AIUnitOrder order,ArmyInfo own,Vector3 destination,bool road){if(own.isMoving==true)return true;return Send(ai,order,new BattleCommand{kind=road?BattleCommandKind.RoadDestination:BattleCommandKind.FreeDestination,destination=destination});}
  protected void Release(AIUnitOrder order){order.ResumeTask();}
  public virtual void CancelResponse(){retry=0;}
  public abstract bool TickResponse(BattleAIController ai,AIUnitOrder order,List<ArmyInfo> contacts,float dt);
 }
}


