using UnityEngine;
namespace CampaignDemo {
 public sealed class BattleReplayControls:MonoBehaviour {
  public BattleWorld world;GUIStyle style,labelStyle;string message="";
  void OnGUI(){if(world==null||!world.Initialized)return;if(style==null){style=new GUIStyle(GUI.skin.button);style.font=Font.CreateDynamicFontFromOSFont(new[]{"Microsoft YaHei","SimHei","Arial"},14);style.fontSize=14;labelStyle=new GUIStyle(GUI.skin.box){font=style.font,fontSize=12,wordWrap=true,alignment=TextAnchor.UpperLeft};labelStyle.normal.textColor=Color.white;}
   if(!world.SimulationEnded){if(GUI.Button(new Rect(18,110,160,36),"结束（调试）",style)){world.EndBattle();message="已结束：按实际移动轨迹显示，已去除各单位自身绕圈；不同单位交叉保留，箭头只在最终位置显示。";}}
   else {if(GUI.Button(new Rect(18,110,160,36),"导出静态复盘图",style)){try{BattleReplayExporter.Export(world.Replay);message="已导出整图 PNG、矢量 SVG 和完整轨迹数据。";}catch(System.Exception e){message="导出失败："+e.Message;Debug.LogException(e);}}}
   if(world.SimulationEnded&&!string.IsNullOrEmpty(world.Replay.LastExport)&&GUI.Button(new Rect(18,224,160,28),"打开导出文件夹",style))Application.OpenURL(new System.Uri(System.IO.Path.GetDirectoryName(world.Replay.LastExport)).AbsoluteUri);
   if(world.SimulationEnded){var display=world.Replay.GetComponent<BattleReplayPresentation>();if(display!=null){bool tactical=GUI.Toggle(new Rect(18,260,240,28),display.ShowTactical,"战术箭头（移动轨迹）",style);display.SetTacticalVisibility(tactical);}}
   if(!string.IsNullOrEmpty(message)){GUI.Label(new Rect(18,154,220,64),message,labelStyle);}
  }
 }
}
