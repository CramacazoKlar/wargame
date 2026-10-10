using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
namespace CampaignDemo.Editor
{
    [InitializeOnLoad]
    public static class FortLineRuntimeVerification
    {
        static double start;static float phase;static int stage;static BattleUnit engineer;static Vector3 a,b;static RecoveryPoint payer;static float cost,time;static readonly List<string> results=new List<string>();
        static FortLineRuntimeVerification(){if(SessionState.GetBool("FortLineRuntime",false)){start=EditorApplication.timeSinceStartup;EditorApplication.update+=Poll;}}
        public static void Run(){FortLineInstaller.Configure();FortLineVerification.Validate();CampaignRevisionVerification.Validate();SessionState.SetBool("FortLineRuntime",true);start=EditorApplication.timeSinceStartup;EditorApplication.update+=Poll;EditorApplication.isPlaying=true;}
        static void Check(bool value,string name){if(!value)throw new Exception(name);results.Add("PASS "+name);}
        static void Poll()
        {
            try
            {
                if(EditorApplication.timeSinceStartup-start>120)throw new Exception("Line runtime timeout");if(!EditorApplication.isPlaying||Time.time<1)return;var c=UnityEngine.Object.FindObjectOfType<CampaignRuntime>();if(c==null||!c.world.Initialized)return;var hud=UnityEngine.Object.FindObjectOfType<CampaignHUD>();
                if(stage==0)
                {
                    c.world.setup.modes.EnterInspectionMode();foreach(var u in c.world.units)if(u.ownerId=="A"&&u.combatDefinition.role==UnitRole.Engineer)engineer=u;Check(engineer!=null,"engineer present");
                    bool found=false;for(int i=0;i<16&&!found;i++){float angle=-Mathf.PI/2+i*Mathf.PI/8;Vector3 d=new Vector3(Mathf.Cos(angle),Mathf.Sin(angle));a=engineer.transform.position+d*.9f;b=a+new Vector3(-d.y,d.x)*.7f;if(c.lineWorks.CanBuild(engineer,new[]{a,b},out _))found=true;}Check(found,"legal drawable line near engineer");payer=c.lineWorks.NearestCity("A",engineer.transform.position);cost=Vector2.Distance(a,b)*c.combined.engineering.metersPerWorldUnit*c.combined.engineering.lineCostPerMeter;time=Vector2.Distance(a,b)*c.combined.engineering.metersPerWorldUnit*c.combined.engineering.lineBuildSecondsPerMeter;
                    hud.SelectUnit(engineer);hud.ToggleTool(CampaignHUD.Tool.FortBuild);hud.BeginFortStroke(a);hud.AppendFortPoint(engineer.transform.position+Vector3.right*100,true);var points=hud.FortPreviewPoints;Check(Vector2.Distance(points[points.Length-1],engineer.transform.position)<=engineer.combatDefinition.visionRadius+.0001f,"actual HUD drawing endpoint clamps at vision boundary");hud.CancelTool();hud.ToggleTool(CampaignHUD.Tool.FortBuild);hud.BeginFortStroke(a);hud.AppendFortPoint(b,true);float stock=payer.resourceStock;Check(hud.ConfirmFortStroke().accepted,"HUD confirms a whole line stroke");Check(Mathf.Abs(payer.resourceStock-(stock-cost))<.01f,"HUD construction charges nearest city by length");stage=1;phase=Time.time;return;
                }
                if(Time.time-phase<.2f)return;
                if(stage==1)
                {
                    Button original=null;foreach(var button in hud.GetComponentsInChildren<Button>())if(button.name=="修建工事")original=button;Check(original!=null&&original.GetComponentInChildren<Text>().text=="取消","original build button becomes cancel during construction");float stock=payer.resourceStock,remaining=c.lineWorks.Remaining(engineer);original.onClick.Invoke();Check(!engineer.Working&&!c.lineWorks.Working(engineer),"same button cancels actual construction");Check(Mathf.Abs(payer.resourceStock-(stock+cost*remaining/time))<.01f,"HUD cancellation refunds unfinished fraction");
                    hud.ToggleTool(CampaignHUD.Tool.FortBuild);hud.BeginFortStroke(a);hud.AppendFortPoint(b,true);Check(hud.ConfirmFortStroke().accepted,"second stroke starts");c.lineWorks.Tick(time);Check(c.combined.fortifications.Count==1&&c.combined.fortifications[0].IsLine,"completed stroke creates exactly one line object");stage=2;phase=Time.time;return;
                }
                if(stage==2)
                {
                    var line=c.combined.fortifications[0];Check(line.GetComponentsInChildren<LineRenderer>().Length>1,"defense line baseline and parapet marks rendered");hud.SelectUnit(engineer);hud.ToggleTool(CampaignHUD.Tool.FortAttack);Check(hud.Confirm(Vector3.Lerp(a,b,.5f)).accepted,"right panel selects full line for dismantling");Check(c.lineWorks.IsDismantling(engineer),"whole line demolition job active");stage=3;phase=Time.time;return;
                }
                if(stage==3)
                {
                    c.lineWorks.Cancel(engineer);Check(!c.combined.fortifications[0].Destroyed,"cancel demolition preserves full line");var camera=c.world.setup.mapCamera;camera.orthographicSize=2.5f;camera.transform.position=new Vector3((a.x+b.x)/2,(a.y+b.y)/2,camera.transform.position.z);Capture(camera);foreach(var u in c.world.units){var free=u.GetComponent<ArmyFreeMarchMover>();Check((free.AllowedTerrain&TerrainAccess.HighMountain)==0,"high mountain forbidden: "+u.unitId);Check(free.definition.mode2ReductionPercent==0,"no additional free march reduction: "+u.unitId);float expected=FortLineInstaller.Retained(u.combatDefinition.role,TerrainKind.Grass)*u.GetComponent<ArmyRoadMover>().Speed;Check(Mathf.Abs(free.SpeedAt(TerrainKind.Grass)-expected)<.001f,"actual grass speed equals retained percentage: "+u.unitId);}
                    File.WriteAllText("fort-line-runtime-verification.txt",results.Count+" runtime checks passed\n"+string.Join("\n",results));SessionState.SetBool("FortLineRuntime",false);EditorApplication.update-=Poll;EditorApplication.Exit(0);
                }
            }
            catch(Exception e){File.WriteAllText("fort-line-runtime-verification.txt",e+"\n"+string.Join("\n",results));Debug.LogException(e);SessionState.SetBool("FortLineRuntime",false);EditorApplication.update-=Poll;EditorApplication.Exit(1);}
        }
        static void Capture(Camera camera){var rt=new RenderTexture(1600,900,24);camera.targetTexture=rt;foreach(var canvas in UnityEngine.Object.FindObjectsOfType<Canvas>()){canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.sortingOrder=200;canvas.worldCamera=camera;canvas.planeDistance=1;}Canvas.ForceUpdateCanvases();camera.Render();RenderTexture.active=rt;var image=new Texture2D(1600,900,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1600,900),0,0);image.Apply();File.WriteAllBytes("fort-line-preview.png",image.EncodeToPNG());camera.targetTexture=null;RenderTexture.active=null;UnityEngine.Object.Destroy(image);UnityEngine.Object.Destroy(rt);}
    }
}


