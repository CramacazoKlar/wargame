using System.Collections.Generic;
using UnityEngine;
namespace CampaignDemo {
 [DefaultExecutionOrder(180)]
 public sealed class BattleAIController:MonoBehaviour {
  public BattleWorld world;
  public AIBehaviorProfile profile;
  public AITaskReservations reservations;public AIFactionIntel intel;
  public bool autoToggleOnPlayerSwitch=true;
  readonly Dictionary<string,string> trackedTasks=new Dictionary<string,string>();
  public string playerId="B";
  public bool aiEnabled=true;
  public bool pauseWhenPlayerControlled=true;
  public bool assignUnconfiguredUnits=true;
  [Min(.02f)] public float decisionInterval=.25f;
  public AITaskDefinition defaultGuard,defaultArtillery;
  public BattleInformationSource information;
  public BattleCommandService commands;
  float accumulator;
  readonly List<AIUnitOrder> orders=new List<AIUnitOrder>();
  public IReadOnlyList<AIUnitOrder> Orders {get{return orders;}}
  public int ActivateTask(string key){Refresh();int activated=0;foreach(var order in orders)if(!string.IsNullOrEmpty(key)&&order.taskKey==key&&order.GetComponent<BattleUnit>().Alive&&CanRun(order)&&order.life==AITaskLife.Active){bool ready=true;foreach(var required in order.requiredUnits)if(required==null||required.ownerId!=playerId||!required.Alive){ready=false;break;}if(ready){order.Activate();activated++;}}return activated;}
  void Awake(){if(world==null)world=GetComponent<BattleWorld>();}
  void Start(){if(world!=null){world.PlayerChanged+=PlayerControlChanged;PlayerControlChanged();}}
  void OnDestroy(){if(world!=null)world.PlayerChanged-=PlayerControlChanged;}
  public AIBehaviorProfile ProfileFor(AIUnitOrder order){return order!=null&&order.profileOverride!=null?order.profileOverride:profile;}
  int CapabilitySignature(AIBehaviorProfile p){return p==null?0:(p.patrol?1:0)+(p.reinforcement?2:0)+(p.artillery?4:0)+(p.recovery?8:0)+(p.supply?16:0);}
  public void AssignTask(AIUnitOrder order,AITaskDefinition task){CancelTask(order,"任务改派");order.ResetTask(task);}
  public bool CanRun(AIUnitOrder order){var p=ProfileFor(order);return order.task.kind==AITaskKind.Guard||p!=null&&p.Allows(order.task.kind);}
  void PlayerControlChanged(){if(!autoToggleOnPlayerSwitch)return;aiEnabled=world.currentPlayer!=playerId;if(!aiEnabled){Refresh();foreach(var order in orders){var unit=order.GetComponent<BattleUnit>();if(unit!=null&&unit.Alive&&unit.Motor!=null&&unit.Motor.IsMoving&&order.SubmittedCommands>0)commands?.Submit(new BattleCommand{kind=BattleCommandKind.StopMovement,playerId=playerId,unitId=unit.unitId});reservations?.Release(order.RuntimeTaskId);order.ResumeTask();}}}
  void Update(){if(!aiEnabled||world==null||!world.Initialized||world.SimulationEnded)return;accumulator+=Time.deltaTime;float interval=profile!=null&&profile.useProfileTiming?profile.decisionSeconds:decisionInterval;if(accumulator<Mathf.Max(.02f,interval))return;float dt=accumulator;accumulator=0;Tick(dt);}
  public void Refresh(){
   if(world==null||world.units==null)return;if(information==null)information=world.GetComponent<BattleInformationSource>();if(commands==null)commands=world.GetComponent<BattleCommandService>();
   if(reservations==null)reservations=GetComponent<AITaskReservations>();if(reservations==null)reservations=gameObject.AddComponent<AITaskReservations>();if(intel==null)intel=GetComponent<AIFactionIntel>();if(intel==null)intel=gameObject.AddComponent<AIFactionIntel>();intel.controller=this;
   var seen=new HashSet<string>();orders.Clear();foreach(var unit in world.units){if(unit==null||unit.ownerId!=playerId)continue;var order=unit.GetComponent<AIUnitOrder>();if((order==null||order.task==null)&&assignUnconfiguredUnits){if(order==null)order=unit.gameObject.AddComponent<AIUnitOrder>();order.task=unit.combatDefinition.role==UnitRole.Artillery&&profile!=null&&profile.artillery?defaultArtillery:defaultGuard;}if(order!=null&&order.task!=null){order.reservations=reservations;orders.Add(order);seen.Add(unit.unitId);trackedTasks[unit.unitId]=order.RuntimeTaskId;}}
   foreach(var id in new List<string>(trackedTasks.Keys))if(!seen.Contains(id)){reservations.Release(trackedTasks[id]);trackedTasks.Remove(id);}}
  public void Tick(float dt){
   if(!aiEnabled||world==null||!world.Initialized||world.SimulationEnded||(pauseWhenPlayerControlled&&world.currentPlayer==playerId))return;
   Refresh();if(information==null||commands==null)return;intel.Observe(Time.time);
   // Only observer-filtered contacts reach tactical decisions.
   var contacts=new List<ArmyInfo>();foreach(var id in information.GetVisibleArmyIds(playerId)){var contact=information.GetArmy(playerId,id);if(contact!=null&&!contact.own&&contact.visible&&contact.alive&&contact.onMap)contacts.Add(contact);}
   contacts.Sort((a,b)=>string.CompareOrdinal(a.unitId,b.unitId));
   foreach(var order in orders){var unit=order.GetComponent<BattleUnit>();if(!unit.Alive){if(order.life!=AITaskLife.Cancelled&&order.life!=AITaskLife.Failed)CancelTask(order,"单位死亡");continue;}bool missing=false;foreach(var required in order.requiredUnits)if(required==null||required.ownerId!=playerId||!required.Alive){missing=true;break;}if(missing){if(order.task.missingMemberAction==AIMissingMemberAction.Cancel)CancelTask(order,"必需单位缺员");else{reservations.Release(order.RuntimeTaskId);if(unit.Motor!=null&&unit.Motor.IsMoving)commands.Submit(new BattleCommand{kind=BattleCommandKind.StopMovement,playerId=playerId,unitId=unit.unitId});order.State=AIUnitState.Holding;order.finishReason="等待指定单位";}continue;}int signature=CapabilitySignature(ProfileFor(order));if(order.capabilitySignature>=0&&order.capabilitySignature!=signature&&order.life==AITaskLife.Active){CancelTask(order,"能力配置变更");order.ResetTask(order.task);}order.capabilitySignature=signature;if(!CanRun(order)){if(order.life!=AITaskLife.Cancelled)CancelTask(order,"能力未开启");continue;}if(order.life==AITaskLife.Cancelled&&order.finishReason=="能力未开启")order.ResetTask(order.task);if(order.life==AITaskLife.Cancelled||order.life==AITaskLife.Failed){order.State=order.life==AITaskLife.Failed?AIUnitState.Failed:AIUnitState.Holding;continue;}if(order.life==AITaskLife.Completed){if(order.task.arrivalAction==AIArrivalAction.Hold&&unit.OnMap){if(unit.Motor!=null&&unit.Motor.IsMoving)commands.Submit(new BattleCommand{kind=BattleCommandKind.StopMovement,playerId=playerId,unitId=unit.unitId});order.State=AIUnitState.Holding;}continue;}if(!reservations.ClaimUnit(order.RuntimeTaskId,unit.unitId)){order.State=AIUnitState.Failed;order.LastCommandReason="单位已被其他任务预约";continue;}var p=ProfileFor(order);var responses=order.GetComponents<AIUnitResponse>();System.Array.Sort(responses,(a,b)=>b.Priority.CompareTo(a.Priority));bool handled=false;foreach(var response in responses)if(response.isActiveAndEnabled&&p!=null&&((response is AIRecoveryResponse&&p.recovery)||(response is AISupplyResponse&&p.supply))&&response.TickResponse(this,order,contacts,Mathf.Max(0,dt))){if(order.life==AITaskLife.Active&&order.State!=AIUnitState.Failed)order.State=AIUnitState.Responding;handled=true;break;}if(!handled)Step(order,contacts,Mathf.Max(0,dt));}
  }
  public bool IssueCommand(AIUnitOrder order,BattleCommand command){return Send(order,command);}
  bool Send(AIUnitOrder order,BattleCommand command){command.playerId=playerId;command.unitId=order.GetComponent<BattleUnit>().unitId;order.SubmittedCommands++;var result=commands.Submit(command);order.LastCommandReason=result.accepted?"":result.reason;if(!result.accepted){var p=ProfileFor(order);order.retry=p!=null&&p.useProfileTiming?p.retrySeconds:order.task.retrySeconds;order.State=AIUnitState.Failed;order.failures++;if(order.failures>=Mathf.Max(1,p==null?3:p.maximumFailedCommands)){CancelTask(order,"命令连续失败："+result.reason);order.life=AITaskLife.Failed;}}if(result.accepted)order.failures=0;return result.accepted;}
  public void CancelTask(AIUnitOrder order,string reason){if(order==null)return;var u=order.GetComponent<BattleUnit>();if(u!=null&&u.Alive&&u.ownerId==playerId&&commands!=null){if(u.Motor!=null&&u.Motor.IsMoving)commands.Submit(new BattleCommand{kind=BattleCommandKind.StopMovement,playerId=playerId,unitId=u.unitId});if(u.State==ArmyBattleState.Repairing)commands.Submit(new BattleCommand{kind=BattleCommandKind.ToggleRepair,playerId=playerId,unitId=u.unitId});var battery=u.GetComponent<ArtilleryBattery>();if(battery!=null&&battery.DeploymentRemaining>0)commands.Submit(new BattleCommand{kind=BattleCommandKind.ToggleArtilleryDeployment,playerId=playerId,unitId=u.unitId});if(u.Working)commands.Submit(new BattleCommand{kind=BattleCommandKind.CancelEngineering,playerId=playerId,unitId=u.unitId});}foreach(var response in order.GetComponents<AIUnitResponse>())response.CancelResponse();reservations?.Release(order.RuntimeTaskId);order.life=AITaskLife.Cancelled;order.finishReason=reason;order.State=AIUnitState.Holding;}
  public void CompleteTask(AIUnitOrder order,string reason){reservations?.Release(order.RuntimeTaskId);order.life=AITaskLife.Completed;order.finishReason=reason;order.State=AIUnitState.Holding;}
  void Arrive(AIUnitOrder order,BattleUnit unit){if(unit.Motor!=null&&unit.Motor.IsMoving&&!Send(order,new BattleCommand{kind=BattleCommandKind.StopMovement}))return;var city=order.arrivalCity;if(order.task.arrivalAction==AIArrivalAction.EnterCity){if(city==null){CancelTask(order,"未指定入城目标");return;}if(unit.Point==city&&(unit.State==ArmyBattleState.InsidePoint||unit.State==ArmyBattleState.Repairing)){CompleteTask(order,"已进入城市");return;}if(unit.State==ArmyBattleState.EnteringPoint)return;Send(order,new BattleCommand{kind=BattleCommandKind.EnterPoint,pointId=city.pointId});return;}if(order.task.arrivalAction==AIArrivalAction.OccupyPoint){if(city==null||Vector2.Distance(unit.transform.position,city.transform.position)>city.captureRadius){CancelTask(order,"占领目标为空或终点不在占领范围");return;}if(city.ownerId!=playerId){order.State=AIUnitState.Holding;return;}}CompleteTask(order,"到达动作完成");}
  void Step(AIUnitOrder order,List<ArmyInfo> contacts,float dt){
   var unit=order.GetComponent<BattleUnit>();var own=information.GetArmy(playerId,unit.unitId);
   if(order.task.arrivalAction==AIArrivalAction.EnterCity&&order.task.waypoints.Length>0&&order.WaypointIndex>=order.task.waypoints.Length&&unit.Point==order.arrivalCity&&(unit.State==ArmyBattleState.InsidePoint||unit.State==ArmyBattleState.Repairing)){Arrive(order,unit);return;}
   if(own==null||!own.alive||!own.onMap||(unit.Working&&!(order.task.kind==AITaskKind.Artillery&&unit.GetComponent<ArtilleryBattery>()!=null&&unit.GetComponent<ArtilleryBattery>().DeploymentRemaining>0))){order.State=AIUnitState.Unavailable;order.routeIssued=false;return;}
   order.retry=Mathf.Max(0,order.retry-dt);if(order.retry>0)return;
   var task=order.task;var p=ProfileFor(order);if(task.requireActivation&&!order.activated){order.State=AIUnitState.Waiting;return;}
   if(task.kind==AITaskKind.Artillery){Artillery(order,own,contacts);return;}
   ArmyInfo threat=null;float nearest=float.MaxValue;float nearestObserved=float.MaxValue;float range=own.attackRadius??0;
   foreach(var contact in contacts){float distance=Vector2.Distance(own.position,contact.position);nearestObserved=Mathf.Min(nearestObserved,distance);if(distance<=range&&distance<nearest){nearest=distance;threat=contact;}}
   if(threat!=null){
    order.quietTime=0;order.contactTime+=dt;
    if(order.contactTime<(p!=null&&p.useProfileTiming?p.reactionSeconds:task.reactionSeconds)){order.State=AIUnitState.Reacting;order.DecisionReason="敌军进入射程，等待反应时间";return;}
    if(own.isMoving==true){if(!Send(order,new BattleCommand{kind=BattleCommandKind.StopMovement}))return;order.routeIssued=false;}
    order.State=AIUnitState.Engaging;order.DecisionReason="射程内交战，普通火力由战斗系统执行";return;
   }
   bool wasContact=order.contactTime>0;order.contactTime=0;
   if(wasContact||order.State==AIUnitState.Engaging){order.quietTime+=dt;if(order.quietTime<(p!=null&&p.useProfileTiming?p.clearContactSeconds:task.clearContactSeconds)){order.State=AIUnitState.Engaging;order.DecisionReason="射程内交战，普通火力由战斗系统执行";return;}order.quietTime=0;order.routeIssued=false;}
   if(task.kind==AITaskKind.Guard||task.waypoints==null||task.waypoints.Length==0){order.DecisionReason=nearestObserved<float.MaxValue?"守备：最近可见敌军距离 "+nearestObserved.ToString("0.00")+"，攻击射程 "+range.ToString("0.00")+"；不主动追击":"守备：没有可见敌军，不主动移动";if(own.isMoving==true&&!Send(order,new BattleCommand{kind=BattleCommandKind.StopMovement}))return;order.State=AIUnitState.Holding;return;}
   if(order.WaypointIndex>=task.waypoints.Length){if(task.kind==AITaskKind.Patrol)order.WaypointIndex=0;else{Arrive(order,unit);return;}}
   Vector3 destination=task.waypoints[order.WaypointIndex];
   if(Vector2.Distance(own.position,destination)<=task.arrivalDistance){order.WaypointIndex++;order.routeIssued=false;order.State=AIUnitState.Holding;if(order.WaypointIndex>=task.waypoints.Length&&task.kind!=AITaskKind.Patrol)Arrive(order,unit);return;}
   if(order.routeIssued&&own.isMoving==true){order.State=AIUnitState.Marching;return;}
   if(!own.canMove){order.State=AIUnitState.Unavailable;return;}
   order.routeIssued=Send(order,new BattleCommand{kind=task.useRoad?BattleCommandKind.RoadDestination:BattleCommandKind.FreeDestination,destination=destination});if(order.routeIssued)order.State=AIUnitState.Marching;
  }
  void Artillery(AIUnitOrder order,ArmyInfo own,List<ArmyInfo> contacts){
   var battery=order.GetComponent<ArtilleryBattery>();if(battery==null||battery.definition==null){order.State=AIUnitState.Unavailable;order.LastCommandReason="炮兵任务需要ArtilleryBattery及配置";return;}
   if(!battery.Deployed){if(battery.DeploymentRemaining>0){order.State=AIUnitState.Deploying;return;}if(Send(order,new BattleCommand{kind=BattleCommandKind.ToggleArtilleryDeployment}))order.State=AIUnitState.Deploying;return;}
   order.State=AIUnitState.FireSupport;if(!battery.Ready)return;
   foreach(var contact in contacts){if(Vector2.Distance(own.position,contact.position)>battery.definition.range)continue;
    bool unsafeTarget=false;foreach(var id in information.GetVisibleArmyIds(playerId)){var friend=information.GetArmy(playerId,id);if(friend!=null&&friend.own&&friend.onMap&&Vector2.Distance(friend.position,contact.position)<=battery.definition.blastRadius){unsafeTarget=true;break;}}
    if(unsafeTarget)continue;Send(order,new BattleCommand{kind=BattleCommandKind.ArtilleryFire,destination=contact.position});return;
   }
  }
 }
}









