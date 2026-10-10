using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
namespace CampaignDemo.Editor
{
    public static class CombinedArmsVerification
    {
        static readonly List<string> results=new List<string>();
        static void Check(bool value,string name){if(!value)throw new Exception(name);results.Add("PASS "+name);}
        static BattleUnit Unit(Transform root,string id,string owner,UnitRole role,Vector3 p)
        {var g=new GameObject(id);g.transform.SetParent(root);g.transform.position=p;var d=ScriptableObject.CreateInstance<ArmyCombatDefinition>();d.role=role;d.echelon=ArmyEchelon.Squad;d.fullStrengthAttack=0;d.defense=0;d.visionRadius=5;var u=g.AddComponent<BattleUnit>();u.unitId=id;u.ownerId=owner;u.combatDefinition=d;var m=g.AddComponent<ArmyRouteMotor>();m.Initialize(new GameObject(id+" route").AddComponent<RoadRouteView>());return u;}
        public static void Run()
        {
            try{CombinedArmsInstaller.Configure();Tests();File.WriteAllText("combined-arms-verification.txt",results.Count+" checks passed\n"+string.Join("\n",results));Debug.Log("COMBINED_ARMS_VERIFY_PASS: "+results.Count);EditorApplication.Exit(0);}
            catch(Exception e){File.WriteAllText("combined-arms-verification.txt",e.ToString()+"\n"+string.Join("\n",results));Debug.LogException(e);EditorApplication.Exit(1);}
        }
        public static void Validate(){Tests();File.WriteAllText("combined-arms-verification.txt",results.Count+" checks passed\n"+string.Join("\n",results));}
        static void Tests()
        {
            var sceneWorld=UnityEngine.Object.FindObjectOfType<BattleWorld>();Check(sceneWorld.GetComponent<CombinedArmsWorld>()!=null,"scene system configured");Check(UnityEngine.Object.FindObjectOfType<CombinedArmsHUD>()!=null,"right panel extension configured");
            var root=new GameObject("Combined arms test");var w=root.AddComponent<BattleWorld>();w.enabled=false;w.setup=sceneWorld.setup;w.points=new RecoveryPoint[0];w.players=new[]{"A","B"};w.currentPlayer="A";
            var s=root.AddComponent<CombinedArmsWorld>();s.enabled=false;s.world=w;s.engineering=ScriptableObject.CreateInstance<EngineeringDefinition>();s.engineering.buildSeconds=1;
            var infantry=Unit(root.transform,"infantry","A",UnitRole.Infantry,new Vector3(100,100));var artillery=Unit(root.transform,"artillery","A",UnitRole.Artillery,new Vector3(100.1f,100));var engineer=Unit(root.transform,"engineer","A",UnitRole.Engineer,new Vector3(100.2f,100));var scout=Unit(root.transform,"scout","A",UnitRole.Scout,new Vector3(100.3f,100));var vehicle=Unit(root.transform,"carrier","A",UnitRole.PersonnelCarrier,new Vector3(100,100));var enemy=Unit(root.transform,"enemy","B",UnitRole.Infantry,new Vector3(102,100));var tank=Unit(root.transform,"tank","A",UnitRole.Tank,new Vector3(100,102));
            var c=vehicle.gameObject.AddComponent<PersonnelCarrier>();c.capacity=100;var b=artillery.gameObject.AddComponent<ArtilleryBattery>();var ad=ScriptableObject.CreateInstance<ArtilleryDefinition>();ad.damage=2;ad.delaySeconds=1;ad.cooldownSeconds=3;b.definition=ad;s.supportDefinition=ad;
            w.units=new[]{infantry,artillery,engineer,scout,vehicle,enemy,tank};w.Initialize();var commands=w.GetComponent<BattleCommandService>();
            Check(c.Health==200,"vehicle uses health independent of crew strength");Check(!vehicle.CanFire&&!artillery.CanFire,"carrier and artillery excluded from automatic fire");
            Check(c.Load(infantry).accepted&&c.Load(artillery).accepted&&c.Load(engineer).accepted&&c.Load(scout).accepted,"all four permitted roles board");Check(!infantry.OnMap&&!infantry.CanMove&&!infantry.CanFire,"passenger removed from map movement and combat");
            Check(!c.Load(enemy).accepted&&!c.Load(tank).accepted,"enemy and tank cannot board");Check(!c.Load(infantry).accepted,"duplicate boarding rejected");Check(c.SpeedMultiplier==.5f,"artillery reduces speed by 50 percent");c.artilleryReductionPercent=30;Check(Mathf.Abs(c.SpeedMultiplier-.7f)<.001f,"artillery penalty configurable");
            var map=w.setup.freeMarchMap;var savedMap=w.setup.freeMarchMap;
            // Use actual map positions for exit geometry, not the isolated combat coordinates.
            vehicle.transform.position=sceneWorld.units[0].transform.position;c.SyncPassengers();
            Check(c.Unload(artillery,w).accepted&&artillery.OnMap,"instant unload restores map unit");Check(c.SpeedMultiplier==1,"unloading final artillery restores speed");Check(c.Load(artillery).accepted,"immediate reboarding has no cooldown");
            c.KillPassengers();Check(!infantry.Alive&&!artillery.Alive&&!engineer.Alive&&!scout.Alive&&c.Passengers.Count==0,"all passengers killed together");
            infantry.Initialize();artillery.Initialize();engineer.Initialize();scout.Initialize();vehicle.Initialize();vehicle.transform.position=new Vector3(100,100);infantry.transform.position=vehicle.transform.position;artillery.transform.position=new Vector3(100.1f,100);engineer.transform.position=new Vector3(100.2f,100);scout.transform.position=new Vector3(100.3f,100);
            Check(c.Load(infantry).accepted,"boarding after reset");vehicle.Damage(10000);Check(!vehicle.Alive&&!infantry.Alive,"actual vehicle death kills passengers");
            Check(!commands.Submit(new BattleCommand{kind=BattleCommandKind.ArtilleryFire,playerId="B",unitId=artillery.unitId,destination=enemy.transform.position}).accepted,"ownership enforced for artillery");
            Check(b.Ready&&b.Fire(w,"A",enemy.transform.position).accepted,"manual artillery fires");float before=enemy.Personnel;Check(!b.Fire(w,"A",enemy.transform.position).accepted,"cooldown rejects second shot");s.Tick(.5f);Check(enemy.Personnel==before,"shell waits for delay");s.Tick(.6f);Check(enemy.Personnel<before,"shell applies area damage");s.Tick(2);Check(b.Ready,"cooldown expires");
            Check(!b.Fire(w,"A",new Vector3(1000,100)).accepted,"out of range rejected");Check(!b.Fire(w,"A",new Vector3(107,100)).accepted,"target requires friendly vision");Check(!b.Fire(w,"A",new Vector3(float.NaN,100)).accepted,"invalid coordinates rejected");
            Check(s.Support("A",enemy.transform.position).accepted&&!s.Support("A",enemy.transform.position).accepted,"support cooldown per player");Check(s.SupportRemaining("B")==0,"switching player does not share cooldown");
            var f=s.CreateFortification(new Vector3(101,102),"B");Check(!s.PermitMovement(engineer,new Vector3(100,102),new Vector3(102,102)),"non tank blocked by swept segment");Check(s.PermitMovement(tank,new Vector3(100,102),new Vector3(102,102))&&f.Destroyed,"tank crossing destroys fortification");
            engineer.transform.position=new Vector3(100,100);var wall=s.CreateFortification(new Vector3(100.6f,100),"A");Check(s.Dismantle(engineer,wall.transform.position).accepted,"engineer starts demolition");Check(engineer.Working&&!engineer.CanMove,"construction locks movement");float engineerTime=s.WorkRemaining(engineer);s.Cancel(engineer);Check(!engineer.Working,"cancel unlocks movement");
            infantry.Initialize();infantry.transform.position=engineer.transform.position;Check(s.Dismantle(infantry,wall.transform.position).accepted,"ordinary infantry dismantles");Check(s.WorkRemaining(infantry)>engineerTime*4,"engineer demolition substantially faster");s.Cancel(infantry);Check(s.Dismantle(engineer,wall.transform.position).accepted,"engineer restarts demolition");s.Tick(engineerTime+.1f);Check(wall.Destroyed&&!engineer.Working,"demolition completes and unlocks");
            engineer.transform.position=sceneWorld.units[0].transform.position;Vector3 buildAt=engineer.transform.position+new Vector3(0,.65f);int count=s.fortifications.Count;Check(s.Build(engineer,buildAt).accepted,"engineer starts nearby construction");s.Tick(1.1f);Check(s.fortifications.Count==count+1&&!engineer.Working,"construction creates blocking fortification");
            Check(!s.Build(infantry,buildAt).accepted,"non engineer cannot construct");
            foreach(var fort in s.fortifications)if(fort!=null)UnityEngine.Object.DestroyImmediate(fort.gameObject);
            UnityEngine.Object.DestroyImmediate(root);
        }
    }
}
