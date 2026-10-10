using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using CampaignDemo;
public static class AISituationInstaller {
 public static void Run(){
  var scene=EditorSceneManager.OpenScene("Assets/Scenes/Battle01.unity");var world=Object.FindObjectOfType<BattleWorld>();var ai=world.GetComponent<BattleAIController>();
  var trigger=world.GetComponent<AICityReliefTrigger>();if(trigger==null)trigger=world.gameObject.AddComponent<AICityReliefTrigger>();trigger.controller=ai;
  foreach(var u in world.units)if(u!=null&&u.ownerId==ai.playerId&&world.campaign!=null&&(world.campaign.CanAttack(u)||u.combatDefinition.role==UnitRole.Artillery)){if(u.GetComponent<AIRecoveryResponse>()==null)u.gameObject.AddComponent<AIRecoveryResponse>();var order=u.GetComponent<AIUnitOrder>();if(order.task==null)order.task=u.combatDefinition.role==UnitRole.Artillery?ai.defaultArtillery:ai.defaultGuard;}
  EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
  string folder="Assets/Game/AIResponsePrefabs";Directory.CreateDirectory(folder);
  foreach(var name in new[]{"Infantry","TransportTruck"}){
   var original=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Game/UnitLibrary/Prefabs/Enemy/"+name+"_Squad.prefab");var g=(GameObject)PrefabUtility.InstantiatePrefab(original);g.name=name=="Infantry"?"敌步兵班_自动恢复":"敌资源车班_自动运输";var order=g.GetComponent<AIUnitOrder>();if(order==null)order=g.AddComponent<AIUnitOrder>();order.task=ai.defaultGuard;
   if(name=="Infantry")g.AddComponent<AIRecoveryResponse>();else g.AddComponent<AISupplyResponse>();
   PrefabUtility.SaveAsPrefabAsset(g,folder+"/"+(name=="Infantry"?"InfantryRecovery_Squad":"ResourceSupply_Squad")+".prefab");Object.DestroyImmediate(g);
  }
  AssetDatabase.SaveAssets();Debug.Log("AI_SITUATION_INSTALLED: relief trigger and recovery/supply prefab templates");
 }
}

