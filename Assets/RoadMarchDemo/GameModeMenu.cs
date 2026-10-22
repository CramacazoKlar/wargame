using UnityEngine;
using UnityEngine.SceneManagement;
namespace CampaignDemo {
 public sealed class GameModeMenu:MonoBehaviour {
  public string pveScene="DoubleValley9x",pvpScene="DoubleValley9x";Font font;string username="",password="",code="",status="",endpoint="game.klarnote.com.cn";bool busy,pvp;
  void Start(){InternetLobby.InBattle=false;if(PvpStartup.Side!=null&&!PvpStartup.Consumed){PvpStartup.Consumed=true;PvpStartup.Starting=true;SceneManager.LoadScene(PvpStartup.Map??"DoubleValley9x");}}
  public void OpenPve(){if(string.IsNullOrEmpty(InternetLobby.Token))return;LocalPvpSession.CloseActive();InternetLobby.InBattle=false;SceneManager.LoadScene(pveScene);}
  public void OpenPvp(){if(!string.IsNullOrEmpty(InternetLobby.Token))pvp=true;}
  async void Authenticate(bool register){busy=true;status="正在连接账号服务…";InternetLobby.Host=endpoint.Trim();try{var r=await InternetLobby.Request(new LobbyRequest{op=register?"register":"login",username=username,password=password});InternetLobby.Token=r.token;password="";status="";}catch(System.Exception e){status=e.Message;}finally{busy=false;}}
  async void Room(bool create){busy=true;status="正在连接房间…";try{var r=await InternetLobby.Request(new LobbyRequest{op=create?"create":"join",code=code.Trim()});LocalPvpSession.CloseActive();InternetLobby.Code=r.code;InternetLobby.Ticket=r.ticket;InternetLobby.Role=r.role;InternetLobby.InBattle=true;SceneManager.LoadScene(pvpScene);}catch(System.Exception e){status=e.Message;}finally{busy=false;}}
  void OnGUI(){if(font==null)font=Font.CreateDynamicFontFromOSFont(new[]{"Microsoft YaHei","SimHei","Arial"},20);var label=new GUIStyle(GUI.skin.label){font=font,fontSize=18,wordWrap=true};var button=new GUIStyle(GUI.skin.button){font=font,fontSize=20};var field=new GUIStyle(GUI.skin.textField){font=font,fontSize=18};float x=Screen.width*.5f-240,y=Screen.height*.5f-200;GUI.Box(new Rect(x,y,480,400),"");GUI.enabled=!busy;
   if(string.IsNullOrEmpty(InternetLobby.Token)){GUI.Label(new Rect(x+25,y+15,430,35),"注册 / 登录",label);GUI.Label(new Rect(x+25,y+65,110,30),"游戏服务器",label);endpoint=GUI.TextField(new Rect(x+140,y+65,310,30),endpoint,field);GUI.Label(new Rect(x+25,y+110,110,30),"用户名",label);username=GUI.TextField(new Rect(x+140,y+110,310,30),username,24,field);GUI.Label(new Rect(x+25,y+155,110,30),"密码",label);password=GUI.PasswordField(new Rect(x+140,y+155,310,30),password,'*',128,field);if(GUI.Button(new Rect(x+25,y+215,200,50),"注册并进入",button))Authenticate(true);if(GUI.Button(new Rect(x+255,y+215,200,50),"登录",button))Authenticate(false);}
   else if(!pvp){GUI.Label(new Rect(x+25,y+25,430,35),"选择游戏模式 · 双河谷扩展地图",label);if(GUI.Button(new Rect(x+25,y+100,200,100),"PVP 模式",button))OpenPvp();if(GUI.Button(new Rect(x+255,y+100,200,100),"PVE 模式",button))OpenPve();if(GUI.Button(new Rect(x+25,y+240,430,40),"退出登录",button)){InternetLobby.Token=null;pvp=false;}}
   else{GUI.Label(new Rect(x+25,y+25,430,40),"双人对战 · 双河谷扩展地图",label);if(GUI.Button(new Rect(x+25,y+85,430,50),"创建房间 · 蓝方",button))Room(true);code=GUI.TextField(new Rect(x+25,y+160,200,40),code,6,field);if(GUI.Button(new Rect(x+255,y+160,200,40),"加入房间 · 红方",button))Room(false);if(GUI.Button(new Rect(x+25,y+240,430,40),"返回模式选择",button))pvp=false;}
   GUI.enabled=true;GUI.Label(new Rect(x+25,y+305,430,80),status,label);
  }
  void OnDestroy(){if(font!=null)Destroy(font);}
 }
 public static class PvpSceneBootstrap {
  [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]static void Install(){SceneManager.sceneLoaded-=Loaded;SceneManager.sceneLoaded+=Loaded;}
  static void Loaded(Scene scene,LoadSceneMode mode){if((InternetLobby.InBattle||scene.name=="PvpBattle"||(PvpStartup.Starting&&scene.name==(PvpStartup.Map??"DoubleValley9x")))&&LocalPvpSession.Active==null)new GameObject("双人联网服务").AddComponent<LocalPvpSession>();}
 }
 public static class PvpStartup {
  public static bool Consumed,Starting;
  public static string Value(string key){var args=System.Environment.GetCommandLineArgs();for(int i=0;i<args.Length;i++)if(args[i]==key)return i+1<args.Length&&!args[i+1].StartsWith("-")?args[i+1]:"";return null;}
  public static string Side {get{return Value("-pvp-host")!=null?"host":Value("-pvp-join")!=null?"client":null;}}
  public static string Map {get{return Value("-pvp-map");}}
 }
}
