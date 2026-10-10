using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
namespace CampaignDemo.Editor
{
    [InitializeOnLoad]
    public static class CampaignRevisionRuntimeVerification
    {
        static double start;static float phase;static int stage;static BattleUnit infantry,scout;static TransportTruck truck;static RecoveryPoint truckCity;static Vector3 truckPosition;static Vector3 placement;static ResourceDepot depot;static readonly List<string> results=new List<string>();
        static CampaignRevisionRuntimeVerification(){if(SessionState.GetBool("CampaignRevisionRuntime",false)){start=EditorApplication.timeSinceStartup;EditorApplication.update+=Poll;}}
        public static void Run(){CampaignRevisionInstaller.Configure();CampaignRevisionVerification.Validate();SessionState.SetBool("CampaignRevisionRuntime",true);start=EditorApplication.timeSinceStartup;EditorApplication.update+=Poll;EditorApplication.isPlaying=true;}
        static void Check(bool value,string label){if(!value)throw new Exception(label);results.Add("PASS "+label);}
        static void Poll()
        {
            try
            {
                if(EditorApplication.timeSinceStartup-start>120)throw new Exception("Campaign runtime timeout");if(!EditorApplication.isPlaying||Time.time<1)return;var c=UnityEngine.Object.FindObjectOfType<CampaignRuntime>();if(c==null||!c.world.Initialized)return;var w=c.world;var hud=UnityEngine.Object.FindObjectOfType<CampaignHUD>();
                if(stage==0){Check(hud!=null,"unified HUD running");w.setup.modes.EnterInspectionMode();foreach(var u in w.units){if(u.ownerId=="A"&&u.combatDefinition.role==UnitRole.Infantry&&infantry==null)infantry=u;if(u.ownerId=="A"&&u.combatDefinition.role==UnitRole.Scout)scout=u;}hud.ClearSelection();phase=Time.time;stage=1;return;}
                if(Time.time-phase<.2f)return;
                if(stage==1)
                {
                    var right=hud.transform.Find("Campaign right panel");Check(right!=null&&right.gameObject.activeInHierarchy,"right panel exists");int count=0;foreach(var b in UnityEngine.Object.FindObjectsOfType<Button>()){Check(b.transform.IsChildOf(right),"every active gameplay button is in right panel: "+b.name);count++;}Check(count==2,"unselected view has only support and collector buttons");
                    hud.ToggleTool(CampaignHUD.Tool.Support);stage=2;phase=Time.time;return;
                }
                if(stage==2)
                {
                    var button=hud.transform.Find("Campaign right panel/Global operations/炮火覆盖").GetComponent<Button>();Check(button.GetComponentInChildren<Text>().text=="取消","active support button changes to cancel");hud.ToggleTool(CampaignHUD.Tool.Collector);Check(hud.ActiveTool==CampaignHUD.Tool.Collector,"switching tool cancels old support selection");hud.ToggleTool(CampaignHUD.Tool.Collector);Check(hud.ActiveTool==CampaignHUD.Tool.None,"same button cancels tool");hud.ToggleTool(CampaignHUD.Tool.Support);hud.SelectUnit(infantry);Check(hud.ActiveTool==CampaignHUD.Tool.None,"object selection clears old pending tool");
                    var siteObj=new GameObject("runtime test resource region");var site=siteObj.AddComponent<ResourceSite>();site.siteId="runtime-resource";site.displayName="验证资源区";site.transform.position=infantry.transform.position;site.areaSize=new Vector2(4,4);var sites=new List<ResourceSite>(c.logistics.sites){site};c.logistics.sites=sites.ToArray();bool found=false;for(float x=-1;x<=1&&!found;x+=.1f)for(float y=-1;y<=1&&!found;y+=.1f){var p=infantry.transform.position+new Vector3(x,y);if(c.CanDeployCollector("A",p)){placement=p;found=true;}}Check(found,"valid owned resource placement exists");hud.ToggleTool(CampaignHUD.Tool.Collector);hud.Preview(placement);Check(hud.PreviewValid&&w.territory.suppressPresentation,"legal green preview hides ownership overlay");hud.Preview(new Vector3(10000,10000));Check(!hud.PreviewValid,"illegal deployment preview red");hud.Preview(placement);Check(hud.Confirm(placement).accepted,"collector deploys on owned resource land");Check(hud.ActiveTool==CampaignHUD.Tool.None&&!w.territory.suppressPresentation,"successful placement restores normal ownership view");
                    depot=c.CreateDepot(infantry.transform.position+Vector3.right*.2f,"A",123,false);hud.SelectBuilding(depot);stage=3;phase=Time.time;return;
                }
                if(stage==3)
                {
                    Check(hud.SelectedBuilding==depot,"warehouse selection opens lower context");Check(hud.transform.Find("Campaign right panel/Selection details").gameObject.activeInHierarchy,"selected object has lower details");
                    hud.SelectUnit(infantry);Check(hud.GetComponentsInChildren<Slider>().Length>0,"unit absolute speed slider present");var before=infantry.Personnel;hud.ToggleTool(CampaignHUD.Tool.Support);var result=hud.Send(new BattleCommand{kind=BattleCommandKind.SetAbsoluteSpeed,unitId=infantry.unitId,amount=.3f});Check(result.accepted&&hud.ActiveTool==CampaignHUD.Tool.None,"speed adjustment safely cancels pending bombardment");Check(Mathf.Abs(infantry.GetComponent<CampaignUnit>().desiredSpeed-.3f)<.001f,"absolute speed command applied");Check(infantry.Personnel==before,"UI speed change does not alter troop strength");
                    var city=w.points[0];if(city.ownerId!="A")foreach(var p in w.points)if(p.ownerId=="A"){city=p;break;}infantry.transform.position=city.transform.position;w.Tick(.1f);Check(hud.Send(new BattleCommand{kind=BattleCommandKind.EnterPoint,unitId=infantry.unitId,pointId=city.pointId}).accepted,"unit enters friendly city from revised command path");stage=4;phase=Time.time;return;
                }
                if(stage==4)
                {
                    Check(hud.SelectedUnit==null,"entering city automatically clears unit detail");w.Tick(w.points[0].transitionSeconds+1);RecoveryPoint city=infantry.Point;Check(city!=null&&!infantry.OnMap,"unit stored inside city");hud.SelectBuilding(city);Check(hud.GetComponentsInChildren<Button>().Length>2,"city contains stationed troop controls");hud.SelectUnit(infantry);Check(hud.Send(new BattleCommand{kind=BattleCommandKind.ExitPoint,unitId=infantry.unitId}).accepted,"stationed unit can leave through command");w.Tick(city.transitionSeconds+1);Check(infantry.OnMap,"unit leaves city");
                    var road=infantry.GetComponent<ArmyRoadMover>();var free=infantry.GetComponent<ArmyFreeMarchMover>();var at=new RoadPoint(infantry.transform.position.x,infantry.transform.position.y);bool moved=false;for(int i=0;i<16;i++){float a=i*Mathf.PI/8;var to=new RoadPoint(at.X+Mathf.Cos(a)*.2f,at.Y+Mathf.Sin(a)*.2f);if(free.CommitRoute(new[]{at,to},w.setup.freeMarchMap)){moved=true;break;}}Check(moved,"actual scene accepts movement after city exit");stage=5;phase=Time.time;return;
                }
                if(stage==5)
                {
                    Check(infantry.Motor.CurrentSpeed<=.3001f,"actual movement respects absolute speed cap");infantry.Motor.Stop();hud.SelectBuilding(depot);stage=6;phase=Time.time;return;
                }
                if(stage==6)
                {
                    Capture(w.setup.mapCamera);foreach(var point in w.points)if(point.ownerId=="A"){truckCity=point;break;}Check(c.logistics.BuildTruckFor("A",truckCity,null,ArmyEchelon.Squad),"resource truck construction still works");c.logistics.Tick(10);foreach(var unit in w.units)if(unit.GetComponent<TransportTruck>()!=null){truck=unit.GetComponent<TransportTruck>();break;}Check(truck!=null&&truck.Unit.Campaign==c,"dynamically built truck bound to new rules");truckPosition=truck.transform.position;hud.SelectUnit(truck.Unit);stage=8;phase=Time.time;return;
                }
                if(stage==8)
                {
                    Check(hud.GetComponentsInChildren<Slider>().Length==3,"resource truck has speed and two quantity sliders");var fill=hud.GetComponentsInChildren<Button>();Button load=null,unload=null;foreach(var b in fill){if(b.name=="填装")load=b;if(b.name=="卸载")unload=b;}Check(load!=null&&unload!=null,"manual load unload buttons present");var sliders=hud.GetComponentsInChildren<Slider>();sliders[1].value=25;stage=9;phase=Time.time;return;
                }
                if(stage==9)
                {
                    Button load=null;foreach(var b in hud.GetComponentsInChildren<Button>())if(b.name=="填装")load=b;Check(load.interactable,"load button enables with valid nearby stock and amount");load.onClick.Invoke();Check(Mathf.Abs(truck.Cargo-25)<.001f,"right panel loads selected amount only");truck.transform.position=new Vector3(1000,1000);stage=10;phase=Time.time;return;
                }
                if(stage==10)
                {
                    foreach(var b in hud.GetComponentsInChildren<Button>())if(b.name=="填装"||b.name=="卸载")Check(!b.interactable,"out of range transfer button gray: "+b.name);truck.transform.position=truckPosition;hud.SelectBuilding(depot);w.setup.modes.SetMode(MouseCommandMode.RoadMarch);stage=7;phase=Time.time;return;
                }
                if(stage==7)
                {
                    Check(!hud.transform.Find("Campaign right panel").gameObject.activeInHierarchy,"mode1 hides unified management panel");File.WriteAllText("campaign-runtime-verification.txt",results.Count+" runtime checks passed\n"+string.Join("\n",results));SessionState.SetBool("CampaignRevisionRuntime",false);EditorApplication.update-=Poll;EditorApplication.Exit(0);
                }
            }
            catch(Exception e){File.WriteAllText("campaign-runtime-verification.txt",e+"\n"+string.Join("\n",results));Debug.LogException(e);SessionState.SetBool("CampaignRevisionRuntime",false);EditorApplication.update-=Poll;EditorApplication.Exit(1);}
        }
        static void Capture(Camera camera)
        {var rt=new RenderTexture(1600,900,24);camera.targetTexture=rt;foreach(var canvas in UnityEngine.Object.FindObjectsOfType<Canvas>()){canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=1;}Canvas.ForceUpdateCanvases();camera.Render();RenderTexture.active=rt;var image=new Texture2D(1600,900,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1600,900),0,0);image.Apply();File.WriteAllBytes("campaign-revision-preview.png",image.EncodeToPNG());camera.targetTexture=null;RenderTexture.active=null;UnityEngine.Object.Destroy(image);UnityEngine.Object.Destroy(rt);}
    }
}
