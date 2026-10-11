using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using CampaignDemo;
[InitializeOnLoad]
public static class CityArchitectureVerification
{
    const string Key="CityArchitectureVerification";static double start;static int stage;static float next,stock;static int beforeUnits;static BattleUnit soldier;
    static readonly List<string> checks=new List<string>();
    static CityArchitectureVerification(){if(SessionState.GetBool(Key,false)){start=EditorApplication.timeSinceStartup;EditorApplication.update+=Poll;}}
    public static void Run(){var scene=EditorSceneManager.OpenScene("Assets/Scenes/Battle01.unity");CityArchitectureSetup.Configure(UnityEngine.Object.FindObjectOfType<BattleWorld>());EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();SessionState.SetBool(Key,true);start=EditorApplication.timeSinceStartup;EditorApplication.update+=Poll;EditorApplication.isPlaying=true;}
    static void Check(bool value,string label){if(!value)throw new Exception(label);checks.Add("PASS "+label);}
    static Button Button(CampaignHUD hud,string name){return hud.GetComponentsInChildren<Button>(true).First(b=>b.gameObject.activeInHierarchy&&b.name==name);}
    static void Poll(){try{
        if(EditorApplication.timeSinceStartup-start>120)throw new Exception("City test timeout");if(!EditorApplication.isPlaying||Time.time<1||Time.time<next)return;
        var world=UnityEngine.Object.FindObjectOfType<BattleWorld>();if(world==null||!world.Initialized)return;var hud=UnityEngine.Object.FindObjectOfType<CampaignHUD>();var service=world.GetComponent<CityManagement>();var production=service.Production;var city=world.points.First(p=>p.ownerId=="A");
        if(stage==0)
        {
            world.enabled=false;production.enabled=false;world.SetPlayer("A");world.setup.modes.EnterInspectionMode();var commander=world.GetComponent<AIBasicCommander>();commander.HandOff();
            UnityEngine.Object.DestroyImmediate(world.GetComponent<BattleAIController>());UnityEngine.Object.DestroyImmediate(commander);
            production.rules=UnityEngine.Object.Instantiate(production.rules);production.rules.infantryRequiresFactory=true;production.rules.maximumQueuedPerCity=5;city.hasFactory=true;city.resourceStock=4000;city.transitionSeconds=.1f;
            soldier=world.units.First(u=>u.ownerId=="A");soldier.transform.position=city.ExitPosition;soldier.Damage(3);soldier.UnderFire=false;hud.SelectBuilding(city);stage=1;next=Time.time+.2f;return;
        }
        if(stage==1)
        {
            Check(world.GetComponent<BattleAIController>()==null&&world.GetComponent<AIBasicCommander>()==null,"city interface works without AI components");
            foreach(var title in new[]{"城市概况","驻军与修复","附近部队入城","建筑与生产","生产订单"})Check(hud.GetComponentsInChildren<RectTransform>(true).Any(t=>t.name==title),"city section exists: "+title);
            Button(hud,"驻军与入城").onClick.Invoke();
            var enter=hud.GetComponentsInChildren<Button>().First(b=>b.name.StartsWith("入城：")&&b.interactable);enter.onClick.Invoke();Check(soldier.Point==city&&soldier.State==ArmyBattleState.EnteringPoint,"city entry button submits real entry command");world.Tick(.2f);Check(soldier.State==ArmyBattleState.InsidePoint,"entry completes into city garrison");stage=2;next=Time.time+.2f;return;
        }
        if(stage==2)
        {
            Button(hud,"建筑与生产").onClick.Invoke();Button(hud,"数量：1").onClick.Invoke();Button(hud,"数量：1").onClick.Invoke();stock=city.resourceStock;beforeUnits=world.units.Length;Button(hud,"加入生产").onClick.Invoke();
            Check(production.PendingCount("A")==3,"quantity selector creates three production orders without AI");Check(city.resourceStock==stock-production.Cost(ArmyEchelon.Squad)*3,"batch charged exactly once");
            Button(hud,"驻军与入城").onClick.Invoke();Button(hud,"开始修复").onClick.Invoke();Check(soldier.State==ArmyBattleState.Repairing,"repair button starts garrison recovery");world.Tick(1);
            Check(soldier.Personnel>soldier.Capacity-3&&production.PendingCount("A")==3,"repair and production progress independently");
            float remaining=production.Jobs[1].remaining;production.Tick(production.Duration(ArmyEchelon.Squad));
            Check(world.units.Length==beforeUnits+1&&production.PendingCount("A")==2,"city production completes one order at a time");Check(production.Jobs[0].remaining==remaining,"queued orders do not run in parallel");stage=3;next=Time.time+.2f;return;
        }
        if(stage==3)
        {
            Button(hud,"建筑与生产").onClick.Invoke();Capture(world,hud);Button(hud,"展开订单").onClick.Invoke();float paid=production.Jobs[0].paidCost;stock=city.resourceStock;Button(hud,"取消该订单").onClick.Invoke();Check(production.PendingCount("A")==1&&city.resourceStock==stock+paid,"cancel one order refunds its actual paid cost");
            Button(hud,"取消全部生产并退款").onClick.Invoke();Check(production.PendingCount("A")==0,"cancel all clears remaining production orders");
            Check(!world.GetComponent<BattleCommandService>().Submit(new BattleCommand{kind=BattleCommandKind.CancelCityProduction,playerId="B",pointId=city.pointId}).accepted,"enemy cannot cancel city production");
            world.Tick(10);stage=4;next=Time.time+.2f;return;
        }
        if(stage==4)
        {
            Button(hud,"驻军与入城").onClick.Invoke();Button(hud,"出城").onClick.Invoke();world.Tick(.2f);Check(soldier.Point==null&&soldier.OnMap,"garrison exit button returns soldier to map");
            city.hasFactory=false;stock=city.resourceStock;Check(service.Upgrade(city,"A").allowed,"factory upgrade availability comes from business service");
            world.GetComponent<BattleCommandService>().Submit(new BattleCommand{kind=BattleCommandKind.BuildFactory,playerId="A",pointId=city.pointId});
            Check(service.Logistics.IsBuilding(city),"factory uses city construction state");
            Check(world.GetComponent<BattleCommandService>().Submit(new BattleCommand{kind=BattleCommandKind.CancelCityProduction,playerId="A",pointId=city.pointId}).accepted&&city.resourceStock==stock,"factory cancellation refunds captured upgrade fee");
            Check(!production.Quote("A",city,ArmyEchelon.Squad).allowed,"factory requirement shared by UI and production");city.hasFactory=true;
            Check(production.Quote("A",city,ArmyEchelon.Squad).allowed,"factory completed enables production independently of AI");
            Check(service.Snapshot(city,"B")==null,"enemy cannot read garrison or resource snapshot");
            city.ownerId="B";Check(!production.Quote("A",city,ArmyEchelon.Squad).allowed,"ownership change blocks stale player commands");city.ownerId="A";
            File.WriteAllText("city-architecture-verification-result.txt",checks.Count+" checks passed\n"+string.Join("\n",checks));Debug.Log("CITY_ARCHITECTURE_PASS "+checks.Count);SessionState.SetBool(Key,false);EditorApplication.update-=Poll;EditorApplication.Exit(0);
        }
    }catch(Exception e){Debug.LogException(e);File.WriteAllText("city-architecture-verification-result.txt",e+"\n"+string.Join("\n",checks));SessionState.SetBool(Key,false);EditorApplication.update-=Poll;EditorApplication.Exit(1);}}
    static void Capture(BattleWorld world,CampaignHUD hud)
    {
        if(SystemInfo.graphicsDeviceType==UnityEngine.Rendering.GraphicsDeviceType.Null)return;
        var camera=world.setup.mapCamera;var canvas=hud.GetComponentInParent<Canvas>();var texture=new RenderTexture(1600,900,24);var target=camera.targetTexture;var mode=canvas.renderMode;var previousCamera=canvas.worldCamera;
        canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=1;camera.targetTexture=texture;Canvas.ForceUpdateCanvases();camera.Render();
        var previous=RenderTexture.active;RenderTexture.active=texture;var output=new Texture2D(1600,900,TextureFormat.RGB24,false);output.ReadPixels(new Rect(0,0,1600,900),0,0);output.Apply();File.WriteAllBytes("city-management-preview.png",output.EncodeToPNG());RenderTexture.active=previous;camera.targetTexture=target;canvas.renderMode=mode;canvas.worldCamera=previousCamera;UnityEngine.Object.Destroy(output);texture.Release();UnityEngine.Object.Destroy(texture);
    }
}
