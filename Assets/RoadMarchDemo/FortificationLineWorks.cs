using System;
using System.Collections.Generic;
using UnityEngine;
namespace CampaignDemo
{
    public sealed class FortificationLineWorks:MonoBehaviour
    {
        public CampaignRuntime campaign;
        public sealed class Project
        {public BattleUnit worker;public Vector3[] points;public Vector3 side;public Fortification fort;public RecoveryPoint payer;public float cost,totalTime,remaining,lengthMeters;public int level;public string owner;public bool approaching;public Vector3 site;public float cutFrom,cutTo;}
        readonly Dictionary<BattleUnit,PvpUnit> networkWork=new Dictionary<BattleUnit,PvpUnit>();
        public void ApplyNetworkWork(BattleUnit worker,PvpUnit data){networkWork[worker]=data;}
        bool ReplicaWork(BattleUnit worker,out PvpUnit data){data=null;return LocalPvpSession.Active!=null&&!LocalPvpSession.Active.IsHost&&networkWork.TryGetValue(worker,out data);}
        readonly List<Project> projects=new List<Project>();
        sealed class Helper {public BattleUnit worker;public Project project;public bool arriving;}
        readonly List<Helper> helpers=new List<Helper>();
        public CommandResult Assist(BattleUnit worker,Vector3 point){
            if(worker==null||!worker.OnMap||worker.combatDefinition.role!=UnitRole.Engineer)return CommandResult.No("需地图上的工兵");
            if(Working(worker)){Cancel(worker);return CommandResult.No("已取消原施工任务，请再次右键指派");}
            if(worker.Working)return CommandResult.No("工兵正忙");Project chosen=null;float distance=.75f;
            foreach(var p in projects)if(p.fort==null&&p.owner==worker.ownerId)for(int i=1;i<p.points.Length;i++){Vector2 d=p.points[i]-p.points[i-1];float t=d.sqrMagnitude>0?Mathf.Clamp01(Vector2.Dot((Vector2)(point-p.points[i-1]),d)/d.sqrMagnitude):0;float r=Vector2.Distance(point,(Vector2)p.points[i-1]+d*t);if(r<distance){distance=r;chosen=p;}}
            if(chosen==null)return CommandResult.No("请右键点击己方正在施工的防线");
            var free=worker.GetComponent<ArmyFreeMarchMover>();var route=ConstructionNavigation.Plan(campaign.world.setup.freeMarchMap,free.AllowedTerrain,worker.transform.position,chosen.site,Rules.navigationCellSize,Rules.navigationSearchLimit);
            if(route==null||!free.CommitRoute(route,campaign.world.setup.freeMarchMap))return CommandResult.No("工兵无法抵达施工点");helpers.Add(new Helper{worker=worker,project=chosen,arriving=true});return CommandResult.Yes();
        }
        public List<PvpWork> NetworkProjects(string viewer){var result=new List<PvpWork>();foreach(var p in projects)if(p.fort==null&&p.owner==viewer)result.Add(new PvpWork{worker=p.worker.unitId,owner=p.owner,points=p.points,side=p.side,site=p.site,total=p.totalTime,remaining=p.remaining,length=p.lengthMeters,level=p.level,approaching=p.approaching});return result;}
        public void ApplyNetworkProjects(List<PvpWork> incoming){projects.Clear();if(incoming==null)return;foreach(var data in incoming){BattleUnit worker=null;foreach(var u in campaign.world.units)if(u!=null&&u.unitId==data.worker){worker=u;break;}if(worker!=null)projects.Add(new Project{worker=worker,owner=data.owner,points=data.points,side=data.side,site=data.site,totalTime=data.total,remaining=data.remaining,lengthMeters=data.length,level=data.level,approaching=data.approaching});}}
        public IEnumerable<Project> ActiveProjects {get{return projects;}}
        public float Progress(Project p){return p.totalTime<=0?1:1-p.remaining/p.totalTime;}
        sealed class Removal {public string player;public float remaining,total;}
        readonly Dictionary<Fortification,Removal> removals=new Dictionary<Fortification,Removal>();
        Material material;
        public EngineeringDefinition Rules {get{return campaign.combined.engineering;}}
        public static Vector3 ClampToVision(BattleUnit engineer,Vector3 origin,Vector3 point){point.z=0;Vector3 delta=point-origin;return origin+Vector3.ClampMagnitude(delta,engineer.combatDefinition.visionRadius);}
        public static float Length(IList<Vector3> points){float length=0;if(points!=null)for(int i=1;i<points.Count;i++)length+=Vector2.Distance(points[i-1],points[i]);return length;}
        public RecoveryPoint NearestCity(string player,Vector3 position){RecoveryPoint nearest=null;float distance=float.MaxValue;foreach(var city in campaign.world.points)if(city!=null&&city.ownerId==player&&city.kind==FacilityKind.RecoveryPoint){float d=Vector2.Distance(city.transform.position,position);if(d<distance){distance=d;nearest=city;}}return nearest;}
        public bool CanBuild(BattleUnit worker,IList<Vector3> points,out string reason)
        {
            reason="";if(worker==null||!worker.OnMap||worker.Working||Working(worker)||worker.combatDefinition.role!=UnitRole.Engineer){reason="只有空闲地图工兵可以施工";return false;}
            if(points==null||points.Count<2||points.Count>Rules.maximumLinePoints||Length(points)<Rules.minimumLineWorldLength){reason="防线太短或点数超限";return false;}
            var map=campaign.world.setup.freeMarchMap;var free=worker.GetComponent<ArmyFreeMarchMover>();if(map==null||free==null){reason="缺少地形引用";return false;}
            if(LocalTacticalBattle.EnabledFor(this)&&Length(points)>(LocalPrototypeOrders.EnabledFor(this)?Mathf.Max(8,Rules.maximumLineWorldLength):Rules.maximumLineWorldLength)){reason="超过单次最大修建长度（"+(LocalPrototypeOrders.EnabledFor(this)?Mathf.Max(8,Rules.maximumLineWorldLength):Rules.maximumLineWorldLength).ToString("0.#")+" km）";return false;}
            foreach(var p in points)if(!CombinedArmsWorld.Finite(p)||(!LocalTacticalBattle.EnabledFor(this)&&Vector2.Distance(worker.transform.position,p)>worker.combatDefinition.visionRadius+.0001f)){reason="防线超出工兵视野";return false;}
            // Connected, crossing and overlapping lines are allowed; terrain still applies.
            for(int i=1;i<points.Count;i++){var a=points[i-1];var b=points[i];if(!map.CanTraverse(new RoadPoint(a.x,a.y),new RoadPoint(b.x,b.y),free.AllowedTerrain)){reason="防线经过水域或禁行地形";return false;}}
            Vector3 site=LocalTacticalBattle.EnabledFor(this)?points[0]:(points[0]+points[points.Count-1])/2;if(!map.CanTraverse(new RoadPoint(site.x,site.y),new RoadPoint(site.x,site.y),free.AllowedTerrain)){reason="起终点中点不通行，无法施工";return false;}
            float cost=Length(points)*Rules.metersPerWorldUnit*Rules.lineCostPerMeter;var city=NearestCity(worker.ownerId,worker.transform.position);if(city==null||city.resourceStock<cost){reason="最近己方城市库存不足；不会改扣更远城市";return false;}return true;
        }
        public CommandResult Build(BattleUnit worker,IList<Vector3> points)
        {
            if(!CanBuild(worker,points,out var reason))return CommandResult.No(reason);
            Vector3 site=LocalTacticalBattle.EnabledFor(this)?points[0]:(points[0]+points[points.Count-1])/2;site.z=0;var free=worker.GetComponent<ArmyFreeMarchMover>();var route=ConstructionNavigation.Plan(campaign.world.setup.freeMarchMap,free.AllowedTerrain,worker.transform.position,site,Rules.navigationCellSize,Rules.navigationSearchLimit);if(route==null)return CommandResult.No("施工点不通行或无法到达");
            bool approaching=Vector2.Distance(worker.transform.position,site)>Rules.constructionArrivalDistance;if(approaching&&(worker.Motor==null||!free.CommitRoute(route,campaign.world.setup.freeMarchMap)))return CommandResult.No("工兵无法执行到中点的移动路线");
            float length=Length(points)*Rules.metersPerWorldUnit;var city=NearestCity(worker.ownerId,worker.transform.position);float cost=length*Rules.lineCostPerMeter;city.resourceStock-=cost;float time=length*Rules.lineBuildSecondsPerMeter;
            var p=new Project{worker=worker,points=new List<Vector3>(points).ToArray(),side=worker.transform.position,site=site,approaching=approaching,payer=city,cost=cost,totalTime=time,remaining=time,lengthMeters=length,level=worker.GetComponent<CampaignUnit>().fortBuildLevel,owner=worker.ownerId};Begin(p);return CommandResult.Yes();
        }
        void Begin(Project p){if(!p.approaching&&p.worker.Motor!=null)p.worker.Motor.Stop();p.worker.RevealForest();p.worker.GetComponent<CampaignUnit>().fortTarget=null;p.worker.SetWorking(!p.approaching);projects.Add(p);}
        public sealed class BreachPlan
        {public Fortification target;public float from,to,lengthMeters,time;public Vector3 site;public Vector3[] effectivePoints;public bool own;}
        public bool PlanBreach(BattleUnit worker,IList<Vector3> drawing,out BreachPlan plan,out string reason)
        {
            plan=null;reason="";if(worker==null||!worker.OnMap||worker.Working||Working(worker)||worker.combatDefinition.role!=UnitRole.Engineer){reason="需空闲地图工兵";return false;}
            if(drawing==null||drawing.Count<2||drawing.Count>Rules.maximumLinePoints){reason="至少设置两个拆除节点";return false;}
            var samples=new List<Vector3>();for(int i=0;i<drawing.Count;i++){if(!CombinedArmsWorld.Finite(drawing[i])||Vector2.Distance(worker.transform.position,drawing[i])>worker.combatDefinition.visionRadius+.0001f){reason="拆除节点超出工兵视野";return false;}if(i==0){samples.Add(drawing[i]);continue;}float length=Vector2.Distance(drawing[i-1],drawing[i]);int count=Mathf.Max(1,Mathf.CeilToInt(length/Mathf.Max(.001f,Rules.demolitionSampleDistance)));for(int j=1;j<=count;j++)samples.Add(Vector3.Lerp(drawing[i-1],drawing[i],(float)j/count));}
            Fortification selected=null;float selectedFrom=0,selectedTo=0,best=float.MaxValue;
            foreach(var f in campaign.combined.fortifications)
            {
                if(f==null||f.Destroyed||!f.IsLine||!f.VisibleTo(campaign.world,worker.ownerId))continue;
                float score=0,min=float.MaxValue,max=0;bool valid=true;int direction=0;float previous=-1;
                foreach(var sample in samples){float arc=f.ProjectArc(sample,out var projected);float distance=Vector2.Distance(sample,projected);if(distance>Rules.demolitionSnapDistance||(f.ownerId!=worker.ownerId&&campaign.world.fogEnabled&&!campaign.world.VisibleAt(worker.ownerId,projected))){valid=false;break;}if(previous>=0&&Mathf.Abs(arc-previous)>.0001f){int sign=arc>previous?1:-1;if(direction!=0&&sign!=direction){valid=false;break;}direction=sign;}previous=arc;min=Mathf.Min(min,arc);max=Mathf.Max(max,arc);score+=distance;}
                if(valid&&max-min>=Rules.minimumBreachWorldLength&&score<best){selected=f;selectedFrom=min;selectedTo=max;best=score;}
            }
            if(selected==null){reason="拆除线需贴近同一条可见防线，不能跨线或来回折返";return false;}
            var points=selected.Slice(selectedFrom,selectedTo);Vector3 site=(points[0]+points[points.Length-1])/2;var free=worker.GetComponent<ArmyFreeMarchMover>();var map=campaign.world.setup.freeMarchMap;if(free==null||map==null||!map.CanTraverse(new RoadPoint(site.x,site.y),new RoadPoint(site.x,site.y),free.AllowedTerrain)){reason="拆除段中点在禁行地形";return false;}
            float physical=selected.WorldLength>0?selected.builtLengthMeters/selected.WorldLength:Rules.metersPerWorldUnit;float meters=(selectedTo-selectedFrom)*physical;bool own=selected.ownerId==worker.ownerId;
            plan=new BreachPlan{target=selected,from=selectedFrom,to=selectedTo,effectivePoints=points,site=site,lengthMeters=meters,own=own,time=meters*Rules.lineDismantleSecondsPerMeter*(own?Rules.ownLineDemolitionTimeMultiplier:1)};return true;
        }
        public CommandResult DismantleSection(BattleUnit worker,IList<Vector3> drawing)
        {
            if(!PlanBreach(worker,drawing,out var plan,out var reason))return CommandResult.No(reason);foreach(var existing in projects)if(existing.fort==plan.target)return CommandResult.No("该防线已有拆除任务，请等待或取消");
            var free=worker.GetComponent<ArmyFreeMarchMover>();var route=ConstructionNavigation.Plan(campaign.world.setup.freeMarchMap,free.AllowedTerrain,worker.transform.position,plan.site,Rules.navigationCellSize,Rules.navigationSearchLimit);if(route==null)return CommandResult.No("拆除中点无法到达");
            bool approaching=Vector2.Distance(worker.transform.position,plan.site)>Rules.constructionArrivalDistance;if(approaching&&(worker.Motor==null||!free.CommitRoute(route,campaign.world.setup.freeMarchMap)))return CommandResult.No("无法执行拆除路线");
            Begin(new Project{worker=worker,fort=plan.target,points=plan.effectivePoints,site=plan.site,approaching=approaching,cutFrom=plan.from,cutTo=plan.to,lengthMeters=plan.lengthMeters,totalTime=plan.time,remaining=plan.time,owner=worker.ownerId});return CommandResult.Yes();
        }
        public List<Fortification> OpenBreach(Fortification original,float from,float to)
        {
            var result=new List<Fortification>();if(original==null||original.Destroyed||!original.IsLine)return result;float total=original.WorldLength;from=Mathf.Clamp(from,0,total);to=Mathf.Clamp(to,from,total);if(to-from<=.00001f)return result;
            float current=original.Health,maximum=original.MaximumHealth;string owner=original.ownerId,group=original.lineGroupId;int level=original.level;Vector3 side=original.engineerSide;float metersPerWorld=original.builtLengthMeters/total;
            foreach(var interval in new[]{new Vector2(0,from),new Vector2(to,total)})if(interval.y-interval.x>.00001f){var points=original.Slice(interval.x,interval.y);if(points.Length<2)continue;float length=interval.y-interval.x;var fragment=CreateLine(points,side,owner,level,length*metersPerWorld);fragment.radius=original.radius;fragment.lineGroupId=group;fragment.SetFragmentHealth(current*length/total,maximum*length/total);result.Add(fragment);}
            original.Demolish();return result;
        }
        public CommandResult Dismantle(BattleUnit worker,Fortification fort){return CommandResult.No("请选择工兵画出要拆除的防线段");}
        public bool IsBuilding(BattleUnit worker){if(ReplicaWork(worker,out var data))return data.engineering==1;return projects.Exists(p=>p.worker==worker&&p.fort==null)||helpers.Exists(h=>h.worker==worker);}
        public bool IsDismantling(BattleUnit worker){if(ReplicaWork(worker,out var data))return data.engineering==2;return projects.Exists(p=>p.worker==worker&&p.fort!=null);}
        public bool Working(BattleUnit worker){if(ReplicaWork(worker,out var data))return data.engineering!=0;return projects.Exists(p=>p.worker==worker)||helpers.Exists(h=>h.worker==worker);}
        public float Remaining(BattleUnit worker){if(ReplicaWork(worker,out var data))return data.workRemaining;var p=projects.Find(j=>j.worker==worker)??helpers.Find(h=>h.worker==worker)?.project;return p==null?0:p.remaining;}
        public bool Approaching(BattleUnit worker){if(ReplicaWork(worker,out var data))return data.workApproaching;return projects.Exists(p=>p.worker==worker&&p.approaching)||helpers.Exists(h=>h.worker==worker&&h.arriving);}
        public float RemovalRemaining(Fortification fort){return removals.TryGetValue(fort,out var r)?r.remaining:0;}
        public bool Removing(Fortification fort){return fort!=null&&removals.ContainsKey(fort);}
        public CommandResult ToggleAutomaticRemoval(string player,Fortification fort){return CommandResult.No("远程拆除已取消，请用工兵画拆除线");}
        void TickRemovals(float dt)
        {foreach(var fort in new List<Fortification>(removals.Keys)){var r=removals[fort];if(fort==null||fort.Destroyed||(!Rules.allowEnemyAutomaticDemolition&&fort.ownerId!=r.player)){removals.Remove(fort);continue;}r.remaining=Mathf.Max(0,r.remaining-Mathf.Max(0,dt));if(r.remaining<=0){fort.Demolish();removals.Remove(fort);}}}
        public void Cancel(BattleUnit worker){for(int i=helpers.Count-1;i>=0;i--)if(helpers[i].worker==worker){worker.Motor?.Stop();worker.SetWorking(false);helpers.RemoveAt(i);}for(int i=projects.Count-1;i>=0;i--)if(projects[i].worker==worker){Refund(projects[i]);Finish(projects[i]);projects.RemoveAt(i);}}
        void Refund(Project p){if(p.fort==null&&p.payer!=null&&p.totalTime>0)p.payer.resourceStock+=p.cost*Mathf.Clamp01(p.remaining/p.totalTime);}
        void Finish(Project p){for(int i=helpers.Count-1;i>=0;i--)if(helpers[i].project==p){if(helpers[i].worker!=null){helpers[i].worker.Motor?.Stop();helpers[i].worker.SetWorking(false);}helpers.RemoveAt(i);}if(p.worker!=null){if(p.approaching&&p.worker.Motor!=null)p.worker.Motor.Stop();p.worker.SetWorking(false);}}
        public void Tick(float dt)
        {
            for(int k=helpers.Count-1;k>=0;k--){var h=helpers[k];if(h.worker==null||!h.worker.OnMap||h.worker.UnderFire||!projects.Contains(h.project)){if(h.worker!=null)h.worker.SetWorking(false);helpers.RemoveAt(k);continue;}if(h.arriving&&Vector2.Distance(h.worker.transform.position,h.project.site)<=Rules.constructionArrivalDistance){h.worker.Motor.Stop();h.worker.SetWorking(true);h.arriving=false;}}
            for(int i=projects.Count-1;i>=0;i--)
            {
                var p=projects[i];bool invalid=p.worker==null||!p.worker.OnMap||p.worker.ownerId!=p.owner||p.worker.UnderFire||(p.fort!=null&&p.fort.Destroyed);
                if(!invalid&&p.approaching)
                {
                    if(Vector2.Distance(p.worker.transform.position,p.site)<=Rules.constructionArrivalDistance){if(p.worker.Motor!=null)p.worker.Motor.Stop();p.worker.transform.position=p.site;p.approaching=false;p.worker.SetWorking(true);}
                    else {if(p.worker.Motor==null||!p.worker.Motor.IsMoving)invalid=true;else continue;}
                }
                if(!invalid&&!p.worker.Working)invalid=true;
                if(!invalid&&p.fort!=null)invalid=p.fort.Destroyed||Vector2.Distance(p.worker.transform.position,p.site)>Rules.constructionArrivalDistance;
                if(!invalid&&p.fort==null&&!p.approaching)invalid=Vector2.Distance(p.worker.transform.position,p.site)>Rules.constructionArrivalDistance;
                if(invalid){Refund(p);Finish(p);projects.RemoveAt(i);continue;}p.remaining=Mathf.Max(0,p.remaining-Mathf.Max(0,dt)*(1+helpers.FindAll(h=>h.project==p&&!h.arriving).Count));if(p.remaining>0)continue;
                if(p.fort!=null)OpenBreach(p.fort,p.cutFrom,p.cutTo);else CreateLine(p.points,p.side,p.owner,p.level,p.lengthMeters);Finish(p);projects.RemoveAt(i);
            }
            TickRemovals(dt);
        }
        public Fortification CreateLine(Vector3[] points,Vector3 side,string owner,int level,float lengthMeters)
        {
            var g=new GameObject("整条防御线");g.transform.position=points[0];var f=g.AddComponent<Fortification>();f.ownerId=owner;f.lineGroupId=Guid.NewGuid().ToString("N");f.level=level;f.radius=Rules.fortificationRadius;f.linePoints=(Vector3[])points.Clone();f.engineerSide=side;f.builtLengthMeters=lengthMeters;f.SetLineHealth(lengthMeters*Rules.lineHealthPerMeter*Mathf.Pow(campaign.rules.fortLevelHealthFactor,level));campaign.combined.fortifications.Add(f);
            g.AddComponent<StaffDefenseLine>().fort=f;
            return f;
        }
        static Vector3 Along(Vector3[] points,float distance){for(int i=1;i<points.Length;i++){float length=Vector2.Distance(points[i-1],points[i]);if(distance<=length)return length>0?Vector3.Lerp(points[i-1],points[i],distance/length):points[i];distance-=length;}return points[points.Length-1];}
        void Draw(Transform parent,Vector3[] points,float width,Color color,int order){if(material==null)material=new Material(Shader.Find("Sprites/Default"));var obj=new GameObject("军事防线符号");obj.transform.SetParent(parent,false);var line=obj.AddComponent<LineRenderer>();line.useWorldSpace=true;line.loop=false;line.positionCount=points.Length;line.SetPositions(points);line.sharedMaterial=material;line.startWidth=line.endWidth=width;line.startColor=line.endColor=color;line.numCornerVertices=0;line.numCapVertices=0;line.sortingOrder=order;}
        void OnDestroy(){foreach(var p in projects){Refund(p);Finish(p);}projects.Clear();if(material!=null){if(Application.isPlaying)Destroy(material);else DestroyImmediate(material);}}
    }
}









