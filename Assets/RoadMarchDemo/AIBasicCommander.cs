using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace CampaignDemo
{
    public enum AIBasicPhase { Preparing,Gathering,Advancing,Fighting,Withdrawing }
    // One commander owns the mission; legacy unit orders still own commands and reservations.
    public sealed class AIBasicCommander : MonoBehaviour
    {
        public BattleAIController controller;
        public AIBasicProfile profile;
        public AIBasicPhase phase;
        public RecoveryPoint target;
        [SerializeField] string status="等待初始化";
        public string Status {get{return status;}}
        public string EconomyStatus {get;private set;}
        public float DefenseRequirement {get;private set;}
        public float DefenseAllocated {get;private set;}
        public float AttackStrength {get;private set;}
        public IReadOnlyList<BattleUnit> Attackers {get{return attackers;}}
        readonly List<BattleUnit> attackers=new List<BattleUnit>();
        readonly Dictionary<string,RecoveryPoint> defenders=new Dictionary<string,RecoveryPoint>();
        readonly Dictionary<string,float> targetCooldown=new Dictionary<string,float>();
        readonly Dictionary<string,float> retryAfter=new Dictionary<string,float>();
        readonly Dictionary<string,float> contactTimers=new Dictionary<string,float>();
        readonly Dictionary<string,Vector3> destinations=new Dictionary<string,Vector3>();
        readonly List<RecoveryPoint> cities=new List<RecoveryPoint>();
        readonly HashSet<string> managedTrucks=new HashSet<string>();
        float clock,nextPlan,nextEconomy,departureStrength,gatherStarted;
        Vector3 rally;
        string reservationKey;
        public bool Active {get{return enabled&&profile!=null&&controller!=null&&controller.CanThink;}}
        void Awake(){if(controller==null)controller=GetComponent<BattleAIController>();}
        bool Infantry(BattleUnit u){return u!=null&&u.Alive&&u.ownerId==BattleFactions.RedPlayerId&&u.combatDefinition!=null&&u.combatDefinition.role==UnitRole.Infantry;}
        bool Managed(AIUnitOrder o){var u=o.GetComponent<BattleUnit>();return Infantry(u)&&o.task!=null&&o.task.kind==AITaskKind.Guard&&!o.task.requireActivation;}
        public float Strength(BattleUnit u){return u==null||!u.Alive?0:u.Personnel/BasePersonnel();}
        float BasePersonnel(){return controller.world.campaign!=null&&controller.world.campaign.rules.troopStrength.Length>0?Mathf.Max(1,controller.world.campaign.rules.troopStrength[0]):7;}
        bool Recovering(BattleUnit u){var r=u.GetComponent<AIRecoveryResponse>();return r!=null&&r.IsRecovering;}
        public void HandOff()
        {
            if(reservationKey!=null)controller.reservations?.Release(reservationKey);
            reservationKey=null;attackers.Clear();defenders.Clear();destinations.Clear();retryAfter.Clear();contactTimers.Clear();
            target=null;phase=AIBasicPhase.Preparing;nextPlan=0;status="红方手动控制，AI暂停";
        }
        public void Plan(float dt)
        {
            if(!Active)return;SyncSkills();clock+=Mathf.Max(0,dt);
            if(clock<nextPlan)return;nextPlan=clock+1;
            cities.Clear();cities.AddRange(controller.world.points.Where(p=>p!=null&&p.kind==FacilityKind.RecoveryPoint&&p.ownerId==BattleFactions.RedPlayerId).OrderBy(p=>p.pointId));
            var units=controller.Orders.Where(Managed).Select(o=>o.GetComponent<BattleUnit>()).OrderBy(u=>u.unitId).ToList();
            foreach(var u in units) if(profile.recover)
            {
                var response=u.GetComponent<AIRecoveryResponse>();if(response==null)response=u.gameObject.AddComponent<AIRecoveryResponse>();
                response.seekRecoveryBelow=profile.retreatHealth;response.returnAbove=profile.recoveryHealth;
                response.keepLastCityGuard=true;response.returnToGuardPosition=false;
            }
            attackers.RemoveAll(u=>!Infantry(u)||u.Fraction<=profile.retreatHealth||Recovering(u));
            AttackStrength=attackers.Sum(Strength);
            if(target!=null&&target.ownerId==BattleFactions.RedPlayerId)Finish(false,"已占领目标，重新分配驻军");
            if(target!=null&&(attackers.Count==0||AttackStrength<=departureStrength*profile.squadRetreatRatio))
            {phase=AIBasicPhase.Withdrawing;status="进攻队兵力不足，整队撤回";}
            if(phase==AIBasicPhase.Withdrawing&&attackers.All(u=>cities.Any(p=>Vector2.Distance(u.transform.position,p.transform.position)<=p.captureRadius)))Finish(true,"进攻队已撤回");
            AssignDefense(units);
            if(target==null&&profile.attackCities&&clock>=nextAttackAfter)TryAttack(units);
            if(target!=null&&phase==AIBasicPhase.Gathering&&(clock-gatherStarted>=profile.gatheringSeconds||attackers.All(u=>Vector2.Distance(u.transform.position,rally)<.5f)))
            {phase=AIBasicPhase.Advancing;departureStrength=AttackStrength;status="进攻："+target.displayName;destinations.Clear();foreach(var u in attackers)Stop(u);}
            if(clock>=nextEconomy){nextEconomy=clock+profile.economyInterval;Economy();Recruit();}
        }
        float nextAttackAfter;
        void AssignDefense(List<BattleUnit> units)
        {
            DefenseRequirement=Mathf.Max(cities.Count*profile.minimumCityDefense,units.Sum(Strength)*profile.defenseRatio);
            defenders.Clear();DefenseAllocated=0;
            var available=units.Where(u=>!attackers.Contains(u)&&u.OnMap&&!Recovering(u)).ToList();
            foreach(var city in cities)
            {
                float local=0;foreach(var u in available.OrderBy(u=>Vector2.Distance(u.transform.position,city.transform.position)).ThenBy(u=>u.unitId).ToArray())
                {if(local>=profile.minimumCityDefense)break;defenders[u.unitId]=city;local+=Strength(u);DefenseAllocated+=Strength(u);available.Remove(u);}
            }
            foreach(var u in available.ToArray())
            {if(DefenseAllocated>=DefenseRequirement)break;defenders[u.unitId]=NearestCity(u.transform.position);DefenseAllocated+=Strength(u);available.Remove(u);}
        }
        RecoveryPoint NearestCity(Vector3 position){return cities.OrderBy(p=>Vector2.Distance(position,p.transform.position)).FirstOrDefault();}
        void TryAttack(List<BattleUnit> units)
        {
            var candidates=units.Where(u=>u.OnMap&&u.CanMove&&u.Fraction>=profile.deployHealth&&!Recovering(u)&&!defenders.ContainsKey(u.unitId)).ToList();
            if(DefenseAllocated+.001f<DefenseRequirement){status="等待：留守兵力不足";return;}
            if(candidates.Sum(Strength)<profile.minimumAttackStrength){status="等待：可出击兵力不足";return;}
            var leader=candidates[0];RecoveryPoint chosen=null;float best=float.MaxValue;
            foreach(var city in controller.world.points.Where(p=>p!=null&&p.kind==FacilityKind.RecoveryPoint&&p.ownerId!=BattleFactions.RedPlayerId).OrderBy(p=>p.pointId))
            {
                float cooldown;if(targetCooldown.TryGetValue(city.pointId,out cooldown)&&clock<cooldown)continue;
                var route=AIPathService.Plan(controller.world,leader,city.transform.position,profile.preferRoad,profile.allowGroundFallback);
                if(route!=null&&route.length<best){chosen=city;best=route.length;}
            }
            if(chosen==null){status="等待：没有可达的非己方城市";return;}
            candidates=candidates.Where(u=>AIPathService.Plan(controller.world,u,chosen.transform.position,profile.preferRoad,profile.allowGroundFallback)!=null).ToList();
            if(candidates.Sum(Strength)<profile.minimumAttackStrength){status="等待：可达目标的进攻兵力不足";return;}
            float missing=units.Sum(u=>Mathf.Max(0,u.Capacity-u.Personnel));
            var recoveryCity=NearestCity(leader.transform.position);float cost=recoveryCity==null?1:recoveryCity.resourcePerPerson;
            float required=(missing+candidates.Sum(u=>u.Capacity)*profile.expectedLossRatio)*cost+profile.productionReserve;
            reservationKey="basic-attack-"+GetInstanceID();
            float remaining=required;
            foreach(var city in cities){string key=city.GetInstanceID().ToString();float amount=Mathf.Min(remaining,Mathf.Max(0,city.resourceStock-controller.reservations.ReservedAt(key)));if(amount>0&&controller.reservations.Reserve(reservationKey,key,amount,city.resourceStock))remaining-=amount;}
            if(remaining>.001f){controller.reservations.Release(reservationKey);reservationKey=null;status="等待：恢复及生产资源不足，需要 "+required.ToString("0");return;}
            attackers.AddRange(candidates);target=chosen;phase=AIBasicPhase.Gathering;
            rally=leader.transform.position;gatherStarted=clock;departureStrength=candidates.Sum(Strength);AttackStrength=departureStrength;
            destinations.Clear();status="集结，准备进攻："+target.displayName;
        }
        void Finish(bool failed,string reason)
        {
            if(failed&&target!=null)targetCooldown[target.pointId]=clock+profile.targetRetrySeconds;
            foreach(var u in attackers)Stop(u);
            attackers.Clear();target=null;phase=AIBasicPhase.Preparing;destinations.Clear();
            if(reservationKey!=null)controller.reservations.Release(reservationKey);reservationKey=null;
            nextAttackAfter=clock+2;status=reason;
        }
        void Stop(BattleUnit u){if(u!=null&&u.Alive&&u.Motor!=null&&u.Motor.IsMoving)controller.IssueCommand(u.GetComponent<AIUnitOrder>(),new BattleCommand{kind=BattleCommandKind.StopMovement});}
        public bool StepUnit(AIUnitOrder order,List<ArmyInfo> contacts,float dt)
        {
            if(!Active)return false;
            var directTruck=order.GetComponent<TransportTruck>();
            if(LocalTacticalBattle.EnabledFor(this)&&directTruck!=null){order.State=directTruck.Unit.Motor!=null&&directTruck.Unit.Motor.IsMoving?AIUnitState.Marching:AIUnitState.Holding;order.DecisionReason="运输车直接采集并运回城市";return true;}
            if(!Managed(order))return false;
            var u=order.GetComponent<BattleUnit>();
            if(!u.OnMap){order.State=AIUnitState.Unavailable;return true;}
            if(u.Fraction<=profile.retreatHealth||Recovering(u)){Move(order,NearestCity(u.transform.position),"返回恢复点");return true;}
            bool retreat=phase==AIBasicPhase.Withdrawing&&attackers.Contains(u);
            float timer;contactTimers.TryGetValue(u.unitId,out timer);
            bool contact=contacts.Any(c=>Vector2.Distance(u.transform.position,c.position)<=u.combatDefinition.attackRadius);
            if(!retreat&&contact)
            {
                timer+=dt;contactTimers[u.unitId]=timer;
                if(timer>=controller.ProfileFor(order).reactionSeconds){Stop(u);destinations.Remove(u.unitId);order.State=AIUnitState.Engaging;order.DecisionReason="基础AI：射程内交战";if(attackers.Contains(u))phase=AIBasicPhase.Fighting;}
                else order.State=AIUnitState.Reacting;return true;
            }
            contactTimers[u.unitId]=0;
            if(attackers.Contains(u))
            {
                if(retreat){Move(order,NearestCity(u.transform.position),"整队撤回");return true;}
                if(target==null)return true;
                Vector3 destination=phase==AIBasicPhase.Gathering?rally:target.transform.position;
                float radius=phase==AIBasicPhase.Gathering?.4f:Mathf.Max(.05f,target.captureRadius*.5f);
                Move(order,destination,radius,phase==AIBasicPhase.Gathering?"集结":"进攻 "+target.displayName);return true;
            }
            RecoveryPoint guard;if(defenders.TryGetValue(u.unitId,out guard)&&guard!=null)Move(order,guard,"留守 "+guard.displayName);
            else{Stop(u);order.State=AIUnitState.Holding;order.DecisionReason=status;}
            return true;
        }
        void Move(AIUnitOrder order,RecoveryPoint city,string reason)
        {
            if(city==null){Stop(order.GetComponent<BattleUnit>());order.State=AIUnitState.Holding;order.DecisionReason="等待己方恢复点";return;}
            Move(order,city.transform.position,Mathf.Max(.05f,city.captureRadius*.5f),reason);
        }
        void Move(AIUnitOrder order,Vector3 destination,float radius,string reason)
        {
            var u=order.GetComponent<BattleUnit>();order.DecisionReason="基础AI："+reason;
            if(Vector2.Distance(u.transform.position,destination)<=radius){Stop(u);order.State=AIUnitState.Holding;return;}
            Vector3 previous;
            if(destinations.TryGetValue(u.unitId,out previous)&&previous==destination&&u.Motor!=null&&u.Motor.IsMoving){order.State=AIUnitState.Marching;return;}
            float retry;if(retryAfter.TryGetValue(u.unitId,out retry)&&clock<retry)return;
            if(!u.CanMove){order.State=AIUnitState.Unavailable;return;}
            var route=AIPathService.Plan(controller.world,u,destination,profile.preferRoad,profile.allowGroundFallback);
            if(route==null){retryAfter[u.unitId]=clock+5;order.DecisionReason="等待：路线不可达";return;}
            bool accepted=controller.IssueCommand(order,new BattleCommand{kind=route.road?BattleCommandKind.RoadDestination:BattleCommandKind.FreeRoute,route=route.points,destination=destination});
            if(accepted){destinations[u.unitId]=destination;order.State=AIUnitState.Marching;}
            else retryAfter[u.unitId]=clock+5;
        }
        bool lastTransport=true;
        public InfantryProduction Production {get{return GetComponent<InfantryProduction>();}}
        public float DesiredArmyStrength
        {
            get
            {
                if(profile==null)return 0;
                float defense=cities.Count*profile.minimumCityDefense;
                float attack=profile.attackCities?profile.targetAttackStrength:0;
                float total=profile.defenseRatio>=.999f?defense:Mathf.Max(defense+attack,attack/Mathf.Max(.01f,1-profile.defenseRatio));
                return Mathf.Min(Mathf.Max(1,profile.maximumArmyStrength),Mathf.Ceil(total));
            }
        }
        void SyncSkills()
        {
            if(!profile.attackCities&&target!=null&&phase!=AIBasicPhase.Withdrawing)
            {phase=AIBasicPhase.Withdrawing;destinations.Clear();status="正面进攻已关闭，进攻队撤回";}
            if(!profile.recover)foreach(var order in controller.Orders)
            {
                var response=order.GetComponent<AIRecoveryResponse>();var unit=order.GetComponent<BattleUnit>();
                if(response!=null&&response.IsRecovering){Stop(unit);response.CancelResponse();}
                if(unit!=null&&unit.Alive&&(unit.State==ArmyBattleState.InsidePoint||unit.State==ArmyBattleState.Repairing))controller.IssueCommand(order,new BattleCommand{kind=BattleCommandKind.ExitPoint});
            }
            if(lastTransport&&!profile.transportResources)foreach(var order in controller.Orders)
            {
                if(!managedTrucks.Contains(order.GetComponent<BattleUnit>().unitId))continue;
                var response=order.GetComponent<AISupplyResponse>();if(response!=null){Stop(order.GetComponent<BattleUnit>());response.CancelResponse();controller.reservations.Release(order.RuntimeTaskId);}
            }
            lastTransport=profile.transportResources;
            if(!profile.recruitInfantry&&Production!=null)Production.CancelAutomated(BattleFactions.RedPlayerId);
        }
        void Recruit()
        {
            if(!profile.recruitInfantry)return;
            var production=Production;
            if(production==null){EconomyStatus="未配置步兵制造接口";return;}
            float squad=BasePersonnel();
            var troops=controller.world.units.Where(Infantry).ToArray();
            // Damaged survivors can recover; count their full capacity to avoid replacing them twice.
            float potential=troops.Sum(u=>u.Capacity/squad)+production.PendingStrength(BattleFactions.RedPlayerId);
            float goal=DesiredArmyStrength;
            if(potential>=goal||production.PendingCount(BattleFactions.RedPlayerId)>=Mathf.Max(1,profile.maximumProductionJobs))return;
            float batch=controller.world.campaign.rules.At(controller.world.campaign.rules.troopStrength,profile.recruitmentEchelon)/squad;
            if(batch<=0||potential+batch>profile.maximumArmyStrength){EconomyStatus="等待：制造编制超过兵力上限";return;}
            foreach(var city in cities.OrderByDescending(c=>c.resourceStock))
            {
                float healing=0;
                foreach(var troop in troops)if(NearestCity(troop.transform.position)==city)healing+=Mathf.Max(0,troop.Capacity-troop.Personnel)*city.resourcePerPerson;
                float free=Mathf.Max(0,city.resourceStock-controller.reservations.ReservedAt(city.GetInstanceID().ToString())-healing-profile.productionReserve);
                if(free<production.Cost(profile.recruitmentEchelon))continue;
                bool accepted=controller.commands.Submit(new BattleCommand{kind=BattleCommandKind.BuildInfantry,playerId=BattleFactions.RedPlayerId,pointId=city.pointId,echelon=profile.recruitmentEchelon,automatedProduction=true}).accepted;
                if(accepted){potential+=batch;EconomyStatus="补充步兵；在造部队已计入目标";}
                if(potential>=goal||production.PendingCount(BattleFactions.RedPlayerId)>=Mathf.Max(1,profile.maximumProductionJobs))break;
            }
        }

        void DirectResourceEconomy(CampaignRuntime campaign,LogisticsWorld logistics){if(LocalPrototypeOrders.EnabledFor(this)){if(profile.transportResources)controller.world.GetComponent<LocalPrototypeOrders>()?.EnsureAiTransports();return;}EconomyStatus="运输车直接采集资源并运往城市";if(!profile.transportResources)return;foreach(var unit in controller.world.units){var truck=unit==null?null:unit.GetComponent<TransportTruck>();if(truck==null||unit.ownerId!=BattleFactions.RedPlayerId||!unit.OnMap||unit.Motor==null||unit.Motor.IsMoving)continue;var site=logistics.sites.Where(s=>s!=null&&s.stock>0).OrderBy(s=>Vector2.Distance(s.transform.position,unit.transform.position)).FirstOrDefault();if(site!=null&&site.Area.Contains(unit.transform.position)&&truck.Cargo<truck.Capacity*.99f)continue;Vector3 goal;if(truck.Cargo>0){var city=cities.OrderBy(c=>Vector2.Distance(c.transform.position,unit.transform.position)).FirstOrDefault();if(city==null)continue;if(Vector2.Distance(city.transform.position,unit.transform.position)<=UnitRanges.Interaction(unit))continue;goal=city.transform.position;}else{if(site==null)continue;goal=site.transform.position;}controller.commands.Submit(new BattleCommand{playerId=unit.ownerId,unitId=unit.unitId,kind=BattleCommandKind.FreeDestination,destination=goal});}}
        void Economy(){var campaign=controller.world.campaign;if(campaign==null||campaign.logistics==null){EconomyStatus="未配置资源系统";return;}DirectResourceEconomy(campaign,campaign.logistics);}

    }
}

