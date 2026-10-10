using System;
using System.Collections.Generic;
using UnityEngine;
namespace CampaignDemo
{
    [DefaultExecutionOrder(100)]
    public sealed class BattleWorld : MonoBehaviour
    {
        public RoadMarchSceneSetup setup;
        public CampaignRuntime campaign;
        public BattleUnit[] units;
        public RecoveryPoint[] points;
        public string[] players={"A","B"};
        public string currentPlayer="A";
        public bool fogEnabled=true;
        [Tooltip("森林中开火后取消藏匿，敌人可按视野发现。关闭可测试持续隐蔽射击。")] public bool revealForestOnAttack=true;
        public bool revealTypesWithoutFog;
        public Sprite unknownUnitSprite;
        [Min(.02f)] public float simulationStep=.1f;
        [Range(0,1)] public float escapeLossFraction=.1f;
        [Min(.1f)] public float escapeGraceSeconds=5;
        [Min(1)] public int personnelLimit=20000;
        public TerritoryControlSystem territory;
        public PlayerIntelStore Intel { get; private set; }=new PlayerIntelStore();
        public event Action<CombatEffectEvent> CombatEffectOccurred;
        public void NotifyCombatEffect(CombatEffectEvent effect){if(SimulationEnded)return;var listeners=CombatEffectOccurred;if(listeners==null)return;foreach(Action<CombatEffectEvent> listener in listeners.GetInvocationList()){try{listener(effect);}catch(Exception exception){Debug.LogException(exception,this);}}}
        public event Action PlayerChanged;
        public event Action<BattleUnit> UnitRegistered;
        readonly List<CombatEffectEvent> pendingEffects=new List<CombatEffectEvent>();
        readonly Dictionary<BattleUnit,float> pendingDamage=new Dictionary<BattleUnit,float>();
        readonly HashSet<BattleUnit> forestShooters=new HashSet<BattleUnit>();
        readonly HashSet<BattleUnit> engaged=new HashSet<BattleUnit>();
        readonly Dictionary<string,float> localCounts=new Dictionary<string,float>();
        public bool SimulationEnded {get;private set;}public BattleReplayRecorder Replay {get;private set;}
        float accumulator; public bool Initialized { get; private set; }
        void Start() { Initialize(); }
        public void Initialize()
        {
            if(Initialized)return;
            if(setup==null)setup=GetComponent<RoadMarchSceneSetup>();
            if(units==null||points==null||setup==null)throw new ArgumentException("BattleWorld: assign setup, units and points.");
            if(campaign==null)campaign=GetComponent<CampaignRuntime>();if(campaign!=null){campaign.Bind();if(campaign.rules==null)throw new ArgumentException("Assign Campaign Rules");}
            var ids=new HashSet<string>();foreach(var u in units){if(u==null)throw new ArgumentException("Null battle unit");if(string.IsNullOrEmpty(u.unitId)||!ids.Add(u.unitId))throw new ArgumentException("Unique unit IDs required");if(Array.IndexOf(players,u.ownerId)<0)throw new ArgumentException("Unit owner must be a configured player: "+u.name);if(campaign!=null)campaign.ConfigureUnit(u);u.Initialize();var info=u.GetComponent<ArmyTacticalInfo>();if(info!=null){info.ownerId=u.ownerId;info.visionRadius=u.combatDefinition.visionRadius;info.attackRadius=u.combatDefinition.attackRadius;}var v=u.GetComponent<BattleUnitView>();if(v==null)v=u.gameObject.AddComponent<BattleUnitView>();v.Initialize();}
            if(territory!=null){territory.world=this;territory.Initialize();}
            Initialized=true;BattleServices.Ensure(this);Replay=GetComponent<BattleReplayRecorder>();if(Replay==null)Replay=gameObject.AddComponent<BattleReplayRecorder>();Replay.Initialize(this);var replayControls=GetComponent<BattleReplayControls>();if(replayControls==null)replayControls=gameObject.AddComponent<BattleReplayControls>();replayControls.world=this;
        }
        void Update()
        { if(!Initialized||SimulationEnded)return;accumulator+=Time.deltaTime;float dt=Mathf.Max(.02f,simulationStep);while(accumulator>=dt){Tick(dt);accumulator-=dt;} }
        public void Tick(float dt)
        {
            if(!Initialized||SimulationEnded)return;dt=Mathf.Max(0,dt);Replay?.Advance(dt);
            foreach(var u in units)if(u!=null&&u.Alive){u.AdvanceTimer(dt);if(u.HiddenInForest&&!InForest(u))u.RevealForest();}
            if(campaign!=null)campaign.Tick(dt);else ResolveCombat(dt);ResolvePoints(dt);AdvancePointUnits(dt);if(territory!=null)territory.Resolve();
        }
        void ResolveCombat(float dt)
        {
            pendingDamage.Clear();pendingEffects.Clear();engaged.Clear();forestShooters.Clear();
            foreach(var attacker in units)
            {
                if(attacker==null||!attacker.CanFire)continue;BattleUnit target=null;float nearest=float.MaxValue;
                float range=Mathf.Min(attacker.combatDefinition.attackRadius,attacker.combatDefinition.visionRadius);
                foreach(var candidate in units)
                {
                    if(candidate==null||!candidate.OnMap||candidate.ownerId==attacker.ownerId||!IsVisible(attacker.ownerId,candidate))continue;
                    float d=(candidate.transform.position-attacker.transform.position).sqrMagnitude;
                    if(d>range*range)continue;
                    if(d<nearest||(d==nearest&&target!=null&&string.CompareOrdinal(candidate.unitId,target.unitId)<0)){nearest=d;target=candidate;}
                }
                if(target==null){var logistics=GetComponent<LogisticsWorld>();if(logistics!=null&&logistics.AttackCollector(attacker,dt)){engaged.Add(attacker);forestShooters.Add(attacker);}continue;}
                float damage=CombatMath.Damage(attacker.combatDefinition.fullStrengthAttack,attacker.Fraction,target.combatDefinition.defense,dt);
                if(damage<=0)continue;
                if(!pendingDamage.ContainsKey(target))pendingDamage.Add(target,0);pendingDamage[target]+=damage;
                engaged.Add(attacker);forestShooters.Add(attacker);if(target.State!=ArmyBattleState.Escaping)engaged.Add(target);
                Intel.Identify(attacker.ownerId,target);Intel.Identify(target.ownerId,attacker);
                pendingEffects.Add(CombatEffectEvent.Direct(attacker,target.unitId,target.ownerId,target.transform.position));
            }
            if(revealForestOnAttack)foreach(var shooter in forestShooters)shooter.RevealForest();
            foreach(var u in units)if(u!=null){if(engaged.Contains(u))u.Engage();else u.ReleaseCombat();}
            foreach(var effect in pendingEffects)NotifyCombatEffect(effect);
            // Every attack reads the same pre-damage state; iteration order grants no first-strike advantage.
            foreach(var pair in pendingDamage)pair.Key.Damage(pair.Value);
        }
        void ResolvePoints(float dt)
        {
            foreach(var point in points)
            {
                if(point==null||point.kind==FacilityKind.Mine)continue;localCounts.Clear();
                foreach(var u in units)if(u!=null&&u.OnMap&&(u.transform.position-point.transform.position).sqrMagnitude<=point.captureRadius*point.captureRadius)
                { if(!localCounts.ContainsKey(u.ownerId))localCounts.Add(u.ownerId,0);localCounts[u.ownerId]+=u.Personnel; }
                string winner=null;float high=0;bool tie=false;
                foreach(var pair in localCounts){if(pair.Value>high){high=pair.Value;winner=pair.Key;tie=false;}else if(pair.Value==high)tie=true;}
                bool defended=!string.IsNullOrEmpty(point.ownerId)&&localCounts.TryGetValue(point.ownerId,out var defenders)&&defenders>0;
                if(tie||winner==null||winner==point.ownerId||defended){point.CapturingPlayer=null;point.CaptureProgress=0;continue;}
                if(point.CapturingPlayer!=winner){point.CapturingPlayer=winner;point.CaptureProgress=0;}
                point.CaptureProgress+=dt;
                if(point.CaptureProgress<point.captureSeconds)continue;
                foreach(var u in units)if(u!=null&&u.Alive&&u.Point==point&&!u.OnMap&&u.ownerId!=winner)u.Eliminate();
                point.ownerId=winner;point.CapturingPlayer=null;point.CaptureProgress=0;
            }
        }
        void AdvancePointUnits(float dt)
        {
            foreach(var u in units)
            {
                if(u==null||!u.Alive||u.Point==null)continue;var p=u.Point;
                if(p.ownerId!=u.ownerId){if(!u.OnMap)u.Eliminate();continue;}
                if(u.State==ArmyBattleState.EnteringPoint&&u.StateTimer<=0)
                { if(EnemiesNear(p)){u.CompleteEnter();u.CompleteExit();}else{u.CompleteEnter();Intel.Reset(u);} }
                else if(u.State==ArmyBattleState.ExitingPoint&&u.StateTimer<=0)u.CompleteExit();
                else if(u.State==ArmyBattleState.Repairing)
                {
                    float wanted=Mathf.Min(u.Capacity-u.Personnel,u.Capacity*p.repairFractionPerSecond*dt);
                    float available=p.resourcePerPerson<=0?wanted:Mathf.Min(wanted,p.resourceStock/p.resourcePerPerson);
                    u.Restore(available);p.resourceStock=Mathf.Max(0,p.resourceStock-available*p.resourcePerPerson);
                    if(u.Fraction>=.999999f||available<wanted)u.CancelRepair();
                }
            }
        }
        public bool EnemiesNear(RecoveryPoint p)
        { foreach(var u in units)if(u!=null&&u.OnMap&&u.ownerId!=p.ownerId&&(u.transform.position-p.transform.position).sqrMagnitude<=p.captureRadius*p.captureRadius)return true;return false; }
        public bool CanEnter(BattleUnit u,RecoveryPoint p)
        {
            if(u!=null&&campaign!=null&&u.UnderFire)return false;
            if(u==null||p==null||p.kind!=FacilityKind.RecoveryPoint||!u.CanMove||p.ownerId!=u.ownerId||EnemiesNear(p))return false;
            if((u.transform.position-p.transform.position).sqrMagnitude>p.interactionRadius*p.interactionRadius)return false;
            int count=0;foreach(var other in units)if(other!=null&&other.Alive&&other.Point==p)count++;
            return count<p.capacity;
        }
        public bool InForest(BattleUnit u)
        {return u!=null&&setup!=null&&setup.freeMarchMap!=null&&setup.freeMarchMap.IsForest(new RoadPoint(u.transform.position.x,u.transform.position.y));}
        public bool CanHide(BattleUnit u)
        {return u!=null&&u.ownerId==currentPlayer&&u.Alive&&(campaign==null||!u.UnderFire)&&u.State==ArmyBattleState.Deployed&&u.combatDefinition!=null&&u.combatDefinition.canHideInForest&&InForest(u);}
        bool Inspection {get{return setup!=null&&setup.modes!=null&&setup.modes.CurrentMode==MouseCommandMode.Interaction;}}
        public bool RequestForestHide(BattleUnit u)
        {if(!Inspection||u==null||u.ownerId!=currentPlayer)return false;if(u.HiddenInForest){u.RevealForest();return true;}return CanHide(u)&&u.HideInForest();}
        public bool RequestForestAttack(BattleUnit u,bool enabled)
        {return Inspection&&u!=null&&u.ownerId==currentPlayer&&InForest(u)&&u.SetForestAttack(enabled);}
        public bool RequestEnter(BattleUnit u,RecoveryPoint p) { return u!=null&&u.ownerId==currentPlayer&&CanEnter(u,p)&&u.BeginEnter(p); }
        public bool RequestEscape(BattleUnit u) { return u!=null&&u.ownerId==currentPlayer&&u.Escape(escapeLossFraction,escapeGraceSeconds); }
        public bool IsVisible(string player,BattleUnit target) { return target!=null&&target.OnMap&&(target.ownerId==player||(!target.HiddenInForest&&(!fogEnabled||VisibleAt(player,target.transform.position)))); }
        public bool VisibleAt(string player,Vector3 position)
        { foreach(var u in units)if(u!=null&&u.OnMap&&u.ownerId==player&&(u.transform.position-position).sqrMagnitude<=u.combatDefinition.visionRadius*u.combatDefinition.visionRadius)return true;return false; }
        public bool IsKnown(string player,BattleUnit u) { return (!fogEnabled&&revealTypesWithoutFog)||Intel.Knows(player,u); }
        public void SetPlayer(string player)
        {
            if(currentPlayer==player||Array.IndexOf(players,player)<0)return;
            var mode=setup.modes.CurrentMode;setup.modes.SetMode(MouseCommandMode.None);setup.modes.SetMode(mode);
            var selection=GetComponent<ArmySelection>();if(selection!=null)selection.Clear();currentPlayer=player;if(PlayerChanged!=null)PlayerChanged();
        }
        public void RegisterUnit(BattleUnit unit,ArmyRoadMover mover)
        {
            if(SimulationEnded)throw new InvalidOperationException("作战已经结束");
            if(campaign!=null&&unit!=null&&unit.Campaign==null){campaign.ConfigureUnit(unit);unit.Initialize();}
            if(unit==null||mover==null||string.IsNullOrEmpty(unit.unitId))throw new ArgumentException("Registered unit and mover required");
            foreach(var existing in units)if(existing!=null&&(existing==unit||existing.unitId==unit.unitId))throw new ArgumentException("Duplicate unit registration");
            var allUnits=new List<BattleUnit>(units){unit};units=allUnits.ToArray();var armies=new List<ArmyRoadMover>(setup.sceneArmies){mover};setup.sceneArmies=armies.ToArray();Replay?.SampleUnit(unit);if(UnitRegistered!=null)UnitRegistered(unit);
        }
        public void EndBattle()
        {
            if(!Initialized||SimulationEnded)return;
            Replay.Sample();SimulationEnded=true;accumulator=0;setup.modes.SetMode(MouseCommandMode.None);
            foreach(var unit in units)if(unit!=null){var motor=unit.Motor;if(motor!=null){motor.Stop();motor.MovementLocked=true;}foreach(var sprite in unit.GetComponentsInChildren<SpriteRenderer>(true))sprite.enabled=false;}
            GetComponent<CombatEffectPresenter>()?.Reset();
            var ranges=GetComponent<CampaignRangeDisplay>();if(ranges!=null)ranges.enabled=false;
            Replay.Finish();
        }
        public void NextPlayer() { int i=Array.IndexOf(players,currentPlayer);if(players.Length>0)SetPlayer(players[(i+1)%players.Length]); }
        void LateUpdate()
        {
            if(!Initialized||SimulationEnded)return;
            foreach(var u in units)if(u!=null)
            { var view=u.View;if(view!=null)view.Apply(IsVisible(currentPlayer,u),u.ownerId==currentPlayer,IsKnown(currentPlayer,u),unknownUnitSprite);var m=u.Motor;if(m!=null)m.SetRouteVisible(setup.showCommittedRoutes&&u.ownerId==currentPlayer&&u.OnMap&&!m.PlanningPaused&&GetComponent<ArmySelection>()!=null&&GetComponent<ArmySelection>().Selected==u.GetComponent<ArmyRoadMover>()); }
        }
    }
}










