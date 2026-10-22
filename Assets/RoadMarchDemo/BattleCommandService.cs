using System;
using System.Collections.Generic;
using UnityEngine;
namespace CampaignDemo
{
    public static class RoadOrderPlanner
    {
        public static List<RoadPoint> ToDestination(RoadNetwork network,Vector3 from,Vector3 to,List<Vector2> custom=null,bool reverse=false)
        {
            if(network==null)return null;var path=new List<RoadPoint>{new RoadPoint(from.x,from.y)};
            if(custom!=null&&custom.Count>=2){if(reverse){for(int i=custom.Count-1;i>=0;i--)RoadNetwork.Append(path,new RoadPoint(custom[i].x,custom[i].y));}else foreach(var p in custom)RoadNetwork.Append(path,new RoadPoint(p.x,p.y));RoadNetwork.Append(path,new RoadPoint(to.x,to.y));return path;}
            var a=network.Candidates(path[0],double.PositiveInfinity);var b=network.Candidates(new RoadPoint(to.x,to.y),double.PositiveInfinity);if(a.Count==0||b.Count==0||!network.Connect(a[0],b[0],double.PositiveInfinity,out var middle))return null;foreach(var p in middle)RoadNetwork.Append(path,p);RoadNetwork.Append(path,new RoadPoint(to.x,to.y));return path;
        }
        public static bool Validate(RoadNetwork network,IList<RoadPoint> path,bool finalApproach)
        {
            if(network==null||path==null||path.Count<2)return false;
            for(int i=1;i<path.Count;i++)
            {
                var a=network.Candidates(path[i-1],double.PositiveInfinity);var b=network.Candidates(path[i],double.PositiveInfinity);if(a.Count==0||b.Count==0)return false;
                if(a[0].Distance>.02){if(i!=1||RoadPoint.Distance(path[i],a[0].Position)>.02)return false;continue;}
                if(b[0].Distance>.02){if(!finalApproach||i!=path.Count-1||RoadPoint.Distance(path[i-1],b[0].Position)>.02)return false;continue;}
                bool onRoad=false;foreach(var from in network.Candidates(path[i-1],.02))foreach(var to in network.Candidates(path[i],.02))if(network.Connect(from,to,RoadPoint.Distance(path[i-1],path[i])+.04,out var connector)){onRoad=true;break;}if(!onRoad)return false;
            }
            return true;
        }
    }
    public static class BattleServices
    {
        public static void Ensure(BattleWorld world){if(world==null)return;var commands=world.GetComponent<BattleCommandService>();if(commands==null)commands=world.gameObject.AddComponent<BattleCommandService>();commands.world=world;commands.logistics=world.GetComponent<LogisticsWorld>();var info=world.GetComponent<BattleInformationSource>();if(info==null)info=world.gameObject.AddComponent<BattleInformationSource>();info.world=world;info.logistics=commands.logistics;}
    }
    public enum BattleCommandKind { RoadRoute,FreeRoute,RoadDestination,FreeDestination,Escape,EnterPoint,ToggleRepair,ExitPoint,ToggleForestHide,SetForestAttack,BuildFactory,BuildTruck,ReservedLegacy14,SetTransport,SaveTransportRoute,ArtilleryFire,ReservedLegacy16,BoardCarrier,UnloadCarrier,BuildFortification,DismantleFortification,CancelEngineering,StopMovement,SetAbsoluteSpeed,ReservedArtillerySetup,ReservedLegacy25,BuildResourceDepot,RemoveResourceDepot,LoadResources,UnloadResources,AttackFortification,ToggleFortRemoval,DismantleFortSection,BuildInfantry,CancelCityProduction,SetFortBuildLevel,StopSelected,NudgeSelected,StraightDestination,SplitFlank,SplitFeint,SplitInfiltrate,MergeDetachment,DeployRadar,TacticalOrder }
    [Serializable] public sealed class BattleCommand
    {
        public string tacticalAction,fortNetworkId;public List<NetworkWaypoint> waypoints;
        public UnitRole productionRole=UnitRole.Infantry;public int count=1;public string productionJobId;
        public List<string> unitIds;
        public int fortId;public string targetUnitId,depotId;public float amount;public List<Vector3> fortLine;
        public string playerId,unitId,pointId,siteId,sourceCityId,destinationCityId;
        public BattleCommandKind kind;public Vector3 destination;public List<RoadPoint> route;
        public bool enabled,preserveTransportAutomation,automatedProduction;public ArmyEchelon echelon=ArmyEchelon.Company;
    }
    public struct CommandResult
    {
        public bool accepted,pending;public string reason;
        public static CommandResult Yes(){return new CommandResult{accepted=true};}
        public static CommandResult No(string reason){return new CommandResult{reason=reason};}
    }
    public interface IBattleCommands {CommandResult Submit(BattleCommand command);}
    [DefaultExecutionOrder(105)]
    public sealed class BattleCommandService : MonoBehaviour,IBattleCommands
    {
        public BattleWorld world;public LogisticsWorld logistics;
        void Awake(){if(world==null)world=GetComponent<BattleWorld>();if(logistics==null)logistics=GetComponent<LogisticsWorld>();}
        BattleUnit Unit(string id){if(string.IsNullOrEmpty(id))return null;foreach(var u in world.units)if(u!=null&&u.unitId==id)return u;return null;}
        RecoveryPoint Point(string id){if(string.IsNullOrEmpty(id))return null;foreach(var p in world.points)if(p!=null&&p.pointId==id)return p;return null;}
        
        bool remoteCommand;
        public CommandResult SubmitRemote(BattleCommand c,string authenticatedPlayer)
        {
            if(LocalPvpSession.Active==null||!LocalPvpSession.Active.IsHost)return CommandResult.No("需要权威端处理");
            if(c==null||!Enum.IsDefined(typeof(BattleCommandKind),c.kind)||float.IsNaN(c.amount)||float.IsInfinity(c.amount)||c.amount<0||!CombinedArmsWorld.Finite(c.destination)||(c.route!=null&&c.route.Count>512)||(c.fortLine!=null&&c.fortLine.Count>512))return CommandResult.No("命令数据无效");
            c.playerId=authenticatedPlayer;c.automatedProduction=false;c.preserveTransportAutomation=false;
            if(c.route!=null&&c.route.Count>0&&(c.kind==BattleCommandKind.FreeRoute||c.kind==BattleCommandKind.RoadRoute)){var unit=Unit(c.unitId);if(unit!=null&&unit.ownerId==authenticatedPlayer){var position=unit.transform.position;c.route[0]=new RoadPoint(position.x,position.y);}}
            remoteCommand=true;try{return NetworkTacticalOrders.AsPlayer(authenticatedPlayer,()=>Submit(c));}finally{remoteCommand=false;}
        }
        public CommandResult Submit(BattleCommand c)
        {
            var connection=LocalPvpSession.Active;if(connection!=null&&!connection.Ready)return CommandResult.No("等待双方连接完成");if(connection!=null&&!connection.IsHost&&!remoteCommand)return connection.Forward(c);
            if(c==null||world==null||!world.Initialized||Array.IndexOf(world.players,c.playerId)<0)return CommandResult.No("战场未初始化或玩家编号无效");
            if(world.SimulationEnded)return CommandResult.No("作战已结束，只能查看复盘地图");
            if(c.kind==BattleCommandKind.TacticalOrder)return NetworkTacticalOrders.Execute(world,c);
            GetComponent<UnitContextOrders>()?.CancelSplitPreviewFor(c);
            if(LocalPrototypeOrders.EnabledFor(this)){GetComponent<LocalCoordinatedOrders>()?.CancelCommand(c);GetComponent<LocalPrototypeOrders>()?.CancelCommand(c);}
            if(c.kind==BattleCommandKind.StopSelected||c.kind==BattleCommandKind.NudgeSelected)return SelectedArmyOrders.Execute(world,c);
            if(logistics==null)logistics=GetComponent<LogisticsWorld>();var point=Point(c.pointId);
            if(c.kind==BattleCommandKind.BuildInfantry){var production=world.GetComponent<InfantryProduction>();if(production==null)return CommandResult.No("尚未配置生产模块");var quote=production.Quote(c.playerId,point,c.echelon,c.automatedProduction,c.count,c.productionRole);return quote.allowed?Result(production.Queue(c.playerId,point,c.echelon,c.automatedProduction,c.count,c.productionRole)):CommandResult.No(quote.reason);}
            if(c.kind==BattleCommandKind.CancelCityProduction){var production=world.GetComponent<InfantryProduction>();bool cancelled=production!=null&&production.Cancel(c.playerId,point,c.productionJobId);if(string.IsNullOrEmpty(c.productionJobId)&&logistics!=null)cancelled=logistics.CancelBuildFor(c.playerId,point)||cancelled;return Result(cancelled);}
            if(c.kind==BattleCommandKind.BuildFactory)return Result(logistics!=null&&logistics.BuildFactoryFor(c.playerId,point));
            if(c.kind==BattleCommandKind.BuildTruck)return Result(logistics!=null&&logistics.BuildTruckFor(c.playerId,point,null,c.echelon));
            var combined=world.GetComponent<CombinedArmsWorld>();
            var campaign=world.campaign;
            if(c.kind==BattleCommandKind.RemoveResourceDepot){ResourceDepot depot=null;if(campaign!=null)foreach(var d in campaign.depots)if(d!=null&&d.depotId==c.depotId){depot=d;break;}return campaign==null?CommandResult.No("未绑定战役系统"):campaign.RemoveDepot(c.playerId,depot);}
            if(c.kind==BattleCommandKind.ToggleFortRemoval){if(campaign!=null&&combined!=null)foreach(var f in combined.fortifications)if(f!=null&&(!string.IsNullOrEmpty(c.fortNetworkId)?LocalPvpReplication.FortId(f)==c.fortNetworkId:f.GetInstanceID()==c.fortId))return campaign.lineWorks.ToggleAutomaticRemoval(c.playerId,f);return CommandResult.No("防线不存在");}
            var u=Unit(c.unitId);if(LocalTacticalBattle.EnabledFor(this)&&u!=null&&campaign!=null&&campaign.lineWorks.Working(u)&&c.kind!=BattleCommandKind.CancelEngineering&&c.kind!=BattleCommandKind.SetAbsoluteSpeed&&c.kind!=BattleCommandKind.SetFortBuildLevel){campaign.lineWorks.Cancel(u);return CommandResult.No("已取消原施工任务；请再次下达新命令");}if(u==null||!u.Alive||u.ownerId!=c.playerId)return CommandResult.No("军队不存在、已消灭或不属于该玩家");
            if(u.GetComponent<RadarInstallation>()!=null)return CommandResult.No("雷达是固定设施");
            if(c.kind==BattleCommandKind.StopMovement||c.kind==BattleCommandKind.FreeRoute||c.kind==BattleCommandKind.RoadRoute||c.kind==BattleCommandKind.FreeDestination||c.kind==BattleCommandKind.RoadDestination||c.kind==BattleCommandKind.StraightDestination||c.kind==BattleCommandKind.EnterPoint){var priorFocus=u.GetComponent<CampaignUnit>();if(priorFocus!=null)priorFocus.fortTarget=null;}
            switch(c.kind)
            {
                case BattleCommandKind.DeployRadar:return RadarInstallation.Deploy(world,u,c.destination);
                case BattleCommandKind.SplitFlank:return TacticalDetachment.Split(world,u,DetachmentKind.Flank);
                case BattleCommandKind.SplitFeint:return TacticalDetachment.Split(world,u,DetachmentKind.Feint);
                case BattleCommandKind.SplitInfiltrate:return TacticalDetachment.Split(world,u,DetachmentKind.Infiltrate);
                case BattleCommandKind.MergeDetachment:return TacticalDetachment.Merge(world,u,Unit(c.targetUnitId));
                case BattleCommandKind.SetFortBuildLevel:var grade=u.GetComponent<CampaignUnit>();if(grade==null||u.combatDefinition.role!=UnitRole.Engineer||campaign==null||c.count<0||c.count>=campaign.rules.fortLevelCount)return CommandResult.No("工事等级无效");grade.fortBuildLevel=c.count;return CommandResult.Yes();
                case BattleCommandKind.StopMovement:if(u.Motor!=null)u.Motor.Stop();var truckStop=u.GetComponent<TransportTruck>();if(truckStop!=null)truckStop.StopAutomatic();return CommandResult.Yes();
                case BattleCommandKind.SetAbsoluteSpeed:if(LocalPrototypeOrders.EnabledFor(this))return CommandResult.No("单位速度设置已取消，请使用时间倍率或行军方式");var motion=u.GetComponent<CampaignUnit>();if(motion==null||float.IsNaN(c.amount)||float.IsInfinity(c.amount)||c.amount<0)return CommandResult.No("速度无效");motion.desiredSpeed=Mathf.Min(c.amount,u.GetComponent<ArmyRoadMover>().Speed);return CommandResult.Yes();
                case BattleCommandKind.BuildResourceDepot:return campaign==null?CommandResult.No("未绑定战役系统"):campaign.BuildDepot(u,c.destination);
                case BattleCommandKind.LoadResources:
                case BattleCommandKind.UnloadResources:
                case BattleCommandKind.AttackFortification:
                    var state=u.GetComponent<CampaignUnit>();if(state==null||u.combatDefinition.role!=UnitRole.Engineer||!u.OnMap||u.Working||u.GetComponent<PersonnelCarrier>()!=null||u.GetComponent<TransportTruck>()!=null||ArtilleryBattery.IsArtilleryRole(u.combatDefinition.role))return CommandResult.No("当前不能拆除工事");if(!c.enabled){state.fortTarget=null;return CommandResult.Yes();}if(combined!=null)foreach(var f in combined.fortifications)if(f!=null&&!f.Destroyed&&f.DistanceTo(c.destination)<=f.radius&&f.VisibleTo(world,c.playerId)){state.fortTarget=f;u.RevealForest();return CommandResult.Yes();}return CommandResult.No("未点击可见工事");
                case BattleCommandKind.ArtilleryFire:
                    var battery=u.GetComponent<ArtilleryBattery>();return battery==null?CommandResult.No("不是炮兵"):battery.Fire(world,c.playerId,c.destination);
                case BattleCommandKind.BoardCarrier:
                    var vehicle=Unit(c.targetUnitId);var carrier=vehicle==null?null:vehicle.GetComponent<PersonnelCarrier>();return carrier==null?CommandResult.No("运兵车不存在"):carrier.Load(u);
                case BattleCommandKind.UnloadCarrier:
                    var transport=u.GetComponent<PersonnelCarrier>();return transport==null?CommandResult.No("不是运兵车"):transport.Unload(Unit(c.targetUnitId),world);
                case BattleCommandKind.DismantleFortSection:return campaign==null?CommandResult.No("未绑定战役系统"):campaign.lineWorks.DismantleSection(u,c.fortLine);
                case BattleCommandKind.BuildFortification:if(world.campaign!=null&&c.fortLine!=null)return world.campaign.lineWorks.Build(u,c.fortLine);return combined==null?CommandResult.No("工事系统未配置"):combined.Build(u,c.destination);
                case BattleCommandKind.DismantleFortification:if(world.campaign!=null){c.kind=BattleCommandKind.AttackFortification;c.enabled=true;return Submit(c);}return combined==null?CommandResult.No("工事系统未配置"):combined.Dismantle(u,c.destination);
                case BattleCommandKind.CancelEngineering:if(combined==null)return CommandResult.No("工事系统未配置");combined.Cancel(u);if(world.campaign!=null)world.campaign.lineWorks.Cancel(u);return CommandResult.Yes();
                case BattleCommandKind.RoadRoute:
                case BattleCommandKind.RoadDestination:
                    if(campaign!=null&&!c.preserveTransportAutomation&&campaign.lineWorks.Working(u))campaign.lineWorks.Cancel(u);if(!u.CanMove||u.Motor==null)return CommandResult.No("当前状态不能移动");
                    var road=u.GetComponent<ArmyRoadMover>();var graph=world.setup.roadMap.Network;
                    var route=c.kind==BattleCommandKind.RoadDestination?RoadOrderPlanner.ToDestination(graph,u.transform.position,c.destination):c.route==null?null:new List<RoadPoint>(c.route);
                    if(!RoadOrderPlanner.Validate(graph,route,c.kind==BattleCommandKind.RoadDestination||c.preserveTransportAutomation))return CommandResult.No("路线没有沿道路连接，或道路不连通");
                    return Result(road!=null&&(c.preserveTransportAutomation?road.CommitAutomaticRoute(route):road.CommitRoute(route)));
                case BattleCommandKind.StraightDestination:
                    if(campaign!=null&&campaign.lineWorks.Working(u))campaign.lineWorks.Cancel(u);if(!u.CanMove||!CombinedArmsWorld.Finite(c.destination))return CommandResult.No("当前状态不能直线行军");
                    var straightMover=u.GetComponent<ArmyFreeMarchMover>();var straightStart=new RoadPoint(u.transform.position.x,u.transform.position.y);var straightEnd=new RoadPoint(c.destination.x,c.destination.y);
                    if(straightMover==null||!straightMover.CanTraverse(world.setup.freeMarchMap,straightStart,straightEnd))return CommandResult.No("直线穿过不可通行地形，请更换目的地或切换1状态");
                    return Result(straightMover.CommitRoute(new[]{straightStart,straightEnd},world.setup.freeMarchMap));
                case BattleCommandKind.FreeRoute:
                case BattleCommandKind.FreeDestination:
                    if(campaign!=null&&campaign.lineWorks.Working(u))campaign.lineWorks.Cancel(u);if(!u.CanMove)return CommandResult.No("当前状态不能移动");var free=u.GetComponent<ArmyFreeMarchMover>();
                    var freeRoute=c.kind==BattleCommandKind.FreeDestination?ConstructionNavigation.Plan(world.setup.freeMarchMap,free==null?TerrainAccess.None:free.AllowedTerrain,u.transform.position,c.destination,.2f,40000):c.route==null?null:new List<RoadPoint>(c.route);
                    return Result(free!=null&&free.CommitRoute(freeRoute,world.setup.freeMarchMap));
                case BattleCommandKind.Escape:if(campaign!=null)return CommandResult.No("逃跑操作已取消，请直接移动或停止");return Result(u.Escape(world.escapeLossFraction,world.escapeGraceSeconds));
                case BattleCommandKind.EnterPoint:return Result(point!=null&&world.EnterPointNow(u,point));
                case BattleCommandKind.ToggleRepair:
                    if(LocalPrototypeOrders.EnabledFor(this))return Result(FacilityRecoveryQueue.For(world).Toggle(u));
                    if(u.Point==null||u.Point.ownerId!=c.playerId)return CommandResult.No("不在己方据点内");if(u.State==ArmyBattleState.Repairing){u.CancelRepair();return CommandResult.Yes();}return Result(u.BeginRepair());
                case BattleCommandKind.ExitPoint:return Result(u.Point!=null&&u.Point.ownerId==c.playerId&&u.BeginExit());
                case BattleCommandKind.ToggleForestHide:
                    if(u.HiddenInForest){u.RevealForest();return CommandResult.Yes();}return Result(u.combatDefinition.canHideInForest&&world.InForest(u)&&u.HideInForest());
                case BattleCommandKind.SetForestAttack:return Result(world.InForest(u)&&u.SetForestAttack(c.enabled));
                case BattleCommandKind.SetTransport:
                case BattleCommandKind.SaveTransportRoute:
                    var t=u.GetComponent<TransportTruck>();if(t==null)return CommandResult.No("不是运输车");var sourceCity=Point(c.sourceCityId);var target=Point(c.destinationCityId);
                    if(target==null||target.ownerId!=c.playerId||target.kind!=FacilityKind.RecoveryPoint)return CommandResult.No("目的城市无效");
                    if(sourceCity==null||sourceCity.ownerId!=c.playerId||sourceCity.kind!=FacilityKind.RecoveryPoint||sourceCity==target)return CommandResult.No("运输来源无效");
                    var saved=c.kind==BattleCommandKind.SaveTransportRoute&&u.Motor!=null?u.Motor.CopyRoute():null;if(c.kind==BattleCommandKind.SaveTransportRoute&&(saved==null||saved.Count<2))return CommandResult.No("没有可保存的路线");
                    t.sourceCity=sourceCity;t.destinationCity=target;
                    if(c.kind==BattleCommandKind.SaveTransportRoute){t.SaveRoute(saved);return CommandResult.Yes();}
                    if(c.enabled)t.StartAutomatic();else{t.StopAutomatic();if(u.Motor!=null)u.Motor.Stop();}return CommandResult.Yes();
            }
            return CommandResult.No("不支持的命令");
        }
        static CommandResult Result(bool accepted){return accepted?CommandResult.Yes():CommandResult.No("条件不满足或路线不可通行");}
    }
}












