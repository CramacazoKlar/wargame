using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace CampaignDemo {
 [Serializable] public sealed class NetworkWaypoint { public Vector3 position;public string city,site; }
 public static class NetworkTacticalOrders {
  public const int Version=2;
  static string executingPlayer;static bool? executingStraight;
  public static bool Straight(BattleWorld world)=>executingStraight??world.setup.modes.CurrentMode==MouseCommandMode.FreeMarch;
  public static bool Executing=>executingPlayer!=null;
  public static bool Authority=>LocalPvpSession.Active==null||LocalPvpSession.Active.IsHost;
  public static string Player(BattleWorld world,string owner=null)=>executingPlayer??(LocalPvpSession.Active!=null&&Authority&&owner!=null?owner:world.currentPlayer);
  public static bool Forward(BattleWorld world,BattleUnit unit,string action,out bool result,BattleUnit target=null,RecoveryPoint city=null,IList<LocalPrototypeOrders.Waypoint> nodes=null,bool straight=false,Fortification fort=null,int style=0,Vector3 destination=default){
   result=false;if(LocalPvpSession.Active==null||Executing)return false;
   if(unit==null||unit.ownerId!=world.currentPlayer)return true;
   var command=new BattleCommand{kind=BattleCommandKind.TacticalOrder,playerId=world.currentPlayer,unitId=unit.unitId,tacticalAction=action,targetUnitId=target==null?null:target.unitId,pointId=city==null?null:city.pointId,enabled=straight||action!="route"&&world.setup.modes.CurrentMode==MouseCommandMode.FreeMarch,count=style,destination=destination,fortNetworkId=fort==null?null:LocalPvpReplication.FortId(fort)};
   if(nodes!=null)command.waypoints=nodes.Select(n=>new NetworkWaypoint{position=n.position,city=n.city==null?null:n.city.pointId,site=n.site==null?null:n.site.siteId}).ToList();
   var reply=world.GetComponent<BattleCommandService>().Submit(command);result=reply.accepted;return true;
  }
  public static CommandResult AsPlayer(string player,Func<CommandResult> execute,bool? straight=null){var previous=executingPlayer;var previousStraight=executingStraight;executingPlayer=player;if(straight.HasValue)executingStraight=straight;try{return execute();}finally{executingPlayer=previous;executingStraight=previousStraight;}}
  public static CommandResult Execute(BattleWorld world,BattleCommand command){
   var unit=world.units.FirstOrDefault(u=>u!=null&&u.unitId==command.unitId&&u.ownerId==command.playerId&&u.Alive);
   if(unit==null)return CommandResult.No("单位不属于该玩家或已失效");
   var target=world.units.FirstOrDefault(u=>u!=null&&u.unitId==command.targetUnitId);
   var city=world.points.FirstOrDefault(p=>p!=null&&p.pointId==command.pointId);
   var nodes=new List<LocalPrototypeOrders.Waypoint>();
   if(command.waypoints!=null){if(command.waypoints.Count>32)return CommandResult.No("路线节点过多");foreach(var n in command.waypoints){if(n==null||!CombinedArmsWorld.Finite(n.position))return CommandResult.No("路线节点无效");var node=new LocalPrototypeOrders.Waypoint{position=n.position};if(!string.IsNullOrEmpty(n.city)){node.city=world.points.FirstOrDefault(p=>p!=null&&p.pointId==n.city&&p.ownerId==unit.ownerId);if(node.city==null)return CommandResult.No("城市不可用");}if(!string.IsNullOrEmpty(n.site)){node.site=world.campaign.logistics.sites.FirstOrDefault(s=>s!=null&&s.siteId==n.site);if(node.site==null)return CommandResult.No("资源点不可用");}nodes.Add(node);}}
   return AsPlayer(command.playerId,()=>{
    bool ok=false;var orders=world.GetComponent<LocalPrototypeOrders>();var linked=world.GetComponent<LocalCoordinatedOrders>();var context=world.GetComponent<UnitContextOrders>();var pursuit=world.GetComponent<TacticalTargetOrders>();
    switch(command.tacticalAction){
     case "assist":ok=world.campaign.lineWorks.Assist(unit,command.destination).accepted;break;
     case "patrol":ok=orders!=null&&orders.StartPatrol(unit,nodes);break;
     case "route":ok=orders!=null&&orders.StartRoute(unit,nodes,command.enabled);break;
     case "automatic":ok=orders!=null&&orders.StartAutomatic(unit.GetComponent<TransportTruck>());break;
     case "delivery":ok=orders!=null&&orders.StartDelivery(unit,nodes);break;
     case "rotation":ok=target!=null&&target.ownerId==unit.ownerId&&linked!=null&&linked.StartRotation(unit,target);break;
     case "defense":ok=target!=null&&target.ownerId==unit.ownerId&&linked!=null&&linked.LinkDefense(unit,target);break;
     case "pursuit":ok=pursuit!=null&&pursuit.QueuePursuit(unit,target);break;
     case "fort":var fort=world.GetComponent<CombinedArmsWorld>()?.fortifications.FirstOrDefault(f=>f!=null&&LocalPvpReplication.FortId(f)==command.fortNetworkId);ok=pursuit!=null&&pursuit.QueueFortPursuit(unit,fort);break;
     case "entry":ok=context!=null&&(city!=null&&city.ownerId==unit.ownerId||target!=null&&target.ownerId==unit.ownerId)&&context.QueueEntry(unit,city,target==null?null:target.GetComponent<PersonnelCarrier>());break;
     case "merge":ok=target!=null&&target.ownerId==unit.ownerId&&context!=null&&context.QueueMerge(unit,target);break;
     case "march":if(command.count>=0&&command.count<=2){var order=unit.GetComponent<LocalMarchOrder>()??unit.gameObject.AddComponent<LocalMarchOrder>();order.world=world;order.style=(LocalMarchStyle)command.count;ok=true;}break;
    }
    return ok?CommandResult.Yes():CommandResult.No("当前无法执行该战术命令");
   },command.enabled);
  }
 }
 public sealed class NetworkUnitPresentation:MonoBehaviour {public string task,coordination;public bool assigned,rotating,supporting;public string[] links;}
}
