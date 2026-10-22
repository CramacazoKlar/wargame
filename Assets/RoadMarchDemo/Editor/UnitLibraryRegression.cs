using System;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using CampaignDemo;
public static class UnitLibraryRegression {
 static void Check(bool value,string label){if(!value)throw new Exception(label);}
 public static void Run(){
  EditorSceneManager.OpenScene("Assets/Scenes/Battle01.unity");CampaignDemo.Editor.CampaignRevisionVerification.Validate();
  var root=new GameObject("Artillery regression");var world=root.AddComponent<BattleWorld>();world.enabled=false;world.fogEnabled=false;world.points=new RecoveryPoint[0];var campaign=root.AddComponent<CampaignRuntime>();campaign.enabled=false;world.campaign=campaign;campaign.world=world;campaign.rules=ScriptableObject.CreateInstance<CampaignRules>();var combined=root.AddComponent<CombinedArmsWorld>();combined.enabled=false;combined.world=world;campaign.combined=combined;
  var gun=new GameObject("gun").AddComponent<BattleUnit>();gun.ownerId="A";gun.unitId="gun";gun.combatDefinition=ScriptableObject.CreateInstance<ArmyCombatDefinition>();gun.combatDefinition.role=UnitRole.Artillery;gun.combatDefinition.echelon=ArmyEchelon.Squad;campaign.ConfigureUnit(gun);gun.Initialize();var battery=gun.gameObject.AddComponent<ArtilleryBattery>();battery.definition=AssetDatabase.LoadAssetAtPath<ArtilleryDefinition>("Assets/Game/Data/CombinedArms/DefaultArtillery.asset");
  var target=new GameObject("target").AddComponent<BattleUnit>();target.unitId="target";target.ownerId="B";target.combatDefinition=ScriptableObject.CreateInstance<ArmyCombatDefinition>();target.combatDefinition.echelon=ArmyEchelon.Regiment;target.transform.position=Vector3.right;campaign.ConfigureUnit(target);target.Initialize();world.units=new[]{gun,target};
  Check(battery.Ready,"stationary artillery needs no setup");
  for(int grade=0;grade<5;grade++){
   gun.combatDefinition.echelon=(ArmyEchelon)grade;gun.Initialize();target.Initialize();float before=target.Personnel;Check(battery.Fire(world,"A",target.transform.position).accepted,"manual fire");combined.Tick(battery.definition.delaySeconds*.5f);Check(target.Personnel==before,"no damage before impact delay");Check(!battery.Fire(world,"A",target.transform.position).accepted,"cooldown blocks duplicate launch");combined.Tick(battery.definition.delaySeconds*.5f);float expected=Mathf.Min(gun.Personnel*campaign.rules.artilleryDamagePerStrength,before*campaign.rules.shellMaximumLossFraction);Check(Mathf.Abs(target.Personnel-Mathf.Max(0,before-expected))<.01f,"echelon impact damage "+grade);battery.Tick(battery.definition.cooldownSeconds);
  }
  gun.combatDefinition.echelon=ArmyEchelon.Squad;gun.Initialize();gun.Damage(gun.Personnel*.5f);target.ownerId="A";target.Initialize();float friendlyBefore=target.Personnel;Check(battery.Fire(world,"A",target.transform.position).accepted,"half strength fire");combined.Tick(battery.definition.delaySeconds);Check(Mathf.Abs(target.Personnel-(friendlyBefore-gun.Personnel*campaign.rules.artilleryDamagePerStrength))<.01f,"half strength and equal friendly fire");
  battery.Tick(battery.definition.cooldownSeconds);gun.Initialize();target.Initialize();Check(battery.Fire(world,"A",target.transform.position).accepted,"snapshot shot launch");gun.Eliminate();target.Damage(100);float beforeImpact=target.Personnel;combined.Tick(battery.definition.delaySeconds);Check(Mathf.Abs(target.Personnel-(beforeImpact-1.4f))<.01f,"launch strength remains frozen after gun death and impact uses current target strength");
  UnityEngine.Object.DestroyImmediate(gun.gameObject);UnityEngine.Object.DestroyImmediate(target.gameObject);UnityEngine.Object.DestroyImmediate(root);Debug.Log("UNIT_LIBRARY_REGRESSION_PASS: existing 62 checks plus artillery deployment, five echelons, delay, cooldown, reduced strength and friendly fire");
 }
}


