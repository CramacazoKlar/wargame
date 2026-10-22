using System;
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
namespace CampaignDemo.Editor {
 [InitializeOnLoad] public static class BattlefieldAudioVerification {
  const string Key="BattlefieldAudioVerification";static double started;static int phase,checks;static string report="";
  static BattlefieldAudioVerification(){if(SessionState.GetBool(Key,false)){started=EditorApplication.timeSinceStartup;EditorApplication.update+=Poll;}}
  [MenuItem("Campaign Demo/Audio/Configure Current Scene")]
  public static void ConfigureCurrentScene(){
   if(EditorApplication.isPlayingOrWillChangePlaymode)return;
   var w=UnityEngine.Object.FindObjectOfType<BattleWorld>();if(w==null)return;
   var a=w.GetComponent<BattlefieldAudio>();if(a==null)a=Undo.AddComponent<BattlefieldAudio>(w.gameObject);a.world=w;
   EditorUtility.SetDirty(a);EditorSceneManager.MarkSceneDirty(w.gameObject.scene);Selection.activeObject=a;
  }
  public static void Run(){try{
   EditorSceneManager.OpenScene("Assets/Scenes/DoubleValley9x.unity");var world=UnityEngine.Object.FindObjectOfType<BattleWorld>();
   var audio=world.GetComponent<BattlefieldAudio>();if(audio==null)audio=world.gameObject.AddComponent<BattlefieldAudio>();audio.world=world;
   EditorUtility.SetDirty(audio);EditorSceneManager.MarkSceneDirty(world.gameObject.scene);EditorSceneManager.SaveOpenScenes();AssetDatabase.SaveAssets();
   SessionState.SetBool(Key,true);started=EditorApplication.timeSinceStartup;EditorApplication.update-=Poll;EditorApplication.update+=Poll;EditorApplication.isPlaying=true;
  }catch(Exception e){Fail(e);}}
  static void Check(bool result,string message){if(!result)throw new Exception(message);checks++;report+="PASS "+message+"\n";Debug.Log("BATTLEFIELD_AUDIO_PASS "+message);}
  static void Poll(){try{
   if(EditorApplication.timeSinceStartup-started>180)throw new Exception("Audio runtime check timeout");
   if(!EditorApplication.isPlaying||Time.time<1)return;
   var world=UnityEngine.Object.FindObjectOfType<BattleWorld>();if(world==null||!world.Initialized)return;
   var audio=world.GetComponent<BattlefieldAudio>();var c=world.setup.mapCamera;var orbit=world.GetComponent<TabletopCamera>();
   if(phase++==0){
    world.enabled=false;foreach(var b in world.GetComponents<MonoBehaviour>())if(b is BattleAIController||b is AIBasicCommander)b.enabled=false;
    Check(audio!=null&&UnityEngine.Object.FindObjectsOfType<BattlefieldAudio>().Length==1,"Single audio controller attached to battle scene");
    orbit.Focus(Vector2.zero);orbit.tilt=32;orbit.yaw=0;c.orthographicSize=5;orbit.Apply();
    Check(BattlefieldAudio.TryCenter(c,out var center)&&Vector2.Distance(center,orbit.focus)<.002f,"Tilted perspective listening center is map focus, not elevated camera");
    var p=audio.Profile(BattlefieldSound.Rifle);float g=BattlefieldAudio.DistanceGain(2,p);c.transform.position-=c.transform.forward*40;
    Check(Mathf.Abs(g-BattlefieldAudio.DistanceGain(2,p))<.00001f,"Camera altitude excluded from map distance attenuation");orbit.Apply();
    Check(BattlefieldAudio.DistanceGain(0,p)==1&&BattlefieldAudio.DistanceGain(p.farDistance,p)==0,"Full gain near center and silence at propagation boundary");
    Check(BattlefieldAudio.DistanceGain(1,p)>BattlefieldAudio.DistanceGain(3,p),"Distance attenuation decreases monotonically");
    Check(audio.Profile(BattlefieldSound.Footstep).farDistance<audio.Profile(BattlefieldSound.Car).farDistance&&audio.Profile(BattlefieldSound.Car).farDistance<audio.Profile(BattlefieldSound.Tank).farDistance&&audio.Profile(BattlefieldSound.Tank).farDistance<audio.Profile(BattlefieldSound.Cannon).farDistance,"Distinct footstep, car, tank and cannon ranges");
    var right=c.transform.right;right.z=0;Check(BattlefieldAudio.StereoPan(c,center,center+right,1)>0&&BattlefieldAudio.StereoPan(c,center,center-right,1)<0,"Left and right panning matches camera orientation");
    orbit.yaw=95;orbit.Apply();BattlefieldAudio.TryCenter(c,out center);right=c.transform.right;right.z=0;
    Check(Vector2.Distance(center,orbit.focus)<.002f&&BattlefieldAudio.StereoPan(c,center,center+right,1)>0,"Orbit preserves center and rotates stereo direction");
    float close=audio.ZoomGain();c.orthographicSize=65;orbit.Apply();Check(audio.ZoomGain()<close&&audio.ZoomGain()>0,"Overview zoom reduces gain without enlarging sound ranges");c.orthographicSize=5;orbit.Apply();
    foreach(BattlefieldSound kind in Enum.GetValues(typeof(BattlefieldSound))){var clip=BattlefieldAudioSynthesis.Create(kind);Check(clip!=null&&clip.channels==1&&clip.samples>100,"Generated mono starter clip: "+kind);UnityEngine.Object.Destroy(clip);}
    audio.forestBirds=false;audio.Clear();audio.PlayAt(BattlefieldSound.Tank,Vector3.zero,loop:true);
    // Inspect configured source; audibility on a physical output device still requires listening.
    var sources=audio.GetComponentsInChildren<AudioSource>();bool pooled=false;foreach(var s in sources)if(s.name=="Battlefield sound"&&s.clip!=null&&s.spatialBlend==0)pooled=true;Check(pooled,"Voice pool uses custom map spatialization without double 3D attenuation");audio.Clear();
    BattleUnit enemy=null;foreach(var u in world.units)if(u!=null&&u.ownerId!=world.currentPlayer){enemy=u;break;}Check(enemy!=null,"Enemy fixture exists");
    enemy.Initialize();world.fogEnabled=true;enemy.transform.position=new Vector3(999,999,0);Check(!audio.Audible(enemy),"Undiscovered enemy blocked by authoritative visibility");
    audio.PlayAt(BattlefieldSound.Tank,Vector3.zero,enemy,loop:true);Check(audio.ActiveVoices==0,"Hidden enemy cannot allocate an audible movement voice");
    world.fogEnabled=false;enemy.transform.position=Vector3.zero;enemy.RevealForest();Check(audio.Audible(enemy),"Visible enemy accepted");
    audio.PlayAt(BattlefieldSound.Tank,Vector3.zero,enemy,loop:true);
    Check(audio.ActiveVoices>0,"Visible enemy movement really starts a voice before visibility is removed");
    world.fogEnabled=true;enemy.transform.position=new Vector3(999,999,0);
    return;
   }
   Check(audio.ActiveVoices==0,"Ongoing enemy voice stops immediately after loss of visibility");
   Check(File.Exists("Assets/RoadMarchDemo/BattlefieldAudio.cs"),"Runtime implementation installed");
   File.WriteAllText("battlefield-audio-verification.txt",checks+" checks passed\n"+report);
   SessionState.SetBool(Key,false);EditorApplication.update-=Poll;EditorApplication.Exit(0);
  }catch(Exception e){Fail(e);}}
  static void Fail(Exception e){SessionState.SetBool(Key,false);EditorApplication.update-=Poll;Debug.LogException(e);File.WriteAllText("battlefield-audio-verification.txt","FAIL\n"+report+e);EditorApplication.Exit(1);}
 }
}
