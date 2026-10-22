using System.Linq;
using UnityEngine;
namespace CampaignDemo
{
    public sealed class CitySnapshot
    {
        public string name,owner,building;
        public float stock;public int capacity;public bool factory;
        public BattleUnit[] garrison,nearby;
        public InfantryProduction.Job[] orders;
    }
    public sealed class CityManagement : MonoBehaviour
    {
        public BattleWorld world;
        public InfantryProduction Production {get{return GetComponent<InfantryProduction>();}}
        public LogisticsWorld Logistics {get{return GetComponent<LogisticsWorld>();}}
        void Awake(){world=GetComponent<BattleWorld>();if(Production==null)gameObject.AddComponent<InfantryProduction>();}
        public CitySnapshot Snapshot(RecoveryPoint city,string viewer)
        {
            if(city==null||world==null||city.ownerId!=viewer||System.Array.IndexOf(world.players,viewer)<0)return null;
            var garrison=world.units.Where(u=>u!=null&&u.Alive&&u.ownerId==viewer&&u.Point==city).OrderBy(u=>u.unitId).ToArray();
            return new CitySnapshot{name=city.displayName,owner=city.ownerId,stock=city.resourceStock,capacity=city.capacity,factory=city.hasFactory,
                building=Logistics==null?"未配置建筑模块":Logistics.BuildStatus(city),garrison=garrison,
                nearby=world.units.Where(u=>u!=null&&u.Alive&&u.ownerId==viewer&&u.OnMap&&u.Point!=city&&Vector2.Distance(u.transform.position,city.transform.position)<=UnitRanges.Interaction(u)).OrderBy(u=>u.unitId).ToArray(),
                orders=Production==null?new InfantryProduction.Job[0]:Production.Jobs.Where(j=>j.city==city&&j.owner==viewer).ToArray()};
        }
        CityActionQuote Owned(RecoveryPoint city,string viewer)
        {return world!=null&&world.Initialized&&!world.SimulationEnded&&city!=null&&city.ownerId==viewer?CityActionQuote.Yes():CityActionQuote.No("只能管理正在对局中的己方城市");}
        public CityActionQuote Enter(BattleUnit unit,RecoveryPoint city,string viewer)
        {
            var own=Owned(city,viewer);if(!own.allowed)return own;
            if(unit==null||!unit.Alive||unit.ownerId!=viewer)return CityActionQuote.No("单位不属于己方");
            if(Snapshot(city,viewer).garrison.Length>=city.capacity)return CityActionQuote.No("驻军位置已满");
            if(!world.CanEnter(unit,city))return CityActionQuote.No("当前不满足距离、状态或安全入城条件");
            return CityActionQuote.Yes(0,0);
        }
        public CityActionQuote Repair(BattleUnit unit,RecoveryPoint city,string viewer)
        {
            var own=Owned(city,viewer);if(!own.allowed)return own;
            if(unit==null||!unit.Alive||unit.ownerId!=viewer||unit.Point!=city)return CityActionQuote.No("单位不在己方城市中");
            if(FacilityRecoveryQueue.For(world)?.Contains(unit)==true||unit.State==ArmyBattleState.Repairing)return CityActionQuote.Yes();
            if(unit.State!=ArmyBattleState.InsidePoint)return CityActionQuote.No("请等待入城完成");
            if(unit.Fraction>=1)return CityActionQuote.No("已经恢复至满编");
            if(!LocalPrototypeOrders.EnabledFor(this)&&city.resourcePerPerson>0&&city.resourceStock<=0)return CityActionQuote.No("城市没有修复资源");
            return CityActionQuote.Yes((unit.Capacity-unit.Personnel)*city.resourcePerPerson,(1-unit.Fraction)/Mathf.Max(.001f,city.repairFractionPerSecond));
        }
        public CityActionQuote Exit(BattleUnit unit,RecoveryPoint city,string viewer)
        {
            var own=Owned(city,viewer);if(!own.allowed)return own;
            return unit!=null&&unit.Alive&&unit.ownerId==viewer&&unit.Point==city&&(unit.State==ArmyBattleState.InsidePoint||unit.State==ArmyBattleState.Repairing)?CityActionQuote.Yes(0,LocalPrototypeOrders.EnabledFor(this)?0:city.transitionSeconds):CityActionQuote.No("请等待进出城完成");
        }
        public CityActionQuote Upgrade(RecoveryPoint city,string viewer)
        {
            var own=Owned(city,viewer);if(!own.allowed)return own;
            if(city.isRepairSite)return CityActionQuote.No("修复点不能生产或升级兵工厂");if(Logistics==null)return CityActionQuote.No("建筑模块未配置");
            if(city.hasFactory)return CityActionQuote.No("兵工厂已建成");
            if(Logistics.IsBuilding(city))return CityActionQuote.No("城市正在生产或升级");
            if(Logistics.FactoryCount(viewer,true)>=Logistics.factoryLimitPerPlayer)return CityActionQuote.No("兵工厂数量已达上限");
            if(city.resourceStock<Logistics.factoryBuildCost)return CityActionQuote.No("城市资源不足");
            return CityActionQuote.Yes(Logistics.factoryBuildCost,Logistics.factoryBuildSeconds);
        }
        public CityActionQuote Truck(RecoveryPoint city,string viewer,ArmyEchelon level)
        {
            var own=Owned(city,viewer);if(!own.allowed)return own;
            if(city.isRepairSite)return CityActionQuote.No("修复点不能生产单位");if(!LevelUnitAvailability.Allowed(world,UnitRole.Logistics))return CityActionQuote.No("本关未开放资源车");
            var prefab=Logistics==null?null:Logistics.truckPrefab;var truck=prefab==null?null:prefab.GetComponent<TransportTruck>();
            if(truck==null||truck.definition==null)return CityActionQuote.No("资源车预制体未配置");
            if(Logistics.IsBuilding(city))return CityActionQuote.No("城市生产位正在使用");
            float cost=truck.definition.BuildCost(level);if(city.resourceStock<cost)return CityActionQuote.No("城市资源不足");
            return CityActionQuote.Yes(cost,truck.definition.buildSeconds);
        }
        public CityActionQuote ProductionQuote(RecoveryPoint city,string viewer,bool truck,ArmyEchelon level,int count,UnitRole role=UnitRole.Infantry)
        {
            if(city!=null&&city.isRepairSite)return CityActionQuote.No("修复点不能生产单位");var quote=truck?Truck(city,viewer,level):Production==null?CityActionQuote.No("生产模块未配置"):Production.Quote(viewer,city,level,false,count,role);
            if(!quote.allowed)
            {
                var template=Logistics==null||Logistics.truckPrefab==null?null:Logistics.truckPrefab.GetComponent<TransportTruck>();
                quote.cost=truck?(template==null||template.definition==null?0:template.definition.BuildCost(level)):(Production==null?0:Production.Cost(level,role)*count);
                quote.seconds=truck?(template==null||template.definition==null?0:template.definition.buildSeconds):(Production==null?0:Production.Duration(level,role)*count);
            }
            return quote;
        }
    }
}
