using System;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Build.Reporting;
namespace CampaignDemo.Editor {
 public static class InternetVerification {
  static bool running;static double start;
  [InitializeOnLoadMethod]static void Resume(){if(!SessionState.GetBool("InternetVerify",false))return;start=EditorApplication.timeSinceStartup;EditorApplication.update+=Update;}
  public static void Run(){EditorSceneManager.OpenScene("Assets/Scenes/Battle01.unity");SessionState.SetBool("InternetVerify",true);start=EditorApplication.timeSinceStartup;EditorApplication.update+=Update;EditorApplication.EnterPlaymode();}
  static void Update(){if(EditorApplication.timeSinceStartup-start>90){Finish("FAIL timeout",1);return;}if(running||!EditorApplication.isPlaying||Time.time<.8f)return;running=true;Verify();}
  static void Check(bool b,string m){if(!b)throw new Exception(m);}
  static async void Verify(){LocalPvpWire guest=null;try{
   var world=UnityEngine.Object.FindObjectOfType<BattleWorld>();Check(world!=null&&world.Initialized,"battle initialization");InternetLobby.Host="127.0.0.1";InternetLobby.Port=17988;
   string name="unity"+DateTime.UtcNow.Ticks;var a=await InternetLobby.Request(new LobbyRequest{op="register",username=name,password="Unity-test-123"});var b=await InternetLobby.Request(new LobbyRequest{op="register",username=name+"b",password="Unity-test-123"});InternetLobby.Token=a.token;var room=await InternetLobby.Request(new LobbyRequest{op="create"});InternetLobby.Code=room.code;InternetLobby.Ticket=room.ticket;InternetLobby.Role="host";InternetLobby.InBattle=true;
   var session=new GameObject("Internet verification host").AddComponent<LocalPvpSession>();await Task.Delay(800);Check(session.IsHost&&session.LocalPlayer=="A"&&!session.Ready,"blue host waits");Check(!world.GetComponent<BattleAIController>().enabled&&!world.GetComponent<AIBasicCommander>().enabled,"AI disabled");
   InternetLobby.Token=b.token;var join=await InternetLobby.Request(new LobbyRequest{op="join",code=room.code});var pair=await InternetLobby.Connect();await InternetLobby.Write(pair.Item2,JsonUtility.ToJson(new LobbyRequest{op="bind",token=b.token,ticket=join.ticket}));var reply=JsonUtility.FromJson<LobbyReply>(await InternetLobby.Read(pair.Item2));Check(reply.ok,"guest bind");guest=new LocalPvpWire(pair.Item1,pair.Item2);await Task.Delay(200);guest.Send(JsonUtility.ToJson(new PvpMessage{type="hello",map=session.MapId(),code=room.code}));InternetLobby.Token=a.token;
   await Task.Delay(600);Check(session.Ready&&LocalPvpSession.Simulate,"host simulates");Check(!session.KeyboardCaptured,"battle keyboard available");bool welcome=false,snapshot=false;while(guest.Receive(out var json)){var m=JsonUtility.FromJson<PvpMessage>(json);if(m.type=="welcome")welcome=m.player=="B";if(m.type=="snapshot")snapshot=m.snapshot!=null;}Check(welcome&&snapshot,"red assignment and real battle snapshot");guest.Dispose();await Task.Delay(400);Check(!session.Ready&&!LocalPvpSession.Simulate,"disconnect ends simulation");session.Stop();UnityEngine.Object.Destroy(session.gameObject);await Task.Delay(200);var fresh=await InternetLobby.Request(new LobbyRequest{op="create"});Check(fresh.code!=room.code,"fresh room");
   var hostPair=await InternetLobby.Connect();await InternetLobby.Write(hostPair.Item2,JsonUtility.ToJson(new LobbyRequest{op="bind",token=a.token,ticket=fresh.ticket}));Check(JsonUtility.FromJson<LobbyReply>(await InternetLobby.Read(hostPair.Item2)).ok,"new host bind");guest=new LocalPvpWire(hostPair.Item1,hostPair.Item2);
   InternetLobby.Token=b.token;var redRoom=await InternetLobby.Request(new LobbyRequest{op="join",code=fresh.code});InternetLobby.Code=fresh.code;InternetLobby.Ticket=redRoom.ticket;InternetLobby.Role="guest";InternetLobby.InBattle=true;
   var redSession=new GameObject("Internet verification red client").AddComponent<LocalPvpSession>();await Task.Delay(800);bool hello=false;while(guest.Receive(out var message)){var m=JsonUtility.FromJson<PvpMessage>(message);if(m.type=="hello")hello=m.map==redSession.MapId();}Check(hello,"red client sends map handshake");
   guest.Send(JsonUtility.ToJson(new PvpMessage{type="welcome",player="B",token="verification"}));guest.Send(JsonUtility.ToJson(new PvpMessage{type="snapshot",snapshot=LocalPvpReplication.Capture(world,"B",99)}));await Task.Delay(600);
   Check(redSession.Ready&&!redSession.IsHost&&redSession.LocalPlayer=="B"&&world.currentPlayer=="B","real red client owns red");Check(redSession.ReceivedTick==99&&!LocalPvpSession.Simulate&&!redSession.KeyboardCaptured,"red applies snapshot with controls available");redSession.Stop();UnityEngine.Object.Destroy(redSession.gameObject);Finish("PASS: real Battle01, account service, blue host, red guest, AI disabled, relay welcome, battle snapshot, disconnect pause, fresh room, real red client handshake, snapshot application, keyboard controls.",0);
  }catch(Exception e){Finish("FAIL "+e,1);}finally{guest?.Dispose();}}
  static void Finish(string result,int code){SessionState.SetBool("InternetVerify",false);File.WriteAllText(Environment.GetEnvironmentVariable("INTERNET_VERIFY_RESULT"),result);EditorApplication.Exit(code);}
  public static void Build(){var output=Environment.GetEnvironmentVariable("INTERNET_BUILD_PATH");if(string.IsNullOrEmpty(output))throw new Exception("INTERNET_BUILD_PATH required");Directory.CreateDirectory(Path.GetDirectoryName(output));var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{"Assets/Scenes/ModeSelection.unity","Assets/Scenes/DoubleValley9x.unity"},locationPathName=output,target=BuildTarget.StandaloneWindows64,options=BuildOptions.None});if(report.summary.result!=BuildResult.Succeeded)throw new Exception("Build failed: "+report.summary.result);}
 }
}
