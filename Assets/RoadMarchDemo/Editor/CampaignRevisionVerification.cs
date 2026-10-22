using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
namespace CampaignDemo.Editor
{
    public static class CampaignRevisionVerification
    {
        static readonly List<string> results=new List<string>();
        static void Check(bool value,string label){if(!value)throw new Exception(label);results.Add("PASS "+label);}
        static void Near(float actual,float expected,float tolerance,string label){Check(Mathf.Abs(actual-expected)<=tolerance,label+" actual="+actual);}
        static BattleWorld scene;
        sealed class Fixture:IDisposable
        {
            public GameObject root;public BattleWorld world;public CampaignRuntime runtime;public CampaignRules rules;
            public Fixture(){root=new GameObject("Campaign verification");world=root.AddComponent<BattleWorld>();world.enabled=false;world.setup=scene.setup;world.points=new RecoveryPoint[0];world.players=new[]{"A","B"};world.currentPlayer="A";rules=ScriptableObject.CreateInstance<CampaignRules>();rules.troopStrength=new[]{100f,300f,900f,2700f,8100f,24300f,72900f,218700f};rules.lossPerStrengthPerSecond=.1f;rules.encirclementIncrement=0;runtime=root.AddComponent<CampaignRuntime>();runtime.enabled=false;runtime.world=world;runtime.rules=rules;runtime.combined=root.AddComponent<CombinedArmsWorld>();runtime.combined.enabled=false;runtime.combined.world=world;runtime.combined.engineering=ScriptableObject.CreateInstance<EngineeringDefinition>();runtime.logistics=root.AddComponent<LogisticsWorld>();runtime.logistics.enabled=false;runtime.logistics.world=world;world.campaign=runtime;}
            public BattleUnit Unit(string id,string player,UnitRole role,Vector3 position,float percent=100){var g=new GameObject(id);g.transform.SetParent(root.transform);g.transform.position=position;var u=g.AddComponent<BattleUnit>();u.unitId=id;u.ownerId=player;u.startingPersonnelPercent=percent;u.combatDefinition=ScriptableObject.CreateInstance<ArmyCombatDefinition>();u.combatDefinition.role=role;u.combatDefinition.echelon=ArmyEchelon.Squad;u.combatDefinition.attackRadius=5;u.combatDefinition.visionRadius=8;runtime.ConfigureUnit(u);u.Initialize();return u;}
            public void Units(params BattleUnit[] units){world.units=units;world.Initialize();}
            public void Dispose(){foreach(var f in runtime.combined.fortifications)if(f!=null)UnityEngine.Object.DestroyImmediate(f.gameObject);foreach(var d in runtime.depots)if(d!=null)UnityEngine.Object.DestroyImmediate(d.gameObject);UnityEngine.Object.DestroyImmediate(root);UnityEngine.Object.DestroyImmediate(rules);}
        }
        public static void Run(){try{CampaignRevisionInstaller.Configure();Validate();EditorApplication.Exit(0);}catch(Exception e){File.WriteAllText("campaign-revision-verification.txt",e+"\n"+string.Join("\n",results));Debug.LogException(e);EditorApplication.Exit(1);}}
        public static void Validate()
        {
            results.Clear();scene=UnityEngine.Object.FindObjectOfType<BattleWorld>();
            using(var f=new Fixture())
            {
                var a=f.Unit("A","A",UnitRole.Infantry,new Vector3(100,100));var b=f.Unit("B","B",UnitRole.Infantry,new Vector3(101,100),50);f.Units(a,b);
                f.world.Tick(1);Near(a.Personnel,95.4921f,.01f,"continuous aimed fire first second");Near(b.Personnel,40.2335f,.01f,"both sides take simultaneous attrition");
                for(int i=0;i<120;i++)f.world.Tick(.05f);Check(!b.Alive,"weaker army eliminated");Near(a.Personnel,Mathf.Sqrt(100*100-50*50),.05f,"unmodified battle follows square law survivor");
                a.Initialize();b.Initialize();for(int i=0;i<20;i++)f.world.Tick(.1f);float health=a.Personnel;a.Initialize();b.Initialize();for(int i=0;i<10;i++)f.world.Tick(.2f);Near(a.Personnel,health,.002f,"integration result insensitive to outer simulation tick");
                var motor=a.gameObject.AddComponent<ArmyRouteMotor>();var route=RoadRouteView.Create(a.transform,"test movement",.02f,1,Color.white);motor.Initialize(route);a.Initialize();b.Initialize();Check(motor.Commit(new[]{new RoadPoint(100,100),new RoadPoint(102,100)},()=>1),"route accepted");f.world.Tick(.1f);Check(motor.IsMoving&&a.CanMove,"encounter does not stop route or lock movement");Check(!a.Escape(.1f,1),"escape operation disabled in revised rules");
            }
            using(var f=new Fixture())
            {
                var a=f.Unit("A","A",UnitRole.Infantry,new Vector3(100,100));var near=f.Unit("near","B",UnitRole.Scout,new Vector3(101,100));var far=f.Unit("far","B",UnitRole.Scout,new Vector3(102,100));f.Units(a,near,far);f.world.Tick(1);Near(100-near.Personnel,7.5f,.002f,"nearest receives normalized 75 percent");Near(100-far.Personnel,2.5f,.002f,"farther receives normalized 25 percent");Near((100-near.Personnel)+(100-far.Personnel),10,.002f,"multiple targets cannot inflate base output");
                var flank=near.GetComponent<CampaignUnit>();flank.RecordMovement(Vector3.right);Near(flank.FlankFrom(near.transform.position+Vector3.right,f.rules),1,.001f,"front attack has base multiplier");Check(flank.FlankFrom(near.transform.position+Vector3.up,f.rules)>1,"moving facing exposes side");flank.Advance(f.rules.infantryFacingLingerSeconds/2);Check(flank.HasFacing,"infantry retains transient facing after stop");flank.Advance(f.rules.infantryFacingLingerSeconds);Check(!flank.HasFacing,"infantry loses facing after configured linger");near.combatDefinition.role=UnitRole.Tank;flank.RecordMovement(Vector3.right);flank.Advance(100);Check(flank.HasFacing,"tank retains facing while stationary");Near(flank.FlankFrom(near.transform.position-Vector3.right,f.rules),f.rules.tankRearMultiplier,.001f,"tank rear multiplier from configuration");
            }
            using(var f=new Fixture())
            {
                var a=f.Unit("A","A",UnitRole.Infantry,new Vector3(100,100));var b=f.Unit("B","A",UnitRole.Infantry,new Vector3(102,100));var t=f.Unit("target","B",UnitRole.Scout,new Vector3(101,100));f.Units(a,b,t);f.world.Tick(1);Near(t.Personnel,80,.005f,"many to one sums independent attackers");a.Initialize();b.Initialize();t.Initialize();f.rules.encirclementIncrement=.5f;f.rules.encirclementCap=1.3f;f.world.Tick(1);Near(t.Personnel,74,.01f,"multiple directions capped at configured multiplier");
            }
            using(var f=new Fixture())
            {
                var a=f.Unit("attack","A",UnitRole.Infantry,new Vector3(100,100));var b=f.Unit("guard","B",UnitRole.Scout,new Vector3(101,100));f.Units(a,b);var fort=f.runtime.combined.CreateFortification(b.transform.position,"B");float hp=fort.Health;f.world.Tick(1);Check(fort.Health<hp&&b.Personnel==100,"fortification takes ordinary attack before garrison");Check(f.runtime.combined.PermitMovement(a,new Vector3(100,100),new Vector3(102,100))&&!fort.Destroyed,"fort is passable and tank no longer instantly demolishes");Near(f.runtime.MovementMultiplier(a,a.transform.position,b.transform.position),f.rules.fortMovementMultiplier,.001f,"crossing fort uses configured slow");
                Check(f.world.GetComponent<BattleCommandService>().Submit(new BattleCommand{playerId="A",unitId=a.unitId,kind=BattleCommandKind.DismantleFortification,destination=fort.transform.position}).accepted&&a.GetComponent<CampaignUnit>().fortTarget==fort,"legacy dismantle command routes to health based attack");a.GetComponent<CampaignUnit>().fortTarget=null;
                a.Initialize();b.Initialize();fort.Initialize(f.rules);f.runtime.Shell(b.transform.position,1,20);Near(b.Personnel,95,.001f,"artillery splits fort and protected personnel damage");Near(fort.Health,f.rules.fortHealth-5,.001f,"artillery primary damage hits fort");var level1=f.runtime.combined.CreateFortification(new Vector3(104,100),"B",1);Check(level1.Health>f.rules.fortHealth,"higher fort level has configured greater health");
                level1.Demolish();a.combatDefinition.role=UnitRole.Scout;b.combatDefinition.role=UnitRole.Infantry;a.Initialize();b.Initialize();fort.Initialize(f.rules);f.world.Tick(1);Near(a.Personnel,100-10*f.rules.fortAttackMultiplier,.002f,"living fort increases garrison firepower");fort.Demolish();a.Initialize();b.Initialize();f.world.Tick(1);Near(a.Personnel,90,.002f,"destroyed fort removes garrison output multiplier");Near(f.runtime.MovementMultiplier(a,new Vector3(90,100),new Vector3(91,100)),1,.001f,"distant fort does not slow unrelated movement segment");
                fort.Demolish();a.Initialize();b.Initialize();f.runtime.Shell(new Vector3(100.5f,100),1,20);Near(a.Personnel,90,.001f,"shell shares budget equally with friend");Near(b.Personnel,90,.001f,"shell shares budget equally with foe");
            }
            using(var f=new Fixture())
            {
                var car=f.Unit("car","A",UnitRole.PersonnelCarrier,new Vector3(100,100));var carrier=car.gameObject.AddComponent<PersonnelCarrier>();var p=f.Unit("passenger","A",UnitRole.Infantry,new Vector3(100.1f,100),5);f.Units(car,p);Near(car.Capacity,f.rules.vehicleHealthPerEquivalent,.001f,"transport squad counts one vehicle equivalent");Check(carrier.Load(p).accepted,"nonmechanized unit boards carrier");float before=p.Personnel;car.Damage(car.Personnel*.25f);Near(p.Personnel,before*.75f,.001f,"passengers lose same fraction when carrier damaged");car.Damage(10000);Check(!p.Alive,"carrier terminal damage also eliminates passengers");
            }
            using(var f=new Fixture())
            {
                var car=f.Unit("truck","A",UnitRole.Logistics,new Vector3(100,100));var truck=car.gameObject.AddComponent<TransportTruck>();var enemy=f.Unit("enemy","B",UnitRole.Scout,new Vector3(110,100));f.Units(car,enemy);var cityObj=new GameObject("city");cityObj.transform.SetParent(f.root.transform);cityObj.transform.position=car.transform.position;var city=cityObj.AddComponent<RecoveryPoint>();city.pointId="city";city.ownerId="A";city.resourceStock=80;f.world.points=new[]{city};
                Check(f.runtime.Transfer(truck,city,true,60).accepted,"manual partial loading");Near(truck.Cargo,60,.001f,"manual cargo amount");Near(city.resourceStock,20,.001f,"source stock conserved");Check(!f.runtime.Transfer(truck,city,true,21).accepted,"request cannot exceed available source stock");Check(f.runtime.Transfer(truck,city,false,30).accepted,"manual partial unloading");Near(city.resourceStock+truck.Cargo,80,.001f,"resource conservation across transfers");city.transform.position+=Vector3.right*10;Check(!f.runtime.Transfer(truck,city,true,1).accepted,"remote transfer rejected");float cargo=truck.Cargo;f.runtime.logistics.Tick(1);Near(truck.Cargo,cargo,.001f,"legacy auto fill and unload disabled");
                var depot=f.runtime.CreateDepot(car.transform.position,"B",120,false);Check(f.runtime.TransferMaximum(truck,depot,true)==0,"enemy depot cannot be looted without capture");enemy.transform.position=depot.transform.position;car.transform.position+=Vector3.right*10;f.runtime.Tick(f.rules.warehouseCaptureSeconds+.1f);Check(depot.ownerId=="B","owner defender retains warehouse");depot.ownerId="A";f.runtime.Tick(f.rules.warehouseCaptureSeconds+.1f);Check(depot.ownerId=="B"&&depot.stock==120,"capture transfers intact local stock");Check(!f.runtime.RemoveDepot("A",depot).accepted,"enemy cannot destroy warehouse");Check(f.runtime.RemoveDepot("B",depot).accepted&&depot.Removed,"owner may dismantle warehouse");var pile=f.runtime.depots.Find(d=>d!=null&&d.resourcePile);Check(pile!=null&&pile.stock==120,"dismantled stock remains capturable resource pile");
            }
            using(var f=new Fixture())
            {
                var a=f.Unit("artillery","A",UnitRole.Artillery,new Vector3(100,100));var target=f.Unit("friend","A",UnitRole.Scout,new Vector3(101,100));f.Units(a,target);var battery=a.gameObject.AddComponent<ArtilleryBattery>();battery.definition=ScriptableObject.CreateInstance<ArtilleryDefinition>();battery.definition.delaySeconds=1;battery.definition.damage=10;battery.definition.requireVision=false;Check(battery.Ready&&a.CanMove,"stationary artillery can fire or move without setup");Check(battery.Fire(f.world,"A",target.transform.position).accepted,"manual artillery fire");float before=target.Personnel;f.runtime.combined.Tick(.5f);Near(target.Personnel,before,.001f,"no artillery injury before configured delay");f.runtime.combined.Tick(.6f);Near(target.Personnel,before-15,.001f,"delayed shell caps friendly casualties");Check(a.CanMove,"firing does not require packing to move");
            }
            Check(scene.GetComponent<CampaignRuntime>()!=null,"scene has revised runtime");var hud=UnityEngine.Object.FindObjectOfType<CampaignHUD>();Check(hud!=null,"unified HUD installed");Check(!hud.GetComponent<BattleHUD>().enabled&&!hud.GetComponent<CombinedArmsHUD>().enabled,"all three old scattered interfaces disabled");
            File.WriteAllText("campaign-revision-verification.txt",results.Count+" checks passed\n"+string.Join("\n",results));Debug.Log("CAMPAIGN_REVISION_VERIFY_PASS "+results.Count);
        }
    }
}


