using System;
using System.Collections.Generic;
using UnityEngine;
namespace CampaignDemo
{
    [DefaultExecutionOrder(140)]
    public sealed class CampaignRuntime:MonoBehaviour
    {
        public BattleWorld world;
        public CampaignRules rules;
        public CombinedArmsWorld combined;
        public LogisticsWorld logistics;
        public FortificationLineWorks lineWorks;
        public readonly List<ResourceDepot> depots=new List<ResourceDepot>();
        void Awake(){Bind();}
        public void Bind(){if(world==null)world=GetComponent<BattleWorld>();if(combined==null)combined=GetComponent<CombinedArmsWorld>();if(logistics==null)logistics=GetComponent<LogisticsWorld>();if(lineWorks==null)lineWorks=GetComponent<FortificationLineWorks>();if(lineWorks==null)lineWorks=gameObject.AddComponent<FortificationLineWorks>();lineWorks.campaign=this;}
        void Start(){foreach(var d in FindObjectsOfType<ResourceDepot>())if(d.gameObject.scene==gameObject.scene&&!depots.Contains(d))depots.Add(d);}
        public void ConfigureUnit(BattleUnit u){u.Campaign=this;if(u.GetComponent<CampaignUnit>()==null)u.gameObject.AddComponent<CampaignUnit>();}
        public bool Vehicle(BattleUnit u){return u.GetComponent<PersonnelCarrier>()!=null||u.GetComponent<TransportTruck>()!=null;}
        public float CapacityFor(BattleUnit u){return Vehicle(u)?rules.At(rules.transportEquivalents,u.combatDefinition.echelon)*rules.vehicleHealthPerEquivalent:rules.At(rules.troopStrength,u.combatDefinition.echelon);}
        public bool CanAttack(BattleUnit u){var role=u.combatDefinition.role;return role==UnitRole.Infantry||role==UnitRole.MountainInfantry||role==UnitRole.Engineer||role==UnitRole.Tank;}
        public void Tick(float dt)
        {if(world==null||world.SimulationEnded)return;foreach(var u in world.units)if(u!=null){u.UnderFire=false;u.GetComponent<CampaignUnit>()?.BeginCombatObservation();if(u.OnMap)u.GetComponent<CampaignUnit>()?.Advance(dt);if(u.GetComponent<CampaignUnit>()?.fortTarget!=null&&u.GetComponent<CampaignUnit>().fortTarget.Destroyed)u.GetComponent<CampaignUnit>().fortTarget=null;}if(combined!=null)combined.Tick(dt);if(logistics!=null)logistics.Tick(dt);if(lineWorks!=null)lineWorks.Tick(dt);CampaignCombatSolver.Resolve(this,dt);foreach(var unit in world.units)if(unit!=null){var state=unit.GetComponent<CampaignUnit>();if(state!=null)state.ResolveRetreatMovement(dt,rules);}CaptureDepots(dt);}
        public Fortification ProtectingFort(BattleUnit u)
        {if(combined==null)return null;Fortification best=null;float bestBonus=0;foreach(var f in combined.fortifications)if(f!=null&&!f.Destroyed&&f.ownerId==u.ownerId&&f.DistanceTo(u.transform.position)<=u.combatDefinition.attackRadius+f.radius){float bonus=rules.fortAttackMultiplier*Mathf.Pow(rules.fortLevelAttackFactor,f.level);if(bonus>bestBonus){best=f;bestBonus=bonus;}}return best;}
        public Fortification CoveringFort(BattleUnit u)
        {if(combined==null)return null;Fortification best=null;foreach(var f in combined.fortifications)if(f!=null&&!f.Destroyed&&f.ownerId==u.ownerId&&f.DistanceTo(u.transform.position)<=f.radius&&(best==null||f.level>best.level))best=f;return best;}        public float MovementMultiplier(BattleUnit u,Vector3 from,Vector3 to)
        {if(combined==null)return 1;foreach(var f in combined.fortifications)if(f!=null&&!f.Destroyed&&f.Crosses(from,to))return rules.fortMovementMultiplier;return 1;}
        public static bool SegmentHits(Vector3 a,Vector3 b,Vector3 p,float radius)
        {Vector2 d=(Vector2)(b-a);float t=d.sqrMagnitude>0?Mathf.Clamp01(Vector2.Dot((Vector2)(p-a),d)/d.sqrMagnitude):0;return Vector2.Distance((Vector2)a+d*t,p)<=radius;}
        public void Shell(Vector3 target,float radius,float damage,string sourceId=null)
        { CampaignShellResolver.Resolve(this,target,radius,damage,sourceId); }
        void CaptureDepots(float dt)
        {
            foreach(var d in depots)if(d!=null&&!d.Removed&&!d.resourcePile)
            {
                var counts=new Dictionary<string,float>();foreach(var u in world.units)if(u!=null&&u.OnMap&&Vector2.Distance(u.transform.position,d.transform.position)<=rules.warehouseCaptureRadius){if(!counts.ContainsKey(u.ownerId))counts[u.ownerId]=0;counts[u.ownerId]+=Vehicle(u)?rules.At(rules.transportEquivalents,u.combatDefinition.echelon)*u.Fraction:u.Personnel;}
                string winner=null;float max=0;bool tie=false;foreach(var p in counts){if(p.Value>max){winner=p.Key;max=p.Value;tie=false;}else if(p.Value==max)tie=true;}
                if(winner==null||tie||winner==d.ownerId||(!string.IsNullOrEmpty(d.ownerId)&&counts.ContainsKey(d.ownerId))){d.captureProgress=0;d.capturingPlayer=null;continue;}
                if(d.capturingPlayer!=winner){d.capturingPlayer=winner;d.captureProgress=0;}d.captureProgress+=dt;if(d.captureProgress>=rules.warehouseCaptureSeconds){d.ownerId=winner;d.captureProgress=0;d.capturingPlayer=null;}
            }
        }
        public bool OwnedFootprint(Rect rect,string player){return world.territory!=null&&world.territory.OwnsRect(rect,player);}
        public ResourceSite SiteAt(Vector3 p)
        {if(logistics==null)return null;foreach(var s in logistics.sites)if(s!=null&&s.Area.Contains(p))return s;return null;}
        public RecoveryPoint Payer(string player,float cost)
        {foreach(var p in world.points)if(p!=null&&p.ownerId==player&&p.resourceStock>=cost)return p;return null;}
        public void SyncCollectorSettings(){if(logistics==null)return;logistics.collectorSettings.buildCost=rules.collectorBuildCost;logistics.collectorSettings.collectionInterval=rules.collectorInterval;logistics.collectorSettings.amountPerInterval=rules.collectorBatch;logistics.collectorSettings.health=rules.collectorHealth;logistics.collectorSettings.footprint=rules.collectorFootprint;}
        public bool CanDeployCollector(string player,Vector3 p)
        {SyncCollectorSettings();if(!CombinedArmsWorld.Finite(p))return false;var footprint=new Rect((Vector2)p-rules.collectorFootprint/2,rules.collectorFootprint);var site=SiteAt(p);return logistics!=null&&site!=null&&OwnedFootprint(footprint,player)&&Payer(player,rules.collectorBuildCost)!=null&&logistics.CanPlace(site,p);}
        public CommandResult DeployCollector(string player,Vector3 p)
        {if(!CanDeployCollector(player,p))return CommandResult.No("需己方领土内合法资源区，且付款城市库存足够");var payer=Payer(player,rules.collectorBuildCost);return logistics.BuildCollectorFor(player,SiteAt(p),p,payer)!=null?CommandResult.Yes():CommandResult.No(logistics.Message);}
        public bool CanBuildDepot(BattleUnit engineer,Vector3 p)
        {
            if(engineer==null||!engineer.OnMap||engineer.Working||engineer.combatDefinition.role!=UnitRole.Engineer||!CombinedArmsWorld.Finite(p)||Vector2.Distance(engineer.transform.position,p)>rules.constructionRange)return false;
            float radius=rules.warehouseBuildRadius;var rect=new Rect((Vector2)p-Vector2.one*radius,Vector2.one*radius*2);if(!OwnedFootprint(rect,engineer.ownerId))return false;
            foreach(var d in depots)if(d!=null&&!d.Removed&&Vector2.Distance(p,d.transform.position)<radius*2)return false;
            var free=engineer.GetComponent<ArmyFreeMarchMover>();return free!=null&&world.setup.freeMarchMap.CanTraverse(new RoadPoint(engineer.transform.position.x,engineer.transform.position.y),new RoadPoint(p.x,p.y),free.AllowedTerrain)&&Payer(engineer.ownerId,rules.warehouseBuildCost)!=null;
        }
        public CommandResult BuildDepot(BattleUnit engineer,Vector3 p)
        {if(!CanBuildDepot(engineer,p))return CommandResult.No("仓库需工兵附近己方合法陆地，不能与仓库重叠");Payer(engineer.ownerId,rules.warehouseBuildCost).resourceStock-=rules.warehouseBuildCost;CreateDepot(p,engineer.ownerId,0,false);return CommandResult.Yes();}
        public ResourceDepot CreateDepot(Vector3 p,string owner,float stock,bool pile)
        {var g=new GameObject(pile?"资源堆":"资源仓库");g.transform.position=p;var d=g.AddComponent<ResourceDepot>();d.depotId=Guid.NewGuid().ToString("N");d.ownerId=owner;d.stock=stock;d.resourcePile=pile;d.displayName=g.name;var sprite=g.AddComponent<SpriteRenderer>();sprite.sprite=world.unknownUnitSprite;sprite.sortingOrder=10;sprite.color=pile?new Color(.8f,.65f,.2f):new Color(.7f,.55f,.9f);g.transform.localScale=Vector3.one*rules.warehouseBuildRadius;depots.Add(d);return d;}
        public CommandResult RemoveDepot(string player,ResourceDepot depot)
        {if(depot==null||depot.Removed||depot.resourcePile||depot.ownerId!=player)return CommandResult.No("只能自行拆除自己的仓库");if(depot.stock>0)CreateDepot(depot.transform.position,"",depot.stock,true);depot.stock=0;depot.Remove();return CommandResult.Yes();}
        public float Stock(UnityEngine.Object target){return target is RecoveryPoint p?p.resourceStock:target is ResourceCollector c?c.Stock:target is ResourceDepot d?d.stock:0;}
        public string Owner(UnityEngine.Object target){return target is RecoveryPoint p?p.ownerId:target is ResourceCollector c?c.ownerId:target is ResourceDepot d?d.ownerId:null;}
        public string Name(UnityEngine.Object target){return target is RecoveryPoint p?p.displayName:target is ResourceCollector?"采集器":target is ResourceDepot d?d.displayName:"";}
        public Vector3 Position(UnityEngine.Object target){return ((Component)target).transform.position;}
        public List<UnityEngine.Object> StorageTargets(string player)
        {var targets=new List<UnityEngine.Object>();foreach(var p in world.points)if(p!=null&&p.ownerId==player)targets.Add(p);if(logistics!=null)foreach(var c in logistics.collectors)if(c!=null&&c.Alive&&c.ownerId==player)targets.Add(c);foreach(var d in depots)if(d!=null&&!d.Removed&&(d.ownerId==player||d.resourcePile))targets.Add(d);return targets;}
        public float TransferMaximum(TransportTruck truck,UnityEngine.Object target,bool load)
        {
            if(truck==null||!truck.Unit.OnMap||target==null||(!(target is ResourceDepot d&&d.resourcePile)&&Owner(target)!=truck.Unit.ownerId)||Vector2.Distance(truck.transform.position,Position(target))>rules.transportInteractionRadius)return 0;
            if(target is ResourceCollector collector&&!collector.Alive||target is ResourceDepot depot&&depot.Removed)return 0;
            return Mathf.Max(0,load?Mathf.Min(Stock(target),truck.Capacity-truck.Cargo):truck.Cargo);
        }
        public CommandResult Transfer(TransportTruck truck,UnityEngine.Object target,bool load,float amount)
        {
            if(float.IsNaN(amount)||float.IsInfinity(amount)||amount<=0||amount>TransferMaximum(truck,target,load))return CommandResult.No("数量超过当前可装卸量，或目标不在交互范围");
            float delta=load?-amount:amount;if(target is RecoveryPoint p)p.resourceStock+=delta;else if(target is ResourceCollector c){if(load)c.Take(amount);else c.Store(amount);}else if(target is ResourceDepot d)d.stock+=delta;truck.SetCargo(truck.Cargo-delta);return CommandResult.Yes();
        }
        void Update()
        {foreach(var d in depots)if(d!=null&&!d.Removed){bool show=d.ownerId==world.currentPlayer||!world.fogEnabled||world.VisibleAt(world.currentPlayer,d.transform.position);var sprite=d.GetComponent<SpriteRenderer>();if(sprite!=null)sprite.enabled=show;}}
    }
}









