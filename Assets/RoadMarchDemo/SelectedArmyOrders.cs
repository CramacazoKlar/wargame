using System.Collections.Generic;
using UnityEngine;
namespace CampaignDemo {
 public static class SelectedArmyOrders {
  public static CommandResult Execute(BattleWorld world,BattleCommand command){
   if(command.unitIds==null||command.unitIds.Count==0||command.unitIds.Count>2048)return CommandResult.No("没有选中部队或数量超限");
   var selected=new List<BattleUnit>();var ids=new HashSet<string>();foreach(var id in command.unitIds){if(!ids.Add(id))continue;BattleUnit unit=null;foreach(var candidate in world.units)if(candidate!=null&&candidate.unitId==id){unit=candidate;break;}if(unit==null||unit.ownerId!=command.playerId)return CommandResult.No("所选部队不存在或不属于该玩家");selected.Add(unit);}
   if(command.kind==BattleCommandKind.StopSelected){foreach(var unit in selected){if(LocalPrototypeOrders.EnabledFor(world)){var linked=world.GetComponent<LocalCoordinatedOrders>();linked?.CancelRotation(unit);linked?.CancelDefense(unit);world.GetComponent<LocalPrototypeOrders>()?.CancelUnit(unit,true);world.GetComponent<UnitContextOrders>()?.CancelEntriesFor(unit,false);world.GetComponent<TacticalTargetOrders>()?.CancelFor(unit,false);FacilityRecoveryQueue.For(world)?.Cancel(unit);world.campaign?.lineWorks.Cancel(unit);unit.RevealForest();}unit.Motor?.Stop();var focus=unit.GetComponent<CampaignUnit>();if(focus!=null)focus.fortTarget=null;unit.GetComponent<TransportTruck>()?.StopAutomatic();}return CommandResult.Yes();}
   if(LocalTacticalBattle.EnabledFor(world)&&world.campaign!=null){bool cancelled=false;foreach(var unit in selected)if(world.campaign.lineWorks.Working(unit)){world.campaign.lineWorks.Cancel(unit);cancelled=true;}if(cancelled)return CommandResult.No("已取消施工；请再次下达新命令");}

   var offset=command.destination;offset.z=0;if(!CombinedArmsWorld.Finite(offset)||offset.sqrMagnitude<.00000001f||offset.sqrMagnitude>.0625001f)return CommandResult.No("微调距离无效");
   var map=world.setup.freeMarchMap;if(map==null)return CommandResult.No("缺少地形配置");var paths=new List<List<RoadPoint>>();float sharedSpeed=float.MaxValue;
   foreach(var unit in selected){var free=unit.GetComponent<ArmyFreeMarchMover>();if(!unit.CanMove||unit.Motor==null||!unit.Motor.isActiveAndEnabled||unit.Motor.MovementLocked||free==null||!free.isActiveAndEnabled)return CommandResult.No("所选部队当前不能微调");var from=unit.transform.position;var to=from+offset;var route=new List<RoadPoint>{new RoadPoint(from.x,from.y),new RoadPoint(to.x,to.y)};
    if(!free.CanTraverse(map,route[0],route[1]))return CommandResult.No("微调经过不可通行地形，本次整组微调未执行");var split=map.SplitRoute(route);if(split==null||split.Count<2)return CommandResult.No("微调路线无效");
    float speed=float.MaxValue;for(int i=1;i<split.Count;i++)speed=Mathf.Min(speed,free.SpeedAt(map.TerrainAt(RoadPoint.Lerp(split[i-1],split[i],.5))));var carrier=unit.GetComponent<PersonnelCarrier>();if(carrier!=null)speed*=carrier.SpeedMultiplier;if(world.campaign!=null)speed*=world.campaign.MovementMultiplier(unit,from,to);var state=unit.GetComponent<CampaignUnit>();if(state!=null)speed=Mathf.Min(speed,Mathf.Max(0,state.desiredSpeed));sharedSpeed=Mathf.Min(sharedSpeed,speed);paths.Add(route);
   }
   if(sharedSpeed<=0||float.IsNaN(sharedSpeed)||float.IsInfinity(sharedSpeed))return CommandResult.No("所选部队的移动速度为零");
   for(int i=0;i<selected.Count;i++){var unit=selected[i];if(!unit.GetComponent<ArmyFreeMarchMover>().CommitRoute(paths[i],map))return CommandResult.No("微调路线被拒绝");unit.Motor.SetNudgeSpeed(sharedSpeed);}return CommandResult.Yes();
  }
 }
}
