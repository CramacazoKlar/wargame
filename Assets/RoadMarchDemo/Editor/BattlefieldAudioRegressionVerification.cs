using System;
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
namespace CampaignDemo.Editor {
 [InitializeOnLoad] public static class BattlefieldAudioRegressionVerification {
  const string Key="BattlefieldAudioRegression";static double started;static int phase,checks;static float until;
  static string report="";static BattleUnit foot,car,tank;static float startX;
  static BattlefieldAudioRegressionVerification(){if(SessionState.GetBool(Key,false)){started=EditorApplication.timeSinceStartup;EditorApplication.update+=Poll;}}
  public static void Run(){EditorSceneManager.OpenScene("Assets/Scenes/DoubleValley9x.unity");SessionState.SetBool(Key,true);started=EditorApplication.timeSinceStartup;EditorApplication.update-=Poll;EditorApplication.update+=Poll;EditorApplication.isPlaying=true;}
  static void Check(bool ok,string text){if(!ok)throw new Exception(text);checks++;report+="PASS "+text+"\n";Debug.Log("AUDIO_REGRESSION_PASS "+text);}
  static BattleUnit Fixture(BattleWorld w,UnitRole role,float y){
   var go=new GameObject("Audio regression "+role);go.transform.position=new Vector3(6,y,0);
   var motor=go.AddComponent<ArmyRouteMotor>();motor.Initialize(RoadRouteView.Create(go.transform,"Test route",.03f,1,Color.white));
   var u=go.AddComponent<BattleUnit>();u.ownerId=w.currentPlayer;u.unitId="audio-regression-"+role;u.combatDefinition=ScriptableObject.CreateInstance<ArmyCombatDefinition>();u.combatDefinition.role=role;u.Initialize();
   var units=w.units;Array.Resize(ref units,units.Length+1);units[units.Length-1]=u;w.units=units;
   Check(motor.Commit(new[]{new RoadPoint(6,y),new RoadPoint(12,y)},()=>.6f),"Real route accepted for "+role);return u;
  }
  static AudioSource Voice(BattlefieldAudio audio,BattlefieldSound kind){foreach(var source in audio.GetComponentsInChildren<AudioSource>())if(source.isPlaying&&source.clip!=null&&source.clip.name=="Procedural "+kind)return source;return null;}
  static void Poll(){try{
   if(EditorApplication.timeSinceStartup-started>120)throw new Exception("Regression timeout");if(!EditorApplication.isPlaying||Time.time<1)return;
   var w=UnityEngine.Object.FindObjectOfType<BattleWorld>();if(w==null||!w.Initialized)return;
   var audio=w.GetComponent<BattlefieldAudio>();var orbit=w.GetComponent<TabletopCamera>();
   if(phase==0){
    // The open scene has no serialized audio component; exercise fresh defaults.
    Check(audio!=null,"Audio installed automatically");w.enabled=false;foreach(var b in w.GetComponents<MonoBehaviour>())if(b is BattleAIController||b is AIBasicCommander)b.enabled=false;
    w.fogEnabled=false;audio.periodicTelegraph=false;audio.forestBirds=false;audio.Clear();
    orbit.Focus(Vector2.zero);orbit.yaw=0;orbit.tilt=32;w.setup.mapCamera.orthographicSize=25;orbit.Apply();
    var p=audio.Profile(BattlefieldSound.Footstep);Check(p.farDistance>=8&&p.gain>=.5f,"Default footstep range and gain suit the 25-unit camera zoom");
    Check(audio.birdMinimumInterval==3&&audio.birdMaximumInterval==7,"Bird calls halved in frequency using a 3 to 7 second interval");
    foreach(var kind in new[]{BattlefieldSound.Bird,BattlefieldSound.Footstep,BattlefieldSound.Telegraph}){
     int count=BattlefieldAudioSynthesis.VariantCount(kind);var fingerprints=new System.Collections.Generic.HashSet<int>();
     for(int v=0;v<count;v++){var clip=BattlefieldAudioSynthesis.Create(kind,v);var data=new float[clip.samples];clip.GetData(data,0);int hash=17;foreach(float sample in data){if(float.IsNaN(sample)||float.IsInfinity(sample))throw new Exception("Invalid audio sample");unchecked{hash=hash*31+sample.GetHashCode();}}fingerprints.Add(hash);UnityEngine.Object.Destroy(clip);}
     Check(fingerprints.Count==count&&count>=6,"Distinct valid waveforms for all "+kind+" variants");
     AudioClip previous=null;bool repeated=false;var used=new System.Collections.Generic.HashSet<AudioClip>();
     for(int i=0;i<16;i++){audio.Clear();audio.PlayAt(kind,Vector3.zero);var voice=Voice(audio,kind);if(voice==null)throw new Exception("Variant voice failed: "+kind);if(voice.clip==previous)repeated=true;previous=voice.clip;used.Add(previous);}
     Check(!repeated&&used.Count>=3,"Playback varies "+kind+" without consecutive repeats");audio.Clear();
    }
    foot=Fixture(w,UnitRole.Infantry,0);car=Fixture(w,UnitRole.Logistics,1);tank=Fixture(w,UnitRole.Tank,-1);startX=foot.transform.position.x;
    until=Time.unscaledTime+3;phase=1;return;
   }
   if(phase==1){
    var f=Voice(audio,BattlefieldSound.Footstep);var c=Voice(audio,BattlefieldSound.Car);var t=Voice(audio,BattlefieldSound.Tank);
    if(f==null||c==null||t==null){if(Time.unscaledTime>until)throw new Exception("Real movement did not trigger all three sound categories");return;}
    Check(foot.transform.position.x>startX,"ArmyRouteMotor really moved the infantry");
    Check(f.volume>.02f,"Footstep audible gain at six map units and default zoom");Check(c.volume>.05f&&t.volume>.05f,"Car and tank audible gain at default zoom");
    Check(f.panStereo>0&&c.panStereo>0&&t.panStereo>0,"Movement audio appears on the correct side");
    foot.Motor.Stop();car.Motor.Stop();tank.Motor.Stop();until=Time.unscaledTime+.3f;phase=2;return;
   }
   if(phase==2){if(Time.unscaledTime<until)return;
    Check(audio.ActiveVoicesOf(BattlefieldSound.Car)==0&&audio.ActiveVoicesOf(BattlefieldSound.Tank)==0,"Stopping movement stops engine loops");audio.Clear();
    w.NotifyCombatEffect(CombatEffectEvent.Direct(foot,tank.unitId,tank.ownerId,tank.transform.position));
    Check(Voice(audio,BattlefieldSound.Rifle)!=null&&Voice(audio,BattlefieldSound.Rifle).volume>.05f,"Real combat event produces an audible rifle shot");
    w.NotifyCombatEffect(CombatEffectEvent.Direct(tank,foot.unitId,foot.ownerId,foot.transform.position));Check(Voice(audio,BattlefieldSound.Cannon)!=null,"Tank attack event produces cannon sound");
    w.NotifyCombatEffect(new CombatEffectEvent(CombatEffectStage.ArtilleryImpact,tank.unitId,tank.ownerId,UnitRole.Tank,true,tank.transform.position,foot.unitId,foot.ownerId,foot.transform.position));Check(Voice(audio,BattlefieldSound.Explosion)!=null,"Visible artillery impact produces explosion");
    audio.Clear();audio.telegraphMinimumInterval=audio.telegraphMaximumInterval=1;audio.periodicTelegraph=true;audio.enabled=false;audio.enabled=true;
    Check(audio.ActiveVoicesOf(BattlefieldSound.Telegraph)==0,"Telegraph does not play immediately on enable");until=Time.unscaledTime+.7f;phase=3;return;
   }
   if(phase==3){if(Time.unscaledTime<until)return;Check(audio.ActiveVoicesOf(BattlefieldSound.Telegraph)==0,"Telegraph waits for its interval");until=Time.unscaledTime+.7f;phase=4;return;}
   if(phase==4){if(audio.ActiveVoicesOf(BattlefieldSound.Telegraph)==0){if(Time.unscaledTime>until)throw new Exception("Periodic telegraph did not fire without an order");return;}
    Check(true,"Telegraph plays automatically with no click or command");
    audio.periodicTelegraph=false;audio.Clear();
    Check(BattlefieldAudio.TryCenter(w.setup.mapCamera,out var center)&&Vector2.Distance(center,orbit.focus)<.002f,"Listening center remains map focus at overview zoom");
    File.WriteAllText("battlefield-audio-regression.txt",checks+" checks passed\n"+report);Finish(0);
   }
  }catch(Exception e){Debug.LogException(e);File.WriteAllText("battlefield-audio-regression.txt","FAIL\n"+report+e);Finish(1);}}
  static void Finish(int code){SessionState.SetBool(Key,false);EditorApplication.update-=Poll;EditorApplication.Exit(code);}
 }
}
