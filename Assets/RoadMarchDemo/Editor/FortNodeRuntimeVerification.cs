using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
namespace CampaignDemo.Editor
{
    [InitializeOnLoad]
    public static class FortNodeRuntimeVerification
    {
        static double start;static float phase;static int stage;static BattleUnit engineer;static Vector3 a,b;static Fortification fort;static readonly List<string> results=new List<string>();
        static FortNodeRuntimeVerification(){if(SessionState.GetBool("FortNodeRuntime",false)){start=EditorApplication.timeSinceStartup;EditorApplication.update+=Poll;}}
        public static void Run(){FortNodeInstaller.Configure();FortNodeVerification.Validate();CampaignRevisionVerification.Validate();SessionState.SetBool("FortNodeRuntime",true);start=EditorApplication.timeSinceStartup;EditorApplication.update+=Poll;EditorApplication.isPlaying=true;}
        static void Check(bool value,string name){if(!value)throw new Exception(name);results.Add("PASS "+name);}
        static void Poll()
        {
            try
            {
                if(EditorApplication.timeSinceStartup-start>120)throw new Exception("Node UI timeout");if(!EditorApplication.isPlaying||Time.time<1)return;var c=UnityEngine.Object.FindObjectOfType<CampaignRuntime>();if(c==null||!c.world.Initialized)return;var hud=UnityEngine.Object.FindObjectOfType<CampaignHUD>();
                if(stage==0)
                {
                    c.world.setup.modes.EnterInspectionMode();foreach(var u in c.world.units)if(u.ownerId=="A"&&u.combatDefinition.role==UnitRole.Engineer)engineer=u;Check(engineer!=null,"engineer available");bool found=false;
                    for(int i=0;i<16&&!found;i++){float angle=-Mathf.PI/2+i*Mathf.PI/8;var d=new Vector3(Mathf.Cos(angle),Mathf.Sin(angle));a=engineer.transform.position+d*.6f;b=a+new Vector3(-d.y,d.x)*.6f;if(c.lineWorks.CanBuild(engineer,new[]{a,b},out _))found=true;}Check(found,"valid endpoint midpoint reachable");
                    hud.SelectUnit(engineer);hud.ToggleTool(CampaignHUD.Tool.FortBuild);hud.AddFortNode(a);hud.AddFortNode(b);Check(hud.FortPreviewPoints.Length==2&&hud.ActiveTool==CampaignHUD.Tool.FortBuild,"point clicks accumulate without mouse release confirmation");Check(hud.ConfirmFortStroke().accepted,"Esc completion path accepts selected nodes");Check(c.lineWorks.Approaching(engineer)&&engineer.Motor.IsMoving,"actual engineer automatically heads for midpoint");stage=1;phase=Time.time;return;
                }
                if(stage==1)
                {
                    if(c.lineWorks.Approaching(engineer))return;Check(engineer.Working,"actual navigation arrival switches to construction");Check(Vector2.Distance(engineer.transform.position,(a+b)/2)<=c.combined.engineering.constructionArrivalDistance+.001f,"actual worker stands at endpoint midpoint");Check(c.combined.fortifications.Count==0,"no premature construction while walking");c.lineWorks.Tick(c.lineWorks.Remaining(engineer)+.1f);Check(c.combined.fortifications.Count==1,"full line appears after construction timer");fort=c.combined.fortifications[0];hud.SelectBuilding(fort);stage=2;phase=Time.time;return;
                }
                if(Time.time-phase<.2f)return;
                if(stage==2)
                {
                    Check(hud.SelectedBuilding==fort&&hud.SelectedUnit==null,"line directly selectable without a worker selection");Button remove=null;foreach(var button in hud.GetComponentsInChildren<Button>())if(button.name=="拆除")remove=button;Check(remove!=null,"own fort context contains demolition button");var pos=engineer.transform.position;remove.onClick.Invoke();Check(c.lineWorks.Removing(fort)&&!engineer.Working,"demolition starts automatically without engineer job");c.lineWorks.Tick(c.lineWorks.RemovalRemaining(fort)/2);Check(!fort.Destroyed&&engineer.transform.position==pos,"partial demolition leaves entire line and does not move worker");remove.onClick.Invoke();Check(!c.lineWorks.Removing(fort)&&!fort.Destroyed,"same button cancels automatic demolition");stage=3;phase=Time.time;return;
                }
                if(stage==3)
                {
                    var camera=c.world.setup.mapCamera;camera.orthographicSize=2.5f;camera.transform.position=new Vector3((a.x+b.x)/2,(a.y+b.y)/2,camera.transform.position.z);
                    // Add a display-only reference-shaped example, using the exact same renderer and geometry.
                    Vector3 center=(a+b)/2;var example=new[]{center+new Vector3(-1.8f,-.7f),center+new Vector3(-1,-.7f),center+new Vector3(-.5f,-.4f),center+new Vector3(.5f,-.4f),center+new Vector3(1,-.7f),center+new Vector3(1.8f,-.7f)};var display=c.lineWorks.CreateLine(example,center+Vector3.up,"A",0,FortificationLineWorks.Length(example)*c.combined.engineering.metersPerWorldUnit);hud.SelectBuilding(display);stage=4;phase=Time.time;return;
                }
                if(stage==4)
                {
                    Capture(c.world.setup.mapCamera);File.WriteAllText("fort-node-runtime-verification.txt",results.Count+" runtime checks passed\n"+string.Join("\n",results));SessionState.SetBool("FortNodeRuntime",false);EditorApplication.update-=Poll;EditorApplication.Exit(0);
                }
            }
            catch(Exception e){File.WriteAllText("fort-node-runtime-verification.txt",e+"\n"+string.Join("\n",results));Debug.LogException(e);SessionState.SetBool("FortNodeRuntime",false);EditorApplication.update-=Poll;EditorApplication.Exit(1);}
        }
        static void Capture(Camera camera){var rt=new RenderTexture(1600,900,24);camera.targetTexture=rt;foreach(var canvas in UnityEngine.Object.FindObjectsOfType<Canvas>()){canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.sortingOrder=200;canvas.worldCamera=camera;canvas.planeDistance=1;}Canvas.ForceUpdateCanvases();camera.Render();RenderTexture.active=rt;var image=new Texture2D(1600,900,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1600,900),0,0);image.Apply();File.WriteAllBytes("fort-node-preview.png",image.EncodeToPNG());camera.targetTexture=null;RenderTexture.active=null;UnityEngine.Object.Destroy(image);UnityEngine.Object.Destroy(rt);}
    }
}
