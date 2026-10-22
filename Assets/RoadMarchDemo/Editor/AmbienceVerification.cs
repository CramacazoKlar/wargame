using System;
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
namespace CampaignDemo.Editor {
 [InitializeOnLoad] public static class AmbienceVerification {
  const string Key="AmbienceVerification";static double start;static int phase,checks,id;static float until,previousTime;static string report="";
  static AmbienceVerification(){if(SessionState.GetBool(Key,false)){start=EditorApplication.timeSinceStartup;EditorApplication.update+=Poll;}}
  public static void Run(){EditorSceneManager.OpenScene("Assets/Scenes/DoubleValley9x.unity");SessionState.SetBool(Key,true);start=EditorApplication.timeSinceStartup;EditorApplication.update+=Poll;EditorApplication.isPlaying=true;}
  static void Check(bool ok,string text){if(!ok)throw new Exception(text);checks++;report+="PASS "+text+"\n";Debug.Log("AMBIENCE_PASS "+text);}
  static void Poll(){try{
   if(EditorApplication.timeSinceStartup-start>120)throw new Exception("Ambience timeout");if(!EditorApplication.isPlaying||Time.unscaledTime<1)return;
   var w=UnityEngine.Object.FindObjectOfType<BattleWorld>();if(w==null||!w.Initialized)return;var a=w.GetComponent<BattlefieldAmbience>();if(a==null||a.Source==null)return;var s=a.Source;
   if(phase==0){
    Check(a.phaseSeconds==10,"Production volume phases each last ten real seconds");Check(BattlefieldAmbience.Level(5,10,0,.65f,.25f)==.65f&&BattlefieldAmbience.Level(15,10,0,.65f,.25f)==.25f&&BattlefieldAmbience.Level(25,10,0,.65f,.25f)==.65f,"Volume alternates normal, quiet, normal");
    float level=BattlefieldAmbience.Level(10.2,10,.4f,.65f,.25f);Check(level>.25f&&level<.65f,"Volume transitions smoothly at boundaries");
    Check(s.isPlaying&&s.loop&&s.clip.name=="MapAmbience"&&s.clip.length>21.3f,"Full supplied ambience track plays continuously on loop");
    Check(s.pitch==1&&s.spatialBlend==0&&s.panStereo==0&&s.GetComponent<AudioLowPassFilter>()==null,"Original tone preserved without pitch, direction or filtering");
    var audio=w.GetComponent<BattlefieldAudio>();audio.forestBirds=true;audio.PlayAt(BattlefieldSound.Bird,Vector3.zero);Check(audio.ActiveVoicesOf(BattlefieldSound.Bird)==0,"Bird calls cannot play even through the old entry point");audio.enabled=false;audio.enabled=true;Check(w.GetComponents<BattlefieldAmbience>().Length==1,"Re-enabling battle audio does not duplicate ambient playback");
    a.phaseSeconds=.25f;a.transitionSeconds=0;id=s.GetInstanceID();previousTime=s.time;Time.timeScale=0;until=Time.unscaledTime+.35f;phase=1;return;
   }
   if(phase==1){if(Time.unscaledTime<until)return;Check(s.isPlaying&&s.time>previousTime+.15f,"Ambient playback continues while simulation is paused");Check(s.GetInstanceID()==id,"Volume changes retain the same source and playback position");float expected=(a.QuietPhase?a.quietVolume:a.normalVolume)*Mathf.Clamp01(PlayerPrefs.GetFloat("Campaign.EffectsVolume",.8f));Check(Mathf.Abs(s.volume-expected)<.01f,"Runtime volume follows its alternating phase");Time.timeScale=1;File.WriteAllText("ambience-verification.txt",checks+" checks passed\n"+report);Finish(0);}
  }catch(Exception e){Debug.LogException(e);File.WriteAllText("ambience-verification.txt","FAIL\n"+report+e);Finish(1);}}
  static void Finish(int code){Time.timeScale=1;SessionState.SetBool(Key,false);EditorApplication.update-=Poll;EditorApplication.Exit(code);}
 }
}
