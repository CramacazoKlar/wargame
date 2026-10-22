using UnityEngine;
namespace CampaignDemo
{
 // Shared growth; no target selection, attack interval, movement, range or prices change here.
 [DisallowMultipleComponent]public sealed class UnitExperience:MonoBehaviour
 {
  [SerializeField]int level;
  [SerializeField]bool participated;
  BattleUnit unit;
  UnitExperiencePlanning planning;
  public int Level {get{return level;}}
  public void ApplyNetworkState(int value,bool pending){level=Mathf.Clamp(value,0,Maximum);participated=pending;}
  public bool PendingPromotion {get{return participated;}}
  public int Maximum {get{return Plan==null?4:Plan.maximumLevel;}}
  UnitExperiencePlanning Plan {get{if(planning==null)planning=Resources.Load<UnitExperiencePlanning>("UnitExperiencePlanning");return planning;}}
  BattleUnit Unit {get{if(unit==null)unit=GetComponent<BattleUnit>();return unit;}}
  bool Heavy {get{var r=Unit.combatDefinition.role;return r==UnitRole.Tank||ArtilleryBattery.IsArtilleryRole(r)||r==UnitRole.Logistics||r==UnitRole.PersonnelCarrier;}}
  public float OutputBonus {get{var r=Unit.combatDefinition.role;if(r==UnitRole.Logistics||r==UnitRole.PersonnelCarrier)return 0;return level*(Heavy?(Plan==null?.06f:Plan.heavyOutputPerLevel):(Plan==null?.10f:Plan.infantryOutputPerLevel));}}
  public float DamageReduction {get{return Mathf.Clamp(level*(Heavy?(Plan==null?.04f:Plan.heavyReductionPerLevel):(Plan==null?.06f:Plan.infantryReductionPerLevel)),0,.9f);}}
  public string Description {get{return "老练度 "+new string('★',level)+new string('☆',Mathf.Max(0,Maximum-level))+"\n输出 +"+(OutputBonus*100).ToString("0")+"% · 减伤 "+(DamageReduction*100).ToString("0")+"%\n"+(level>=Maximum?"已达上限":participated?"已参战，待回城修满":"待参战");}}
  public static UnitExperience Ensure(BattleUnit u){if(u==null||u.GetComponent<RadarInstallation>()!=null||!LocalPrototypeOrders.EnabledFor(u))return null;return u.GetComponent<UnitExperience>()??u.gameObject.AddComponent<UnitExperience>();}
  public static float OutputMultiplier(BattleUnit u){var e=u==null||!LocalPrototypeOrders.EnabledFor(u)?null:u.GetComponent<UnitExperience>();return e==null?1:1+e.OutputBonus;}
  public static float IncomingMultiplier(BattleUnit u){var e=u==null||!LocalPrototypeOrders.EnabledFor(u)?null:u.GetComponent<UnitExperience>();return e==null?1:1-e.DamageReduction;}
  public static float RecoveryTarget(BattleUnit u,float configured){var e=u==null||!LocalPrototypeOrders.EnabledFor(u)?null:u.GetComponent<UnitExperience>();return e!=null&&e.PendingPromotion?1:configured;}
  void Mark(){if(Unit!=null&&Unit.OnMap&&level<Maximum)participated=true;}
  public static void RecordHit(BattleUnit source,string sourceOwner,BattleUnit target){
   // sourceOwner is the attacker's side, including off-map fire support.
   if(target==null||string.IsNullOrEmpty(sourceOwner)||sourceOwner==target.ownerId)return;
   Ensure(source)?.Mark();Ensure(target)?.Mark();
  }
  public static void RecordStructureHit(BattleUnit source,string targetOwner){if(source!=null&&!string.IsNullOrEmpty(targetOwner)&&source.ownerId!=targetOwner)Ensure(source)?.Mark();}
  public bool TryPromote(){var u=Unit;if(u==null||u.GetComponent<RadarInstallation>()!=null||!LocalPrototypeOrders.EnabledFor(u)||!participated||!u.Alive||u.Point==null||u.Point.ownerId!=u.ownerId||u.Point.kind!=FacilityKind.RecoveryPoint||u.OnMap||u.UnderFire||u.Fraction<.999999f||u.Campaign==null||u.Campaign.world.EnemiesNear(u.Point))return false;level=Mathf.Min(Maximum,level+1);participated=false;return true;}
  public static void TickWorld(BattleWorld w){if(w==null||!LocalPrototypeOrders.EnabledFor(w))return;foreach(var u in w.units)if(u!=null)Ensure(u)?.TryPromote();}
  public static void OnSplit(BattleUnit original,BattleUnit child){var a=Ensure(original);var b=Ensure(child);if(a==null||b==null)return;b.level=a.level;b.participated=false;}
  public static void OnMerge(BattleUnit actor,BattleUnit target){var a=Ensure(actor);var b=Ensure(target);if(a==null||b==null)return;float count=actor.Personnel+target.Personnel;b.level=Mathf.Clamp(Mathf.FloorToInt(count<=0?0:(a.level*actor.Personnel+b.level*target.Personnel)/count+.000001f),0,b.Maximum);b.participated=(a.participated||b.participated)&&b.level<b.Maximum;}
 }
}
