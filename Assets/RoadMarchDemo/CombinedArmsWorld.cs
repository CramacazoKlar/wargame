using System.Collections.Generic;
using UnityEngine;
namespace CampaignDemo
{
    [DefaultExecutionOrder(120)]
    public sealed class CombinedArmsWorld : MonoBehaviour
    {
        public BattleWorld world;
        public EngineeringDefinition engineering;
        public readonly List<Fortification> fortifications=new List<Fortification>();
        sealed class Shell {public CombatEffectEvent launch;public string owner;public Vector3 target;public float time,radius,damage,launchDelay;public bool friendlyFire,pendingLaunch;public BattleUnit source;public Dictionary<BattleUnit,float> lossLimits;}
        sealed class Job {public BattleUnit unit;public Fortification fort;public Vector3 position;public float remaining;public bool build;public int fortLevel;}
        readonly List<Shell> shells=new List<Shell>();
        readonly List<Job> jobs=new List<Job>();
        Material material;
        void Awake(){if(world==null)world=GetComponent<BattleWorld>();}
        void Start(){foreach(var f in FindObjectsOfType<Fortification>())if(f.gameObject.scene==gameObject.scene&&!fortifications.Contains(f)){fortifications.Add(f);f.Initialize(world.campaign==null?null:world.campaign.rules);if(f.IsLine&&f.GetComponent<StaffDefenseLine>()==null)f.gameObject.AddComponent<StaffDefenseLine>().fort=f;if(f.IsLine&&world.campaign!=null)f.SetLineHealth(f.builtLengthMeters*engineering.lineHealthPerMeter*Mathf.Pow(world.campaign.rules.fortLevelHealthFactor,f.level));}}
        public static bool Finite(Vector3 p){return !(float.IsNaN(p.x)||float.IsInfinity(p.x)||float.IsNaN(p.y)||float.IsInfinity(p.y)||float.IsNaN(p.z)||float.IsInfinity(p.z));}
        public void Schedule(string owner,Vector3 target,ArtilleryDefinition definition,float strength,BattleUnit source)
        {
            if(source==null||!source.OnMap||!ArtilleryBattery.IsArtilleryRole(source.combatDefinition.role))return;
            var effect=new CombatEffectEvent(CombatEffectStage.ArtilleryLaunch,
                source.unitId,owner,source.combatDefinition.role,true,
                source.transform.position,null,null,target,Mathf.Max(.01f,definition.blastRadius));
            int rounds=definition.BarrageRounds(source.combatDefinition.role,source.combatDefinition.echelon);
            float duration=definition.BarrageDuration(source.combatDefinition.role,source.combatDefinition.echelon);
            float total=world!=null&&world.campaign!=null?Mathf.Max(0,CampaignShellResolver.EquivalentStrength(world.campaign,source)*world.campaign.rules.artilleryDamagePerStrength*UnitExperience.OutputMultiplier(source)):Mathf.Max(0,definition.damage)*strength;
            float assigned=0;var lossLimits=world!=null&&world.campaign!=null?new Dictionary<BattleUnit,float>():null;
            for(int i=0;i<rounds;i++){
                float offset=rounds==1?0:duration*i/(rounds-1),damage=i==rounds-1?Mathf.Max(0,total-assigned):total/rounds;assigned+=damage;
                shells.Add(new Shell{launch=effect,owner=owner,target=target,source=source,launchDelay=offset,pendingLaunch=i>0,time=offset+Mathf.Max(0,definition.delaySeconds),radius=effect.Radius,damage=damage,friendlyFire=definition.friendlyFire,lossLimits=lossLimits});
            }
            if(world!=null)world.NotifyCombatEffect(effect);
        }
        void Update(){if(world!=null&&world.Initialized&&world.campaign==null)Tick(Time.deltaTime);}
        public void Tick(float dt)
        {if(world==null||world.SimulationEnded)return;
            dt=Mathf.Max(0,dt);
            foreach(var u in world.units)if(u!=null){var battery=u.GetComponent<ArtilleryBattery>();if(battery!=null)battery.Tick(dt);var carrier=u.GetComponent<PersonnelCarrier>();if(carrier!=null)carrier.SyncPassengers();}
            for(int i=0;i<shells.Count;){var s=shells[i];s.time-=dt;
                if(s.pendingLaunch){
                    // Moving, entering a city, dying or changing owner interrupts only
                    // rounds not yet launched. Airborne shells still reach the target.
                    var u=s.source;
                    if(u==null||!u.isActiveAndEnabled||!u.OnMap||u.Working||u.ownerId!=s.owner||Vector2.Distance(u.transform.position,s.launch.SourcePosition)>.01f||(u.Motor!=null&&u.Motor.IsMoving)){shells.RemoveAt(i);continue;}
                    s.launchDelay-=dt;
                    if(s.launchDelay>0.00001f){i++;continue;}
                    s.pendingLaunch=false;world.NotifyCombatEffect(s.launch);
                }
                if(s.time>0.00001f){i++;continue;}shells.RemoveAt(i);
                world.NotifyCombatEffect(new CombatEffectEvent(CombatEffectStage.ArtilleryImpact,s.launch.SourceId,s.owner,s.launch.SourceRole,s.launch.HasSource,s.launch.SourcePosition,null,null,s.target,s.radius));
                if(world.campaign!=null)world.campaign.Shell(s.target,s.radius,s.damage,s.launch.SourceId,s.owner,s.lossLimits);else {                foreach(var u in world.units)if(u!=null&&u.OnMap&&(s.friendlyFire||u.ownerId!=s.owner)&&Vector2.Distance(u.transform.position,s.target)<=s.radius)u.Damage(CombatMath.Damage(s.damage,1,u.combatDefinition.defense,1));


}
            }
            for(int i=jobs.Count-1;i>=0;i--){var j=jobs[i];if(!j.unit.Working||!j.unit.OnMap||j.unit.State!=ArmyBattleState.Deployed||Vector2.Distance(j.unit.transform.position,j.position)>UnitRanges.Interaction(j.unit)||(!j.build&&(j.fort==null||j.fort.Destroyed))){Finish(j);jobs.RemoveAt(i);continue;}
                j.remaining-=dt;if(j.remaining>0)continue;
                if(j.build&&CanBuildAt(j.unit,j.position))CreateFortification(j.position,j.unit.ownerId,j.fortLevel);else if(!j.build)j.fort.Demolish();Finish(j);jobs.RemoveAt(i);
            }
            foreach(var f in fortifications)if(f!=null&&!f.Destroyed){bool visible=f.VisibleTo(world,world.currentPlayer);foreach(var line in f.GetComponentsInChildren<LineRenderer>())line.enabled=visible;}
        }
        void Finish(Job j){j.unit.SetWorking(false);}
        public float WorkRemaining(BattleUnit u){foreach(var j in jobs)if(j.unit==u)return j.remaining;return 0;}
        public void Cancel(BattleUnit u){for(int i=jobs.Count-1;i>=0;i--)if(jobs[i].unit==u){Finish(jobs[i]);jobs.RemoveAt(i);}}
        bool CanWork(BattleUnit u){return engineering!=null&&u!=null&&u.OnMap&&u.State==ArmyBattleState.Deployed&&!u.Working&&u.GetComponent<PersonnelCarrier>()==null&&u.GetComponent<TransportTruck>()==null;}
        bool CanBuildAt(BattleUnit u,Vector3 p)
        {
            var map=world.setup.freeMarchMap;var free=u.GetComponent<ArmyFreeMarchMover>();
            if(map==null||!map.CanTraverse(new RoadPoint(u.transform.position.x,u.transform.position.y),new RoadPoint(p.x,p.y),free==null?TerrainAccess.Ground|TerrainAccess.Road|TerrainAccess.Grass|TerrainAccess.Forest:free.AllowedTerrain))return false;
            foreach(var f in fortifications)if(f!=null&&!f.Destroyed&&Vector2.Distance(f.transform.position,p)<f.radius+engineering.fortificationRadius)return false;
            foreach(var other in world.units)if(other!=null&&other.OnMap&&Vector2.Distance(other.transform.position,p)<engineering.fortificationRadius+.05f)return false;
            return true;
        }
        public bool CanBuildPreview(BattleUnit u,Vector3 p){return CanWork(u)&&u.combatDefinition.role==UnitRole.Engineer&&CombinedArmsWorld.Finite(p)&&Vector2.Distance(u.transform.position,p)<=UnitRanges.Interaction(u)&&CanBuildAt(u,p);}
        public CommandResult Build(BattleUnit u,Vector3 p)
        {
            if(!CanWork(u)||u.combatDefinition.role!=UnitRole.Engineer)return CommandResult.No("只有空闲工兵可以修建工事");
            if(!Finite(p))return CommandResult.No("位置无效");p.z=0;
            if(Vector2.Distance(u.transform.position,p)>UnitRanges.Interaction(u)||!CanBuildAt(u,p))return CommandResult.No("位置太远、不可通行或已被占用");
            Begin(new Job{unit=u,position=p,remaining=Mathf.Max(.1f,engineering.buildSeconds),build=true,fortLevel=u.GetComponent<CampaignUnit>()==null?0:u.GetComponent<CampaignUnit>().fortBuildLevel});return CommandResult.Yes();
        }
        public CommandResult Dismantle(BattleUnit u,Vector3 p)
        {
            if(u==null||u.combatDefinition.role!=UnitRole.Engineer||!CanWork(u)||!Finite(p))return CommandResult.No("只有工兵可以拆除工事");
            Fortification nearest=null;float limit=UnitRanges.Interaction(u);
            foreach(var f in fortifications)if(f!=null&&!f.Destroyed&&(!world.fogEnabled||f.ownerId==u.ownerId||world.VisibleAt(u.ownerId,f.transform.position))&&Vector2.Distance(f.transform.position,p)<=f.radius+.15f&&Vector2.Distance(u.transform.position,f.transform.position)<=limit){nearest=f;break;}
            if(nearest==null)return CommandResult.No("附近没有可拆除工事");
            float duration=nearest.dismantleSeconds*(u.combatDefinition.role==UnitRole.Engineer?engineering.engineerDismantleMultiplier:1);
            Begin(new Job{unit=u,fort=nearest,position=nearest.transform.position,remaining=Mathf.Max(.1f,duration)});return CommandResult.Yes();
        }
        void Begin(Job j){if(j.unit.Motor!=null)j.unit.Motor.Stop();j.unit.RevealForest();j.unit.SetWorking(true);jobs.Add(j);}
        public Fortification CreateFortification(Vector3 p,string owner,int level=0)
        {var g=new GameObject("路障工事");g.transform.position=p;var f=g.AddComponent<Fortification>();f.ownerId=owner;f.level=level;f.Initialize(world.campaign==null?null:world.campaign.rules);f.radius=engineering.fortificationRadius;f.dismantleSeconds=engineering.ordinaryDismantleSeconds;fortifications.Add(f);var line=g.AddComponent<LineRenderer>();line.useWorldSpace=false;line.loop=true;line.positionCount=4;line.SetPositions(new[]{new Vector3(-f.radius,-f.radius),new Vector3(f.radius,-f.radius),new Vector3(f.radius,f.radius),new Vector3(-f.radius,f.radius)});Style(line,new Color(.75f,.55f,.3f));return f;}
        static bool Hits(Vector3 a,Vector3 b,Fortification f)
        {Vector2 start=a,end=b,p=f.transform.position;var delta=end-start;float t=delta.sqrMagnitude<=.000001f?0:Mathf.Clamp01(Vector2.Dot(p-start,delta)/delta.sqrMagnitude);return Vector2.Distance(p,start+t*delta)<=f.radius;}
        public bool IntersectsFortification(Vector3 a,Vector3 b){foreach(var f in fortifications)if(f!=null&&!f.Destroyed&&Hits(a,b,f))return true;return false;}
        public bool PermitMovement(BattleUnit u,Vector3 a,Vector3 b)
        {if(world.campaign!=null)return true;bool blocked=false;foreach(var f in fortifications)if(f!=null&&!f.Destroyed&&Hits(a,b,f)){blocked=true;}return !blocked;}
        void Style(LineRenderer line,Color color){if(material==null)material=new Material(Shader.Find("Sprites/Default"));line.sharedMaterial=material;line.startColor=line.endColor=color;line.startWidth=line.endWidth=.035f;line.sortingOrder=20;}
        void OnDestroy(){if(material!=null){if(Application.isPlaying)Destroy(material);else DestroyImmediate(material);}}
    }
}












