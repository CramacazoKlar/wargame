using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using CampaignDemo;
[InitializeOnLoad]
public static class BattleAIRuntimeVerification {
 static double start;static readonly List<string> checks=new List<string>();
 static BattleAIRuntimeVerification(){if(SessionState.GetBool("BattleAIRuntime",false)){start=EditorApplication.timeSinceStartup;EditorApplication.update+=Poll;}}
 public static void Run(){BattleAIInstaller.Run();SessionState.SetBool("BattleAIRuntime",true);start=EditorApplication.timeSinceStartup;EditorApplication.update+=Poll;EditorApplication.isPlaying=true;}
 static void Check(bool ok,string label){if(!ok)throw new Exception(label);checks.Add("PASS "+label);}
 static void Poll(){try{
  if(EditorApplication.timeSinceStartup-start>90)throw new Exception("AI runtime timeout");if(!EditorApplication.isPlaying||Time.time<.5f)return;
  var world=UnityEngine.Object.FindObjectOfType<BattleWorld>();if(world==null||!world.Initialized)return;var ai=world.GetComponent<BattleAIController>();ai.enabled=false;ai.Refresh();Check(ai.Orders.Count>0,"enemy units automatically receive tasks");
  BattleUnit enemy=null,friend=null;foreach(var u in world.units){if(u.ownerId=="B"&&u.combatDefinition.role==UnitRole.Infantry)enemy=u;if(u.ownerId=="A"&&u.combatDefinition.role==UnitRole.Infantry)friend=u;}Check(enemy!=null&&friend!=null,"existing scene armies reused");
  Vector3 initialEnemyPosition=enemy.transform.position;var order=enemy.GetComponent<AIUnitOrder>();var task=ScriptableObject.CreateInstance<AITaskDefinition>();task.kind=AITaskKind.Guard;task.reactionSeconds=1;order.ResetTask(task);
  foreach(var u in world.units)if(u!=enemy&&u!=friend)u.transform.position=new Vector3(100,100,0);enemy.transform.position=Vector3.zero;friend.transform.position=new Vector3(10,0,0);world.currentPlayer="A";world.fogEnabled=true;
  ai.Tick(.25f);Check(order.State==AIUnitState.Holding,"guard holds without visible contacts");Check(ai.information.GetArmy("B",friend.unitId)==null,"enemy view cannot read hidden player unit");
  friend.transform.position=Vector3.right*.2f;ai.Tick(.25f);Check(order.State==AIUnitState.Reacting,"contact starts reaction delay");ai.Tick(.8f);Check(order.State==AIUnitState.Engaging,"visible in-range contact enters engagement");
  friend.transform.position=Vector3.right*10;ai.Tick(.5f);Check(order.State==AIUnitState.Engaging,"contact loss retains quiet window");ai.Tick(.6f);Check(order.State==AIUnitState.Holding,"clear contact resumes guard");
  task.requireActivation=true;task.kind=AITaskKind.Reinforce;order.ResetTask(task);order.taskKey="city-relief";ai.Tick(.25f);Check(order.State==AIUnitState.Waiting,"reserve waits for explicit mission trigger");ai.ActivateTask("city-relief");ai.Tick(.25f);Check(order.State==AIUnitState.Holding,"trigger activates reserve without inventing a route");
  order.ResetTask(task);world.currentPlayer="B";ai.Tick(2);Check(order.State==AIUnitState.Waiting,"manual B player suspends AI decisions");world.currentPlayer="A";
  task.requireActivation=false;task.kind=AITaskKind.Patrol;task.useRoad=false;task.waypoints=new[]{new Vector2(200,200)};order.ResetTask(task);ai.Tick(.25f);Check(order.State==AIUnitState.Failed&&!string.IsNullOrEmpty(order.LastCommandReason),"illegal terrain route reports command failure");ai.Tick(.25f);Check(order.State==AIUnitState.Failed,"failed command waits before retry instead of spamming");
  friend.transform.position=new Vector3(1000,1000);enemy.transform.position=initialEnemyPosition;enemy.Motor.Stop();task.kind=AITaskKind.Patrol;task.useRoad=false;Vector3 destination=initialEnemyPosition;bool found=false;
  for(int i=0;i<8;i++){var offset=new Vector3(Mathf.Cos(i*Mathf.PI/4),Mathf.Sin(i*Mathf.PI/4))*.2f;var candidate=initialEnemyPosition+offset;if(enemy.GetComponent<ArmyFreeMarchMover>().CanTraverse(world.setup.freeMarchMap,new RoadPoint(initialEnemyPosition.x,initialEnemyPosition.y),new RoadPoint(candidate.x,candidate.y))){destination=candidate;found=true;break;}}
  Check(found,"existing map supplies legal patrol leg");task.waypoints=new[]{(Vector2)destination};order.ResetTask(task);ai.Tick(.25f);Check(order.State==AIUnitState.Marching&&enemy.Motor.IsMoving,"patrol uses existing movement command");int count=order.SubmittedCommands;ai.Tick(.25f);Check(order.SubmittedCommands==count,"active route is not resubmitted each decision");
  friend.transform.position=enemy.transform.position+Vector3.right*.1f;ai.Tick(.25f);Check(enemy.Motor.IsMoving&&order.State==AIUnitState.Reacting,"AI reaction delay preserves movement exposure");ai.Tick(1);Check(!enemy.Motor.IsMoving&&order.State==AIUnitState.Engaging,"AI explicitly stops through command service");friend.transform.position=new Vector3(1000,1000);ai.Tick(1.1f);Check(order.State==AIUnitState.Marching&&enemy.Motor.IsMoving,"contact clear resumes unfinished patrol destination");
  enemy.transform.position=new Vector3(100,100);enemy.Motor.Stop();var gunObject=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Game/UnitLibrary/Prefabs/Enemy/Artillery_Squad.prefab"));gunObject.transform.position=Vector3.zero;var gun=gunObject.GetComponent<BattleUnit>();gun.unitId=Guid.NewGuid().ToString("N");var mover=gun.GetComponent<ArmyRoadMover>();mover.InitializeAt(world.setup.roadMap.Network,new RoadPoint(0,0),RoadRouteView.Create(gunObject.transform,"AI test route",.06f,3,Color.cyan));gun.GetComponent<ArmyFreeMarchMover>().Initialize(mover.Motor);gun.GetComponent<ArmyFreeMarchMover>().BindMap(world.setup.freeMarchMap);world.RegisterUnit(gun,mover);ai.Refresh();var gunOrder=gun.GetComponent<AIUnitOrder>();var battery=gun.GetComponent<ArtilleryBattery>();friend.transform.position=Vector3.right*1.5f;
  Check(battery.Ready,"AI artillery needs no deployment");enemy.transform.position=friend.transform.position+Vector3.right*.1f;ai.Tick(.25f);Check(battery.Remaining==0,"AI artillery refuses current friendly blast risk");enemy.transform.position=new Vector3(100,100);ai.Tick(.25f);Check(battery.Remaining>0,"AI artillery fires at safe visible target");int gunCount=gunOrder.SubmittedCommands;ai.Tick(.25f);Check(gunOrder.SubmittedCommands==gunCount,"artillery respects cooldown without repeated fire commands");
  File.WriteAllText("battle-ai-runtime-verification.txt",checks.Count+" checks passed\n"+string.Join("\n",checks));Debug.Log("BATTLE_AI_RUNTIME_VERIFIED "+checks.Count);SessionState.SetBool("BattleAIRuntime",false);EditorApplication.update-=Poll;EditorApplication.Exit(0);
 }catch(Exception e){File.WriteAllText("battle-ai-runtime-verification.txt",e+"\n"+string.Join("\n",checks));Debug.LogException(e);SessionState.SetBool("BattleAIRuntime",false);EditorApplication.update-=Poll;EditorApplication.Exit(1);}}
}

