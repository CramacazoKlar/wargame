using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using CampaignDemo;
public static class ShellBudgetVerification
{
 static readonly List<string> results=new List<string>();
 static void Check(bool condition,string label){if(!condition)throw new Exception(label);results.Add("PASS "+label);}
 static void Near(float actual,float expected,string label){Check(Mathf.Abs(actual-expected)<.002f,label+" actual="+actual);}
 sealed class Fixture:IDisposable{
  public GameObject root;public BattleWorld world;public CampaignRuntime campaign;public CampaignRules rules;
  public Fixture(){root=new GameObject("Shell budget fixture");world=root.AddComponent<BattleWorld>();world.enabled=false;world.units=new BattleUnit[0];world.points=new RecoveryPoint[0];rules=ScriptableObject.CreateInstance<CampaignRules>();rules.troopStrength=new[]{1000f,3000f,9000f,27000f,81000f};campaign=root.AddComponent<CampaignRuntime>();campaign.enabled=false;campaign.world=world;campaign.rules=rules;world.campaign=campaign;campaign.combined=root.AddComponent<CombinedArmsWorld>();campaign.combined.enabled=false;campaign.combined.world=world;campaign.logistics=root.AddComponent<LogisticsWorld>();campaign.logistics.enabled=false;campaign.logistics.world=world;}
  public BattleUnit Unit(string name,float people,Vector3 pos,string owner="B",UnitRole role=UnitRole.Infantry){var g=new GameObject(name);g.transform.SetParent(root.transform);g.transform.position=pos;var u=g.AddComponent<BattleUnit>();u.unitId=name;u.ownerId=owner;u.combatDefinition=ScriptableObject.CreateInstance<ArmyCombatDefinition>();u.combatDefinition.echelon=ArmyEchelon.Squad;u.combatDefinition.role=role;if(role==UnitRole.PersonnelCarrier)g.AddComponent<PersonnelCarrier>();campaign.ConfigureUnit(u);u.Initialize();u.Damage(u.Personnel-people);var units=new List<BattleUnit>(world.units){u};world.units=units.ToArray();return u;}
  public Fortification Fort(Vector3 pos,float health=100,string owner="B",float radius=.3f){var g=new GameObject("fort");g.transform.SetParent(root.transform);g.transform.position=pos;var f=g.AddComponent<Fortification>();f.ownerId=owner;f.radius=radius;f.Initialize(rules);f.SetLineHealth(health);campaign.combined.fortifications.Add(f);return f;}
  public void Shell(float budget=37.8f,float radius=1){campaign.Shell(Vector3.zero,radius,budget);}
  public void Dispose(){foreach(var u in root.GetComponentsInChildren<BattleUnit>())UnityEngine.Object.DestroyImmediate(u.combatDefinition);UnityEngine.Object.DestroyImmediate(root);UnityEngine.Object.DestroyImmediate(rules);}
 }
 public static void Run(){try{EditorSceneManager.OpenScene("Assets/Scenes/Battle01.unity");Validate();CampaignDemo.Editor.CampaignRevisionVerification.Validate();UnitLibraryRegression.Run();File.WriteAllText("shell-budget-verification.txt",results.Count+" shell budget checks passed\n"+string.Join("\n",results));Debug.Log("SHELL_BUDGET_VERIFIED "+results.Count);}catch(Exception e){File.WriteAllText("shell-budget-verification.txt",e+"\n"+string.Join("\n",results));Debug.LogException(e);EditorApplication.Exit(1);}}
 public static void Validate(){results.Clear();
  using(var f=new Fixture()){var a=f.Unit("A",60,Vector3.left*.1f);var b=f.Unit("B",40,Vector3.right*.1f);f.Shell();Near(a.Personnel,51,"60/40 exposed example A loses 9");Near(b.Personnel,34,"60/40 exposed example B loses 6");}
  using(var f=new Fixture()){var a=f.Unit("A",60,Vector3.left*.1f);var b=f.Unit("B",40,Vector3.right*.1f);var fort=f.Fort(Vector3.zero);f.Shell();Near(a.Personnel,55.5f,"protected example A loses 4.5");Near(b.Personnel,37,"protected example B loses 3");Near(fort.Health,81.1f,"protected example structure budget 18.9");}
  using(var f=new Fixture()){var a=f.Unit("A",60,Vector3.left*.3f);var b=f.Unit("B",40,Vector3.right*.3f);var fort=f.Fort(a.transform.position,100,"B",.1f);f.Shell();Near(a.Personnel,55.5f,"mixed cover only actual covered unit benefits");Near(b.Personnel,34,"mixed cover exposed unit keeps 15 percent limit");Near(fort.Health,88.66f,"mixed cover shares budget by protected strength");}
  using(var f=new Fixture()){var a=f.Unit("friend",60,Vector3.left*.1f,"A");var b=f.Unit("enemy",40,Vector3.right*.1f,"B");f.Shell(10);Near(a.Personnel,54,"below cap proportional friendly loss");Near(b.Personnel,36,"below cap proportional enemy loss");}
  using(var f=new Fixture()){var a=f.Unit("small",7,Vector3.zero);f.Shell(113.4f);Near(a.Personnel,5.95f,"regiment cannot one shot small squad");}
  using(var f=new Fixture()){var a=f.Unit("A",60,Vector3.left*.1f);var b=f.Unit("B",40,Vector3.right*.1f);var fort=f.Fort(Vector3.zero,1);f.Shell();Check(fort.Destroyed,"weak fort destroyed this round");Near(a.Personnel+b.Personnel,92.5f,"destroyed fort still protects impact snapshot");f.Shell();Near(a.Personnel+b.Personnel,78.625f,"next round has no destroyed fort protection");}
  using(var f=new Fixture()){f.Unit("A",100,Vector3.zero);var a=f.Fort(Vector3.zero);var b=f.Fort(Vector3.zero);f.Shell();Near(a.Health,90.55f,"two forts split first half");Near(b.Health,90.55f,"two forts split second half");Near(f.world.units[0].Personnel,92.5f,"overlapping cover does not stack casualty protection");}
  using(var f=new Fixture()){var a=f.Fort(Vector3.zero);var b=f.Fort(Vector3.zero);f.Shell();Near(a.Health+b.Health,181.1f,"empty area uses single half structure budget without transfer");}
  using(var f=new Fixture()){var a=f.Fort(Vector3.zero,100,"B",.1f);a.linePoints=new[]{Vector3.left*10,Vector3.right*10};var b=f.Fort(Vector3.zero,100,"B",.1f);b.linePoints=new[]{Vector3.left*.75f,Vector3.right*.75f};f.Shell(20,.65f);Near(CampaignShellResolver.ExposedLength(a,Vector3.zero,.65f),1.5f,"long line uses clipped impact length");Near(a.Health,95,"long and short same exposure get same share");Near(b.Health,95,"short line same exposure weight");}
  using(var f=new Fixture()){var a=f.Unit("A",100,Vector3.zero);var fort=f.Fort(Vector3.right*.8f,100,"B",.1f);f.Shell();Near(a.Personnel,85,"nearby uncovered fort does not protect whole blast");Near(fort.Health,100,"no protected people means no redistributed structure allocation");}
  using(var f=new Fixture()){var person=f.Unit("foot",60,Vector3.left*.1f,"A");var vehicle=f.Unit("car",100,Vector3.right*.1f,"A",UnitRole.PersonnelCarrier);var passenger=f.Unit("inside",7,vehicle.transform.position,"A");Check(vehicle.GetComponent<PersonnelCarrier>().Load(passenger).accepted,"passenger boards test carrier");f.Shell(6.1f);Near(person.Personnel,54,"vehicle counts one equivalent not 100 health");Near(vehicle.Personnel,90,"vehicle equivalent loss converted to health");Near(passenger.Personnel,6.3f,"passenger receives one proportional onboard loss");}
  using(var f=new Fixture()){var a=f.Unit("in",100,Vector3.zero);var b=f.Unit("out",100,Vector3.right*2);f.world.units=new[]{a,b,a};f.Shell(10);Near(a.Personnel,90,"duplicate references do not multiply damage or strength");Near(b.Personnel,100,"outside blast excluded");}
  using(var f=new Fixture()){f.Shell();Check(true,"empty impact safe");}
  using(var f=new Fixture()){
   var fort=f.Fort(Vector3.zero);var collectors=new List<ResourceCollector>();
   for(int i=0;i<2;i++){var g=new GameObject("collector");g.transform.SetParent(f.root.transform);var c=g.AddComponent<ResourceCollector>();c.Campaign=f.campaign;c.settings.footprint=Vector2.one*.6f;c.Initialize();f.campaign.logistics.collectors.Add(c);collectors.Add(c);}
   f.Shell(20);Near(fort.Health,100-10f/3,"empty fort shares material budget with collectors");Near(collectors[0].Health,500-10f/3,"collector one gets one share");Near(collectors[1].Health,500-10f/3,"collector two gets one share without multiplying output");
  }
  using(var f=new Fixture()){var a=f.Unit("A",100,Vector3.zero);var fort=f.Fort(Vector3.zero);fort.level=4;f.rules.fortArtilleryProtection=.99f;f.rules.fortLevelProtectionIncrement=.9f;f.Shell(10);Near(a.Personnel,95,"legacy protection and fort level do not add second reduction");Near(fort.Health,95,"below cap full cover splits budget fifty fifty");}
 }
}
