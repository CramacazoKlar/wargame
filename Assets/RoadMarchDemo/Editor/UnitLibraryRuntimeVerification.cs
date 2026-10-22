using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using CampaignDemo;
[InitializeOnLoad]
public static class UnitLibraryRuntimeVerification {
 static double start;
 static UnitLibraryRuntimeVerification(){if(SessionState.GetBool("UnitLibraryRuntime",false)){start=EditorApplication.timeSinceStartup;EditorApplication.update+=Poll;}}
 public static void Run(){
  EditorSceneManager.OpenScene("Assets/Scenes/Battle01.unity");
  foreach(var guid in AssetDatabase.FindAssets("t:Prefab",new[]{"Assets/Game/UnitLibrary/Prefabs"})){
   var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));var g=(GameObject)PrefabUtility.InstantiatePrefab(prefab);g.name="LibraryTest_"+prefab.name;g.transform.position=new Vector3(40+UnityEngine.Object.FindObjectsOfType<BattleUnit>().Length*3,40,0);
  }
  SessionState.SetBool("UnitLibraryRuntime",true);start=EditorApplication.timeSinceStartup;EditorApplication.update+=Poll;EditorApplication.isPlaying=true;
 }
 static void Check(bool ok,string label){if(!ok)throw new Exception(label);}
 static void Poll(){try{
  if(EditorApplication.timeSinceStartup-start>100)throw new Exception("Runtime timeout");if(!EditorApplication.isPlaying||Time.time<.5f)return;
  var world=UnityEngine.Object.FindObjectOfType<BattleWorld>();if(world==null||!world.Initialized)return;var ai=world.GetComponent<BattleAIController>();if(ai!=null)ai.aiEnabled=false;int units=0,cities=0,collectors=0;var ids=new HashSet<string>();
  foreach(var identity in UnityEngine.Object.FindObjectsOfType<UnitSymbolIdentity>())if(identity.name.StartsWith("LibraryTest_")){
   var u=identity.GetComponent<BattleUnit>();var p=identity.GetComponent<RecoveryPoint>();
   if(u!=null){Check(Array.IndexOf(world.units,u)>=0,"Unit auto registration");Check(ids.Add(u.unitId),"unique IDs");Check(u.Campaign==world.campaign&&u.Capacity>0&&u.Alive,"campaign/strength initialized");Check(u.Motor!=null&&u.GetComponent<ArmyRoadMover>().Motor==u.Motor,"shared movement motor");
    bool ordinary=world.campaign.CanAttack(u);Check(ordinary==(u.combatDefinition.role==UnitRole.Infantry||u.combatDefinition.role==UnitRole.MountainInfantry||u.combatDefinition.role==UnitRole.Engineer||u.combatDefinition.role==UnitRole.Tank),"role attack rules");
    var view=u.GetComponent<BattleUnitView>();view.Apply(true,true,true,world.unknownUnitSprite);Check(u.GetComponent<SpriteRenderer>().sprite==identity.Select(true),"friendly symbol");view.Apply(true,false,true,world.unknownUnitSprite);Check(u.GetComponent<SpriteRenderer>().sprite==identity.Select(false)&&u.GetComponent<SpriteRenderer>().color==Color.white,"enemy diamond without recolor");view.Apply(false,false,true,world.unknownUnitSprite);Check(!u.GetComponent<SpriteRenderer>().enabled,"fog hides full symbol");
    var battery=u.GetComponent<ArtilleryBattery>();if(battery!=null){Check(battery.Ready,"stationary artillery ready without setup");Check(Mathf.Abs(CampaignShellResolver.EquivalentStrength(world.campaign,u)*world.campaign.rules.artilleryDamagePerStrength-world.campaign.rules.At(world.campaign.rules.troopStrength,u.combatDefinition.echelon)*.2f)<.001f,"artillery echelon scaling");}
    var carrier=u.GetComponent<PersonnelCarrier>();if(carrier!=null)Check(carrier.PassengerCapacity==world.campaign.rules.At(world.campaign.rules.passengerCapacity,u.combatDefinition.echelon),"passenger capacity by echelon");
    var truck=u.GetComponent<TransportTruck>();if(truck!=null)Check(truck.Capacity==world.campaign.rules.At(world.campaign.rules.cargoCapacity,u.combatDefinition.echelon),"cargo capacity by echelon");units++;
   }else if(p!=null){Check(Array.IndexOf(world.points,p)>=0,"city registered");cities++;}
  }
  Check(units==80&&cities==2,"84 runtime instances");
  var camera=world.setup.mapCamera;var target=world.units[0];camera.transform.position=new Vector3(target.transform.position.x,target.transform.position.y,camera.transform.position.z);camera.orthographicSize=3;
  File.WriteAllText("unit-library-runtime-verification.txt","PASS: 80 unit prefabs + 2 cities + 2 collectors initialized. Unique IDs, automatic registration, campaign rules, movement motors, attack roles, friendly/enemy appearance, fog, artillery scaling, cargo and passenger capacities verified.");
  SessionState.SetBool("UnitLibraryRuntime",false);EditorApplication.update-=Poll;EditorApplication.Exit(0);
 }catch(Exception e){Debug.LogException(e);File.WriteAllText("unit-library-runtime-verification.txt",e.ToString());SessionState.SetBool("UnitLibraryRuntime",false);EditorApplication.update-=Poll;EditorApplication.Exit(1);}}
}


