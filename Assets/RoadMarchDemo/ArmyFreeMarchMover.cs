using System.Collections.Generic;
using UnityEngine;
namespace CampaignDemo
{
    public sealed class ArmyFreeMarchMover : MonoBehaviour
    {
        public ArmyFreeMarchDefinition definition;
        public bool overrideSettings;
        [Range(0,100)] public float mode2ReductionPercent=25;
        public TerrainSpeedPenalty[] terrainReductions=new TerrainSpeedPenalty[0];
        public TerrainAccess allowedTerrain=TerrainAccess.Ground|TerrainAccess.Road|TerrainAccess.Grass|TerrainAccess.Forest;
        public float Speed { get { return SpeedAt(CurrentTerrain); } }
        public TerrainKind CurrentTerrain
        { get { return activeMap==null?TerrainKind.Ground:activeMap.TerrainAt(WorldPoint()); } }
        public TerrainAccess AllowedTerrain { get { return (overrideSettings||definition==null?allowedTerrain:definition.allowedTerrain)&~TerrainAccess.Water; } }
        ArmyRouteMotor motor;
        ArmyRoadMover roadMover;
        FreeMarchMapRuntime activeMap;
        public void Initialize(ArmyRouteMotor sharedMotor)
        { motor=sharedMotor; roadMover=GetComponent<ArmyRoadMover>(); }
        public void BindMap(FreeMarchMapRuntime map) { activeMap=map;if(roadMover==null)roadMover=GetComponent<ArmyRoadMover>();if(roadMover!=null)roadMover.terrainMap=map; }
        RoadPoint WorldPoint() { var p=transform.position; return new RoadPoint(p.x,p.y); }
        public float SpeedAt(TerrainKind terrain)
        {
            if(roadMover==null) roadMover=GetComponent<ArmyRoadMover>();
            float original=roadMover==null?0:roadMover.Speed;
            bool own=overrideSettings||definition==null;
            return TerrainSpeedRules.Calculate(original,own?mode2ReductionPercent:definition.mode2ReductionPercent,
                TerrainSpeedRules.TerrainReduction(terrain,own?terrainReductions:definition.terrainReductions));
        }
        public bool CanTraverse(FreeMarchMapRuntime map,RoadPoint from,RoadPoint to) { return map!=null&&map.CanTraverse(from,to,AllowedTerrain); }
        public bool CommitRoute(IList<RoadPoint> points,FreeMarchMapRuntime map)
        {
            if(motor==null||map==null||map.definition==null||points==null||points.Count<2) return false;
            BindMap(map);
            bool accepted=motor.Commit(points,()=>Speed,(a,b)=>this!=null&&CanTraverse(map,a,b),
                ()=>map==null||this==null?int.MinValue:unchecked(map.Version*397^(int)AllowedTerrain),
                path=>map==null?null:map.SplitRoute(path),
                (a,b)=>this==null||map==null?0:SpeedAt(map.TerrainAt(RoadPoint.Lerp(a,b,0.5))),
                MouseCommandMode.FreeMarch);
            var truck=GetComponent<TransportTruck>();if(accepted){if(truck!=null)truck.ManualOrder();var unit=GetComponent<BattleUnit>();if(unit!=null)unit.RevealForest();}return accepted;
        }
    }
}





