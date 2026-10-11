using System;
using System.Collections.Generic;
using UnityEngine;

namespace CampaignDemo
{
    // One per army. Executes accepted routes; does not read mouse input or choose routes.
    public sealed class ArmyRoadMover : MonoBehaviour
    {
        public ArmyDefinition definition;
        public RasterWaterMap waterMap;
        public FreeMarchMapRuntime terrainMap;
        public bool overrideSpeed;
        [Min(0.01f)] public float speedOverride=2;
        public ArmyRouteMotor Motor { get; private set; }
        public bool PlanningPaused { get { return Motor!=null&&Motor.PlanningPaused; } set { if(Motor!=null) Motor.PlanningPaused=value; } }
        public RoadHit CurrentRoad { get; private set; }
        public bool IsOnRoad { get { return CurrentRoad.Distance<=0.01; } }
        MapReferenceHUD distanceReference;
        public float Speed { get {
            if(!overrideSpeed&&definition!=null&&definition.useReferenceTravelTime){
                if(distanceReference==null)distanceReference=FindObjectOfType<MapReferenceHUD>();
                float meters=distanceReference!=null?distanceReference.metersPerWorldUnit:1000;
                return ArmyDefinition.ReferenceRoadSpeed(definition.referenceDistanceMeters,definition.referenceTravelMinutes,meters,definition.roadSpeedRatio);
            }
            return 0.5f*Mathf.Max(0.01f,overrideSpeed||definition==null?speedOverride:definition.speed);
        } }
        public bool IsMoving { get { return Motor!=null&&Motor.IsMoving; } }
        RoadNetwork network;

        public void Initialize(RoadNetwork graph,RoadHit spawn,RoadRouteView view)
        { InitializeAt(graph,spawn.Position,view); }

        // An army can start anywhere. Mode 1 adds a straight approach to the closest road.
        public void InitializeAt(RoadNetwork graph,RoadPoint position,RoadRouteView view)
        {
            if(definition==null||graph==null||view==null) throw new ArgumentException("Army needs a definition, graph and route view.");
            network=graph;
            Motor=GetComponent<ArmyRouteMotor>();
            if(Motor==null) Motor=gameObject.AddComponent<ArmyRouteMotor>();
            Motor.Initialize(view);
            transform.position=new Vector3((float)position.X,(float)position.Y,0);
            RefreshRoad();
            if(network.Edges.Length==0) throw new ArgumentException("Map has no roads.");
        }
        public bool CommitRoute(IList<RoadPoint> points)
        {
            var truck=GetComponent<TransportTruck>();bool accepted=CommitAutomaticRoute(points);if(accepted){if(truck!=null)truck.ManualOrder();var unit=GetComponent<BattleUnit>();if(unit!=null)unit.RevealForest();}return accepted;
        }
        public TerrainAccess AllowedTerrain {get{var free=GetComponent<ArmyFreeMarchMover>();return free!=null?free.AllowedTerrain:definition!=null&&definition.freeMarchDefinition!=null?definition.freeMarchDefinition.allowedTerrain&~TerrainAccess.Water:TerrainAccess.Ground|TerrainAccess.Road|TerrainAccess.Grass|TerrainAccess.Forest|TerrainAccess.Mountain;}}
        public bool CanTraverse(RoadPoint a,RoadPoint b){return (waterMap==null||waterMap.CanTraverse(a,b))&&(terrainMap==null||terrainMap.CanTraverse(a,b,AllowedTerrain));}
        public float RoadLegSpeed(RoadPoint a,RoadPoint b){if(terrainMap==null)return Speed;var kind=terrainMap.TerrainAt(RoadPoint.Lerp(a,b,.5));var free=GetComponent<ArmyFreeMarchMover>();var rules=free!=null?(free.overrideSettings||free.definition==null?free.terrainReductions:free.definition.terrainReductions):definition!=null&&definition.freeMarchDefinition!=null?definition.freeMarchDefinition.terrainReductions:null;return TerrainSpeedRules.Calculate(Speed,0,TerrainSpeedRules.TerrainReduction(kind,rules));}
        public bool CommitAutomaticRoute(IList<RoadPoint> points)
        {
            bool accepted=Motor!=null&&Motor.Commit(points,()=>this==null?float.NaN:Speed,(a,b)=>this!=null&&CanTraverse(a,b),()=>terrainMap==null?0:unchecked(terrainMap.Version*397^(int)AllowedTerrain),path=>terrainMap==null?new List<RoadPoint>(path):terrainMap.SplitRoute(path),(a,b)=>this==null?0:RoadLegSpeed(a,b));if(accepted){var unit=GetComponent<BattleUnit>();if(unit!=null)unit.RevealForest();}return accepted;
        }

        public void RefreshRoad()
        {
            if(network==null) return;
            var at=transform.position;
            var hits=network.Candidates(new RoadPoint(at.x,at.y),double.PositiveInfinity);
            if(hits.Count>0) CurrentRoad=hits[0];
        }
    }
}





