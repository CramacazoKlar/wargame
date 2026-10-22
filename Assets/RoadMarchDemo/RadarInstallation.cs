using System;
using UnityEngine;
namespace CampaignDemo {
 [DisallowMultipleComponent]
 public sealed class RadarInstallation:MonoBehaviour {
  public RadarDefinition definition;
  ArmyCombatDefinition owned;
  void Awake(){var unit=GetComponent<BattleUnit>();if(unit==null||definition==null)return;unit.CapacityOverride=definition.health;owned=Instantiate(unit.combatDefinition);owned.visionRadius=definition.visionRadius;owned.canHideInForest=false;unit.combatDefinition=owned;}
  void OnDestroy(){if(owned!=null)Destroy(owned);}
  public static RadarDefinition Settings {get{return Resources.Load<RadarDefinition>("Radar/RadarPlanning");}}
  public static bool CanDeploy(BattleWorld world,BattleUnit engineer,Vector3 p){
   var plan=Settings;if(plan==null||world==null||!world.Initialized||world.SimulationEnded||!LocalPrototypeOrders.EnabledFor(world)||!CombinedArmsWorld.Finite(p)||engineer==null||!engineer.CanMove||engineer.ownerId!=NetworkTacticalOrders.Player(world,engineer.ownerId)||engineer.combatDefinition.role!=UnitRole.Engineer||engineer.UnderFire)return false;
   if(Vector2.Distance(engineer.transform.position,p)>plan.deploymentDistance)return false;
   var map=world.setup.freeMarchMap;if(map==null||!map.definition.bounds.Contains(p))return false;var kind=map.TerrainAt(new RoadPoint(p.x,p.y));if(kind==TerrainKind.Water||kind==TerrainKind.Blocked||kind==TerrainKind.HighMountain)return false;
   if(map.waterMap!=null&&!map.waterMap.CanTraverse(new RoadPoint(p.x,p.y),new RoadPoint(p.x,p.y)))return false;
   foreach(var u in world.units)if(u!=null&&u.Alive&&u.GetComponent<RadarInstallation>()!=null&&Vector2.Distance(u.transform.position,p)<plan.minimumSpacing)return false;
   foreach(var city in world.points)if(city!=null&&Vector2.Distance(city.transform.position,p)<.4f)return false;
   var payer=world.campaign==null?null:world.campaign.NearestStorage(engineer.ownerId,p);return payer!=null&&payer.resourceStock>=plan.buildCost;
  }
  public static CommandResult Deploy(BattleWorld world,BattleUnit engineer,Vector3 p){
   if(!CanDeploy(world,engineer,p))return CommandResult.No("雷达需位于工兵 1 km 内合法陆地，避开设施；最近己方仓库或据点需有"+ResourceEconomy.Money(Settings==null?20000:Settings.buildCost)+"资源");var prefab=Resources.Load<GameObject>("Radar/Radar");if(prefab==null)return CommandResult.No("缺少雷达预制体");
   var obj=Instantiate(prefab,engineer.transform.parent);p.z=0;obj.transform.position=p;obj.name="雷达";var u=obj.GetComponent<BattleUnit>();u.ownerId=engineer.ownerId;u.unitId=Guid.NewGuid().ToString("N");u.designation="雷达";u.CapacityOverride=Settings.health;
   var mover=obj.GetComponent<ArmyRoadMover>();mover.waterMap=world.setup.freeMarchMap.waterMap;mover.terrainMap=world.setup.freeMarchMap;mover.InitializeAt(world.setup.roadMap.Network,new RoadPoint(p.x,p.y),RoadRouteView.Create(obj.transform,"雷达固定位置",.03f,3,Color.clear));var free=obj.GetComponent<ArmyFreeMarchMover>();free.Initialize(mover.Motor);free.BindMap(world.setup.freeMarchMap);
   u.combatDefinition.echelon=ArmyEchelon.Squad;world.campaign.ConfigureUnit(u);u.Initialize();u.GetComponent<BattleUnitView>().Initialize();world.RegisterUnit(u,mover);world.campaign.NearestStorage(engineer.ownerId,p).resourceStock-=Settings.buildCost;if(engineer.Motor!=null)engineer.Motor.Stop();engineer.RevealForest();return CommandResult.Yes();
  }
 }
}
