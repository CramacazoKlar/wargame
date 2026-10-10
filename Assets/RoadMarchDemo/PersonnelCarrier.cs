using System.Collections.Generic;
using UnityEngine;
namespace CampaignDemo
{
    [RequireComponent(typeof(BattleUnit))]
    public sealed class PersonnelCarrier : MonoBehaviour
    {
        [Min(1)] public int maxHealth=200;
        [Min(1)] public int capacity=2000;
        [Min(.01f)] public float boardingRadius=.7f;
        [Range(0,100)] public float artilleryReductionPercent=50;
        readonly List<BattleUnit> passengers=new List<BattleUnit>();
        public IReadOnlyList<BattleUnit> Passengers {get{return passengers;}}
        public BattleUnit Unit {get{return GetComponent<BattleUnit>();}}
        public float Health {get{return Unit.Personnel;}}
        public float Used {get{float n=0;foreach(var u in passengers)if(u!=null&&u.Alive)n+=u.Personnel;return n;}}
        public float PassengerCapacity {get{return Unit.Campaign==null?capacity:Unit.Campaign.rules.At(Unit.Campaign.rules.passengerCapacity,Unit.combatDefinition.echelon)*Unit.Fraction;}}
        public void ApplyPassengerLoss(float before,float after){if(before<=0)return;float fraction=Mathf.Clamp01(after/before);foreach(var passenger in passengers)if(passenger!=null&&passenger.Alive)passenger.Damage(passenger.Personnel*(1-fraction));}
        public float SpeedMultiplier {get{foreach(var u in passengers)if(u!=null&&u.Alive&&u.combatDefinition.role==UnitRole.Artillery)return 1-Mathf.Clamp(artilleryReductionPercent,0,100)/100;return 1;}}
        public CommandResult Load(BattleUnit passenger)
        {
            if(!Unit.OnMap||Unit.Working||Unit.Point!=null||passenger==null||!passenger.OnMap||passenger.ownerId!=Unit.ownerId||passenger==Unit||passenger.Working||passenger.Point!=null)return CommandResult.No("无法装载该部队");
            if(passenger.State!=ArmyBattleState.Deployed&&passenger.State!=ArmyBattleState.Engaged&&passenger.State!=ArmyBattleState.Escaping)return CommandResult.No("该部队正在切换状态");
            var role=passenger.combatDefinition.role;
            if(role!=UnitRole.Infantry&&role!=UnitRole.MountainInfantry&&role!=UnitRole.Scout&&role!=UnitRole.Artillery&&role!=UnitRole.Engineer)return CommandResult.No("只能装载步兵（含山地步兵）、侦察兵、炮兵、工兵");
            if(Used+passenger.Personnel>PassengerCapacity)return CommandResult.No("运兵车容量不足");
            if(Vector2.Distance(transform.position,passenger.transform.position)>boardingRadius)return CommandResult.No("部队距离运兵车太远");
            var battery=passenger.GetComponent<ArtilleryBattery>();if(battery!=null&&battery.Deployed)return CommandResult.No("炮兵需先撤收再上车");if(passenger.Motor!=null)passenger.Motor.Stop();passenger.RevealForest();passenger.SetCarrier(this);passengers.Add(passenger);return CommandResult.Yes();
        }
        public CommandResult Unload(BattleUnit passenger,BattleWorld world)
        {
            if(!Unit.OnMap||!passengers.Contains(passenger)||passenger==null||!passenger.Alive)return CommandResult.No("乘员不存在或车辆不可用");
            // Find a nearby traversable exit; unloading never teleports across water or a barricade.
            var free=passenger.GetComponent<ArmyFreeMarchMover>();var map=world.setup.freeMarchMap;
            var access=free==null?TerrainAccess.Ground|TerrainAccess.Road|TerrainAccess.Grass|TerrainAccess.Forest:free.AllowedTerrain;
            var system=world.GetComponent<CombinedArmsWorld>();Vector3 origin=transform.position;
            int samples=Unit.Campaign==null?24:Unit.Campaign.rules.unloadSearchSamples;int directions=Unit.Campaign==null?8:Mathf.Max(1,Unit.Campaign.rules.unloadDirections);float first=Unit.Campaign==null?.18f:Unit.Campaign.rules.unloadFirstRadius;float step=Unit.Campaign==null?.12f:Unit.Campaign.rules.unloadRingStep;
            for(int i=0;i<samples;i++)
            {
                float angle=i*Mathf.PI*2/directions;float radius=first+step*(i/directions);var p=origin+new Vector3(Mathf.Cos(angle)*radius,Mathf.Sin(angle)*radius);
                if(map==null||!map.CanTraverse(new RoadPoint(origin.x,origin.y),new RoadPoint(p.x,p.y),access))continue;
                if(Unit.Campaign==null&&system!=null&&system.IntersectsFortification(origin,p))continue;
                passengers.Remove(passenger);passenger.transform.position=p;passenger.SetCarrier(null);return CommandResult.Yes();
            }
            return CommandResult.No("周围没有安全下车位置");
        }
        public void SyncPassengers(){foreach(var u in passengers)if(u!=null)u.transform.position=transform.position;}
        public void KillPassengers(){var copy=passengers.ToArray();passengers.Clear();foreach(var u in copy)if(u!=null){u.Campaign?.world.Replay?.RecordDeath(u);u.SetCarrier(null);u.Eliminate();}}
    }
}




