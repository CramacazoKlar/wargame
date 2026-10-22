using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEditor;
namespace CampaignDemo.Editor {
 public static class BarrageVerification {
  static int checks;static string report="";
  static void Check(bool ok,string message){if(!ok)throw new Exception(message);checks++;report+="PASS "+message+"\n";Debug.Log("BARRAGE_PASS "+message);}
  sealed class Fixture:IDisposable {
   public GameObject root;public BattleWorld world;public CombinedArmsWorld combined;public BattleUnit source,target;public ArtilleryBattery battery;public ArtilleryDefinition definition;readonly List<UnityEngine.Object> data=new List<UnityEngine.Object>();
   public Fixture(UnitRole role,ArmyEchelon rank){root=new GameObject("Barrage test");world=root.AddComponent<BattleWorld>();combined=root.AddComponent<CombinedArmsWorld>();combined.world=world;source=Unit("source",role,rank,Vector3.zero);target=Unit("target",UnitRole.Infantry,ArmyEchelon.Squad,Vector3.right);target.ownerId="B";target.CapacityOverride=1000;target.Initialize();world.units=new[]{source,target};definition=ScriptableObject.CreateInstance<ArtilleryDefinition>();data.Add(definition);definition.damage=100;definition.delaySeconds=.2f;definition.blastRadius=.2f;definition.friendlyFire=false;battery=source.gameObject.AddComponent<ArtilleryBattery>();battery.definition=definition;}
   BattleUnit Unit(string id,UnitRole role,ArmyEchelon rank,Vector3 at){var g=new GameObject(id);g.transform.SetParent(root.transform);g.transform.position=at;var u=g.AddComponent<BattleUnit>();u.unitId=id;u.ownerId="A";u.combatDefinition=ScriptableObject.CreateInstance<ArmyCombatDefinition>();data.Add(u.combatDefinition);u.combatDefinition.role=role;u.combatDefinition.echelon=rank;u.combatDefinition.defense=0;u.Initialize();return u;}
   public void Dispose(){UnityEngine.Object.DestroyImmediate(root);foreach(var o in data)if(o!=null)UnityEngine.Object.DestroyImmediate(o);}
  }
  public static void Run(){try{
   var plan=ScriptableObject.CreateInstance<ArtilleryDefinition>();
   float normalGrowth=plan.BarrageDuration(UnitRole.Artillery,ArmyEchelon.Company)-plan.BarrageDuration(UnitRole.Artillery,ArmyEchelon.Squad),rocketGrowth=plan.BarrageDuration(UnitRole.RocketArtillery,ArmyEchelon.Company)-plan.BarrageDuration(UnitRole.RocketArtillery,ArmyEchelon.Squad);
   Check(normalGrowth>rocketGrowth*3,"Ordinary artillery duration grows much more strongly than rocket artillery");
   for(int rank=1;rank<8;rank++)foreach(var role in new[]{UnitRole.Artillery,UnitRole.RocketArtillery})Check(plan.BarrageDuration(role,(ArmyEchelon)rank)>plan.BarrageDuration(role,(ArmyEchelon)(rank-1)),"Duration increases with echelon: "+role+" "+rank);UnityEngine.Object.DestroyImmediate(plan);
   foreach(var role in new[]{UnitRole.Artillery,UnitRole.RocketArtillery})foreach(var rank in new[]{ArmyEchelon.Squad,ArmyEchelon.Company,ArmyEchelon.Regiment})using(var f=new Fixture(role,rank)){
    int launches=0,impacts=0;float clock=0,lastLaunch=0,lastImpact=0;
    f.world.CombatEffectOccurred+=e=>{if(e.Stage==CombatEffectStage.ArtilleryLaunch){launches++;lastLaunch=clock;}if(e.Stage==CombatEffectStage.ArtilleryImpact){impacts++;lastImpact=clock;}};
    float health=f.target.Personnel,duration=f.battery.BarrageDuration;int expected=f.definition.BarrageRounds(role,rank);
    Check(f.battery.Fire(f.world,"A",Vector3.right).accepted&&launches==1&&f.battery.Firing,"Barrage starts with one immediate shot: "+role+" "+rank);
    Check(!f.battery.Fire(f.world,"A",Vector3.right).accepted,"Cannot start overlapping barrage");
    int ticks=Mathf.CeilToInt((duration+f.definition.delaySeconds+.3f)/.1f);for(int t=0;t<ticks;t++){clock+=.1f;f.combined.Tick(.1f);}
    Check(launches==expected&&impacts==expected,"All scheduled launches and impacts occur: "+role+" "+rank);
    Check(lastLaunch>=duration-.11f&&lastImpact>=duration+f.definition.delaySeconds-.11f,"Shots and impacts extend throughout the configured duration");
    Check(Mathf.Abs((health-f.target.Personnel)-100)<.05f,"Splitting a barrage preserves the original total damage budget");Check(!f.battery.Firing,"Firing status finishes at the end of the barrage");
   }
   using(var f=new Fixture(UnitRole.Artillery,ArmyEchelon.Company)){
    int launches=0,impacts=0;f.world.CombatEffectOccurred+=e=>{if(e.Stage==CombatEffectStage.ArtilleryLaunch)launches++;if(e.Stage==CombatEffectStage.ArtilleryImpact)impacts++;};f.definition.delaySeconds=2;f.battery.Fire(f.world,"A",Vector3.right);f.source.transform.position+=Vector3.up*.5f;f.combined.Tick(1);f.combined.Tick(2);Check(launches==1&&impacts==1&&!f.battery.Firing,"Moving interrupts unlaunched rounds while the airborne first round still lands");
    var campaign=f.root.AddComponent<CampaignRuntime>();campaign.world=f.world;campaign.combined=f.combined;campaign.rules=ScriptableObject.CreateInstance<CampaignRules>();var limits=new Dictionary<BattleUnit,float>();float initial=f.target.Personnel,cap=initial*campaign.rules.shellMaximumLossFraction;
    for(int i=0;i<20;i++)CampaignShellResolver.Resolve(campaign,Vector3.right,.2f,100000,f.source.unitId,"A",limits);
    Check(Mathf.Abs((initial-f.target.Personnel)-cap)<.05f,"The existing casualty cap applies to the entire barrage rather than resetting every round");UnityEngine.Object.DestroyImmediate(campaign.rules);
    var payload=JsonUtility.FromJson<PvpUnit>(JsonUtility.ToJson(new PvpUnit{artilleryCooldown=20,artilleryBarrageRemaining=7}));f.battery.ApplyNetworkState(payload);Check(f.battery.Remaining==20&&f.battery.BarrageRemaining==7,"Network state carries barrage progress separately from cooldown");
   }
   var repair=Resources.LoadAll<AudioClip>("Audio/Construction");Check(repair.Length==6,"Six engineer work sounds extracted from the supplied audio are available");
   File.WriteAllText("barrage-verification.txt",checks+" checks passed\n"+report);EditorApplication.Exit(0);
  }catch(Exception e){Debug.LogException(e);File.WriteAllText("barrage-verification.txt","FAIL\n"+report+e);EditorApplication.Exit(1);}}
 }
}
