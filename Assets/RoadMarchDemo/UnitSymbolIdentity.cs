using UnityEngine;
using System.Collections.Generic;
namespace CampaignDemo {
 [DefaultExecutionOrder(-200)]
 public sealed class UnitSymbolIdentity:MonoBehaviour {
  public UnitSymbolCatalog catalog;
  public int citySortingOrder=20;
  BattleWorld world;BattleUnit symbolUnit;
  Material neutralCityMaterial,originalCityMaterial;SpriteRenderer cityRenderer;bool ownsFactionMaterial;
  void SetNeutralCityMaterial(SpriteRenderer renderer,bool neutral)
  {
   if(neutral)
   {
    if(neutralCityMaterial==null){var shader=Shader.Find("Campaign Demo/Neutral City");if(shader==null)return;neutralCityMaterial=new Material(shader);originalCityMaterial=renderer.sharedMaterial;if(originalCityMaterial!=null&&originalCityMaterial.shader.name=="Campaign Demo/Neutral City"){originalCityMaterial=new Material(Shader.Find("Sprites/Default"));ownsFactionMaterial=true;}cityRenderer=renderer;}
    renderer.sharedMaterial=neutralCityMaterial;
   }
   else if(neutralCityMaterial!=null&&renderer.sharedMaterial==neutralCityMaterial)renderer.sharedMaterial=originalCityMaterial;
  }
  void OnDestroy(){if(ownsFactionMaterial&&originalCityMaterial!=null){if(Application.isPlaying)Destroy(originalCityMaterial);else DestroyImmediate(originalCityMaterial);}if(neutralCityMaterial!=null){if(cityRenderer!=null&&cityRenderer.sharedMaterial==neutralCityMaterial)cityRenderer.sharedMaterial=originalCityMaterial;if(Application.isPlaying)Destroy(neutralCityMaterial);else DestroyImmediate(neutralCityMaterial);}}

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
   if(p!=null&&p.kind==FacilityKind.RecoveryPoint){if(GetComponent<StaffObjectiveMarker>()==null)gameObject.AddComponent<StaffObjectiveMarker>();return;}
  }
  public Sprite Select(bool own){if(symbolUnit==null)symbolUnit=GetComponent<BattleUnit>();var u=symbolUnit;return catalog==null||u==null||u.combatDefinition==null?null:catalog.Unit(u.combatDefinition.role,u.combatDefinition.echelon,u.ownerId==BattleFactions.BluePlayerId);}
  void LateUpdate(){
   if(GetComponent<BattleUnit>()!=null||catalog==null)return;if(world==null)world=FindObjectOfType<BattleWorld>();if(world==null)return;
   var r=GetComponent<SpriteRenderer>();if(r==null)return;var p=GetComponent<RecoveryPoint>();
   string owner=p!=null?p.ownerId:"";bool own=owner==BattleFactions.BluePlayerId;
   r.sprite=StaffMapSymbols.Facility(own,p!=null);r.color=p!=null&&string.IsNullOrEmpty(owner)?StaffMapStyle.Rule:Color.white;SetNeutralCityMaterial(r,p!=null&&string.IsNullOrEmpty(owner));if(p!=null&&p.kind==FacilityKind.RecoveryPoint)r.sortingOrder=citySortingOrder;
  }
 }
}


