using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace CampaignDemo {
 [DefaultExecutionOrder(95)] public sealed class LocalPvpSession:MonoBehaviour {
  public static LocalPvpSession Active {get;private set;}
  public bool IsHost {get;private set;}public bool Ready {get;private set;}public string LocalPlayer {get;private set;}="A";
  public bool KeyboardCaptured {get{return InternetLobby.InBattle?!Ready:showRoom||!Ready;}}
  public string Status {get;private set;}="创建房间，或输入地址加入房间";
  public string address="127.0.0.1",roomCode="123456";public int port=7777;
  public string LastCommandResult {get;private set;}="";public long ReceivedTick {get;private set;}=-1;
  public BattleWorld World {get;private set;}
  TcpListener listener;LocalPvpWire peer;bool started,connecting,configured,remoteReserved,rejected;string resumeToken,initialMap;float nextSnapshot,helloDeadline,lastHeard,nextHeartbeat,rateWindow;long sequence,lastRemoteSequence,snapshotSequence;int generation,rateCount;
  readonly List<PvpEffect> effects=new List<PvpEffect>();readonly Dictionary<string,float> effectTimes=new Dictionary<string,float>();
  readonly ConcurrentQueue<Action> dispatch=new ConcurrentQueue<Action>();Font font;bool showRoom=true;
  string automaticSide;bool previousBackground,internetSession;
  public static bool Simulate {get{return Active==null||Active.IsHost&&Active.Ready;}}
  public static void CloseActive(){if(Active!=null){Active.Stop();Destroy(Active.gameObject);Active=null;}}
  void Awake(){if(Active!=null&&Active!=this){Destroy(gameObject);return;}Active=this;previousBackground=Application.runInBackground;Application.runInBackground=true;}
  void Start(){if(InternetLobby.InBattle){internetSession=true;IsHost=InternetLobby.Role=="host";LocalPlayer=IsHost?"A":"B";roomCode=InternetLobby.Code;automaticSide="internet";return;}automaticSide=PvpStartup.Starting?PvpStartup.Side:null;PvpStartup.Starting=false;if(automaticSide==null)return;IsHost=automaticSide=="host";LocalPlayer=IsHost?"A":"B";var hostAddress=PvpStartup.Value("-pvp-join");if(!string.IsNullOrEmpty(hostAddress))address=hostAddress;var password=PvpStartup.Value("-pvp-code");if(password!=null)roomCode=password;if(int.TryParse(PvpStartup.Value("-pvp-port"),out int selectedPort)&&selectedPort>0&&selectedPort<=65535)port=selectedPort;}
  void Configure(){if(World==null)World=FindObjectOfType<BattleWorld>();if(World==null||!World.Initialized||configured)return;configured=true;if(initialMap==null)initialMap=LocalPvpReplication.Fingerprint(World);World.CombatEffectOccurred-=Combat;World.CombatEffectOccurred+=Combat;
   if(World.campaign!=null&&World.GetComponent<CityManagement>()==null)World.gameObject.AddComponent<CityManagement>();
   var ai=World.GetComponent<BattleAIController>();if(ai!=null){ai.aiEnabled=false;ai.autoToggleOnPlayerSwitch=false;ai.enabled=false;}
   var basic=World.GetComponent<AIBasicCommander>();if(basic!=null)basic.enabled=false;
   World.SetPlayer(LocalPlayer);if(!IsHost)foreach(var unit in World.units)if(unit!=null)unit.NetworkVisible=unit.ownerId==LocalPlayer;
  }
  public string MapId(){if(World==null)return "empty-scene-v1";if(!World.Initialized)return null;return initialMap??LocalPvpReplication.Fingerprint(World);}
  public void Host(){if(connecting||listener!=null)return;IsHost=true;LocalPlayer="A";configured=false;Configure();if(World!=null&&!World.Initialized){Status="请先完成战场配置";return;}try{listener=new TcpListener(IPAddress.Any,port);listener.Start(2);started=true;Ready=false;resumeToken=null;remoteReserved=false;lastRemoteSequence=0;Status="房间已创建，等待红方加入";}catch(Exception e){Status="开房失败："+e.Message;listener=null;}}
  public void Join(){if(connecting||Ready)return;IsHost=false;LocalPlayer="B";configured=false;rejected=false;Configure();if(World!=null&&!World.Initialized){Status="请先完成战场配置";return;}if(peer!=null)peer.Dispose();peer=null;connecting=true;started=true;Status="正在连接…";int request=++generation;var socket=new TcpClient();string host=address;int endpointPort=port;
   Task.Run(async()=>{try{var connect=socket.ConnectAsync(host,endpointPort);if(await Task.WhenAny(connect,Task.Delay(5000))!=connect)throw new TimeoutException("连接超时");await connect;dispatch.Enqueue(()=>{if(request!=generation){socket.Close();return;}connecting=false;peer=new LocalPvpWire(socket);helloDeadline=Time.unscaledTime+8;lastHeard=Time.unscaledTime;Send(new PvpMessage{type="hello",map=MapId(),code=roomCode,token=resumeToken});Status="等待房间验证";});}catch(Exception e){socket.Close();dispatch.Enqueue(()=>{if(request==generation){connecting=false;Status="连接失败："+e.Message;}});}});
  }
  public CommandResult Forward(BattleCommand command){if(command==null)return CommandResult.No("命令为空");if(!Ready||peer==null||peer.Closed)return CommandResult.No("尚未连接到对手");if(command.fortId!=0&&World!=null){var fort=World.GetComponent<CombinedArmsWorld>()?.fortifications.Find(f=>f!=null&&f.GetInstanceID()==command.fortId);if(fort!=null)command.fortNetworkId=LocalPvpReplication.FortId(fort);}command.playerId=LocalPlayer;command.automatedProduction=false;command.preserveTransportAutomation=false;Send(new PvpMessage{type="command",sequence=++sequence,command=command});return new CommandResult{accepted=true,pending=true,reason="命令已发送，等待服务器确认"};}
  void Send(PvpMessage message,bool snapshot=false){if(peer!=null)peer.Send(JsonUtility.ToJson(message),snapshot);}
  void Update(){Configure();while(dispatch.TryDequeue(out var action))action();if(automaticSide!=null&&(World==null||World.Initialized)){var side=automaticSide;automaticSide=null;if(side=="internet")ConnectInternet();else if(side=="host")Host();else Join();}
   if(IsHost&&listener!=null&&listener.Pending()){var socket=listener.AcceptTcpClient();if(peer!=null&&!peer.Closed)socket.Close();else{peer=new LocalPvpWire(socket);Ready=false;helloDeadline=Time.unscaledTime+8;lastHeard=Time.unscaledTime;}}
   if(peer!=null){int count=0;while(count++<64&&peer.Receive(out var json)){try{if(IsHost&&json.Length>131072)throw new Exception("命令过大");Handle(JsonUtility.FromJson<PvpMessage>(json));}catch(Exception e){Status="协议错误："+e.Message;peer.Dispose();break;}}
    if(!Ready&&Time.unscaledTime>helloDeadline&&!peer.Closed)peer.Dispose();
    if(Ready&&Time.unscaledTime>=nextHeartbeat){nextHeartbeat=Time.unscaledTime+2;Send(new PvpMessage{type="ping"});}if(Ready&&Time.unscaledTime-lastHeard>8)peer.Dispose();
    if(peer.Closed){Ready=false;if(InternetLobby.InBattle){Status="本局已结束，请返回模式选择重新开房";rejected=true;}if(!rejected)Status=IsHost?"对手已断线，战场暂停；等待原玩家重连":"连接已断开，战场暂停；可重新加入";peer.Dispose();peer=null;}
   }
  }
  async void ConnectInternet(){
   connecting=true;started=true;Status="正在进入房间 "+InternetLobby.Code;int request=++generation;System.Net.Sockets.TcpClient socket=null;
   try{var pair=await InternetLobby.Connect();socket=pair.Item1;await InternetLobby.Write(pair.Item2,JsonUtility.ToJson(new LobbyRequest{op="bind",token=InternetLobby.Token,ticket=InternetLobby.Ticket}));var read=InternetLobby.Read(pair.Item2);if(await Task.WhenAny(read,Task.Delay(8000))!=read)throw new TimeoutException("房间连接超时");var reply=JsonUtility.FromJson<LobbyReply>(await read);if(reply==null||!reply.ok)throw new Exception(reply==null?"无效回复":reply.error);if(request!=generation){socket.Close();return;}peer=new LocalPvpWire(socket,pair.Item2);connecting=false;helloDeadline=Time.unscaledTime+300;lastHeard=Time.unscaledTime;Status="房间号："+roomCode+" · 等待另一位玩家";
   }catch(Exception e){if(socket!=null)socket.Close();if(request==generation){connecting=false;Status="连接失败："+e.Message;var ignored=InternetLobby.Leave();}}
  }
  void Combat(CombatEffectEvent effect){if(!IsHost||!Ready||World==null)return;string key=effect.Stage+":"+effect.SourceId+":"+effect.TargetId;if(effect.Stage==CombatEffectStage.DirectAttack){if(effectTimes.TryGetValue(key,out var next)&&Time.unscaledTime<next)return;effectTimes[key]=Time.unscaledTime+.25f;}if(effects.Count<256){var filtered=LocalPvpReplication.FilterEffect(World,"B",effect);if(filtered!=null)effects.Add(filtered);}}
  void LateUpdate(){if(IsHost&&Ready&&World!=null&&World.Initialized&&Time.unscaledTime>=nextSnapshot){nextSnapshot=Time.unscaledTime+.1f;var snapshot=LocalPvpReplication.Capture(World,"B",++snapshotSequence);snapshot.effects.AddRange(effects);effects.Clear();Send(new PvpMessage{type="snapshot",snapshot=snapshot},snapshot.effects.Count==0);}}
  void Handle(PvpMessage message){if(message==null||(message.type!="relay-ready"&&message.version!=NetworkTacticalOrders.Version))throw new Exception("联网版本不一致");lastHeard=Time.unscaledTime;if(message.type=="relay-ready"){helloDeadline=Time.unscaledTime+8;if(!IsHost)Send(new PvpMessage{type="hello",map=MapId(),code=roomCode});return;}if(message.type=="ping"){Send(new PvpMessage{type="pong"});return;}if(message.type=="pong")return;
   if(IsHost){
    if(message.type=="hello"&&!Ready){if(message.map!=MapId()||message.code!=roomCode||(remoteReserved&&message.token!=resumeToken)){Send(new PvpMessage{type="reject",reason="地图、房间密码不一致，或不是原玩家重连"});Status="加入请求被拒绝";return;}
     if(!remoteReserved){resumeToken=Guid.NewGuid().ToString("N");remoteReserved=true;lastRemoteSequence=0;}Ready=true;Status=World==null?"双方已连接；PVP场景为空，等待你布置战场":"红蓝双方已连接，战斗进行中";Send(new PvpMessage{type="welcome",player="B",token=resumeToken});return;
    }
    if(message.type=="command"&&Ready){if(Time.unscaledTime-rateWindow>=1){rateWindow=Time.unscaledTime;rateCount=0;}CommandResult result;if(++rateCount>60)result=CommandResult.No("命令过于频繁");else if(message.sequence<=lastRemoteSequence)result=CommandResult.No("重复命令");else{lastRemoteSequence=message.sequence;result=World==null?CommandResult.No("PVP场景尚未配置战场"):World.GetComponent<BattleCommandService>().SubmitRemote(message.command,"B");}Send(new PvpMessage{type="result",sequence=message.sequence,accepted=result.accepted,reason=result.reason});}
   }else{
    if(message.type=="welcome"){if(message.player!="B")throw new Exception("阵营分配无效");LocalPlayer=message.player;configured=false;Configure();resumeToken=message.token;ReceivedTick=-1;Ready=true;Status=World==null?"已加入；PVP场景为空，等待布置战场":"已加入红方，战斗进行中";return;}
    if(message.type=="reject"){Status=message.reason;rejected=true;connecting=false;if(peer!=null)peer.Dispose();return;}
    if(message.type=="snapshot"&&Ready&&World!=null&&message.snapshot!=null&&message.snapshot.tick>ReceivedTick){ReceivedTick=message.snapshot.tick;LocalPvpReplication.Apply(World,LocalPlayer,message.snapshot);}
    if(message.type=="result"){LastCommandResult=message.accepted?"服务器已确认命令":"命令被拒绝："+message.reason;}
   }
  }
  public void Stop(){if(internetSession&&InternetLobby.InBattle&&InternetLobby.Code==roomCode){internetSession=false;var ignored=InternetLobby.Leave();}internetSession=false;generation++;connecting=false;Ready=false;started=false;if(peer!=null)peer.Dispose();peer=null;if(listener!=null)listener.Stop();listener=null;Status="已退出房间";}
  void OnGUI(){if(InternetLobby.InBattle){DrawInternetRoom();return;}if(font==null)font=Font.CreateDynamicFontFromOSFont(new[]{"Microsoft YaHei","SimHei","Arial"},14);var label=new GUIStyle(GUI.skin.label){font=font,fontSize=13,wordWrap=true};var button=new GUIStyle(GUI.skin.button){font=font,fontSize=13};
   if(Ready&&World!=null){GUI.Label(new Rect(Screen.width*.5f-230,4,460,26),"PVP · "+(IsHost?"蓝方（开房端）":"红方")+" · "+LastCommandResult,label);if(GUI.Button(new Rect(Screen.width*.5f+235,4,72,26),showRoom?"收起房间":"房间",button))showRoom=!showRoom;}
   if(!showRoom&&Ready)return;float x=Screen.width*.5f-205,y=Ready&&World!=null?40:Screen.height*.5f-150;GUI.Box(new Rect(x,y,410,300),"");GUI.Label(new Rect(x+20,y+12,370,30),"PVP 本机 / 局域网房间",label);GUI.Label(new Rect(x+20,y+47,65,25),"服务器",label);address=GUI.TextField(new Rect(x+88,y+45,195,25),address);string portText=GUI.TextField(new Rect(x+292,y+45,95,25),port.ToString());if(int.TryParse(portText,out int parsed)&&parsed>0&&parsed<=65535)port=parsed;GUI.Label(new Rect(x+20,y+81,65,25),"房间密码",label);roomCode=GUI.TextField(new Rect(x+88,y+79,295,25),roomCode);
   GUI.enabled=!started&&!connecting;if(GUI.Button(new Rect(x+20,y+117,175,32),"创建房间（蓝方）",button))Host();GUI.enabled=!IsHost&&!Ready&&!connecting;if(GUI.Button(new Rect(x+212,y+117,175,32),"加入 / 重连（红方）",button))Join();GUI.enabled=true;
   GUI.Label(new Rect(x+20,y+160,370,52),Status,label);if(World==null)GUI.Label(new Rect(x+20,y+210,370,35),"场景保持空白，地图、部队和城市由你自行布置。",label);
   if(GUI.Button(new Rect(x+20,y+254,175,30),"退出房间",button))Stop();if(GUI.Button(new Rect(x+212,y+254,175,30),"返回模式选择",button)){Stop();SceneManager.LoadScene("ModeSelection");}
  }
  void DrawInternetRoom(){if(font==null)font=Font.CreateDynamicFontFromOSFont(new[]{"Microsoft YaHei","SimHei","Arial"},16);var style=new GUIStyle(GUI.skin.label){font=font,fontSize=16,wordWrap=true};GUI.Box(new Rect(10,8,520,100),"");GUI.Label(new Rect(20,15,490,48),"房间 "+roomCode+" · "+(IsHost?"蓝方（房主）":"红方")+" · "+Status,style);if(GUI.Button(new Rect(20,65,220,30),"结束本局 / 返回模式选择")){Stop();SceneManager.LoadScene("ModeSelection");}}
  void OnDestroy(){Stop();if(World!=null)World.CombatEffectOccurred-=Combat;if(Active==this){Active=null;Application.runInBackground=previousBackground;}if(font!=null)Destroy(font);}
 }
}
