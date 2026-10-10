using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
namespace CampaignDemo.Editor
{
    public static class BattleVerification
    {
        static double start;static int stage,assertions;static BattleWorld sceneWorld;static readonly List<string> results=new List<string>();
        [InitializeOnLoadMethod]static void Resume(){if(!SessionState.GetBool("BattleVerify",false))return;start=EditorApplication.timeSinceStartup;EditorApplication.update+=Update;Application.logMessageReceived+=Log;}
        public static void ConfigureAndRun(){BattleSceneInstaller.Configure();Run();}
        public static void Run(){EditorSceneManager.OpenScene("Assets/Scenes/Battle01.unity");SessionState.SetBool("BattleVerify",true);start=EditorApplication.timeSinceStartup;EditorApplication.update+=Update;Application.logMessageReceived+=Log;EditorApplication.EnterPlaymode();}
        static void Log(string message,string trace,LogType type){if(type==LogType.Exception||type==LogType.Error){File.WriteAllText("battle-verification-error.txt",message+"\n"+trace);SessionState.SetBool("BattleVerify",false);EditorApplication.Exit(1);}}
        static void Check(bool valid,string label){assertions++;if(!valid)throw new Exception("Battle verification failed: "+label);results.Add("PASS "+label);}
        static void Equal(float a,float b,string label){Check(Mathf.Abs(a-b)<.02f,label);}
        static BattleUnit Unit(Transform root,string id,string owner,ArmyCombatDefinition definition,Vector3 position)
        {var g=new GameObject(id);g.transform.SetParent(root);g.transform.position=position;var renderer=g.AddComponent<SpriteRenderer>();renderer.sprite=sceneWorld.setup.sceneArmies[0].GetComponent<SpriteRenderer>().sprite;var motor=g.AddComponent<ArmyRouteMotor>();motor.Initialize(RoadRouteView.Create(root,id+" test route",.05f,4,Color.cyan));var u=g.AddComponent<BattleUnit>();u.unitId=id;u.ownerId=owner;u.combatDefinition=definition;return u;}
        static void Update()
        {
            if(EditorApplication.timeSinceStartup-start>100){Debug.LogError("Battle verification timed out");return;}
            if(!EditorApplication.isPlaying||Time.time<.6f)return;
            try
            {
                if(stage==0)
                {
                    sceneWorld=UnityEngine.Object.FindObjectOfType<BattleWorld>();Check(sceneWorld!=null&&sceneWorld.Initialized,"configured scene initialized");Tests();
                    sceneWorld.SetPlayer("A");sceneWorld.setup.modes.SetMode(MouseCommandMode.Interaction);var hud=UnityEngine.Object.FindObjectOfType<BattleHUD>();hud.RefreshPresentation();
                    foreach(var unit in sceneWorld.units){int circles=0;foreach(var line in unit.GetComponentsInChildren<LineRenderer>(true))if(line.gameObject.name=="Vision range"||line.gameObject.name=="Attack range"){if(line.enabled)circles++;}Check(circles==(unit.ownerId=="A"&&unit.OnMap?2:0),"inspection circles only for own deployed army "+unit.unitId);}
                    sceneWorld.setup.modes.SetMode(MouseCommandMode.RoadMarch);hud.RefreshPresentation();
                    foreach(var unit in sceneWorld.units)foreach(var line in unit.GetComponentsInChildren<LineRenderer>(true))if(line.gameObject.name=="Vision range"||line.gameObject.name=="Attack range")Check(!line.enabled,"road mode hides range "+unit.unitId);
                    sceneWorld.setup.modes.SetMode(MouseCommandMode.Interaction);hud.SelectUnit(sceneWorld.units[0]);stage=1;start=EditorApplication.timeSinceStartup;return;
                }
                if(stage==1&&EditorApplication.timeSinceStartup-start>.4)
                {
                    Capture(sceneWorld.setup.mapCamera,"战斗系统场景预览");
                    var hud=UnityEngine.Object.FindObjectOfType<BattleHUD>();hud.SelectUnit(sceneWorld.units[0]);var point=sceneWorld.points[0];Check(sceneWorld.RequestEnter(sceneWorld.units[0],point),"scene recovery entry command");sceneWorld.Tick(point.transitionSeconds+.1f);Check(!sceneWorld.units[0].OnMap,"scene army hidden inside recovery point");sceneWorld.units[0].BeginRepair();hud.SelectPoint(point);stage=2;start=EditorApplication.timeSinceStartup;return;
                }
                if(stage==2&&EditorApplication.timeSinceStartup-start>.4)
                {
                    Capture(sceneWorld.setup.mapCamera,"据点恢复场景预览");File.WriteAllText("battle-verification-result.txt",assertions+" assertions passed\n"+string.Join("\n",results));File.WriteAllText("C:/Users/ZWQ/Documents/Codex/2026-10-03/x20-mu-q/outputs/战斗系统验证结果.txt",assertions+" assertions passed\n"+string.Join("\n",results));Debug.Log("BATTLE_VERIFY_PASS: "+assertions+" assertions");SessionState.SetBool("BattleVerify",false);EditorApplication.Exit(0);
                }
            }catch(Exception exception){Debug.LogException(exception);}
        }
        static void Tests()
        {
            Equal(CombatMath.Strength(1),1,"100% strength");Equal(CombatMath.Strength(.75f),1,"75% boundary");Equal(CombatMath.Strength(.749f),.75f,"below 75%");Equal(CombatMath.Strength(.5f),.75f,"50% boundary");Equal(CombatMath.Strength(.499f),.5f,"below 50%");Equal(CombatMath.Strength(.25f),.5f,"25% boundary");Equal(CombatMath.Strength(.249f),.25f,"below 25%");Equal(CombatMath.Strength(.01f),.25f,"1% strength");Equal(CombatMath.Strength(0),0,"zero personnel");Check(CombatMath.FullPersonnel(7,3,4)==567,"regiment capacity");Check(CombatMath.FullPersonnel(7,3,5)==1701,"brigade capacity");
            var root=new GameObject("Isolated battle verification");var world=root.AddComponent<BattleWorld>();world.enabled=false;world.setup=sceneWorld.setup;world.currentPlayer="A";world.players=new[]{"A","B"};world.escapeGraceSeconds=.2f;
            var definition=ScriptableObject.CreateInstance<ArmyCombatDefinition>();definition.fullStrengthAttack=120;definition.defense=20;definition.attackRadius=1;definition.visionRadius=3;
            var a=Unit(root.transform,"test-A","A",definition,new Vector3(100,100));var b=Unit(root.transform,"test-B","B",definition,new Vector3(105,100));world.units=new[]{a,b};
            var pg=new GameObject("Test recovery");pg.transform.SetParent(root.transform);pg.transform.position=new Vector3(100,100);var point=pg.AddComponent<RecoveryPoint>();point.ownerId="A";point.captureSeconds=.5f;point.transitionSeconds=.2f;world.points=new[]{point};world.Initialize();
            Check(!world.IsVisible("A",b),"enemy outside vision hidden");b.transform.position=new Vector3(102.5f,100);world.Tick(.1f);Check(world.IsVisible("A",b)&&!world.IsKnown("A",b),"visible enemy initially unknown");Equal(a.Personnel,a.Capacity,"vision alone does not attack");
            Check(a.Motor.Commit(new[]{new RoadPoint(100,100),new RoadPoint(101,100)},()=>1),"deployed army accepts movement");sceneWorld.setup.modes.EnterInspectionMode();sceneWorld.setup.modes.EnterInspectionMode();Check(a.Motor.IsMoving&&!a.Motor.MovementLocked,"mode 3 preserves committed movement");Check(sceneWorld.setup.modes.DisplayLayer==TacticalMapLayer.ArmyStatus,"mode 3 is idempotent");a.Motor.Stop();
            b.transform.position=new Vector3(100.5f,100);world.Tick(.1f);Equal(a.Personnel,b.Personnel,"simultaneous symmetric casualties");float expected=a.Personnel;Check(a.State==ArmyBattleState.Engaged&&b.State==ArmyBattleState.Engaged,"automatic engagement locks both armies");Check(world.IsKnown("A",b)&&world.IsKnown("B",a),"first combat identifies both sides");Check(!a.Motor.Commit(new[]{new RoadPoint(100,100),new RoadPoint(101,100)},()=>1),"engaged movement command rejected");Check(!world.RequestEnter(a,point),"engaged army cannot hide in point");
            a.Initialize();b.Initialize();world.units=new[]{b,a};world.Tick(.1f);Equal(a.Personnel,expected,"attack result independent of array order");world.units=new[]{a,b};
            float before=a.Personnel;Check(world.RequestEscape(a),"escape accepted");Equal(a.Personnel,before*.9f,"escape loses ten percent of current personnel");Check(!world.RequestEscape(a),"repeat escape cannot charge twice");Check(!a.Motor.MovementLocked,"escape unlocks movement");world.Tick(.1f);Check(a.State==ArmyBattleState.Escaping,"escape grace prevents instant relock");world.Tick(.2f);Check(a.State==ArmyBattleState.Engaged,"enemy can reengage after escape grace");
            b.transform.position=new Vector3(105,100);world.Tick(.1f);Check(a.CanMove,"engagement ends when no enemy remains in range");Check(!world.IsVisible("A",b)&&world.IsKnown("A",b),"identification persists outside vision");b.transform.position=new Vector3(102.5f,100);Check(world.IsVisible("A",b)&&world.IsKnown("A",b),"returning enemy retains identified icon");b.transform.position=new Vector3(105,100);
            world.Intel.Identify("B",a);Check(world.RequestEnter(a,point),"friendly recovery point entry");world.Tick(.3f);Check(a.State==ArmyBattleState.InsidePoint&&!a.OnMap&&!a.CanFire,"garrison hidden and cannot attack");Check(!world.IsKnown("B",a),"entry clears enemy intelligence");
            point.resourceStock=1000;float personnel=a.Personnel,stock=point.resourceStock;Check(a.BeginRepair(),"repair begins");world.Tick(1);Check(a.Personnel>personnel&&point.resourceStock<stock,"repair restores personnel and spends stock");a.CancelRepair();Check(a.State==ArmyBattleState.InsidePoint,"repair cancellation");Check(a.BeginExit(),"exit command accepted");Check(!a.OnMap&&!a.CanFire,"exit transition remains outside combat");world.Tick(.3f);Check(a.OnMap&&a.CanMove&&a.Point==null,"completed exit returns army to map");b.transform.position=a.transform.position+new Vector3(2.5f,0);Check(world.IsVisible("B",a)&&!world.IsKnown("B",a),"exited army rediscovered as unknown");
            b.transform.position=new Vector3(105,100);a.transform.position=point.transform.position;Check(world.RequestEnter(a,point),"entry after exit");world.Tick(.3f);b.transform.position=point.transform.position;float enemyPersonnel=b.Personnel;world.Tick(.6f);Check(point.ownerId=="B"&&!a.Alive,"capture eliminates sheltered enemy army");Equal(b.Personnel,enemyPersonnel,"garrison never fires at captor");
            world.points=new RecoveryPoint[0];a.Initialize();b.Initialize();a.transform.position=new Vector3(104,104);b.transform.position=new Vector3(120,120);var grid=root.AddComponent<TerritoryControlSystem>();grid.world=world;grid.usePixelCellSize=false;grid.bounds=new Rect(98,98,10,10);grid.cellSize=2;grid.Initialize();world.territory=grid;grid.Resolve();Check(grid.OwnerAt(new Vector3(104,104))=="A","neutral cell controlled by present army");a.transform.position=new Vector3(120,120);grid.Resolve();Check(grid.OwnerAt(new Vector3(104,104))=="A","control persists after departure");b.transform.position=new Vector3(104,104);grid.Resolve();Check(grid.OwnerAt(new Vector3(104,104))=="B","superior enemy takes existing control");a.transform.position=b.transform.position;grid.Resolve();Check(grid.OwnerAt(new Vector3(104,104))=="B","equal personnel do not erase existing control");a.transform.position=b.transform.position=new Vector3(100,100);grid.Resolve();Check(grid.OwnerAt(new Vector3(100,100))=="","equal forces leave neutral cell neutral");int cell=grid.CellAt(new Vector3(104,104));sceneWorld.setup.zoomController.SetSize(3);Check(grid.CellAt(new Vector3(104,104))==cell,"control cells independent of camera zoom");sceneWorld.setup.zoomController.ResetToMiddle();
            var selection=sceneWorld.GetComponent<ArmySelection>();sceneWorld.SetPlayer("A");selection.SelectAt(sceneWorld.setup.mapCamera.WorldToScreenPoint(sceneWorld.units[3].transform.position));Check(selection.Selected==null||selection.Selected.GetComponent<BattleUnit>().ownerId=="A","enemy cannot receive player movement selection");sceneWorld.SetPlayer("B");Check(selection.Selected==null,"player switching clears private selection");sceneWorld.SetPlayer("A");
            UnityEngine.Object.DestroyImmediate(root);UnityEngine.Object.DestroyImmediate(definition);
        }
        static void Capture(Camera camera,string filename)
        {
            var canvas=GameObject.Find("Command UI").GetComponent<Canvas>();var previousMode=canvas.renderMode;canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=1;var target=new RenderTexture(1280,720,24);camera.targetTexture=target;camera.aspect=1280f/720;Canvas.ForceUpdateCanvases();canvas.GetComponent<BattleHUD>().RefreshPresentation();Canvas.ForceUpdateCanvases();camera.Render();var previous=RenderTexture.active;RenderTexture.active=target;var image=new Texture2D(1280,720,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1280,720),0,0);image.Apply();File.WriteAllBytes("C:/Users/ZWQ/Documents/Codex/2026-10-03/x20-mu-q/outputs/"+filename+".png",image.EncodeToPNG());camera.targetTexture=null;RenderTexture.active=previous;canvas.renderMode=previousMode;UnityEngine.Object.DestroyImmediate(image);target.Release();UnityEngine.Object.DestroyImmediate(target);
        }
    }
}
