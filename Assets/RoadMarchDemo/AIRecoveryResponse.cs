using System.Collections.Generic;
using UnityEngine;
namespace CampaignDemo {
 public sealed class AIRecoveryResponse:AIUnitResponse {
  [Range(0,1)] public float seekRecoveryBelow=.4f;
  [Range(0,1)] public float returnAbove=.9f;
  [Min(0)] public float visibleDangerRadius=2;
  public bool useRoad=true,keepLastCityGuard=true,returnToGuardPosition=true;
  public RecoveryPoint preferredCity;
  public int responsePriority=100;
  public override int Priority {get{return responsePriority;}}
  enum Phase { Idle,Travelling,Entering,Repairing,Exiting,Returning }
  Phase phase;RecoveryPoint city;Vector3 startPosition;
  public bool IsRecovering {get{return phase!=Phase.Idle;}}
  bool Safe(BattleAIController ai,RecoveryPoint point,List<ArmyInfo> contacts){if(point==null||point.ownerId!=ai.playerId||point.kind!=FacilityKind.RecoveryPoint||(point.resourcePerPerson>0&&point.resourceStock<=0))return false;foreach(var contact in contacts)if(Vector2.Distance(contact.position,point.transform.position)<=visibleDangerRadius)return false;return true;}
  bool LastGuard(BattleAIController ai,BattleUnit unit){if(!keepLastCityGuard)return false;foreach(var point in ai.world.points){if(point==null||point.ownerId!=ai.playerId||Vector2.Distance(unit.transform.position,point.transform.position)>point.captureRadius)continue;bool other=false;foreach(var u in ai.world.units)if(u!=null&&u!=unit&&u.ownerId==ai.playerId&&u.OnMap&&ai.world.campaign.CanAttack(u)&&Vector2.Distance(u.transform.position,point.transform.position)<=point.captureRadius){other=true;break;}if(!other)return true;}return false;}
  public override void CancelResponse(){base.CancelResponse();phase=Phase.Idle;city=null;}
  public override bool TickResponse(BattleAIController ai,AIUnitOrder order,List<ArmyInfo> contacts,float dt){
   var unit=GetComponent<BattleUnit>();var own=ai.information.GetArmy(ai.playerId,unit.unitId);if(own==null||!own.alive||unit.Carrier!=null){phase=Phase.Idle;ResponseState="不可执行恢复";return false;}
   retry=Mathf.Max(0,retry-dt);if(retry>0)return phase!=Phase.Idle;
   if(phase==Phase.Idle){
    if(unit.Fraction>seekRecoveryBelow||unit.Working||!own.onMap)return false;
    if(LastGuard(ai,unit)){ResponseState="保持最后一支城外守军";return false;}
    city=Safe(ai,preferredCity,contacts)?preferredCity:null;float best=city!=null?Vector2.Distance(unit.transform.position,city.transform.position):float.MaxValue;
    if(city==null)foreach(var p in ai.world.points)if(Safe(ai,p,contacts)){float distance=Vector2.Distance(unit.transform.position,p.transform.position);if(distance<best){city=p;best=distance;}}
    if(city==null){ResponseState="没有安全且有资源的己方城市";retry=retrySeconds;return false;}
    startPosition=own.position;phase=Phase.Travelling;order.ResumeTask();if(own.isMoving==true&&!Send(ai,order,new BattleCommand{kind=BattleCommandKind.StopMovement}))return true;
   }
   if(city==null||city.ownerId!=ai.playerId){if(own.isMoving==true)Send(ai,order,new BattleCommand{kind=BattleCommandKind.StopMovement});phase=Phase.Idle;ResponseState="目标城市易主，取消恢复";retry=retrySeconds;Release(order);return false;}
   if(phase==Phase.Travelling){
    ResponseState="前往城市恢复";var battery=GetComponent<ArtilleryBattery>();if(battery!=null&&(battery.Deployed||battery.DeploymentRemaining>0)){if(battery.DeploymentRemaining<=0)Send(ai,order,new BattleCommand{kind=BattleCommandKind.ToggleArtilleryDeployment});return true;}
    if(unit.Working)return true;
    if(Vector2.Distance(own.position,city.transform.position)<=UnitRanges.Interaction(unit)){if(own.isMoving==true&&!Send(ai,order,new BattleCommand{kind=BattleCommandKind.StopMovement}))return true;if(Send(ai,order,new BattleCommand{kind=BattleCommandKind.EnterPoint,pointId=city.pointId}))phase=Phase.Entering;return true;}
    if(!Safe(ai,city,contacts)){if(own.isMoving==true)Send(ai,order,new BattleCommand{kind=BattleCommandKind.StopMovement});phase=Phase.Idle;ResponseState="城市出现可见威胁，等待重新选择";retry=retrySeconds;Release(order);return false;}
    Move(ai,order,own,city.transform.position,useRoad);return true;
   }
   if(phase==Phase.Entering){ResponseState="等待入城";if(own.state==ArmyBattleState.InsidePoint)phase=Phase.Repairing;else return true;}
   if(phase==Phase.Repairing){
    ResponseState="消耗城市资源恢复";
    if(unit.Fraction>=Mathf.Max(seekRecoveryBelow,returnAbove)||(city.resourcePerPerson>0&&city.resourceStock<=0)){if(Send(ai,order,new BattleCommand{kind=BattleCommandKind.ExitPoint}))phase=Phase.Exiting;return true;}
    if(own.state==ArmyBattleState.InsidePoint)Send(ai,order,new BattleCommand{kind=BattleCommandKind.ToggleRepair});return true;
   }
   if(phase==Phase.Exiting){ResponseState="等待出城";if(!own.onMap)return true;phase=Phase.Returning;Release(order);}
   if(phase==Phase.Returning){
    ResponseState="返回原任务";
    if(returnToGuardPosition&&order.task.kind==AITaskKind.Guard&&Vector2.Distance(own.position,startPosition)>order.task.arrivalDistance){Move(ai,order,own,startPosition,useRoad);return true;}
    phase=Phase.Idle;retry=retrySeconds;Release(order);return false;
   }
   return true;
  }
 }
}



