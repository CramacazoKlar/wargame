using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
namespace CampaignDemo.Editor {
 public static class LocalTacticalInstaller {
  const string Data="Assets/Game/TacticalSandbox/";
  static T Copy<T>(T source,string name)where T:UnityEngine.Object{var path=Data+name+".asset";var existing=AssetDatabase.LoadAssetAtPath<T>(path);if(existing!=null)return existing;var value=UnityEngine.Object.Instantiate(source);value.name=name;AssetDatabase.CreateAsset(value,path);return value;}
  static Vector2[] Box(float x,float y,float w,float h){return new[]{new Vector2(x,y),new Vector2(x+w,y),new Vector2(x+w,y+h),new Vector2(x,y+h)};}
  [MenuItem("Campaign Demo/Create offline TacticalSandbox")]
  public static void Build(){
   Directory.CreateDirectory(Data);EditorSceneManager.OpenScene("Assets/Scenes/Battle01.unity");
   // Save under a fresh identity before changing any scene objects or data assets.
   EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(),"Assets/Scenes/TacticalSandbox.unity");
   var world=UnityEngine.Object.FindObjectOfType<BattleWorld>();var setup=world.setup;setup.showCommittedRoutes=true;setup.showDebugUI=false;
   var road=Copy(setup.roadMap.definition,"Roads");road.nodes=new[]{new Vector2(-14,-5),new Vector2(-7,-5),new Vector2(0,-5),new Vector2(7,-5),new Vector2(14,-5),new Vector2(-14,4),new Vector2(-7,4),new Vector2(0,4),new Vector2(7,4),new Vector2(14,4),new Vector2(-7,0),new Vector2(7,0)};
   road.connections=new[]{new RoadMapDefinition.Connection(0,1),new RoadMapDefinition.Connection(1,2),new RoadMapDefinition.Connection(2,3),new RoadMapDefinition.Connection(3,4),new RoadMapDefinition.Connection(5,6),new RoadMapDefinition.Connection(6,7),new RoadMapDefinition.Connection(7,8),new RoadMapDefinition.Connection(8,9),new RoadMapDefinition.Connection(1,10),new RoadMapDefinition.Connection(10,6),new RoadMapDefinition.Connection(3,11),new RoadMapDefinition.Connection(11,8),new RoadMapDefinition.Connection(10,11)};
   road.background=Background();road.backgroundCenter=Vector2.zero;road.backgroundSize=new Vector2(32,20);road.roadWidth=.6f;road.showRoads=false;road.MarkDirty();setup.roadMap.definition=road;setup.roadMap.transform.position=Vector3.zero;setup.roadMap.transform.localScale=Vector3.one;
   var terrain=Copy(setup.freeMarchMap.definition,"Terrain");terrain.bounds=new Rect(-16,-10,32,20);terrain.defaultTerrain=TerrainKind.Ground;terrain.regions=new[]{new FreeMarchMapDefinition.Region{name="西侧林区",terrain=TerrainKind.Forest,polygon=Box(-12,1,3,2)},new FreeMarchMapDefinition.Region{name="东侧林区",terrain=TerrainKind.Forest,polygon=Box(9,-3,4,2)},new FreeMarchMapDefinition.Region{name="北方高地",terrain=TerrainKind.Mountain,polygon=Box(-4,6,6,3)}};terrain.MarkDirty();setup.freeMarchMap.definition=terrain;setup.freeMarchMap.waterMap=null;setup.freeMarchMap.roadTerrainWidth=.6f;
   foreach(var water in UnityEngine.Object.FindObjectsOfType<RasterWaterMap>())UnityEngine.Object.DestroyImmediate(water);
   foreach(var a in UnityEngine.Object.FindObjectsOfType<ArmyRoadMover>())a.waterMap=null;
   var map=setup.roadMap.gameObject.AddComponent<LocalBattleMap>();map.terrain=setup.freeMarchMap;map.roads=setup.roadMap;map.baseRegions=terrain.regions;map.rivers=new[]{new LocalBattleMap.River{width=.8f,points=new[]{new Vector2(1,-10),new Vector2(1,-3),new Vector2(3,2),new Vector2(3,10)}}};map.Rebuild();
   var own=Copy(setup.ownershipMap.definition,"Ownership");own.regions=new[]{new OwnershipMapDefinition.Region{name="蓝方出发区",polygon=Box(-16,-10,16,20),state=OwnershipState.Occupied,ownerId="A"},new OwnershipMapDefinition.Region{name="红方出发区",polygon=Box(0,-10,16,20),state=OwnershipState.Occupied,ownerId="B"}};setup.ownershipMap.definition=own;
   var campaign=world.GetComponent<CampaignRuntime>();campaign.rules=Copy(campaign.rules,"CampaignRules");campaign.combined.engineering=Copy(campaign.combined.engineering,"Engineering");campaign.combined.engineering.maximumLineWorldLength=4;
   // Keep existing configured unit prefabs and combat definitions, but lay out a new battlefield.
   var armyRoot=world.units[0].transform.parent;foreach(var u in world.units)UnityEngine.Object.DestroyImmediate(u.gameObject);
   var newUnits=new List<BattleUnit>();string[] types={"Infantry","Infantry","Tank","Engineer","Engineer","Artillery","TransportTruck","PersonnelCarrier"};
   for(int side=0;side<2;side++)for(int i=0;i<types.Length;i++){var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Game/UnitLibrary/Prefabs/"+(side==0?"Friendly":"Enemy")+"/"+types[i]+"_Company.prefab");if(prefab==null)throw new Exception("Missing unit prefab "+types[i]);var g=(GameObject)PrefabUtility.InstantiatePrefab(prefab);g.transform.SetParent(armyRoot);g.transform.position=new Vector3((side==0?-11:9)+(i%3)*1.1f,-5+(i/3)*1.4f);var u=g.GetComponent<BattleUnit>();u.unitId=(side==0?"A":"B")+"-LOCAL-"+i;u.ownerId=side==0?"A":"B";u.designation=(side==0?"蓝方":"红方")+" "+u.combatDefinition.unitType+" "+(i+1);u.startingPersonnelPercent=100;var mover=g.GetComponent<ArmyRoadMover>();mover.waterMap=null;mover.overrideSpeed=false;newUnits.Add(u);}
   world.units=newUnits.ToArray();setup.sceneArmies=newUnits.Select(u=>u.GetComponent<ArmyRoadMover>()).ToArray();
   int pointIndex=0;foreach(var city in world.points){city.transform.position=city.ownerId=="A"?new Vector3(-13,-5):city.ownerId=="B"?new Vector3(13,4):new Vector3(-2,0);city.resourceStock=15000;pointIndex++;}
   var logistics=world.GetComponent<LogisticsWorld>();for(int i=0;i<logistics.sites.Length;i++){var site=logistics.sites[i];site.transform.position=i%2==0?new Vector3(-7,-5):new Vector3(7,4);site.areaSize=new Vector2(2,1.7f);site.stock=20000;site.displayName="资源区 "+(i+1);}
   if(world.territory!=null){world.territory.bounds=terrain.bounds;world.territory.cellSize=1;}
   world.gameObject.AddComponent<LocalTacticalBattle>();world.gameObject.AddComponent<LocalMapSettings>();
   setup.modes.SetMode(MouseCommandMode.RoadMarch);setup.mapCamera.transform.position=new Vector3(1.8f,0,-10);setup.mapCamera.orthographicSize=10;setup.zoomController.minimumSize=2;setup.zoomController.maximumSize=12;setup.zoomController.startAtMiddle=false;
   if(setup.zoomController.zoomSlider!=null)UnityEngine.Object.DestroyImmediate(setup.zoomController.zoomSlider.gameObject);if(setup.zoomController.resetNode!=null)UnityEngine.Object.DestroyImmediate(setup.zoomController.resetNode.gameObject);setup.zoomController.zoomSlider=null;setup.zoomController.resetNode=null;
   var commandBar=GameObject.Find("Command bar");if(commandBar!=null)UnityEngine.Object.DestroyImmediate(commandBar);
   var hud=UnityEngine.Object.FindObjectOfType<CampaignHUD>();hud.uiFontSize=11;hud.rightPanelWidth=280;
   foreach(var overview in UnityEngine.Object.FindObjectsOfType<BattleOverviewHUD>())UnityEngine.Object.DestroyImmediate(overview);
   foreach(var old in UnityEngine.Object.FindObjectsOfType<OpeningCameraFocus>())UnityEngine.Object.DestroyImmediate(old);
   var presenter=world.GetComponent<CombatEffectPresenter>();if(presenter==null)presenter=world.gameObject.AddComponent<CombatEffectPresenter>();var effects=world.gameObject.AddComponent<LocalCombatEffects>();presenter.world=world;presenter.effectPlayer=effects;
   foreach(var b in UnityEngine.Object.FindObjectsOfType<BattleHUD>())b.enabled=false;foreach(var b in UnityEngine.Object.FindObjectsOfType<CombinedArmsHUD>())b.enabled=false;
   foreach(var text in UnityEngine.Object.FindObjectsOfType<UnityEngine.UI.Text>())if(text.text.Contains("3 ")||text.text.Contains("军事地图"))text.gameObject.SetActive(false);
   // New local scene is the first play/build entry; old and network scenes stay available.
   EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene("Assets/Scenes/TacticalSandbox.unity",true)}.Concat(EditorBuildSettings.scenes.Where(s=>s.path!="Assets/Scenes/TacticalSandbox.unity")).ToArray();
   EditorUtility.SetDirty(road);EditorUtility.SetDirty(terrain);EditorUtility.SetDirty(own);EditorUtility.SetDirty(campaign.combined.engineering);AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
   File.WriteAllText("local-sandbox-build.txt","TacticalSandbox created; units="+world.units.Length+"; sites="+logistics.sites.Length+"; cities="+world.points.Length);Debug.Log("LOCAL_SANDBOX_CREATED");
  }
  static Sprite Background(){string path=Data+"TacticalMap.png";var t=new Texture2D(1024,640);for(int y=0;y<640;y++)for(int x=0;x<1024;x++){Color c=Color.white;t.SetPixel(x,y,c);}t.Apply();File.WriteAllBytes(path,t.EncodeToPNG());UnityEngine.Object.DestroyImmediate(t);AssetDatabase.ImportAsset(path);var imp=(TextureImporter)AssetImporter.GetAtPath(path);imp.textureType=TextureImporterType.Sprite;imp.spritePixelsPerUnit=32;imp.SaveAndReimport();return AssetDatabase.LoadAssetAtPath<Sprite>(path);}
 }
}
