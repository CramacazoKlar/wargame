using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
namespace CampaignDemo.Editor {
 [InitializeOnLoad] public static class BattlefieldFeatureVerification {
  const string Key="BattlefieldFeatureVerification";static double start;static int phase,checks;static float deadline;static string report="";static BattleUnit engineer;
  static BattlefieldFeatureVerification(){if(SessionState.GetBool(Key,false)){start=EditorApplication.timeSinceStartup;EditorApplication.update+=Poll;}}
  static void Check(bool ok,string message){if(!ok)throw new Exception(message);checks++;report+="PASS "+message+"\n";Debug.Log("AUDIO_FEATURE_PASS "+message);}
  sealed class AlwaysVoice:System.Random {public int calls;public override int Next(int maxValue){calls++;return 0;}}
  public static void Run(){try{RadarRocketInstaller.RefreshRocketColors();
   foreach(string faction in new[]{"Blue","Red"}){var path="Assets/Resources/Radar/Rocket_"+faction+"_128.png";var texture=new Texture2D(2,2);texture.LoadImage(File.ReadAllBytes(path));
    var fill=texture.GetPixel(60,192);var expected=faction=="Blue"?StaffMapStyle.BlueFill:StaffMapStyle.RedFill;
    Check(fill.a>.99f&&Vector3.Distance(new Vector3(fill.r,fill.g,fill.b),new Vector3(expected.r,expected.g,expected.b))<.02f,faction+" rocket icon has opaque faction-colored fill");
    var border=texture.GetPixel(256,334);Check(border.a>.99f&&border.r<.02f&&border.g<.02f&&border.b<.02f,faction+" rocket outline is black");UnityEngine.Object.DestroyImmediate(texture);
   }
   SessionState.SetString(Key+"Report",report);SessionState.SetInt(Key+"Checks",checks);EditorSceneManager.OpenScene("Assets/Scenes/DoubleValley9x.unity");SessionState.SetBool(Key,true);start=EditorApplication.timeSinceStartup;EditorApplication.update+=Poll;EditorApplication.isPlaying=true;
  }catch(Exception e){Fail(e);}}
  static void Poll(){try{
   if(EditorApplication.timeSinceStartup-start>150)throw new Exception("Feature verification timeout");if(!EditorApplication.isPlaying||Time.time<1)return;
   var w=UnityEngine.Object.FindObjectOfType<BattleWorld>();if(w==null||!w.Initialized)return;var audio=w.GetComponent<BattlefieldAudio>();
   if(phase==0){report=SessionState.GetString(Key+"Report","");checks=SessionState.GetInt(Key+"Checks",0);w.enabled=false;foreach(var b in w.GetComponents<MonoBehaviour>())if(b is BattleAIController||b is AIBasicCommander)b.enabled=false;
    audio.Clear();audio.forestBirds=false;audio.periodicTelegraph=false;var orbit=w.GetComponent<TabletopCamera>();orbit.Focus(Vector2.zero);w.setup.mapCamera.orthographicSize=25;orbit.Apply();
    Check(audio.birdMinimumInterval==3&&audio.birdMaximumInterval==7,"Bird interval doubled to 3-7 seconds");
    var crowd=BattlefieldAudioSynthesis.Create(BattlefieldSound.Footstep);var samples=new float[crowd.samples];crowd.GetData(samples,0);
    int occupied=0;for(int window=0;window<6;window++){float energy=0;for(int i=(int)(window*.1f*crowd.frequency);i<(int)((window*.1f+.1f)*crowd.frequency);i++)energy+=samples[i]*samples[i];if(energy>.01f)occupied++;}
    Check(crowd.length>.7f&&occupied==6,"Crowd footstep clip contains six staggered energetic impact windows");UnityEngine.Object.Destroy(crowd);
    Check(BattlefieldAudioSynthesis.VariantCount(BattlefieldSound.Construction)==4,"Four different construction clangs available");
    var clips=Resources.LoadAll<AudioClip>("Audio/CommandVoices");Check(clips.Length==5&&clips.All(c=>c.length>1),"Five real speech WAV clips imported");
    audio.PlayAt(BattlefieldSound.CommandVoice,Vector3.zero);Check(audio.ActiveVoicesOf(BattlefieldSound.CommandVoice)==1,"Speech playback uses the imported voice clips");audio.Clear();
    int outcomes=0;for(int roll=0;roll<5;roll++)if(BattlefieldAudio.CommandVoiceRoll(roll))outcomes++;Check(outcomes==1,"Exactly one of five equiprobable outcomes triggers a movement voice");
    var rngField=typeof(BattlefieldAudio).GetField("commandRandom",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance);var oldRandom=rngField.GetValue(audio);var deterministic=new AlwaysVoice();rngField.SetValue(audio,deterministic);
    var move=new BattleCommand{kind=BattleCommandKind.FreeRoute,playerId=w.currentPlayer};audio.MovementCommandAccepted(move);Check(audio.ActiveVoicesOf(BattlefieldSound.CommandVoice)==1,"A successful probability roll triggers movement speech");audio.Clear();audio.MovementCommandAccepted(move);
    Check(deterministic.calls==1&&audio.ActiveVoicesOf(BattlefieldSound.CommandVoice)==0,"Multiple units in one group order share a single probability roll");rngField.SetValue(audio,oldRandom);
    var rejected=w.GetComponent<BattleCommandService>().Submit(new BattleCommand{kind=BattleCommandKind.FreeDestination,playerId=w.currentPlayer,unitId="missing-audio-test-unit"});Check(!rejected.accepted&&audio.ActiveVoicesOf(BattlefieldSound.CommandVoice)==0,"Rejected movement orders never trigger speech");
    engineer=w.units.First(u=>u.ownerId==w.currentPlayer&&u.combatDefinition.role==UnitRole.Engineer);engineer.Initialize();
    foreach(var city in w.points)if(city.ownerId==engineer.ownerId)city.resourceStock=1000000;
    var works=w.campaign.lineWorks;bool found=false;Vector3[] line=null;
    for(int x=-4;x<=4&&!found;x++)for(int y=-4;y<=4&&!found;y++){var a=new Vector3(x,y);engineer.transform.position=a;line=new[]{a,a+Vector3.right*.7f};found=works.CanBuild(engineer,line,out _);}
    Check(found&&works.Build(engineer,line).accepted,"A real engineer construction project starts at its site");Check(engineer.Working&&!works.Approaching(engineer),"Engineer is actually building, not still walking to site");
    deadline=Time.unscaledTime+3;phase=1;return;
   }
   if(phase==1){if(audio.ActiveVoicesOf(BattlefieldSound.Construction)==0){if(Time.unscaledTime>deadline)throw new Exception("Construction did not trigger clang audio");return;}
    Check(true,"Active construction project triggers metallic clang audio");w.campaign.lineWorks.Cancel(engineer);deadline=Time.unscaledTime+.2f;phase=2;return;
   }
   if(phase==2){if(Time.unscaledTime<deadline)return;Check(audio.ActiveVoicesOf(BattlefieldSound.Construction)==0,"Cancelling construction immediately stops its sound");File.WriteAllText("battlefield-audio-features.txt",checks+" checks passed\n"+report);Finish(0);}
  }catch(Exception e){Fail(e);}}
  static void Fail(Exception e){Debug.LogException(e);File.WriteAllText("battlefield-audio-features.txt","FAIL\n"+report+e);Finish(1);}
  static void Finish(int code){SessionState.SetBool(Key,false);EditorApplication.update-=Poll;EditorApplication.Exit(code);}
 }
}
