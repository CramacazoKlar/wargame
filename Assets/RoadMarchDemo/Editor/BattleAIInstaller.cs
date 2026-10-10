using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using CampaignDemo;
public static class BattleAIInstaller {
 const string Root="Assets/Game/Data/AI";
 public static void Run(){
  Directory.CreateDirectory(Root);
  var guard=Make("DefaultGuard",AITaskKind.Guard,false);
  var artillery=Make("DefaultArtillery",AITaskKind.Artillery,false);
  Make("PatrolTemplate",AITaskKind.Patrol,false);Make("ReinforcementTemplate",AITaskKind.Reinforce,true);
  var scene=EditorSceneManager.OpenScene("Assets/Scenes/Battle01.unity");var world=Object.FindObjectOfType<BattleWorld>();
  var ai=world.GetComponent<BattleAIController>();if(ai==null)ai=world.gameObject.AddComponent<BattleAIController>();
  ai.world=world;ai.playerId="B";ai.defaultGuard=guard;ai.defaultArtillery=artillery;ai.aiEnabled=true;ai.pauseWhenPlayerControlled=true;ai.assignUnconfiguredUnits=true;
  EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();Debug.Log("BATTLE_AI_INSTALLED: map-independent task assets and B controller");
 }
 static AITaskDefinition Make(string name,AITaskKind kind,bool wait){string path=Root+"/"+name+".asset";var task=AssetDatabase.LoadAssetAtPath<AITaskDefinition>(path);if(task==null){task=ScriptableObject.CreateInstance<AITaskDefinition>();task.kind=kind;task.requireActivation=wait;AssetDatabase.CreateAsset(task,path);}return task;}
}
