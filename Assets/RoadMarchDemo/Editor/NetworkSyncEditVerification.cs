using System;
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
namespace CampaignDemo.Editor {
 public static class NetworkSyncEditVerification {
  static int count;static string report="";
  static void Check(bool v,string text){if(!v)throw new Exception(text);count++;report+="PASS "+text+"\n";}
  public static void Run(){try{
   foreach(UnitRole role in Enum.GetValues(typeof(UnitRole))){var p=LocalPvpReplication.Prefab(role);Check(p!=null&&p.GetComponent<BattleUnit>().combatDefinition.role==role,"Network prefab role matches "+role);}
   var plan=AssetDatabase.LoadAssetAtPath<ArtilleryDefinition>("Assets/Game/Data/CombinedArms/DefaultArtillery.asset");Check(plan.BarrageDuration(UnitRole.Artillery,ArmyEchelon.Company)==5.5f,"Actual company duration 5.5 seconds");Check(plan.shellIntervalSeconds==.9f,"Actual ordinary interval 0.9 seconds");Check(plan.BarrageDuration(UnitRole.RocketArtillery,ArmyEchelon.Company)==3,"Rocket duration retained");
   foreach(var name in new[]{"ArtilleryLaunch","RocketBarrage"})Check(Convert.ToBase64String(File.ReadAllBytes("Assets/Resources/Audio/Combat/"+name+".wav"))==Convert.ToBase64String(File.ReadAllBytes("Assets/Resources/Audio/Combat/TankAttack.wav")),"Requested launch recording "+name);
   Check(Mathf.Abs(Resources.Load<AudioClip>("Audio/Combat/ArtilleryImpact").length-4.25f)<.01f,"Latest shared explosion recording imported");Check(Resources.Load<AudioClip>("Audio/Ambience/MapAmbience")!=null,"Continuous ambience is in network client resources");Check(Resources.LoadAll<AudioClip>("Audio/CommandVoices").Length==0,"Removed voices absent from network resources");
   EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);var root=new GameObject("Network filters");var world=root.AddComponent<BattleWorld>();world.units=new BattleUnit[0];world.currentPlayer="A";
   var hiddenLaunch=new CombatEffectEvent(CombatEffectStage.ArtilleryLaunch,"hidden","B",UnitRole.RocketArtillery,true,new Vector3(200,200),null,"A",Vector3.zero);Check(LocalPvpReplication.FilterEffect(world,"A",hiddenLaunch)==null,"Hidden enemy launch is not sent to client");
   var hiddenImpact=new CombatEffectEvent(CombatEffectStage.ArtilleryImpact,null,"B",UnitRole.Artillery,false,Vector3.zero,null,"B",new Vector3(200,200));Check(LocalPvpReplication.FilterEffect(world,"A",hiddenImpact)==null,"Unseen impact is not relocated to firing origin");
   var message=new PvpMessage{type="snapshot",snapshot=new PvpSnapshot()};message.snapshot.units.Add(new PvpUnit{artillery=JsonUtility.ToJson(plan),artilleryBarrageRemaining=2,experience=3,participated=true,marchStyle=0,task="巡逻",assigned=true,links=new[]{"other"}});message.snapshot.recoveryQueue=new[]{"worker"};message.snapshot.works.Add(new PvpWork{worker="worker",points=new[]{Vector3.zero,Vector3.right},remaining=4,total=8});var restored=JsonUtility.FromJson<PvpMessage>(JsonUtility.ToJson(message));Check(restored.version==2&&restored.snapshot.units[0].experience==3&&restored.snapshot.units[0].links[0]=="other"&&restored.snapshot.works[0].remaining==4&&restored.snapshot.recoveryQueue[0]=="worker","New state survives actual Unity network JSON serialization");
   Check(NetworkTacticalOrders.AsPlayer("B",()=>NetworkTacticalOrders.Player(world)=="B"?CommandResult.Yes():CommandResult.No("wrong")).accepted&&NetworkTacticalOrders.Player(world)=="A","Authenticated player scope restores host viewer");try{NetworkTacticalOrders.AsPlayer("B",()=>{throw new Exception("test");});}catch{}Check(!NetworkTacticalOrders.Executing,"Player scope also restores after exceptions");
   var invalid=new BattleCommand{playerId="B",unitId="missing",tacticalAction="patrol"};Check(!NetworkTacticalOrders.Execute(world,invalid).accepted,"Missing and foreign unit commands rejected");
   File.WriteAllText("network-sync-edit-result.txt",count+" checks passed\n"+report);EditorApplication.Exit(0);
  }catch(Exception e){File.WriteAllText("network-sync-edit-result.txt","FAIL\n"+report+e);Debug.LogException(e);EditorApplication.Exit(1);}}
 }
}
