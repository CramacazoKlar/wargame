using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace CampaignDemo
{
    [RequireComponent(typeof(BattleWorld))]
    public sealed class InfantryProduction : MonoBehaviour
    {
        public BattleWorld world;
        public CityProductionRules rules;
        public string Status {get;private set;}
        public sealed class Job
        {
            public string id;public UnitRole role;
            public RecoveryPoint city;
            public string owner;
            public ArmyEchelon echelon;
            public GameObject prefab;
            public float paidCost,remaining;
            public bool automated;
        }
        readonly List<Job> jobs=new List<Job>();
        public IReadOnlyList<Job> Jobs {get{return jobs;}}
        void Awake(){world=GetComponent<BattleWorld>();}
        void Update(){Tick(Time.deltaTime);}
        CityProductionRules Config {get{if(rules==null)rules=Resources.Load<CityProductionRules>("CityProductionRules");return rules;}}
        AIBasicProfile AutoConfig {get{var c=GetComponent<AIBasicCommander>();return c==null?null:c.profile;}}
        GameObject Prefab(UnitRole role){return Config==null?null:role==UnitRole.Infantry?Config.infantryPrefab:Config.Find(role)?.prefab;}
        bool NeedsFactory(UnitRole role){return role==UnitRole.Infantry?RequiresFactory:Config!=null&&Config.Find(role)!=null&&Config.Find(role).requiresFactory;}
        public bool IsBuilding(RecoveryPoint city){return jobs.Any(j=>j.city==city);}
        public Job ActiveJob(RecoveryPoint city){return jobs.FirstOrDefault(j=>j.city==city&&(!j.automated||GetComponent<BattleAIController>()?.CanThink==true));}
        public float PendingStrength(string owner)
        {
            if(world==null||world.campaign==null)return 0;
            float squad=Mathf.Max(1,world.campaign.rules.troopStrength[0]);
            return jobs.Where(j=>j.owner==owner&&j.city!=null&&j.city.ownerId==owner&&j.role==UnitRole.Infantry).Sum(j=>world.campaign.rules.At(world.campaign.rules.troopStrength,j.echelon)/squad);
        }
        float Capacity(UnitRole role,ArmyEchelon level){return world.campaign.rules.At(role==UnitRole.PersonnelCarrier||role==UnitRole.Logistics?world.campaign.rules.transportEquivalents:world.campaign.rules.troopStrength,level)*(role==UnitRole.PersonnelCarrier||role==UnitRole.Logistics?world.campaign.rules.vehicleHealthPerEquivalent:1);}
        public int PendingCount(string owner){return jobs.Count(j=>j.owner==owner&&j.city!=null&&j.city.ownerId==owner);}
        public float Duration(ArmyEchelon echelon,UnitRole role=UnitRole.Infantry){var entry=Config==null?null:Config.Find(role);if(role!=UnitRole.Infantry)return entry==null?float.PositiveInfinity:Mathf.Max(1,entry.squadSeconds)*Mathf.Pow(Mathf.Max(1,entry.timeScale),(int)echelon);var p=Config;return p==null?float.PositiveInfinity:Mathf.Max(1,p.infantrySquadSeconds)*Mathf.Pow(Mathf.Max(1,p.infantryTimeScale),(int)echelon);}
        public string BuildStatus(RecoveryPoint city){var job=jobs.FirstOrDefault(j=>j.city==city);return job==null?"无步兵制造任务":"制造步兵（"+BattleTextFormatter.Echelon(job.echelon)+"） "+Mathf.Max(0,job.remaining).ToString("0.0")+"秒";}
        public bool HasPrefab {get{return Config!=null&&Config.infantryPrefab!=null;}}
        public bool RequiresFactory {get{return Config!=null&&Config.infantryRequiresFactory;}}
        public float Cost(ArmyEchelon echelon,UnitRole role=UnitRole.Infantry){var entry=Config==null?null:Config.Find(role);if(role!=UnitRole.Infantry)return entry==null?float.PositiveInfinity:TransportDefinition.ScaledBuildCost(entry.squadCost,entry.costScale,echelon);var p=Config;return p==null?float.PositiveInfinity:TransportDefinition.ScaledBuildCost(p.infantrySquadCost,p.infantryCostScale,echelon);}
        public CityActionQuote Quote(string player,RecoveryPoint city,ArmyEchelon echelon,bool automated=false,int count=1,UnitRole role=UnitRole.Infantry)
        {
            var p=Config;int level=(int)echelon;
            if(world==null||!world.Initialized||world.SimulationEnded)return CityActionQuote.No("战场未开始或已经结束");
            if(p==null)return CityActionQuote.No("尚未配置城市生产规则");
            if(level<0||level>7||count<1||count>Mathf.Clamp(p.maximumQueuedPerCity,1,10))return CityActionQuote.No("编制或数量无效");
            if(Array.IndexOf(world.players,player)<0||city==null||city.ownerId!=player||city.kind!=FacilityKind.RecoveryPoint)return CityActionQuote.No("只能使用己方城市");
            var ai=GetComponent<BattleAIController>();var automation=AutoConfig;
            if(automated&&(player!=BattleFactions.RedPlayerId||ai==null||!ai.CanThink||automation==null||!automation.recruitInfantry||count!=1||role!=UnitRole.Infantry))return CityActionQuote.No("自动造兵未开启");
            if(!LevelUnitAvailability.Allowed(world,role))return CityActionQuote.No("本关未开放该兵种");
            var prefab=Prefab(role);var unit=prefab==null?null:prefab.GetComponent<BattleUnit>();var mover=prefab==null?null:prefab.GetComponent<ArmyRoadMover>();
            if(unit==null||unit.combatDefinition==null||unit.combatDefinition.role!=role||mover==null||mover.definition==null||world.campaign==null)return CityActionQuote.No("普通步兵预制体未配置");
            var logistics=world.GetComponent<LogisticsWorld>();
            if(logistics!=null&&logistics.HasNonInfantryJob(city))return CityActionQuote.No("城市正在升级或制造资源车");
            if(automated&&IsBuilding(city))return CityActionQuote.No("城市已有生产订单");
            if(jobs.Count(j=>j.city==city)+count>Mathf.Clamp(p.maximumQueuedPerCity,1,10))return CityActionQuote.No("城市生产队列已满");
            if(NeedsFactory(role)&&!city.hasFactory)return CityActionQuote.No("需要先升级兵工厂");
            if(automated&&PendingCount(player)>=Mathf.Max(1,automation.maximumProductionJobs))return CityActionQuote.No("已达到自动制造数量上限");
            float cost=Cost(echelon,role)*count,seconds=Duration(echelon,role)*count;
            if(float.IsNaN(cost)||float.IsInfinity(cost)||float.IsNaN(seconds)||float.IsInfinity(seconds))return CityActionQuote.No("生产规则数值无效");
            var ledger=GetComponent<AITaskReservations>();float reserved=ledger==null?0:ledger.ReservedAt(city.GetInstanceID().ToString());
            if(city.resourceStock-reserved<cost)return CityActionQuote.No("城市可用资源不足");
            float incoming=Capacity(role,echelon)*count;
            if(world.units.Where(u=>u!=null&&u.Alive&&u.ownerId==player).Sum(u=>u.Capacity)+jobs.Where(j=>j.owner==player&&j.city!=null&&j.city.ownerId==player).Sum(j=>Capacity(j.role,j.echelon))+incoming>world.personnelLimit)return CityActionQuote.No("超过兵力上限");
            Vector3 spawn;if(!TrySpawnPoint(city,prefab,out spawn))return CityActionQuote.No("城市附近没有合法出生位置");
            return CityActionQuote.Yes(cost,seconds);
        }
        public bool Queue(string player,RecoveryPoint city,ArmyEchelon echelon,bool automated,int count=1,UnitRole role=UnitRole.Infantry)
        {
            var quote=Quote(player,city,echelon,automated,count,role);if(!quote.allowed){Status=quote.reason;return false;}
            city.resourceStock-=quote.cost;
            for(int i=0;i<count;i++)jobs.Add(new Job{id=Guid.NewGuid().ToString("N"),city=city,owner=player,echelon=echelon,role=role,prefab=Prefab(role),paidCost=Cost(echelon,role),automated=automated,remaining=Duration(echelon,role)});
            Status="步兵已加入生产队列";return true;
        }
        public bool Cancel(string player,RecoveryPoint city,string jobId=null)
        {
            if(world==null||world.SimulationEnded||city==null||city.ownerId!=player)return false;
            var selected=jobs.Where(j=>j.city==city&&j.owner==player&&(string.IsNullOrEmpty(jobId)||j.id==jobId)).ToArray();
            foreach(var job in selected){city.resourceStock+=job.paidCost;jobs.Remove(job);}
            return selected.Length>0;
        }

        public void CancelAutomated(string owner)
        {
            foreach(var job in jobs.Where(j=>j.automated&&j.owner==owner).ToArray())
            {if(job.city!=null&&job.city.ownerId==owner)job.city.resourceStock+=job.paidCost;jobs.Remove(job);}
            Status="已取消自动造兵，退回未完成订单费用";
        }
        public void Tick(float dt)
        {
            if(world==null)world=GetComponent<BattleWorld>();
            if(world==null||!world.Initialized||world.SimulationEnded)return;
            var ai=GetComponent<BattleAIController>();var auto=AutoConfig;
            if((auto==null||!auto.recruitInfantry)&&jobs.Any(j=>j.automated&&j.owner==BattleFactions.RedPlayerId))CancelAutomated(BattleFactions.RedPlayerId);
            jobs.RemoveAll(j=>j.city==null||j.city.ownerId!=j.owner);
            foreach(var city in jobs.Select(j=>j.city).Distinct().ToArray())
            {
                float budget=Mathf.Max(0,dt)*AICheatRules.Production(world,city.ownerId);
                while(true)
                {
                    var job=jobs.FirstOrDefault(j=>j.city==city&&(!j.automated||ai!=null&&ai.CanThink));
                    if(job==null)break;
                    float used=Mathf.Min(budget,Mathf.Max(0,job.remaining));job.remaining-=used;budget-=used;
                    if(job.remaining>0)break;
                    Vector3 position;if(!TrySpawnPoint(city,job.prefab,out position)){Status="等待合法出生位置";break;}
                    Spawn(job,position);jobs.Remove(job);Status="步兵制造完成";
                    if(budget<=0)break;
                }
            }
        }

        bool TrySpawnPoint(RecoveryPoint city,GameObject prefab,out Vector3 position)
        {
            position=city.ExitPosition;var free=prefab.GetComponent<ArmyFreeMarchMover>();
            if(free==null||world.setup==null||world.setup.freeMarchMap==null)return false;
            for(int i=0;i<17;i++)
            {
                var candidate=i==0?city.ExitPosition:city.transform.position+new Vector3(Mathf.Cos(i*Mathf.PI/4),Mathf.Sin(i*Mathf.PI/4))*city.interactionRadius*(i<=8?.4f:.8f);
                var point=new RoadPoint(candidate.x,candidate.y);
                if(world.setup.freeMarchMap.CanTraverse(point,point,free.AllowedTerrain)){position=candidate;return true;}
            }
            return false;
        }
        void Spawn(Job job,Vector3 position)
        {
            Transform parent=world.setup.sceneArmies.Length>0&&world.setup.sceneArmies[0]!=null?world.setup.sceneArmies[0].transform.parent:null;
            var obj=Instantiate(job.prefab,parent);obj.SetActive(true);obj.transform.position=position;
            var unit=obj.GetComponent<BattleUnit>();unit.combatDefinition=Instantiate(unit.combatDefinition);unit.combatDefinition.echelon=job.echelon;
            unit.ownerId=job.owner;unit.unitId=Guid.NewGuid().ToString("N");unit.designation="新建"+LevelUnitAvailability.Name(job.role);unit.startingPersonnelPercent=100;obj.name=LevelUnitAvailability.Name(job.role)+" "+unit.unitId.Substring(0,5);
            var mover=obj.GetComponent<ArmyRoadMover>();mover.waterMap=world.setup.freeMarchMap.waterMap;mover.terrainMap=world.setup.freeMarchMap;
            mover.InitializeAt(world.setup.roadMap.Network,new RoadPoint(position.x,position.y),RoadRouteView.Create(obj.transform,obj.name+" route",.06f,3,job.owner==BattleFactions.RedPlayerId?Color.red:Color.cyan));
            var free=obj.GetComponent<ArmyFreeMarchMover>();free.Initialize(mover.Motor);free.BindMap(world.setup.freeMarchMap);
            world.campaign.ConfigureUnit(unit);unit.Initialize();
            var order=obj.GetComponent<AIUnitOrder>();var ai=GetComponent<BattleAIController>();if(order!=null&&ai!=null&&job.owner==BattleFactions.RedPlayerId){order.ResetTask(ai.defaultGuard);order.profileOverride=ai.profile;}
            var view=obj.GetComponent<BattleUnitView>();if(view==null)view=obj.AddComponent<BattleUnitView>();view.Initialize();
            world.RegisterUnit(unit,mover);
        }
    }
}
