using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
namespace CampaignDemo.Editor
{
    [InitializeOnLoad]
    public static class FortBreachRuntimeVerification
    {
        static double start;static float phase;static int stage;static BattleUnit worker;static Fortification wall;static Vector3 a,b;static readonly List<string> results=new List<string>();
        static FortBreachRuntimeVerification(){if(SessionState.GetBool("BreachRuntime",false)){start=EditorApplication.timeSinceStartup;EditorApplication.update+=Poll;}}
        public static void Run(){FortBreachVerification.Configure();FortBreachVerification.Validate();CampaignRevisionVerification.Validate();SessionState.SetBool("BreachRuntime",true);start=EditorApplication.timeSinceStartup;EditorApplication.update+=Poll;EditorApplication.isPlaying=true;}
        static void Check(bool value,string name){if(!value)throw new Exception(name);results.Add("PASS "+name);}
        static void Draw(CampaignHUD hud){hud.ToggleTool(CampaignHUD.Tool.FortCut);hud.AddFortNode(a);hud.AddFortNode(b);}
        static void Poll()
        {
            try
            {
                if(EditorApplication.timeSinceStartup-start>120)throw new Exception("Breach runtime timeout");if(!EditorApplication.isPlaying||Time.time<1)return;var c=UnityEngine.Object.FindObjectOfType<CampaignRuntime>();if(c==null||!c.world.Initialized)return;var hud=UnityEngine.Object.FindObjectOfType<CampaignHUD>();
                if(stage==0)
                {
                    c.world.setup.modes.EnterInspectionMode();foreach(var u in c.world.units)if(u.ownerId=="A"&&u.combatDefinition.role==UnitRole.Engineer)worker=u;bool found=false;
                    for(int i=0;i<16&&!found;i++){float angle=-Mathf.PI/2+i*Mathf.PI/8;var direction=new Vector3(Mathf.Cos(angle),Mathf.Sin(angle));var normal=new Vector3(-direction.y,direction.x);var center=worker.transform.position+direction*.6f;var points=new[]{center-normal*.9f,center+normal*.9f};if(c.lineWorks.CanBuild(worker,points,out _)){wall=c.lineWorks.CreateLine(points,worker.transform.position,"A",0,1.8f*c.combined.engineering.metersPerWorldUnit);a=center-normal*.4f;b=center+normal*.4f;found=true;}}
                    Check(found,"visible legal wall near engineer");hud.SelectUnit(worker);Draw(hud);Check(hud.ActiveTool==CampaignHUD.Tool.FortCut&&hud.FortPreviewPoints.Length==2,"same point drawing interface for demolition");Check(hud.ConfirmFortStroke().accepted,"section demolition command accepted");stage=1;phase=Time.time;return;
                }
                if(stage==1)
                {
                    if(c.lineWorks.Approaching(worker))return;Check(worker.Working&&c.lineWorks.IsDismantling(worker),"actual engineer reaches site and starts demolition");Check(Vector2.Distance(worker.transform.position,(a+b)/2)<=c.combined.engineering.constructionArrivalDistance+.001f,"actual demolition midpoint reached");stage=2;phase=Time.time;return;
                }
                if(Time.time-phase<.2f)return;
                if(stage==2)
                {
                    Button cancel=null;foreach(var button in hud.GetComponentsInChildren<Button>())if(button.name=="拆除防线")cancel=button;Check(cancel!=null&&cancel.GetComponentInChildren<Text>().text=="取消","original demolition button changes to cancel");Check(!worker.CanMove&&!worker.CanFire,"demolition excludes simultaneous movement and fire");cancel.onClick.Invoke();Check(!worker.Working&&!wall.Destroyed,"cancel creates no premature gap");Draw(hud);Check(hud.ConfirmFortStroke().accepted,"restart section task");
                    var position=worker.transform.position;var move=hud.Send(new BattleCommand{kind=BattleCommandKind.FreeDestination,unitId=worker.unitId,destination=position+new Vector3(.05f,0)});Check(!c.lineWorks.Working(worker)&&!wall.Destroyed,"new manual movement order interrupts work without partial gap");worker.Motor.Stop();worker.transform.position=position;Draw(hud);Check(hud.ConfirmFortStroke().accepted,"task restarts after movement interruption");stage=3;phase=Time.time;return;
                }
                if(stage==3)
                {
                    Check(worker.Working,"restart already at site");worker.Damage(.01f);c.lineWorks.Tick(.1f);Check(!c.lineWorks.Working(worker)&&!wall.Destroyed,"incoming damage interrupts task without partial gap");stage=5;phase=Time.time;return;
                }
                if(stage==5)
                {
                    if(worker.UnderFire)return;Draw(hud);Check(hud.ConfirmFortStroke().accepted,"resume after interruption");c.lineWorks.Tick(c.lineWorks.Remaining(worker)+.1f);Check(wall.Destroyed,"completion replaces original wall");var live=c.combined.fortifications.FindAll(f=>f!=null&&!f.Destroyed);Check(live.Count==2,"actual completed demolition creates two remaining sides");foreach(var f in live)Check(f.DistanceTo((a+b)/2)>.39f,"actual visible gap geometry");hud.SelectBuilding(live[0]);stage=4;phase=Time.time;return;
                }
                if(stage==4)
                {
                    bool direct=false;foreach(var button in hud.GetComponentsInChildren<Button>())if(button.name=="拆除")direct=true;Check(!direct,"old remote one button removal absent from fort panel");var camera=c.world.setup.mapCamera;camera.orthographicSize=2.5f;camera.transform.position=new Vector3((a.x+b.x)/2,(a.y+b.y)/2,camera.transform.position.z);Capture(camera);File.WriteAllText("fort-breach-runtime-verification.txt",results.Count+" runtime checks passed\n"+string.Join("\n",results));SessionState.SetBool("BreachRuntime",false);EditorApplication.update-=Poll;EditorApplication.Exit(0);
                }
            }
            catch(Exception e){File.WriteAllText("fort-breach-runtime-verification.txt",e+"\n"+string.Join("\n",results));Debug.LogException(e);SessionState.SetBool("BreachRuntime",false);EditorApplication.update-=Poll;EditorApplication.Exit(1);}
        }
        static void Capture(Camera camera){var rt=new RenderTexture(1600,900,24);camera.targetTexture=rt;foreach(var canvas in UnityEngine.Object.FindObjectsOfType<Canvas>()){canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.sortingOrder=200;canvas.worldCamera=camera;canvas.planeDistance=1;}Canvas.ForceUpdateCanvases();camera.Render();RenderTexture.active=rt;var image=new Texture2D(1600,900,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1600,900),0,0);image.Apply();File.WriteAllBytes("fort-breach-preview.png",image.EncodeToPNG());camera.targetTexture=null;RenderTexture.active=null;UnityEngine.Object.Destroy(image);UnityEngine.Object.Destroy(rt);}
    }
}

