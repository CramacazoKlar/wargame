using System.Collections.Generic;
using UnityEngine;
namespace CampaignDemo
{
    // Values only: querying information does not create UI or depend on a mouse mode.
    public sealed class PlayerOverviewInfo
    {
        public string playerId;public int unitCount,cityCount,enemyCityCount,factoryCount,personnelLimit,factoryPendingCount,factoryLimit;
        public float personnel,nominalPersonnel,cityResources,transportResources,ownAreaPercent,enemyAreaPercent,neutralAreaPercent;
        public float TotalResources {get{return cityResources+transportResources;}}
    }
    public sealed class ArmyInfo
    {
        public string unitId,ownerId,designation,unitType,pointId,pointName,entryPointId;
        public bool isPersonnelCarrier;
        public bool own,visible,known,alive,onMap,isTransport,canEscape,canEnter,canRepair,canExit,canHide,canToggleAmbush,canMove;
        public Vector3 position;public ArmyEchelon? echelon;public float? personnelPercent,personnel,capacity,strengthPercent,visionRadius,attackRadius,remainingSeconds;
        public bool? isMoving;public float? currentSpeed;public MouseCommandMode? movementMode;
        public ArmyBattleState? state;public bool? hidden,ambushEnabled;
    }
    public sealed class FacilityInfo
    {
        public string pointId,name,ownerId,buildStatus;public Vector3 position;public bool own,observed;public FacilityKind kind;
        public float? stock,capturePercent;public int? capacity;public bool? hasFactory;public string[] stationedUnitIds=new string[0];
    }
    public sealed class TransportInfo
    {
        public string unitId,status,sourceCityId,destinationCityId;public float cargo,capacity;public bool automatic;
    }
    
    public sealed class ResourceSiteInfo {public string siteId,name;public Vector3 position;public Vector2 areaSize;}
    public interface IPlayerBattleInformation
    {
        PlayerOverviewInfo GetOverview(string viewer);
        string[] GetVisibleArmyIds(string viewer,bool includeOwnStationed=false);
        string[] GetFacilityIds(string viewer);
        ResourceSiteInfo[] GetResourceSites(string viewer);
        ArmyInfo GetArmy(string viewer,string unitId);
        Vector2[] GetOwnRoute(string viewer,string unitId);
        FacilityInfo GetFacility(string viewer,string pointId);
        TransportInfo GetTransport(string viewer,string unitId);
    }
    [DefaultExecutionOrder(110)]
    public sealed class BattleInformationSource : MonoBehaviour,IPlayerBattleInformation
    {
        public BattleWorld world;public LogisticsWorld logistics;
        void Awake(){if(world==null)world=GetComponent<BattleWorld>();if(logistics==null)logistics=GetComponent<LogisticsWorld>();}
        bool Player(string viewer){if(logistics==null)logistics=GetComponent<LogisticsWorld>();return world!=null&&world.Initialized&&world.players!=null&&world.units!=null&&world.points!=null&&System.Array.IndexOf(world.players,viewer)>=0;}
        BattleUnit Unit(string id){if(string.IsNullOrEmpty(id))return null;if(world==null||world.units==null)return null;foreach(var u in world.units)if(u!=null&&u.unitId==id)return u;return null;}
        RecoveryPoint Point(string id){if(string.IsNullOrEmpty(id))return null;if(world==null||world.points==null)return null;foreach(var p in world.points)if(p!=null&&p.pointId==id)return p;return null;}
        public string[] GetVisibleArmyIds(string viewer,bool includeOwnStationed=false)
        {var ids=new List<string>();if(!Player(viewer))return ids.ToArray();foreach(var u in world.units)if(u!=null&&u.Alive&&(world.IsVisible(viewer,u)||(includeOwnStationed&&u.ownerId==viewer)))ids.Add(u.unitId);return ids.ToArray();}
        public string[] GetFacilityIds(string viewer)
        {var ids=new List<string>();if(!Player(viewer))return ids.ToArray();foreach(var p in world.points)if(p!=null)ids.Add(p.pointId);return ids.ToArray();}
        
        public ResourceSiteInfo[] GetResourceSites(string viewer)
        {var sites=new List<ResourceSiteInfo>();if(!Player(viewer)||logistics==null)return sites.ToArray();foreach(var s in logistics.sites)if(s!=null)sites.Add(new ResourceSiteInfo{siteId=s.siteId,name=s.displayName,position=s.transform.position,areaSize=s.areaSize});return sites.ToArray();}
        public PlayerOverviewInfo GetOverview(string viewer)
        {
            if(!Player(viewer))return null;var info=new PlayerOverviewInfo{playerId=viewer,personnelLimit=world.personnelLimit};
            foreach(var u in world.units)if(u!=null&&u.Alive&&u.ownerId==viewer){info.unitCount++;info.personnel+=world.campaign!=null&&world.campaign.Vehicle(u)?world.campaign.rules.At(world.campaign.rules.transportEquivalents,u.combatDefinition.echelon)*u.Fraction*TacticalDetachment.Scale(u):u.Personnel;info.nominalPersonnel+=world.campaign!=null&&world.campaign.Vehicle(u)?world.campaign.rules.At(world.campaign.rules.transportEquivalents,u.combatDefinition.echelon)*TacticalDetachment.Scale(u):u.Capacity;var t=u.GetComponent<TransportTruck>();if(t!=null)info.transportResources+=t.Cargo;}
            foreach(var p in world.points)if(p!=null){if(p.ownerId==viewer){info.cityResources+=p.resourceStock;if(p.kind==FacilityKind.RecoveryPoint){info.cityCount++;if(p.hasFactory)info.factoryCount++;}else if(p.kind==FacilityKind.Factory)info.factoryCount++;}else if(!string.IsNullOrEmpty(p.ownerId)&&p.kind==FacilityKind.RecoveryPoint)info.enemyCityCount++;}
            if(logistics!=null){info.factoryPendingCount=Mathf.Max(0,logistics.FactoryCount(viewer,true)-logistics.FactoryCount(viewer));info.factoryLimit=logistics.factoryLimitPerPlayer;}
            info.ownAreaPercent=world.territory==null?0:world.territory.Percent(viewer);info.enemyAreaPercent=world.territory==null?0:world.territory.Percent(viewer,true);info.neutralAreaPercent=Mathf.Max(0,100-info.ownAreaPercent-info.enemyAreaPercent);return info;
        }
        public ArmyInfo GetArmy(string viewer,string id)
        {
            if(!Player(viewer))return null;var u=Unit(id);if(u==null)return null;bool own=u.ownerId==viewer;bool visible=world.IsVisible(viewer,u);if(!own&&!visible)return null;
            bool known=world.IsKnown(viewer,u);var info=new ArmyInfo{unitId=id,ownerId=u.ownerId,own=own,visible=visible,known=known,alive=u.Alive,onMap=u.OnMap,position=u.transform.position};
            if(known&&u.combatDefinition!=null){info.unitType=u.combatDefinition.unitType;info.echelon=u.combatDefinition.echelon;info.personnelPercent=u.StrengthFraction*100;}
            if(!own)return info;
            info.isPersonnelCarrier=u.GetComponent<PersonnelCarrier>()!=null;info.canMove=u.CanMove;var motor=u.Motor;info.isMoving=motor!=null&&motor.IsMoving;info.currentSpeed=motor==null?0:motor.CurrentSpeed;info.movementMode=motor==null?(MouseCommandMode?)null:motor.MovementMode;info.designation=u.DisplayDesignation;info.personnel=u.Personnel;info.capacity=u.StrengthCapacity;info.strengthPercent=CombatMath.Strength(u.StrengthFraction)*100;info.state=u.State;info.hidden=u.HiddenInForest;info.ambushEnabled=u.ForestAttackEnabled;info.visionRadius=u.combatDefinition.visionRadius;info.attackRadius=UnitRanges.Attack(u);info.isTransport=u.GetComponent<TransportTruck>()!=null;
            if(u.Point!=null){info.pointId=u.Point.pointId;info.pointName=u.Point.displayName;}info.remainingSeconds=u.State==ArmyBattleState.Repairing&&u.Point!=null?(1-u.Fraction)/Mathf.Max(.001f,u.Point.repairFractionPerSecond):u.StateTimer;
            info.canEscape=world.campaign==null&&u.State==ArmyBattleState.Engaged;foreach(var p in world.points)if(world.CanEnter(u,p)){info.canEnter=true;info.entryPointId=p.pointId;break;}
            bool inside=u.State==ArmyBattleState.InsidePoint||u.State==ArmyBattleState.Repairing;info.canRepair=inside&&u.Fraction<1;info.canExit=inside;info.canHide=u.HiddenInForest||(u.OnMap&&!u.Working&&u.State==ArmyBattleState.Deployed&&u.combatDefinition.canHideInForest&&world.InForest(u));info.canToggleAmbush=u.HiddenInForest;return info;
        }
        public Vector2[] GetOwnRoute(string viewer,string id)
        {var u=Unit(id);if(!Player(viewer)||u==null||u.ownerId!=viewer)return null;if(u.Motor==null)return new Vector2[0];var route=u.Motor.CopyRoute();var points=new Vector2[route.Count];for(int i=0;i<points.Length;i++)points[i]=new Vector2((float)route[i].X,(float)route[i].Y);return points;}
        public FacilityInfo GetFacility(string viewer,string id)
        {
            if(!Player(viewer))return null;var p=Point(id);if(p==null)return null;bool own=p.ownerId==viewer;bool observed=own||!world.fogEnabled||world.VisibleAt(viewer,p.transform.position);var info=new FacilityInfo{pointId=id,name=p.displayName,position=p.transform.position,kind=p.kind,own=own,observed=observed,ownerId=observed?p.ownerId:null};if(!own)return info;
            info.buildStatus=logistics==null?"":logistics.BuildStatus(p);info.stock=p.resourceStock;info.capacity=p.capacity;info.hasFactory=p.hasFactory;info.capturePercent=p.CapturingPlayer==null?(float?)null:p.CaptureProgress/Mathf.Max(.1f,p.captureSeconds)*100;var ids=new List<string>();foreach(var u in world.units)if(u!=null&&u.Alive&&u.Point==p)ids.Add(u.unitId);info.stationedUnitIds=ids.ToArray();return info;
        }
        public TransportInfo GetTransport(string viewer,string id)
        {
            var u=Unit(id);if(!Player(viewer)||u==null||u.ownerId!=viewer||!u.Alive)return null;var t=u.GetComponent<TransportTruck>();if(t==null)return null;return new TransportInfo{unitId=id,status=t.Status,cargo=t.Cargo,capacity=t.Capacity,automatic=t.Automatic,sourceCityId=t.sourceCity==null?null:t.sourceCity.pointId,destinationCityId=t.destinationCity==null?null:t.destinationCity.pointId};
        }
        
    }
}








