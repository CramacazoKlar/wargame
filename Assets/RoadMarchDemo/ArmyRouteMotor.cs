using System;
using System.Collections.Generic;
using UnityEngine;
namespace CampaignDemo
{
    // One executor for both modes, so two scripts never move the same object simultaneously.
    public sealed class ArmyRouteMotor : MonoBehaviour
    {
        public bool PlanningPaused { get; set; }
        public bool MovementLocked { get; set; }
        float nudgeSpeed=-1;
        public void SetNudgeSpeed(float value){nudgeSpeed=Mathf.Max(0,value);}
        public void Stop() { nudgeSpeed=-1;route.Clear();next=0;CurrentSpeed=0;PlanningPaused=false;Blocked=false;if(view!=null)view.Clear(); }
        public void SetRouteVisible(bool visible) { if(view!=null){var line=view.GetComponent<LineRenderer>();if(line!=null)line.enabled=visible;} }
        public List<RoadPoint> RemainingRoute(){var result=new List<RoadPoint>();if(!IsMoving)return result;var at=transform.position;result.Add(new RoadPoint(at.x,at.y));for(int i=next;i<route.Count;i++)if(RoadPoint.Distance(result[result.Count-1],route[i])>.00001)result.Add(route[i]);return result;}
        public List<RoadPoint> CopyRoute(){return new List<RoadPoint>(route);}
        // Immutable visual anchors for the currently accepted movement order.
        public uint ArrowOrderRevision {get;private set;}
        public Vector2 ArrowOrderStart {get;private set;}
        public Vector2 ArrowOrderEnd {get;private set;}
        public Vector2 ArrowOrderDirection {get;private set;}
        bool networkMoving;
        public bool IsMoving { get { return LocalPvpSession.Active!=null&&!LocalPvpSession.Active.IsHost?networkMoving:next<route.Count; } }
        public bool Blocked { get; private set; }
        List<RoadPoint> route=new List<RoadPoint>();
        int next;
        Func<float> speed;
        Func<RoadPoint,RoadPoint,bool> canMove;
        Func<int> validationVersion;
        int validatedVersion;
        Func<IList<RoadPoint>,List<RoadPoint>> prepareRoute;
        Func<RoadPoint,RoadPoint,float> legSpeed;
        RoadPoint legStart;
        public float CurrentSpeed { get; private set; }
        public MouseCommandMode MovementMode { get; private set; }
        Vector3 expectedPosition;
        RoadRouteView view;readonly List<RoadPoint> arrowRoute=new List<RoadPoint>();
        public void Initialize(RoadRouteView routeView)
        { if(routeView==null) throw new ArgumentNullException(nameof(routeView)); view=routeView;var setup=FindObjectOfType<RoadMarchSceneSetup>();view.ConfigureArrow(setup==null?null:setup.settings); }
        public bool Commit(IList<RoadPoint> points,Func<float> speedProvider,
            Func<RoadPoint,RoadPoint,bool> validator=null,Func<int> versionProvider=null,
            Func<IList<RoadPoint>,List<RoadPoint>> routePreparer=null,
            Func<RoadPoint,RoadPoint,float> legSpeedProvider=null,
            MouseCommandMode movementMode=MouseCommandMode.RoadMarch)
        {
            if(MovementLocked||points==null||points.Count<2||speedProvider==null) return false;
            foreach(var p in points) if(double.IsNaN(p.X)||double.IsNaN(p.Y)||double.IsInfinity(p.X)||double.IsInfinity(p.Y)) return false;
            var at=transform.position;
            var start=new RoadPoint(at.x,at.y);
            if(RoadPoint.Distance(start,points[0])>0.01) return false;
            if(validator!=null&&!Validate(points,1,start,validator)) return false;
            var prepared=routePreparer==null?new List<RoadPoint>(points):routePreparer(points);
            if(prepared==null||prepared.Count<2) return false;
            ArrowOrderStart=new Vector2(at.x,at.y);var last=prepared[prepared.Count-1];ArrowOrderEnd=new Vector2((float)last.X,(float)last.Y);ArrowOrderDirection=Vector2.zero;
            for(int i=1;i<prepared.Count;i++){var delta=new Vector2((float)prepared[i].X,(float)prepared[i].Y)-ArrowOrderStart;if(delta.sqrMagnitude>.00000001f){ArrowOrderDirection=delta.normalized;break;}}
            unchecked{ArrowOrderRevision++;}
            nudgeSpeed=-1;route=prepared; next=1; speed=speedProvider; canMove=validator;
            prepareRoute=routePreparer; legSpeed=legSpeedProvider; legStart=start;
            MovementMode=movementMode;
            validationVersion=versionProvider; validatedVersion=versionProvider==null?0:versionProvider();
            expectedPosition=at; PlanningPaused=false; Blocked=false; if(view!=null)RefreshArrow(); return true;
        }
        static bool Validate(IList<RoadPoint> points,int first,RoadPoint from,Func<RoadPoint,RoadPoint,bool> validator)
        {
            for(int i=first;i<points.Count;i++) { if(!validator(from,points[i])) return false; from=points[i]; }
            return true;
        }
        void RefreshArrow(){if(MergedMovementArrows.Owns(this)){if(view!=null)view.Clear();return;}arrowRoute.Clear();var at=transform.position;arrowRoute.Add(new RoadPoint(at.x,at.y));for(int i=next;i<route.Count;i++)if(RoadPoint.Distance(arrowRoute[arrowRoute.Count-1],route[i])>.00001)arrowRoute.Add(route[i]);view.ShowArrow(arrowRoute);}
        void StopBlocked()
        { Blocked=true; PlanningPaused=false; CurrentSpeed=0; route.Clear(); next=0; if(view!=null) view.Clear(); }
        public void ApplyNetworkState(IList<RoadPoint> points,float actualSpeed,MouseCommandMode mode,bool moving){route=points==null?new List<RoadPoint>():new List<RoadPoint>(points);next=route.Count>1?1:route.Count;networkMoving=moving;CurrentSpeed=actualSpeed;MovementMode=mode;expectedPosition=transform.position;if(view!=null){if(route.Count>1)RefreshArrow();else view.Clear();}}
        void Update(){if(LocalPvpSession.Active==null)Tick(Time.deltaTime);}
        public void Tick(float dt)
        {
            if(MovementLocked||PlanningPaused||!IsMoving||speed==null){CurrentSpeed=0;return;}
            // Full segments were checked at Commit. Recheck only after geometry/permissions
            // change, or another script changes the object's position.
            int version=validationVersion==null?validatedVersion:validationVersion();
            if(canMove!=null&&(version!=validatedVersion||transform.position!=expectedPosition))
            {
                var at=transform.position;
                if(!Validate(route,next,new RoadPoint(at.x,at.y),canMove)) { StopBlocked(); return; }
                if(prepareRoute!=null)
                {
                    var remaining=new List<RoadPoint>{new RoadPoint(at.x,at.y)};
                    for(int i=next;i<route.Count;i++) RoadNetwork.Append(remaining,route[i]);
                    var prepared=prepareRoute(remaining);
                    if(prepared==null||prepared.Count<2) { StopBlocked(); return; }
                    route=prepared; next=1;
                }
                legStart=new RoadPoint(at.x,at.y);
                validatedVersion=version;
            }
            // Combat contact does not cancel an accepted movement path.
            float remainingTime=dt;
            while(IsMoving&&remainingTime>0)
            {
                var p=route[next]; var target=new Vector3((float)p.X,(float)p.Y,0);
                var start=transform.position; float distance=Vector3.Distance(start,target);
                if(distance<0.000001f) { transform.position=target; legStart=p; next++; continue; }
                float value=legSpeed==null?speed():legSpeed(legStart,p);
                if(float.IsNaN(value)||float.IsInfinity(value)||value<0) { StopBlocked(); return; }
                var carrier=GetComponent<PersonnelCarrier>();if(carrier!=null)value*=carrier.SpeedMultiplier;
                float environmentMultiplier=1;var campaignUnit=GetComponent<CampaignUnit>();var actor=GetComponent<BattleUnit>();if(actor!=null&&actor.Campaign!=null){environmentMultiplier=actor.Campaign.MovementMultiplier(actor,start,Vector3.MoveTowards(start,target,value*remainingTime));value*=environmentMultiplier;if(campaignUnit!=null&&!LocalPrototypeOrders.EnabledFor(this))value=Mathf.Min(value,Mathf.Max(0,campaignUnit.desiredSpeed));}
                var localOrder=GetComponent<LocalMarchOrder>();if(localOrder!=null)value=localOrder.ApplySpeed(value,legStart,p,MovementMode,environmentMultiplier);
                if(nudgeSpeed>=0)value=Mathf.Min(value,nudgeSpeed);
                CurrentSpeed=value; if(value==0) break;
                float budget=value*remainingTime;
                var end=distance<=budget?target:Vector3.MoveTowards(start,target,budget);
                var battle=actor;var system=actor!=null&&actor.Campaign!=null?actor.Campaign.combined:FindObjectOfType<CombinedArmsWorld>();
                if(system!=null&&!system.PermitMovement(battle,start,end)){StopBlocked();return;}
                if(campaignUnit!=null)campaignUnit.RecordMovement(end-start);transform.position=end;if(battle!=null&&battle.Campaign!=null)battle.Campaign.world.Replay?.SampleUnit(battle);if(carrier!=null&&battle!=null&&battle.Campaign!=null)foreach(var passenger in carrier.Passengers)battle.Campaign.world.Replay?.SampleUnit(passenger);
                if(distance<=budget) { remainingTime-=distance/value; legStart=p; next++; } else remainingTime=0;
            }
            expectedPosition=transform.position;
            if(IsMoving&&view!=null)RefreshArrow();
            if(!IsMoving) { CurrentSpeed=0; if(view!=null) view.Clear(); }
        }
    }
}






