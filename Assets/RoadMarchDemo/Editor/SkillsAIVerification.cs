using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using CampaignDemo;

[InitializeOnLoad]
public static class SkillsAIVerification
{
    const string Key="SkillsAIVerification";
    static double started;
    static readonly List<string> checks=new List<string>();
    static SkillsAIVerification(){if(SessionState.GetBool(Key,false)){started=EditorApplication.timeSinceStartup;EditorApplication.update+=Poll;}}
    public static void Run()
    {
        var scene=EditorSceneManager.OpenScene("Assets/Scenes/Battle01.unity");var world=UnityEngine.Object.FindObjectOfType<BattleWorld>();
        if(!SkillsAISetup.Configure(world))throw new Exception("Existing commander missing");
        EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
        SessionState.SetBool(Key,true);started=EditorApplication.timeSinceStartup;EditorApplication.update+=Poll;EditorApplication.isPlaying=true;
    }
    static void Check(bool ok,string label){if(!ok)throw new Exception(label);checks.Add("PASS "+label);}
    static void Poll()
    {
        try
        {
            if(EditorApplication.timeSinceStartup-started>120)throw new Exception("Test timeout");
            if(!EditorApplication.isPlaying||Time.time<1)return;
            var world=UnityEngine.Object.FindObjectOfType<BattleWorld>();if(world==null||!world.Initialized)return;
            var ai=world.GetComponent<BattleAIController>();var c=ai.basicCommander;var production=world.GetComponent<InfantryProduction>();
            world.enabled=false;production.enabled=false;world.SetPlayer("A");c.HandOff();
            c.profile=UnityEngine.Object.Instantiate(c.profile);production.rules=UnityEngine.Object.Instantiate(production.rules);var p=c.profile;
            p.produceResources=false;p.transportResources=false;p.recover=false;p.minimumAttackStrength=9999;
            p.targetAttackStrength=20;p.maximumArmyStrength=60;p.maximumProductionJobs=2;p.recruitInfantry=true;production.rules.infantryRequiresFactory=false;
            var city=world.points.First(point=>point.ownerId=="B"&&point.kind==FacilityKind.RecoveryPoint);
            city.resourceStock=10000;city.hasFactory=false;
            Check(!p.enableCheats,"cheats default off");
            Check(production.rules.infantryPrefab!=null&&production.rules.infantryPrefab.GetComponent<BattleUnit>().combatDefinition.role==UnitRole.Infantry,"ordinary infantry prefab connected");
            float before=city.resourceStock;ai.Tick(6);
            Check(production.PendingCount("B")==1,"economic scheduler creates one local infantry job");
            Check(city.resourceStock==before-production.Cost(p.recruitmentEchelon),"manufacture cost deducted exactly once");
            Check(production.PendingStrength("B")==1,"pending squad included as one squad equivalent");
            float initialRemaining=production.Jobs[0].remaining;ai.enabled=false;production.Tick(5);
            Check(!ai.CanThink&&production.Jobs[0].remaining==initialRemaining,"disabled commander component cannot advance automated production");ai.enabled=true;
            float paid=city.resourceStock;for(int i=0;i<5;i++)ai.Tick(6);
            Check(production.PendingCount("B")==1&&city.resourceStock==paid,"repeated AI updates do not duplicate orders");
            Check(!world.campaign.logistics.BuildFactoryFor("B",city),"factory cannot conflict with active infantry production");
            p.recruitInfantry=false;ai.Tick(.25f);
            Check(production.PendingCount("B")==0&&city.resourceStock==before,"turning off recruitment cancels job and refunds its cost");
            ai.Tick(10);Check(production.PendingCount("B")==0,"disabled recruitment creates no new jobs");
            p.recruitInfantry=true;
            ai.reservations.Reserve("protect-test",city.GetInstanceID().ToString(),city.resourceStock-20,city.resourceStock);ai.Tick(6);
            Check(production.PendingCount("B")==0&&city.resourceStock==before,"reserved resources cannot be spent on recruitment");ai.reservations.Release("protect-test");
            city.resourceStock=0;ai.Tick(6);Check(production.PendingCount("B")==0,"resources in other cities cannot pay this city's production");
            city.resourceStock=10000;var prefab=production.rules.infantryPrefab;production.rules.infantryPrefab=null;float missingPrefabStock=city.resourceStock;
            Check(!production.Queue("B",city,ArmyEchelon.Squad,true)&&city.resourceStock==missingPrefabStock,"missing prefab rejects production without charging");production.rules.infantryPrefab=prefab;
            var injured=world.units.First(u=>u.ownerId=="B");injured.Damage(5);
            float healing=world.units.Where(u=>u.Alive&&u.ownerId=="B").Sum(u=>u.Capacity-u.Personnel)*city.resourcePerPerson;
            city.resourceStock=p.productionReserve+healing+production.Cost(p.recruitmentEchelon)-1;ai.Tick(6);
            Check(production.PendingCount("B")==0,"local recovery and minimum reserve take priority over new soldiers");injured.Restore(injured.Capacity);
            city.resourceStock=10000;production.rules.infantryRequiresFactory=true;ai.Tick(6);
            Check(production.PendingCount("B")==0,"factory requirement is honored when enabled");production.rules.infantryRequiresFactory=false;
            ai.Tick(6);Check(production.PendingCount("B")==1,"disabling optional factory requirement permits production");
            var job=production.Jobs[0];float remaining=job.remaining;world.SetPlayer("B");production.Tick(100);
            Check(job.remaining==remaining,"AI-owned production pauses during red manual control");
            world.SetPlayer("A");int unitCount=world.units.Length;production.Tick(100);
            Check(world.units.Length==unitCount+1&&production.PendingCount("B")==0,"finished order registers one real soldier unit");
            var newborn=world.units.Last();
            Check(newborn.ownerId=="B"&&newborn.Alive&&newborn.OnMap&&newborn.Campaign==world.campaign,"new soldier has correct absolute faction and simulation state");
            ai.Refresh();Check(ai.Orders.Any(o=>o.GetComponent<BattleUnit>()==newborn),"new soldier automatically joins red commander");
            Check(!world.GetComponent<BattleCommandService>().Submit(new BattleCommand{kind=BattleCommandKind.BuildInfantry,playerId="A",pointId=world.points.First(point=>point.ownerId=="A").pointId,automatedProduction=true,echelon=ArmyEchelon.Squad}).accepted,"automated recruitment cannot target blue");
            p.resourceIncomeMultiplier=3;p.productionSpeedMultiplier=3;p.omniscientEnemyIntel=true;p.enableCheats=false;
            var blue=world.units.First(u=>u.ownerId=="A");blue.transform.position=new Vector3(2000,2000);world.fogEnabled=true;
            p.enableCheats=false;Check(!world.IsVisible("B",blue),"ordinary AI cannot see hidden distant enemy");
            p.enableCheats=true;Check(world.IsVisible("B",blue)&&world.GetComponent<BattleInformationSource>().GetArmy("B",blue.unitId)!=null,"explicit full-map cheat reaches filtered AI intelligence");
            Check(!AICheatRules.FullIntel(world,"A"),"full-map cheat is not granted to blue");
            world.SetPlayer("B");Check(!world.IsVisible("B",blue),"full-map cheat ends on red manual handoff");world.SetPlayer("A");
            city.resourceStock=10000;
            Check(world.GetComponent<BattleCommandService>().Submit(new BattleCommand{kind=BattleCommandKind.BuildInfantry,playerId="B",pointId=city.pointId,automatedProduction=true,echelon=ArmyEchelon.Squad}).accepted,"production command shared with scheduler works");
            unitCount=world.units.Length;production.Tick(production.rules.infantrySquadSeconds/3+.1f);
            Check(world.units.Length==unitCount+1,"production speed cheat shortens actual build duration");
            Check(production.Queue("B",city,ArmyEchelon.Squad,true),"capture test has a pending local job");
            float capturedStock=city.resourceStock;unitCount=world.units.Length;city.ownerId="A";production.Tick(100);
            Check(production.PendingCount("B")==0&&world.units.Length==unitCount&&city.resourceStock==capturedStock,"captured city cancels job without spawning or refunding to enemy");city.ownerId="B";
            p.enableCheats=false;p.targetAttackStrength=0;p.recruitInfantry=false;p.minimumAttackStrength=3;p.attackCities=true;
            c.HandOff();ai.Tick(2);Check(c.target!=null,"attack enabled creates a mission");p.attackCities=false;ai.Tick(.25f);
            Check(c.target==null||c.phase==AIBasicPhase.Withdrawing,"turning off attack withdraws existing squad safely");
            Check(production.PendingCount("B")==0,"all-disabled configuration has no recruitment orders");
            File.WriteAllText("skills-ai-verification-result.txt",checks.Count+" checks passed\n"+string.Join("\n",checks));
            Debug.Log("SKILLS_AI_PASS "+checks.Count);SessionState.SetBool(Key,false);EditorApplication.update-=Poll;EditorApplication.Exit(0);
        }
        catch(Exception exception){Debug.LogException(exception);File.WriteAllText("skills-ai-verification-result.txt",exception+"\n"+string.Join("\n",checks));SessionState.SetBool(Key,false);EditorApplication.update-=Poll;EditorApplication.Exit(1);}
    }
}
