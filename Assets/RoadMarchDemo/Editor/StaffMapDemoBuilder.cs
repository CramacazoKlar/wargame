using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
namespace CampaignDemo.Editor { public static class StaffMapDemoBuilder {
const string Data="Assets/Game/StaffMapDemo/";
        [MenuItem("Tools/态势地图/建立可编辑示例地图")]
        public static void Build()
        {
            if(!Application.isBatchMode&&!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;
            Directory.CreateDirectory(Data);EditorSceneManager.OpenScene("Assets/Scenes/TacticalSandbox.unity");EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(),"Assets/Scenes/StaffMapDemo.unity");
            var world=UnityEngine.Object.FindObjectOfType<BattleWorld>();var setup=world.setup;
            var road=Clone(setup.roadMap.definition,"Roads");var terrain=Clone(setup.freeMarchMap.definition,"Terrain");setup.roadMap.definition=road;setup.freeMarchMap.definition=terrain;
            road.backgroundSize=new Vector2(32,20);road.backgroundCenter=Vector2.zero;terrain.bounds=new Rect(-16,-10,32,20);road.showRoads=false;
            road.nodes=new[]{new Vector2(-15,8),new Vector2(-10,4),new Vector2(-14,1),new Vector2(-4,2),new Vector2(2,2.6f),new Vector2(11,4),new Vector2(15,8),new Vector2(-14,-7),new Vector2(-7,-4),new Vector2(0,-5),new Vector2(11,-5.5f),new Vector2(15,-7)};
            road.connections=new[]{new RoadMapDefinition.Connection(0,1),new RoadMapDefinition.Connection(1,2),new RoadMapDefinition.Connection(1,3),new RoadMapDefinition.Connection(3,4),new RoadMapDefinition.Connection(4,5),new RoadMapDefinition.Connection(5,6),new RoadMapDefinition.Connection(2,8),new RoadMapDefinition.Connection(7,8),new RoadMapDefinition.Connection(8,9),new RoadMapDefinition.Connection(9,10),new RoadMapDefinition.Connection(10,11),new RoadMapDefinition.Connection(5,10)};road.roadWidth=.24f;road.MarkDirty();
            var local=UnityEngine.Object.FindObjectOfType<LocalBattleMap>();local.roads=setup.roadMap;local.terrain=setup.freeMarchMap;local.baseRegions=new FreeMarchMapDefinition.Region[0];local.roadWidth=.24f;
            var river=StaffMapAuthoring.Spline(new[]{new Vector2(-6,11),new Vector2(-5.5f,7),new Vector2(-2,4.6f),new Vector2(.4f,2),new Vector2(2.2f,-.2f),new Vector2(5,-2),new Vector2(8,-5.5f),new Vector2(16.8f,-8.8f)},10);
            local.rivers=new[]{new LocalBattleMap.River{width=1.05f,points=river.ToArray()}};local.Rebuild();
            var author=local.GetComponent<StaffMapAuthoring>();if(author==null)author=local.gameObject.AddComponent<StaffMapAuthoring>();author.roads=setup.roadMap;author.terrain=setup.freeMarchMap;
            Vector2[] forests={new Vector2(-14,7),new Vector2(-11,8),new Vector2(-1,8),new Vector2(2,7.8f),new Vector2(8,4),new Vector2(13,1),new Vector2(12,-1),new Vector2(10,-7.8f),new Vector2(-7,-7.5f),new Vector2(-10,-1),new Vector2(-13,-2),new Vector2(-4,-7),new Vector2(3,-6),new Vector2(14,-6)};
            for(int i=0;i<forests.Length;i++)author.PaintForest(forests[i],i%3==0?2:1.35f);
            author.mountains=new List<StaffMapAuthoring.MountainStroke>{new StaffMapAuthoring.MountainStroke{id="southwest",name="西南山地",halfWidth=1.5f,points=new List<Vector2>{new Vector2(-14,-7.5f),new Vector2(-13,-5.2f),new Vector2(-10.5f,-5.8f)}},new StaffMapAuthoring.MountainStroke{id="northeast",name="北部高山",highMountain=true,halfWidth=1.7f,points=new List<Vector2>{new Vector2(5.5f,8.5f),new Vector2(8,7.4f),new Vector2(12,8.3f)}},new StaffMapAuthoring.MountainStroke{id="south",name="南部山地",halfWidth=1.15f,points=new List<Vector2>{new Vector2(-3.4f,-8),new Vector2(-.5f,-7.2f),new Vector2(1.8f,-8.4f)}}};author.SyncMountains();
            var cart=author.GetComponent<StaffMapCartography>();cart.resolution=2048;cart.wear=.62f;cart.visualRoadWidth=.085f;cart.Rebuild();
            var parent=world.units[0].transform.parent;foreach(var unit in world.units)UnityEngine.Object.DestroyImmediate(unit.gameObject);var units=new List<BattleUnit>();
            string[] roles={"Infantry","Scout","Artillery","Engineer","Tank","PersonnelCarrier","TransportTruck","MountainInfantry"};
            for(int side=0;side<2;side++)for(int i=0;i<roles.Length;i++)
            {
                var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Game/UnitLibrary/Prefabs/"+(side==0?"Friendly":"Enemy")+"/"+roles[i]+"_Company.prefab");var obj=(GameObject)PrefabUtility.InstantiatePrefab(prefab);obj.transform.SetParent(parent,false);
                obj.transform.position=side==0?new Vector3(-9+(i%3)*2.2f,1.6f-(i/3)*2.4f):new Vector3(5+(i%3)*2.7f,3-(i/3)*2.4f);
                var unit=obj.GetComponent<BattleUnit>();unit.unitId=(side==0?"A":"B")+"-STAFF-"+i;unit.ownerId=side==0?"A":"B";units.Add(unit);obj.GetComponent<ArmyRoadMover>().waterMap=null;
            }
            world.units=units.ToArray();setup.sceneArmies=units.Select(x=>x.GetComponent<ArmyRoadMover>()).ToArray();world.fogEnabled=false;world.revealTypesWithoutFog=true;
            var cityPositions=new[]{new Vector2(-10,4),new Vector2(11,4),new Vector2(12,-5.5f)};for(int i=0;i<world.points.Length;i++)world.points[i].transform.position=cityPositions[i%3];
            var forts=UnityEngine.Object.FindObjectsOfType<Fortification>();foreach(var fort in forts)UnityEngine.Object.DestroyImmediate(fort.gameObject);
            var line=new GameObject("示例 · 红方态势防线");var f=line.AddComponent<Fortification>();f.ownerId="B";f.lineGroupId="staff-demo-red";f.linePoints=new[]{new Vector3(.3f,6.4f),new Vector3(1.8f,4.6f),new Vector3(2.4f,2.8f),new Vector3(3.2f,1),new Vector3(6.8f,-.4f),new Vector3(9,-.5f)};f.engineerSide=new Vector3(9,8);f.builtLengthMeters=f.WorldLength*1000;line.AddComponent<StaffDefenseLine>().fort=f;
            setup.mapCamera.transform.position=new Vector3(0,0,-10);setup.mapCamera.orthographicSize=10.3f;setup.zoomController.minimumSize=2;setup.zoomController.maximumSize=16;setup.zoomController.startAtMiddle=false;
            setup.modes.SetMode(MouseCommandMode.Interaction);var focus=world.GetComponent<OpeningCameraFocus>();if(focus==null)focus=world.gameObject.AddComponent<OpeningCameraFocus>();focus.enabled=false;
            foreach(var ai in UnityEngine.Object.FindObjectsOfType<MonoBehaviour>())if(ai.GetType().Name.StartsWith("AI"))ai.enabled=false;
            EditorUtility.SetDirty(road);EditorUtility.SetDirty(terrain);EditorUtility.SetDirty(author);EditorUtility.SetDirty(local);AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        }
        static T Clone<T>(T original,string name)where T:UnityEngine.Object{string path=Data+name+".asset";var existing=AssetDatabase.LoadAssetAtPath<T>(path);if(existing!=null){EditorUtility.CopySerialized(original,existing);return existing;}var value=UnityEngine.Object.Instantiate(original);value.name=name;AssetDatabase.CreateAsset(value,path);return value;}

} }

