using UnityEngine;
using System.Collections.Generic;
namespace CampaignDemo {
 [DefaultExecutionOrder(-200)]
 public sealed class UnitSymbolIdentity:MonoBehaviour {
  public UnitSymbolCatalog catalog;
  public int citySortingOrder=20;
  BattleWorld world;BattleUnit symbolUnit;
  void Awake(){
   world=FindObjectOfType<BattleWorld>();if(world==null||world.Initialized)return;
   var setup=world.setup!=null?world.setup:world.GetComponent<RoadMarchSceneSetup>();
   var u=GetComponent<BattleUnit>();var m=GetComponent<ArmyRoadMover>();
   if(u!=null&&m!=null&&setup!=null){
    if(string.IsNullOrEmpty(u.unitId)||u.unitId.EndsWith("-template"))u.unitId=System.Guid.NewGuid().ToString("N");
    var units=new List<BattleUnit>(world.units??new BattleUnit[0]);if(!units.Contains(u))units.Add(u);world.units=units.ToArray();
    var armies=new List<ArmyRoadMover>(setup.sceneArmies??new ArmyRoadMover[0]);if(!armies.Contains(m))armies.Add(m);setup.sceneArmies=armies.ToArray();
    m.terrainMap=setup.freeMarchMap;if(setup.freeMarchMap!=null)m.waterMap=setup.freeMarchMap.waterMap;
   }
   var p=GetComponent<RecoveryPoint>();if(p!=null){if(string.IsNullOrEmpty(p.pointId))p.pointId=System.Guid.NewGuid().ToString("N");var points=new List<RecoveryPoint>(world.points??new RecoveryPoint[0]);if(!points.Contains(p))points.Add(p);world.points=points.ToArray();}
   var c=GetComponent<ResourceCollector>();var logistics=world.GetComponent<LogisticsWorld>();if(c!=null&&logistics!=null&&!logistics.collectors.Contains(c))logistics.collectors.Add(c);
  }
  public Sprite Select(bool own){if(symbolUnit==null)symbolUnit=GetComponent<BattleUnit>();var u=symbolUnit;return catalog==null||u==null||u.combatDefinition==null?null:catalog.Unit(u.combatDefinition.role,u.combatDefinition.echelon,own);}
  void LateUpdate(){
   if(GetComponent<BattleUnit>()!=null||catalog==null)return;if(world==null)world=FindObjectOfType<BattleWorld>();if(world==null)return;
   var r=GetComponent<SpriteRenderer>();if(r==null)return;var p=GetComponent<RecoveryPoint>();var c=GetComponent<ResourceCollector>();
   string owner=p!=null?p.ownerId:c!=null?c.ownerId:"";bool own=owner==(world.SimulationEnded?world.players[0]:world.currentPlayer);
   r.sprite=p!=null?(own?catalog.friendlyCity:catalog.enemyCity):(own?catalog.friendlyCollector:catalog.enemyCollector);r.color=Color.white;if(p!=null&&p.kind==FacilityKind.RecoveryPoint)r.sortingOrder=citySortingOrder;
  }
 }
}


