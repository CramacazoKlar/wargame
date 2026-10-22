using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
namespace CampaignDemo {
 // Shared DoubleValley opening; deterministic IDs are required for network handshakes.
 public sealed class LocalOpeningGarrison:MonoBehaviour {
  public int initialArmyCount,initialInsideCount;
  readonly List<BattleUnit> starting=new List<BattleUnit>();float age;bool aiReleased;
  void Update(){Tick(Time.deltaTime);}
  public void Tick(float dt){age+=Mathf.Max(0,dt);if(aiReleased||age<3)return;var w=GetComponent<BattleWorld>();var ai=GetComponent<BattleAIController>();if(w==null||w.SimulationEnded||ai==null||!ai.CanThink)return;foreach(var u in starting)if(u!=null&&u.Alive&&u.ownerId==ai.playerId&&u.Point!=null)w.GetComponent<BattleCommandService>().Submit(new BattleCommand{kind=BattleCommandKind.ExitPoint,playerId=ai.playerId,unitId=u.unitId});aiReleased=true;}
  static BattleUnit SpawnOpeningUnit(BattleWorld w,string owner,RecoveryPoint city,UnitRole role){var prefab=LocalPvpReplication.Prefab(role);if(prefab==null)return null;var obj=Instantiate(prefab,w.transform);obj.transform.position=city.transform.position;obj.name=LevelUnitAvailability.Name(role);var u=obj.GetComponent<BattleUnit>();u.unitId="opening-"+owner+"-"+city.pointId+"-"+role;u.ownerId=owner;u.designation=LevelUnitAvailability.Name(role);var mover=obj.GetComponent<ArmyRoadMover>();mover.waterMap=w.setup.freeMarchMap.waterMap;mover.terrainMap=w.setup.freeMarchMap;mover.InitializeAt(w.setup.roadMap.Network,new RoadPoint(obj.transform.position.x,obj.transform.position.y),RoadRouteView.Create(obj.transform,LevelUnitAvailability.Name(role)+"移动路线",.06f,3,owner==BattleFactions.BluePlayerId?Color.cyan:Color.red));var free=obj.GetComponent<ArmyFreeMarchMover>();free.Initialize(mover.Motor);free.BindMap(w.setup.freeMarchMap);w.campaign.ConfigureUnit(u);u.Initialize();u.GetComponent<BattleUnitView>().Initialize();w.RegisterUnit(u,mover);return u;}
  public static void Apply(BattleWorld w){if(w==null||w.gameObject.scene.name!="DoubleValley9x"||!LocalPrototypeOrders.EnabledFor(w)||w.GetComponent<LocalOpeningGarrison>()!=null)return;var record=w.gameObject.AddComponent<LocalOpeningGarrison>();foreach(var owner in w.players){var city=w.points.FirstOrDefault(p=>p!=null&&!p.isRepairSite&&p.kind==FacilityKind.RecoveryPoint&&p.ownerId==owner);if(city==null)continue;foreach(UnitRole role in Enum.GetValues(typeof(UnitRole)))if(role!=UnitRole.Radar&&LevelUnitAvailability.Allowed(w,role)&&!w.units.Any(u=>u!=null&&u.Alive&&u.ownerId==owner&&u.combatDefinition.role==role))SpawnOpeningUnit(w,owner,city,role);var units=w.units.Where(u=>u!=null&&u.Alive&&u.ownerId==owner&&u.GetComponent<RadarInstallation>()==null).ToArray();city.capacity=Mathf.Max(city.capacity,units.Length);foreach(var u in units){record.starting.Add(u);record.initialArmyCount++;if(u.Point!=null)u.BeginExit();u.Motor?.Stop();u.transform.position=city.transform.position;if(u.BeginEnter(city))record.initialInsideCount++;}}
  }
 }
}
