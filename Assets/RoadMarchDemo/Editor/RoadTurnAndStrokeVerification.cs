using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
namespace CampaignDemo.Editor {
 [InitializeOnLoad]
 public static class RoadTurnAndStrokeVerification {
  const string Flag="RoadTurnStrokeCheck";
  const string Result="C:/Users/ZWQ/Documents/Codex/2026-10-04/unity/outputs/路口与线条修改验证.txt";
  static double started;static int count;
  static RoadTurnAndStrokeVerification(){if(SessionState.GetBool(Flag,false)){started=EditorApplication.timeSinceStartup;EditorApplication.update+=Poll;}}
  static void Check(bool value,string label){if(!value)throw new Exception(label);count++;Debug.Log("PASS "+label);}
  static RoadPoint P(double x,double y){return new RoadPoint(x,y);}
  public static void Run(){try{
   var g=new RoadNetwork(new[]{P(-2,0),P(0,0),P(2,0),P(0,2),P(0,-2)},new[]{new RoadEdge(0,1),new RoadEdge(1,2),new RoadEdge(1,3),new RoadEdge(1,4)});
   var d=new List<RoadPoint>{P(-1,0),P(0,0),P(.04,0),P(.08,0)};g.AppendTurn(d,new[]{P(.08,0),P(0,0),P(0,1)},1,2,.1);Check(d.Count==3&&d[2].Y==1,"crossroad short excursion removed");
   d=new List<RoadPoint>{P(-1,0),P(0,0),P(.3,0)};g.AppendTurn(d,new[]{P(.3,0),P(0,0),P(0,1)},1,2,.1);Check(d.Count==5,"long intentional excursion retained");
   d=new List<RoadPoint>{P(-1,0),P(0,0),P(.08,0)};g.AppendTurn(d,new[]{P(.08,0),P(.01,0)},1,1,.1);Check(d.Count==4,"deliberate same road reversal retained");
   RoadHit next;List<RoadPoint> connector;Check(g.Trace(new RoadHit(1,P(.08,0),0),P(0,.2),.1,.5,out next,out connector)&&next.Edge==2,"trace chooses connected turn");
   d=new List<RoadPoint>{P(-1,0),P(0,0),P(.08,0)};g.AppendTurn(d,connector,1,next.Edge,.1);Check(d.Count==3&&d[2].Y==.2,"real connector has no excursion");
   var raw=new Color[9*9];for(int y=1;y<8;y++){raw[y*9+4]=Color.black;raw[y*9+5]=Color.black;}var thick=UnitSymbolStrokeUtility.Thicken(raw,9,9);Check(thick[4*9+3].a==1&&thick[4*9+6].a==1&&thick[4*9+2].a==0,"two pixel stroke becomes four pixels");
   var pale=new Color[25];for(int i=0;i<pale.Length;i++)pale[i]=new Color(.7f,.85f,.95f,1);var unchanged=UnitSymbolStrokeUtility.Thicken(pale,5,5);Check(unchanged[12]==pale[12],"pale fill unchanged");
   EditorSceneManager.OpenScene("Assets/Scenes/Battle01.unity");var world=UnityEngine.Object.FindObjectOfType<BattleWorld>();Check(world!=null,"existing scene world found");
   Check(world.setup.settings.junctionTurnTolerancePixels==16,"serialized junction tolerance");Check(Mathf.Approximately(world.setup.settings.arrowStrokeWidth,.05f)&&Mathf.Approximately(world.setup.settings.minimumArrowStrokePixels,3),"arrow stroke doubles world and pixel widths");
   SessionState.SetInt("RoadTurnStrokeCount",count);SessionState.SetBool(Flag,true);started=EditorApplication.timeSinceStartup;EditorApplication.update+=Poll;EditorApplication.isPlaying=true;
  }catch(Exception e){Finish(e);}}
  static void Poll(){try{
   if(EditorApplication.timeSinceStartup-started>120)throw new Exception("Runtime initialization timed out");if(!EditorApplication.isPlaying||Time.time<1)return;
   var w=UnityEngine.Object.FindObjectOfType<BattleWorld>();if(w==null||!w.Initialized)return;count=SessionState.GetInt("RoadTurnStrokeCount",0);Check(w.units.Length>0,"scene units initialized");
   foreach(var u in w.units)Check(u!=null&&u.Motor!=null&&u.combatDefinition!=null,"unit initialized "+u.unitId);
   var view=RoadRouteView.Create(w.transform,"Stroke check",.06f,3,Color.blue);view.ConfigureArrow(w.setup.settings,w.setup.mapCamera);view.ShowArrow(new[]{P(0,0),P(1,0),P(1,1)});var line=view.GetComponent<LineRenderer>();Check(line.positionCount>0&&line.startWidth>=.05f,"runtime bent arrow uses thick stroke");UnityEngine.Object.Destroy(view.gameObject);Finish(null);
  }catch(Exception e){Finish(e);}}
  static void Finish(Exception e){SessionState.SetBool(Flag,false);EditorApplication.update-=Poll;File.WriteAllText(Result,e==null?"PASS "+count+" checks. Unity compile and Battle01 initialization succeeded; route regression and doubled stroke verified. Scene was not saved.":e.ToString());if(e!=null)Debug.LogException(e);EditorApplication.Exit(e==null?0:1);}
 }
}
