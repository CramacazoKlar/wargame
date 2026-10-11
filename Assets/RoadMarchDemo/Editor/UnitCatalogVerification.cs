using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using CampaignDemo;
[InitializeOnLoad]
public static class UnitCatalogVerification
{
    const string Key="UnitCatalogVerification";static double start;static readonly List<string> checks=new List<string>();
    static UnitCatalogVerification(){if(SessionState.GetBool(Key,false)){start=EditorApplication.timeSinceStartup;EditorApplication.update+=Poll;}}
    public static void Run(){var scene=EditorSceneManager.OpenScene("Assets/Scenes/Battle01.unity");UnitCatalogSetup.Configure(UnityEngine.Object.FindObjectOfType<BattleWorld>());EditorSceneManager.SaveScene(scene);SessionState.SetBool(Key,true);start=EditorApplication.timeSinceStartup;EditorApplication.update+=Poll;EditorApplication.isPlaying=true;}
    static void Check(bool value,string label){if(!value)throw new Exception(label);checks.Add("PASS "+label);}
    static void Poll(){try{
        if(EditorApplication.timeSinceStartup-start>120)throw new Exception("Catalog test timeout");if(!EditorApplication.isPlaying||Time.time<1)return;var world=UnityEngine.Object.FindObjectOfType<BattleWorld>();if(world==null||!world.Initialized)return;
        world.enabled=false;var ai=world.GetComponent<BattleAIController>();ai.aiEnabled=false;var production=world.GetComponent<InfantryProduction>();production.enabled=false;
        var city=world.points.First(p=>p.ownerId=="A");city.hasFactory=true;city.resourceStock=100000;var options=world.GetComponent<LevelUnitAvailability>();
        foreach(UnitRole role in Enum.GetValues(typeof(UnitRole)))Check(options.Allows(role),"default available: "+role);
        foreach(UnitRole role in Enum.GetValues(typeof(UnitRole)))
        {
            if(role==UnitRole.Logistics)continue;int count=world.units.Length;
            Check(production.Queue("A",city,ArmyEchelon.Squad,false,1,role),"queue configured role: "+role);
            production.Tick(production.Duration(ArmyEchelon.Squad,role)+1);
            Check(world.units.Length==count+1&&world.units.Last().combatDefinition.role==role&&world.units.Last().Alive&&world.units.Last().ownerId=="A","real unit spawned: "+role);
        }
        int beforeTruck=world.units.Length;var logistics=world.GetComponent<LogisticsWorld>();Check(logistics.BuildTruckFor("A",city,null,ArmyEchelon.Squad),"allowed resource truck starts manufacturing");logistics.Tick(logistics.truckPrefab.GetComponent<TransportTruck>().definition.buildSeconds+1);Check(world.units.Length==beforeTruck+1&&world.units.Last().combatDefinition.role==UnitRole.Logistics,"resource truck spawned through existing production");
        options.tank=false;float stock=city.resourceStock;
        Check(!world.GetComponent<BattleCommandService>().Submit(new BattleCommand{kind=BattleCommandKind.BuildInfantry,playerId="A",pointId=city.pointId,productionRole=UnitRole.Tank,echelon=ArmyEchelon.Squad}).accepted&&city.resourceStock==stock,"disabled tank rejected without payment");
        options.logistics=false;Check(!world.GetComponent<LogisticsWorld>().BuildTruckFor("A",city,null,ArmyEchelon.Squad),"disabled truck rejected by legacy production");
        var hud=UnityEngine.Object.FindObjectOfType<CampaignHUD>();world.setup.modes.EnterInspectionMode();hud.SelectBuilding(city);var selector=hud.GetComponentsInChildren<UnityEngine.UI.Button>(true).First(b=>b.name=="兵种：普通步兵");for(int i=0;i<16;i++){selector.onClick.Invoke();Check(!selector.GetComponentInChildren<UnityEngine.UI.Text>().text.Contains("坦克")&&!selector.GetComponentInChildren<UnityEngine.UI.Text>().text.Contains("资源车"),"city selector filters disabled types "+i);}
        options.tank=true;Check(production.Quote("A",city,ArmyEchelon.Squad,false,1,UnitRole.Tank).allowed,"reenabling tank restores production");
        File.WriteAllText("unit-catalog-verification-result.txt",checks.Count+" checks passed\n"+string.Join("\n",checks));Debug.Log("UNIT_CATALOG_PASS "+checks.Count);SessionState.SetBool(Key,false);EditorApplication.update-=Poll;EditorApplication.Exit(0);
    }catch(Exception e){Debug.LogException(e);File.WriteAllText("unit-catalog-verification-result.txt",e+"\n"+string.Join("\n",checks));SessionState.SetBool(Key,false);EditorApplication.update-=Poll;EditorApplication.Exit(1);}}
}
