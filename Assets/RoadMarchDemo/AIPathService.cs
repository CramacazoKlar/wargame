using System.Collections.Generic;
using UnityEngine;
namespace CampaignDemo
{
    public sealed class AIPlannedRoute
    {
        public List<RoadPoint> points;
        public bool road;
        public float length;
    }
    public static class AIPathService
    {
        public static AIPlannedRoute Plan(BattleWorld world, BattleUnit unit, Vector3 destination, bool preferRoad, bool groundFallback)
        {
            if(world==null||world.setup==null||unit==null) return null;
            var mover=unit.GetComponent<ArmyRoadMover>();
            if(mover==null) return null;
            if(LocalTacticalBattle.EnabledFor(world))
            {
                var freeMover=unit.GetComponent<ArmyFreeMarchMover>();
                var route=freeMover==null?null:LocalRoadAStar.Plan(world.setup.freeMarchMap,freeMover.AllowedTerrain,unit.transform.position,destination,Vector3.Distance(unit.transform.position,destination)>12?.4f:.2f,100000);
                return route==null?null:Result(route,false);
            }
            if(preferRoad&&world.setup.roadMap!=null)
            {
                var network=world.setup.roadMap.Network;
                var path=RoadOrderPlanner.ToDestination(network,unit.transform.position,destination);
                bool valid=RoadOrderPlanner.Validate(network,path,true);
                if(valid) for(int i=1;i<path.Count;i++) if(!mover.CanTraverse(path[i-1],path[i])) {valid=false;break;}
                if(valid) return Result(path,true);
            }
            if(!groundFallback) return null;
            var free=unit.GetComponent<ArmyFreeMarchMover>();
            if(free==null) return null;
            var ground=ConstructionNavigation.Plan(world.setup.freeMarchMap,free.AllowedTerrain,unit.transform.position,destination,.35f,40000);
            return ground==null?null:Result(ground,false);
        }
        static AIPlannedRoute Result(List<RoadPoint> points,bool road)
        {
            float length=0;for(int i=1;i<points.Count;i++)length+=(float)RoadPoint.Distance(points[i-1],points[i]);
            return new AIPlannedRoute{points=points,road=road,length=length};
        }
    }
}
