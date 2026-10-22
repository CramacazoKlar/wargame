using System.Collections.Generic;
using UnityEngine;
namespace CampaignDemo
{
    [RequireComponent(typeof(BattleUnit),typeof(ArmyRoadMover))]
    public sealed class TransportTruck : MonoBehaviour
    {
        public TransportDefinition definition;
        public RecoveryPoint sourceCity;
        public RecoveryPoint destinationCity;
        [Tooltip("可保存人工指定的去程世界坐标路线；返程使用反向路线。为空则自动沿道路网寻路。")]
        public List<Vector2> outboundRoute=new List<Vector2>();
        public float Cargo {get;private set;}
        public bool Automatic {get;private set;}
        public string Status {get;private set;}="手动运输";
        public BattleUnit Unit {get{return GetComponent<BattleUnit>();}}
        public float Capacity {get{if(Unit!=null&&Unit.Campaign!=null)return Unit.Campaign.rules.At(Unit.Campaign.rules.cargoCapacity,Unit.combatDefinition.echelon)*Unit.Fraction*TacticalDetachment.Scale(Unit);return definition==null||Unit==null||Unit.combatDefinition==null?0:definition.Capacity(Unit.combatDefinition.echelon)*Mathf.Clamp01(Unit.Fraction);}}
        bool returning;bool issued;float retry;
        public void ApplyPersonnelLoss(float before,float after){Cargo=before<=0?0:Cargo*Mathf.Clamp01(after/before);if(after<=0){Cargo=0;Automatic=false;}}
        public void ApplyNetworkState(float cargo,bool automatic){Cargo=cargo;Automatic=automatic;}
        public void SetCargo(float value){Cargo=Mathf.Clamp(value,0,Capacity);}
        public void Load(float value){Cargo=Mathf.Min(Capacity,Cargo+Mathf.Max(0,value));}
        public void ClearCargo(){Cargo=0;}
        public void SetLocalAutomatic(bool value){Automatic=value;Status=value?"自动运输":"手动运输";}
        public void StartAutomatic(){if(LocalPrototypeOrders.EnabledFor(this)){LocalPrototypeOrders.For(Unit)?.StartAutomatic(this);return;}Automatic=true;returning=Cargo<=0;issued=false;retry=0;Status="自动运输";}
        public void StopAutomatic(){if(LocalPrototypeOrders.EnabledFor(this))LocalPrototypeOrders.For(Unit)?.CancelUnit(Unit,true);Automatic=false;issued=false;Status="手动运输";}
        public void ManualOrder(){if(LocalPrototypeOrders.EnabledFor(this)){LocalPrototypeOrders.For(Unit)?.CancelUnit(Unit,false);return;}if(Automatic)StopAutomatic();}
        public void SaveRoute(IList<RoadPoint> route){outboundRoute.Clear();foreach(var p in route)outboundRoute.Add(new Vector2((float)p.X,(float)p.Y));}
        public void Tick(LogisticsWorld logistics,float dt)
        {
            if(LocalPrototypeOrders.EnabledFor(this))return;var u=Unit;if(u==null||!u.Alive){Cargo=0;Automatic=false;return;}if(!Automatic)return;
            if(u.State!=ArmyBattleState.Deployed){Status="等待部队脱离交战或驻点状态";return;}
            if(definition==null||destinationCity==null||destinationCity.ownerId!=u.ownerId||destinationCity.kind!=FacilityKind.RecoveryPoint||destinationCity==sourceCity){StopAutomatic();Status="运输目标不可用";return;}
            if(sourceCity==null||sourceCity.ownerId!=u.ownerId){StopAutomatic();Status="运输来源失效";return;}
            var motor=u.Motor;if(motor==null)return;if(motor.IsMoving||motor.PlanningPaused)return;
            var target=returning?sourceCity.transform:destinationCity.transform;
            float radius=UnitRanges.Interaction(u);
            if(Vector2.Distance(transform.position,target.position)<=radius)
            {
                if(returning){logistics.LoadAtSource(this);if(Cargo<=0){Status="等待资源";return;}returning=false;}
                else{logistics.Unload(this,destinationCity);returning=true;}
                issued=false;
            }
            else if(issued){issued=false;Status="路线结束但没有到达装卸范围";}
            retry-=dt;if(retry>0)return;retry=1;
            target=returning?sourceCity.transform:destinationCity.transform;
            var mover=GetComponent<ArmyRoadMover>();var route=logistics.RouteTo(transform.position,target.position,outboundRoute,returning);
            var commands=logistics.GetComponent<BattleCommandService>();bool accepted=route!=null&&(commands==null?mover.CommitAutomaticRoute(route):commands.Submit(new BattleCommand{playerId=u.ownerId,unitId=u.unitId,kind=BattleCommandKind.RoadRoute,route=route,preserveTransportAutomation=true}).accepted);
            if(!accepted){Status="道路不连通或路线不可通行";return;}issued=true;Status=returning?"返回装货":"运往城市";
        }
    }
}


