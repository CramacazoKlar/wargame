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
        public List<ResourceCollector> collectors=new List<ResourceCollector>();
        public ResourceCollectorSettings collectorSettings=new ResourceCollectorSettings();
        public Sprite collectorSprite;
        public GameObject truckPrefab;
        [Min(0)] public int factoryLimitPerPlayer=3;
        [Min(0)] public float factoryBuildCost=500;
        [Min(.1f)] public float factoryBuildSeconds=10;
        public string Message {get;private set;}
        sealed class BuildJob {public RecoveryPoint city;public string owner;public GameObject prefab;public float remaining;public bool factory;public ArmyEchelon echelon;}
        readonly List<BuildJob> jobs=new List<BuildJob>();
        void Start(){if(world==null)world=GetComponent<BattleWorld>();foreach(var c in collectors)if(c!=null){c.Campaign=world.campaign;c.Initialize();}}
        void Update(){if(world==null||!world.Initialized||world.campaign!=null)return;Tick(Time.deltaTime);}
        public void Tick(float dt)
        {if(world==null||world.SimulationEnded)return;
            dt=Mathf.Max(0,dt);
            foreach(var c in collectors)if(c!=null){c.Tick(dt);bool visible=c.Alive&&(c.ownerId==world.currentPlayer||!world.fogEnabled||world.VisibleAt(world.currentPlayer,c.transform.position));foreach(var r in c.GetComponentsInChildren<SpriteRenderer>())r.enabled=visible;}

            if(world.campaign==null)foreach(var u in world.units)if(u!=null){var t=u.GetComponent<TransportTruck>();if(t==null||!u.Alive)continue;if(u.OnMap&&u.State!=ArmyBattleState.Engaged){foreach(var p in world.points)if(p!=null&&p.kind==FacilityKind.RecoveryPoint&&p.ownerId==u.ownerId&&Vector2.Distance(t.transform.position,p.transform.position)<=p.interactionRadius){if(p!=t.sourceCity||!t.Automatic)Unload(t,p);}}
                if(!t.Automatic&&u.OnMap&&u.CanMove)LoadAtSource(t);t.Tick(this,dt);}
            for(int i=jobs.Count-1;i>=0;i--){var j=jobs[i];if(j.city==null||j.city.ownerId!=j.owner){jobs.RemoveAt(i);continue;}j.remaining-=dt;if(j.remaining>0)continue;if(j.factory)j.city.hasFactory=true;else SpawnTruck(j);jobs.RemoveAt(i);}
        }
        public bool AttackCollector(BattleUnit attacker,float dt)
        {
            if(attacker==null||!attacker.CanFire||attacker.combatDefinition==null)return false;
            float range=Mathf.Min(attacker.combatDefinition.attackRadius,attacker.combatDefinition.visionRadius);
            ResourceCollector target=null;float nearest=float.MaxValue;
            foreach(var c in collectors)if(c!=null&&c.Alive&&c.ownerId!=attacker.ownerId){float d=Vector2.Distance(c.transform.position,attacker.transform.position);if(d<=range&&d<nearest){nearest=d;target=c;}}
            if(target==null)return false;float damage=CombatMath.Damage(attacker.combatDefinition.fullStrengthAttack,attacker.Fraction,target.settings.defense,dt);
            if(damage<=0)return false;world.NotifyCombatEffect(CombatEffectEvent.Direct(attacker,target.collectorId,target.ownerId,target.transform.position,true));target.Damage(damage);return true;
        }
        public void LoadAtSource(TransportTruck t)
        {
            if(t==null||t.definition==null||t.Unit==null||!t.Unit.OnMap||!t.Unit.CanMove)return;float space=Mathf.Max(0,t.Capacity-t.Cargo);if(space<=0)return;
            if(t.sourceCollector!=null){var c=t.sourceCollector;if(c.Alive&&c.ownerId==t.Unit.ownerId&&Vector2.Distance(t.transform.position,c.transform.position)<=t.definition.loadingRadius)t.Load(c.Take(space));}
            else if(t.sourceCity!=null){var p=t.sourceCity;if(p.kind==FacilityKind.RecoveryPoint&&p.ownerId==t.Unit.ownerId&&Vector2.Distance(t.transform.position,p.transform.position)<=t.definition.loadingRadius){float amount=Mathf.Min(space,Mathf.Max(0,p.resourceStock));p.resourceStock-=amount;t.Load(amount);}}
        }
        public void Unload(TransportTruck t,RecoveryPoint city){if(t==null||t.Unit==null||city==null||city.kind!=FacilityKind.RecoveryPoint||city.ownerId!=t.Unit.ownerId)return;city.resourceStock+=t.Cargo;t.ClearCargo();}
        public int FactoryCount(string owner,bool pending=false){int n=0;foreach(var p in world.points)if(p!=null&&p.ownerId==owner&&p.kind==FacilityKind.RecoveryPoint&&p.hasFactory)n++;if(pending)foreach(var j in jobs)if(j.factory&&j.owner==owner)n++;return n;}
        public bool IsBuilding(RecoveryPoint city){foreach(var j in jobs)if(j.city==city)return true;return false;}
        public string BuildStatus(RecoveryPoint city){foreach(var j in jobs)if(j.city==city)return(j.factory?"升级兵工厂":"制造运输车")+" "+j.remaining.ToString("0.0")+"秒";return "无建造任务";}
        bool Management {get{return world!=null&&world.setup!=null&&world.setup.modes.CurrentMode==MouseCommandMode.Interaction;}}
        public bool BuildFactory(RecoveryPoint city){return Management&&BuildFactoryFor(world.currentPlayer,city);}
        public bool BuildFactoryFor(string player,RecoveryPoint city)
        {
            if(System.Array.IndexOf(world.players,player)<0||city==null||city.ownerId!=player||city.kind!=FacilityKind.RecoveryPoint||city.hasFactory||IsBuilding(city)||FactoryCount(city.ownerId,true)>=factoryLimitPerPlayer||city.resourceStock<factoryBuildCost){Message="无法升级：检查归属、库存、工厂上限或建造任务";return false;}
            city.resourceStock-=factoryBuildCost;jobs.Add(new BuildJob{city=city,owner=city.ownerId,factory=true,remaining=factoryBuildSeconds});Message="开始升级兵工厂";return true;
        }
        public bool BuildTruck(RecoveryPoint city,GameObject prefab=null,ArmyEchelon echelon=ArmyEchelon.Company){return Management&&BuildTruckFor(world.currentPlayer,city,prefab,echelon);}
        public bool BuildTruckFor(string player,RecoveryPoint city,GameObject prefab=null,ArmyEchelon echelon=ArmyEchelon.Company)
        {
            prefab=prefab==null?truckPrefab:prefab;var t=prefab==null?null:prefab.GetComponent<TransportTruck>();var u=prefab==null?null:prefab.GetComponent<BattleUnit>();var mover=prefab==null?null:prefab.GetComponent<ArmyRoadMover>();
            if(System.Array.IndexOf(world.players,player)<0||city==null||city.kind!=FacilityKind.RecoveryPoint||city.ownerId!=player||IsBuilding(city)||t==null||t.definition==null||u==null||u.combatDefinition==null||mover==null||mover.definition==null||city.resourceStock<t.definition.buildCost){Message="无法制造：检查城市、库存和运输车Prefab配置";return false;}
            city.resourceStock-=t.definition.buildCost;jobs.Add(new BuildJob{city=city,owner=city.ownerId,prefab=prefab,echelon=echelon,remaining=t.definition.buildSeconds});Message="开始制造运输车";return true;
        }
        void SpawnTruck(BuildJob job)
        {
            if(job.prefab==null)return;var g=Instantiate(job.prefab,world.setup.sceneArmies[0].transform.parent);g.SetActive(true);g.transform.position=job.city.ExitPosition;var u=g.GetComponent<BattleUnit>();u.combatDefinition=Instantiate(u.combatDefinition);u.combatDefinition.echelon=job.echelon;u.ownerId=job.owner;u.unitId=Guid.NewGuid().ToString("N");g.name="运输车 "+u.unitId.Substring(0,5);var mover=g.GetComponent<ArmyRoadMover>();mover.waterMap=world.setup.freeMarchMap.waterMap;mover.InitializeAt(world.setup.roadMap.Network,new RoadPoint(g.transform.position.x,g.transform.position.y),RoadRouteView.Create(g.transform,g.name+" route",.06f,3,Color.cyan));var free=g.GetComponent<ArmyFreeMarchMover>();if(free==null)free=g.AddComponent<ArmyFreeMarchMover>();if(free.definition==null)free.definition=mover.definition.freeMarchDefinition;free.Initialize(mover.Motor);free.BindMap(world.setup.freeMarchMap);u.Initialize();var view=g.GetComponent<BattleUnitView>();if(view==null)view=g.AddComponent<BattleUnitView>();view.Initialize();
            world.RegisterUnit(u,mover);
        }
        public bool CanPlace(ResourceSite site,Vector2 center)
        {
            if(site==null||collectorSettings.footprint.x<=0||collectorSettings.footprint.y<=0)return false;var rect=new Rect(center-collectorSettings.footprint/2,collectorSettings.footprint);var area=site.Area;if(rect.xMin<area.xMin||rect.xMax>area.xMax||rect.yMin<area.yMin||rect.yMax>area.yMax)return false;
            foreach(var c in collectors)if(c!=null&&c.Alive&&c.Footprint.Overlaps(rect))return false;
            if(world!=null&&world.setup!=null&&world.setup.freeMarchMap!=null){var map=world.setup.freeMarchMap;var corners=new[]{new Vector2(rect.xMin,rect.yMin),new Vector2(rect.xMax,rect.yMin),new Vector2(rect.xMax,rect.yMax),new Vector2(rect.xMin,rect.yMax)};for(int i=0;i<4;i++)if(!map.CanTraverse(new RoadPoint(corners[i].x,corners[i].y),new RoadPoint(corners[(i+1)%4].x,corners[(i+1)%4].y),TerrainAccess.Ground|TerrainAccess.Road|TerrainAccess.Grass|TerrainAccess.Mountain|TerrainAccess.Forest))return false;}
            return true;
        }
        public ResourceCollector BuildCollector(ResourceSite site,Vector2 center,RecoveryPoint payer){return Management?BuildCollectorFor(world.currentPlayer,site,center,payer):null;}
        public ResourceCollector BuildCollectorFor(string player,ResourceSite site,Vector2 center,RecoveryPoint payer)
        {
            if(world.campaign!=null&&!world.campaign.CanDeployCollector(player,center)){Message="部署位置不属于己方合法资源领土";return null;}
            if(System.Array.IndexOf(world.players,player)<0||payer==null||payer.kind!=FacilityKind.RecoveryPoint||payer.ownerId!=player||payer.resourceStock<collectorSettings.buildCost||!CanPlace(site,center)){Message="无法部署：检查可采区、占地重叠、水域和付款城市库存";return null;}
            payer.resourceStock-=collectorSettings.buildCost;var g=new GameObject("资源收集器");g.transform.position=center;var c=g.AddComponent<ResourceCollector>();c.Campaign=world.campaign;c.ownerId=player;c.site=site;c.settings=new ResourceCollectorSettings{health=collectorSettings.health,defense=collectorSettings.defense,buildCost=collectorSettings.buildCost,collectionInterval=collectorSettings.collectionInterval,amountPerInterval=collectorSettings.amountPerInterval,storageLimit=collectorSettings.storageLimit,footprint=collectorSettings.footprint};c.Initialize();var r=g.AddComponent<SpriteRenderer>();r.sprite=collectorSprite;r.sortingOrder=8;var symbols=g.AddComponent<UnitSymbolIdentity>();symbols.catalog=Resources.Load<UnitSymbolCatalog>("UnitSymbols");g.transform.localScale=Vector3.one;collectors.Add(c);Message="部署成功";return c;
        }
        public List<RoadPoint> RouteTo(Vector3 from,Vector3 to,List<Vector2> custom,bool reverse)
        {
            return RoadOrderPlanner.ToDestination(world.setup.roadMap.Network,from,to,custom,reverse);
        }
    }
}







