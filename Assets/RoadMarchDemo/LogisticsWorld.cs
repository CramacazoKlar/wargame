using System;
using System.Collections.Generic;
using UnityEngine;
namespace CampaignDemo
{
    [DefaultExecutionOrder(150)]
    public sealed class LogisticsWorld : MonoBehaviour
    {
        public BattleWorld world;
        public ResourceSite[] sites=new ResourceSite[0];
        public GameObject truckPrefab;
        [Min(0)] public int factoryLimitPerPlayer=3;
        [Min(0)] public float factoryBuildCost=50000;
        [Min(.1f)] public float factoryBuildSeconds=10;
        public string Message {get;private set;}
        sealed class BuildJob {public RecoveryPoint city;public string owner;public GameObject prefab;public float remaining,paidCost;public bool factory;public ArmyEchelon echelon;}
        readonly List<BuildJob> jobs=new List<BuildJob>();
        void Start(){if(world==null)world=GetComponent<BattleWorld>();}
        void Update(){if(world==null||!world.Initialized||world.campaign!=null||!LocalPvpSession.Simulate)return;Tick(Time.deltaTime);}
        public List<PvpProduction> NetworkJobs(string viewer){var list=new List<PvpProduction>();foreach(var job in jobs)if(job.owner==viewer)list.Add(new PvpProduction{id="logistics:"+job.city.pointId,city=job.city.pointId,owner=job.owner,remaining=job.remaining,cost=job.paidCost,echelon=job.echelon,role=UnitRole.Logistics,logistics=true,factory=job.factory});return list;}
        public void ApplyNetworkJobs(List<PvpProduction> incoming){jobs.Clear();foreach(var data in incoming){if(!data.logistics)continue;RecoveryPoint city=null;foreach(var p in world.points)if(p.pointId==data.city)city=p;if(city!=null)jobs.Add(new BuildJob{city=city,owner=data.owner,remaining=data.remaining,paidCost=data.cost,echelon=data.echelon,factory=data.factory,prefab=truckPrefab});}}
        public void Tick(float dt)
        {if(world==null||world.SimulationEnded)return;
            dt=Mathf.Max(0,dt);

            if(world.campaign==null)foreach(var u in world.units)if(u!=null){var t=u.GetComponent<TransportTruck>();if(t==null||!u.Alive)continue;if(u.OnMap&&u.State!=ArmyBattleState.Engaged){foreach(var p in world.points)if(p!=null&&p.kind==FacilityKind.RecoveryPoint&&p.ownerId==u.ownerId&&Vector2.Distance(t.transform.position,p.transform.position)<=UnitRanges.Interaction(u)){if(p!=t.sourceCity||!t.Automatic)Unload(t,p);}}
                if(!t.Automatic&&u.OnMap&&u.CanMove)LoadAtSource(t);t.Tick(this,dt);}
            for(int i=jobs.Count-1;i>=0;i--){var j=jobs[i];if(j.city==null||j.city.ownerId!=j.owner){jobs.RemoveAt(i);continue;}j.remaining-=dt*AICheatRules.Production(world,j.owner);if(j.remaining>0)continue;if(j.factory)j.city.hasFactory=true;else SpawnTruck(j);jobs.RemoveAt(i);}
        }
        
        public void LoadAtSource(TransportTruck t)
        {
            if(t==null||t.definition==null||t.Unit==null||!t.Unit.OnMap||!t.Unit.CanMove)return;float space=Mathf.Max(0,t.Capacity-t.Cargo);if(space<=0)return;
            if(t.sourceCity!=null){var p=t.sourceCity;if(p.kind==FacilityKind.RecoveryPoint&&p.ownerId==t.Unit.ownerId&&Vector2.Distance(t.transform.position,p.transform.position)<=UnitRanges.Interaction(t.Unit)){float amount=Mathf.Min(space,Mathf.Max(0,p.resourceStock));p.resourceStock-=amount;t.Load(amount);}}
        }
        public void Unload(TransportTruck t,RecoveryPoint city){if(t==null||t.Unit==null||city==null||city.kind!=FacilityKind.RecoveryPoint||city.ownerId!=t.Unit.ownerId||!t.Unit.OnMap||Vector2.Distance(t.transform.position,city.transform.position)>UnitRanges.Interaction(t.Unit))return;city.resourceStock+=t.Cargo;t.ClearCargo();}
        public int FactoryCount(string owner,bool pending=false){int n=0;foreach(var p in world.points)if(p!=null&&p.ownerId==owner&&p.kind==FacilityKind.RecoveryPoint&&p.hasFactory)n++;if(pending)foreach(var j in jobs)if(j.factory&&j.owner==owner)n++;return n;}
        public bool HasNonInfantryJob(RecoveryPoint city){foreach(var job in jobs)if(job.city==city)return true;return false;}
        public bool CancelBuildFor(string player,RecoveryPoint city){if(city==null||city.ownerId!=player||world.SimulationEnded)return false;bool any=false;for(int i=jobs.Count-1;i>=0;i--)if(jobs[i].city==city&&jobs[i].owner==player){city.resourceStock+=jobs[i].paidCost;jobs.RemoveAt(i);any=true;}return any;}
        public bool IsBuilding(RecoveryPoint city){var infantry=GetComponent<InfantryProduction>();if(infantry!=null&&infantry.IsBuilding(city))return true;foreach(var j in jobs)if(j.city==city)return true;return false;}
        public string BuildStatus(RecoveryPoint city){var infantry=GetComponent<InfantryProduction>();if(infantry!=null&&infantry.IsBuilding(city))return infantry.BuildStatus(city);foreach(var j in jobs)if(j.city==city)return(j.factory?"升级兵工厂":"制造运输车")+" "+j.remaining.ToString("0.0")+"秒";return "无建造任务";}
        bool Management {get{return world!=null&&world.setup!=null&&(LocalTacticalBattle.EnabledFor(this)||world.setup.modes.CurrentMode==MouseCommandMode.Interaction);}}
        public bool BuildFactory(RecoveryPoint city){return Management&&BuildFactoryFor(world.currentPlayer,city);}
        public bool BuildFactoryFor(string player,RecoveryPoint city)
        {
            if(System.Array.IndexOf(world.players,player)<0||city==null||city.ownerId!=player||city.kind!=FacilityKind.RecoveryPoint||city.isRepairSite||city.hasFactory||IsBuilding(city)||FactoryCount(city.ownerId,true)>=factoryLimitPerPlayer||city.resourceStock<factoryBuildCost){Message="无法升级：检查归属、库存、工厂上限或建造任务";return false;}
            city.resourceStock-=factoryBuildCost;jobs.Add(new BuildJob{city=city,owner=city.ownerId,factory=true,remaining=factoryBuildSeconds,paidCost=factoryBuildCost});Message="开始升级兵工厂";return true;
        }
        public bool BuildTruck(RecoveryPoint city,GameObject prefab=null,ArmyEchelon echelon=ArmyEchelon.Company){return Management&&BuildTruckFor(world.currentPlayer,city,prefab,echelon);}
        public bool BuildTruckFor(string player,RecoveryPoint city,GameObject prefab=null,ArmyEchelon echelon=ArmyEchelon.Company)
        {
            if(!LevelUnitAvailability.Allowed(world,UnitRole.Logistics)){Message="本关未开放资源车";return false;}
            prefab=prefab==null?truckPrefab:prefab;var t=prefab==null?null:prefab.GetComponent<TransportTruck>();var u=prefab==null?null:prefab.GetComponent<BattleUnit>();var mover=prefab==null?null:prefab.GetComponent<ArmyRoadMover>();
            if(System.Array.IndexOf(world.players,player)<0||city==null||city.kind!=FacilityKind.RecoveryPoint||city.isRepairSite||city.ownerId!=player||IsBuilding(city)||t==null||t.definition==null||u==null||u.combatDefinition==null||mover==null||mover.definition==null||city.resourceStock<t.definition.BuildCost(echelon)){Message="无法制造：检查城市、库存和运输车Prefab配置";return false;}
            city.resourceStock-=t.definition.BuildCost(echelon);jobs.Add(new BuildJob{city=city,owner=city.ownerId,prefab=prefab,echelon=echelon,remaining=t.definition.buildSeconds,paidCost=t.definition.BuildCost(echelon)});Message="开始制造运输车";return true;
        }
        void SpawnTruck(BuildJob job)
        {
            if(job.prefab==null)return;var g=Instantiate(job.prefab,world.setup.sceneArmies[0].transform.parent);g.SetActive(true);g.transform.position=job.city.ExitPosition;var u=g.GetComponent<BattleUnit>();u.combatDefinition=Instantiate(u.combatDefinition);u.combatDefinition.echelon=job.echelon;u.ownerId=job.owner;u.unitId=Guid.NewGuid().ToString("N");g.name="运输车 "+u.unitId.Substring(0,5);var mover=g.GetComponent<ArmyRoadMover>();mover.waterMap=world.setup.freeMarchMap.waterMap;mover.InitializeAt(world.setup.roadMap.Network,new RoadPoint(g.transform.position.x,g.transform.position.y),RoadRouteView.Create(g.transform,g.name+" route",.06f,3,Color.cyan));var free=g.GetComponent<ArmyFreeMarchMover>();if(free==null)free=g.AddComponent<ArmyFreeMarchMover>();if(free.definition==null)free.definition=mover.definition.freeMarchDefinition;free.Initialize(mover.Motor);free.BindMap(world.setup.freeMarchMap);u.Initialize();var view=g.GetComponent<BattleUnitView>();if(view==null)view=g.AddComponent<BattleUnitView>();view.Initialize();
            world.RegisterUnit(u,mover);
        }
        
        
        
        public List<RoadPoint> RouteTo(Vector3 from,Vector3 to,List<Vector2> custom,bool reverse)
        {
            return RoadOrderPlanner.ToDestination(world.setup.roadMap.Network,from,to,custom,reverse);
        }
    }
}








