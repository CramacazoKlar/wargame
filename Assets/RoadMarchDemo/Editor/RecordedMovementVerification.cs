using System;
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
namespace CampaignDemo.Editor {
 [InitializeOnLoad] public static class RecordedMovementVerification {
  const string Key="RecordedMovementVerification";static double start;static int phase,checks;static float until;static string report="";static BattleUnit infantry,tank;
  static RecordedMovementVerification(){if(SessionState.GetBool(Key,false)){start=EditorApplication.timeSinceStartup;EditorApplication.update+=Poll;}}
  public static void Run(){EditorSceneManager.OpenScene("Assets/Scenes/DoubleValley9x.unity");SessionState.SetBool(Key,true);start=EditorApplication.timeSinceStartup;EditorApplication.update+=Poll;EditorApplication.isPlaying=true;}
  static void Check(bool ok,string message){if(!ok)throw new Exception(message);checks++;report+="PASS "+message+"\n";Debug.Log("RECORDING_PASS "+message);}
  static BattleUnit Unit(BattleWorld w,UnitRole role,float y){var g=new GameObject("Recorded audio test "+role);g.transform.position=new Vector3(6,y);var motor=g.AddComponent<ArmyRouteMotor>();motor.Initialize(RoadRouteView.Create(g.transform,"Route",.03f,1,Color.white));var u=g.AddComponent<BattleUnit>();u.unitId="recorded-test-"+role;u.ownerId=w.currentPlayer;u.combatDefinition=ScriptableObject.CreateInstance<ArmyCombatDefinition>();u.combatDefinition.role=role;u.Initialize();var units=w.units;Array.Resize(ref units,units.Length+1);units[units.Length-1]=u;w.units=units;Check(motor.Commit(new[]{new RoadPoint(6,y),new RoadPoint(12,y)},()=>.5f),"Movement route accepted: "+role);return u;}
  static AudioSource Source(BattlefieldAudio audio,string name){foreach(var s in audio.GetComponentsInChildren<AudioSource>())if(s.isPlaying&&s.clip!=null&&s.clip.name==name)return s;return null;}
  static void Poll(){try{
   if(EditorApplication.timeSinceStartup-start>120)throw new Exception("Recorded audio verification timeout");if(!EditorApplication.isPlaying||Time.time<1)return;var w=UnityEngine.Object.FindObjectOfType<BattleWorld>();if(w==null||!w.Initialized)return;var audio=w.GetComponent<BattlefieldAudio>();
   if(phase==0){w.enabled=false;foreach(var b in w.GetComponents<MonoBehaviour>())if(b is BattleAIController||b is AIBasicCommander)b.enabled=false;audio.periodicTelegraph=false;audio.forestBirds=false;audio.Clear();var orbit=w.GetComponent<TabletopCamera>();orbit.Focus(Vector2.zero);w.setup.mapCamera.orthographicSize=25;orbit.Apply();
    foreach(string path in new[]{"Audio/Movement/InfantryMarch","Audio/Movement/TankMarch","Audio/Combat/TankAttack"}){var clip=Resources.Load<AudioClip>(path);Check(clip!=null&&clip.channels==1&&clip.frequency==48000&&clip.length>4,"User-provided recording imported: "+path);}
    infantry=Unit(w,UnitRole.Infantry,0);tank=Unit(w,UnitRole.Tank,1);until=Time.unscaledTime+2;phase=1;return;
   }
   if(phase==1){if(Source(audio,"InfantryMarch")==null||Source(audio,"TankMarch")==null){if(Time.unscaledTime>until)throw new Exception("Movement did not select recordings");return;}
    Check(Source(audio,"InfantryMarch").loop&&Source(audio,"TankMarch").loop,"Infantry and tank recordings loop during real movement");until=Time.unscaledTime+1;phase=2;return;
   }
   if(phase==2){if(Time.unscaledTime<until)return;Check(audio.ActiveVoicesOf(BattlefieldSound.Footstep)==1,"Long infantry recording plays once per unit, without repeated overlapping launches");
    w.NotifyCombatEffect(CombatEffectEvent.Direct(tank,infantry.unitId,infantry.ownerId,infantry.transform.position));Check(Source(audio,"TankAttack")!=null&&!Source(audio,"TankAttack").loop,"Tank attack uses supplied recording as a one-shot");Check(audio.ActiveVoicesOf(BattlefieldSound.Cannon)==0,"Tank attack does not reuse ordinary artillery sound");
    w.NotifyCombatEffect(new CombatEffectEvent(CombatEffectStage.ArtilleryLaunch,infantry.unitId,infantry.ownerId,UnitRole.Artillery,true,infantry.transform.position,tank.unitId,tank.ownerId,tank.transform.position));Check(audio.ActiveVoicesOf(BattlefieldSound.Cannon)>0,"Ordinary artillery still uses its separate sound");
    infantry.Motor.Stop();tank.Motor.Stop();until=Time.unscaledTime+.3f;phase=3;return;
   }
   if(phase==3){if(Time.unscaledTime<until)return;Check(audio.ActiveVoicesOf(BattlefieldSound.Footstep)==0&&audio.ActiveVoicesOf(BattlefieldSound.Tank)==0,"Stopping infantry and tank stops both recorded movement loops");
    File.WriteAllText("recorded-audio-verification.txt",checks+" checks passed\n"+report);Finish(0);}
  }catch(Exception e){Debug.LogException(e);File.WriteAllText("recorded-audio-verification.txt","FAIL\n"+report+e);Finish(1);}}
  static void Finish(int code){SessionState.SetBool(Key,false);EditorApplication.update-=Poll;EditorApplication.Exit(code);}
 }
}
