using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using CampaignDemo;
[InitializeOnLoad]
public static class ScenePrefabRuntimeVerification {
 static double start;static int stage;static float phase;static BattleUnit blue,red;static float before;static Vector3 initialBlue,initialRed;static readonly List<string> checks=new List<string>();
 static ScenePrefabRuntimeVerification(){if(SessionState.GetBool("ScenePrefabTest",false)){start=EditorApplication.timeSinceStartup;EditorApplication.update+=Poll;}}
 public static void Run(){ScenePrefabInstaller.Run();foreach(var component in UnityEngine.Object.FindObjectsOfType<MonoBehaviour>())if(component is BattleUnit||component is RecoveryPoint||component is Fortification||component is ResourceCollector||component is ResourceDepot||component is ResourceSite)Check(PrefabUtility.IsPartOfPrefabInstance(component.gameObject),"gameplay object is prefab instance: "+component.name);SessionState.SetBool("ScenePrefabTest",true);start=EditorApplication.timeSinceStartup;EditorApplication.update+=Poll;EditorApplication.isPlaying=true;}
 static void Check(bool value,string label){if(!value)throw new Exception(label);checks.Add("PASS "+label);}
 static void Poll(){try{
  if(EditorApplication.timeSinceStartup-start>90)throw new Exception("Scene prefab test timeout");if(!EditorApplication.isPlaying||Time.time<1)return;var world=UnityEngine.Object.FindObjectOfType<BattleWorld>();if(world==null||!world.Initialized)return;var ai=world.GetComponent<BattleAIController>();
  if(stage==0){foreach(var city in world.points)if(city.kind==FacilityKind.RecoveryPoint)Check(city.transform.localScale==Vector3.one*.5f,"city scale .5: "+city.name);
   Check(world.currentPlayer=="A"&&ai.aiEnabled,"A player enables B AI");foreach(var u in world.units){if(blue==null&&u.ownerId=="A")blue=u;if(red==null&&u.ownerId=="B")red=u;}initialBlue=blue.transform.position;initialRed=red.transform.position;Check(Vector2.Distance(initialBlue,initialRed)>red.combatDefinition.attackRadius,"initial separation outside enemy attack range");Check(red.GetComponent<AIUnitOrder>().State==AIUnitState.Holding,"enemy correctly remains guarding outside attack range");
   // Move only in the verification instance to reproduce real combat, then restore the original observation layout.
   blue.transform.position=red.transform.position+Vector3.right*.4f;before=blue.Personnel;phase=Time.time;stage=1;return;
  }
  if(stage==1){if(Time.time-phase<1.5f)return;Check(red.GetComponent<AIUnitOrder>().State==AIUnitState.Engaging,"B AI reacts to actual in-range enemy");Check(blue.Personnel<before,"real simulation damages attacker during contact");Check(red.GetComponent<AIUnitOrder>().DecisionReason.Contains("交战"),"AI reason explains engagement");world.SetPlayer("B");Check(!ai.aiEnabled,"B manual control turns AI off");world.SetPlayer("A");Check(ai.aiEnabled,"A re-enables B AI");blue.transform.position=initialBlue;red.transform.position=initialRed;phase=Time.time;stage=2;return;}
  if(stage==2){if(Time.time-phase<1.3f)return;Check(red.GetComponent<AIUnitOrder>().State==AIUnitState.Holding,"AI returns to guard once enemy leaves range");Check(red.GetComponent<AIUnitOrder>().DecisionReason.Contains("射程")||red.GetComponent<AIUnitOrder>().DecisionReason.Contains("守备"),"AI status explains why it does not advance");File.WriteAllText("scene-prefab-runtime-verification.txt",checks.Count+" checks passed\n"+string.Join("\n",checks));Debug.Log("SCENE_PREFAB_RUNTIME_PASS "+checks.Count);SessionState.SetBool("ScenePrefabTest",false);EditorApplication.update-=Poll;EditorApplication.Exit(0);}
 }catch(Exception e){Debug.LogException(e);File.WriteAllText("scene-prefab-runtime-verification.txt",e+"\n"+string.Join("\n",checks));SessionState.SetBool("ScenePrefabTest",false);EditorApplication.update-=Poll;EditorApplication.Exit(1);}}
}

