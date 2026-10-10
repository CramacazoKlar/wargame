using System.Collections.Generic;
using UnityEngine;
namespace CampaignDemo {
 [RequireComponent(typeof(TransportTruck))]
 public sealed class AISupplyResponse:AIUnitResponse {
  public RecoveryPoint sourceCity,destinationCity;
  public ResourceCollector sourceCollector;
  public ResourceDepot sourceDepot,destinationDepot;
  public RecoveryPoint[] backupSourceCities=new RecoveryPoint[0],backupDestinationCities=new RecoveryPoint[0];
  public bool useRoad=true,repeat=true,waitWhenThreatened=true;
  [Min(0)] public float visibleThreatRadius=1.5f,reactionSeconds=1;
  [Min(0)] public float loadLimit;
  public int responsePriority=50;
  public override int Priority {get{return responsePriority;}}
  enum Phase { Source,Load,Destination,Unload,Complete }
  [ContextMenu("Restart supply task")] public void RestartTask(){var order=GetComponent<AIUnitOrder>();if(order!=null&&order.life!=AITaskLife.Active)order.ResetTask(order.task);phase=Phase.Source;started=false;currentSource=currentDestination=null;retry=threatTime=0;}
  Phase phase;UnityEngine.Object currentSource,currentDestination;float threatTime;bool started;
  bool Valid(BattleAIController ai,UnityEngine.Object storage){if(storage==null)return false;var campaign=ai.world.campaign;if(storage is RecoveryPoint p)return p.ownerId==ai.playerId&&p.kind==FacilityKind.RecoveryPoint;if(storage is ResourceCollector c)return c.ownerId==ai.playerId&&c.Alive;if(storage is ResourceDepot d)return !d.Removed&&(d.ownerId==ai.playerId||(d.resourcePile&&(!ai.world.fogEnabled||ai.world.VisibleAt(ai.playerId,d.transform.position))));return false;}
  UnityEngine.Object PickSource(BattleAIController ai){var campaign=ai.world.campaign;UnityEngine.Object[] choices={sourceCollector,sourceDepot,sourceCity};foreach(var c in choices)if(Valid(ai,c)&&campaign.Stock(c)>0)return c;foreach(var c in backupSourceCities)if(Valid(ai,c)&&campaign.Stock(c)>0)return c;return null;}
  UnityEngine.Object PickDestination(BattleAIController ai){UnityEngine.Object[] choices={destinationDepot,destinationCity};foreach(var c in choices)if(Valid(ai,c)&&c!=currentSource)return c;foreach(var c in backupDestinationCities)if(Valid(ai,c)&&c!=currentSource)return c;return null;}
  BattleCommand Transfer(UnityEngine.Object storage,bool load,float amount){var c=new BattleCommand{kind=load?BattleCommandKind.LoadResources:BattleCommandKind.UnloadResources,amount=amount};if(storage is RecoveryPoint p)c.pointId=p.pointId;else if(storage is ResourceCollector collector)c.collectorId=collector.collectorId;else if(storage is ResourceDepot d)c.depotId=d.depotId;return c;}
  public override void CancelResponse(){base.CancelResponse();phase=Phase.Source;started=false;currentSource=currentDestination=null;threatTime=0;}
  public override bool TickResponse(BattleAIController ai,AIUnitOrder order,List<ArmyInfo> contacts,float dt){
   var truck=GetComponent<TransportTruck>();var unit=truck.Unit;var own=ai.information.GetArmy(ai.playerId,unit.unitId);if(own==null||!own.alive)return false;
   if(!started){phase=truck.Cargo>0?Phase.Destination:Phase.Source;started=true;Release(order);if(own.isMoving==true)Send(ai,order,new BattleCommand{kind=BattleCommandKind.StopMovement});}
   if(phase==Phase.Complete){ResponseState="运输任务完成";return true;}
   retry=Mathf.Max(0,retry-dt);if(retry>0||!own.onMap||unit.Working)return true;
   if(waitWhenThreatened){bool threatened=false;foreach(var c in contacts)if(Vector2.Distance(c.position,own.position)<=visibleThreatRadius){threatened=true;break;}if(threatened){threatTime+=dt;if(threatTime>=reactionSeconds){if(own.isMoving==true)Send(ai,order,new BattleCommand{kind=BattleCommandKind.StopMovement});ResponseState="等待可见威胁解除或护送";return true;}}else threatTime=0;}
   if(phase==Phase.Source||phase==Phase.Load){
    if(!Valid(ai,currentSource)||ai.world.campaign.Stock(currentSource)<=0){var next=PickSource(ai);if(next!=currentSource&&own.isMoving==true)Send(ai,order,new BattleCommand{kind=BattleCommandKind.StopMovement});currentSource=next;phase=Phase.Source;}
    if(currentSource==null){ResponseState="等待可用货源";retry=retrySeconds;return true;}
    if(Vector2.Distance(own.position,ai.world.campaign.Position(currentSource))>ai.world.campaign.rules.transportInteractionRadius){ResponseState="前往货源";Move(ai,order,own,ai.world.campaign.Position(currentSource),useRoad);return true;}
    if(own.isMoving==true&&!Send(ai,order,new BattleCommand{kind=BattleCommandKind.StopMovement}))return true;
    float amount=ai.world.campaign.TransferMaximum(truck,currentSource,true);if(loadLimit>0)amount=Mathf.Min(amount,loadLimit);ResponseState="装载资源";
    string storageKey=currentSource.GetInstanceID().ToString();amount=Mathf.Min(amount,Mathf.Max(0,ai.world.campaign.Stock(currentSource)-ai.reservations.ReservedAt(storageKey)));if(amount<=0){ResponseState="货源已被预约，等待";retry=retrySeconds;return true;}if(!ai.reservations.Reserve(order.RuntimeTaskId,storageKey,amount,ai.world.campaign.Stock(currentSource)))return true;bool loaded=Send(ai,order,Transfer(currentSource,true,amount));ai.reservations.ReleaseResource(order.RuntimeTaskId,storageKey);if(!loaded)return true;phase=Phase.Destination;
   }
   if(phase==Phase.Destination||phase==Phase.Unload){
    if(!Valid(ai,currentDestination)||currentDestination==currentSource){var next=PickDestination(ai);if(next!=currentDestination&&own.isMoving==true)Send(ai,order,new BattleCommand{kind=BattleCommandKind.StopMovement});currentDestination=next;phase=Phase.Destination;}
    if(currentDestination==null){ai.reservations.SetIncoming(order.RuntimeTaskId,"",0);ResponseState="没有己方卸货目标，保留货物";retry=retrySeconds;return true;}
    ai.reservations.SetIncoming(order.RuntimeTaskId,currentDestination.GetInstanceID().ToString(),truck.Cargo);
    if(Vector2.Distance(own.position,ai.world.campaign.Position(currentDestination))>ai.world.campaign.rules.transportInteractionRadius){ResponseState="前往卸货地点";Move(ai,order,own,ai.world.campaign.Position(currentDestination),useRoad);return true;}
    if(own.isMoving==true&&!Send(ai,order,new BattleCommand{kind=BattleCommandKind.StopMovement}))return true;
    float amount=ai.world.campaign.TransferMaximum(truck,currentDestination,false);ResponseState="卸载资源";if(amount>0&&!Send(ai,order,Transfer(currentDestination,false,amount)))return true;
    ai.reservations.SetIncoming(order.RuntimeTaskId,"",0);phase=repeat?Phase.Source:Phase.Complete;if(!repeat)ai.CompleteTask(order,"单趟运输完成");retry=retrySeconds;
   }
   return true;
  }
 }
}





