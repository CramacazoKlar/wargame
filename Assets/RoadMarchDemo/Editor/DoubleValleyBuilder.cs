using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
namespace CampaignDemo.Editor {
public static class DoubleValleyBuilder {
 const string Folder="Assets/Game/DoubleValley/";
 static Vector2 P(float x,float y){return new Vector2((x-9)*96/18,(y-6)*5);}
 static List<Vector2> nodes=new List<Vector2>(); static List<RoadMapDefinition.Connection> edges=new List<RoadMapDefinition.Connection>();
 static void Road(params float[] xy){var pts=new List<Vector2>();for(int i=0;i<xy.Length;i+=2)pts.Add(P(xy[i],xy[i+1]));var curve=StaffMapAuthoring.Spline(pts,5);int last=-1;foreach(var p in curve){int n=nodes.FindIndex(v=>Vector2.Distance(v,p)<.015f);if(n<0){n=nodes.Count;nodes.Add(p);}if(last>=0&&last!=n)edges.Add(new RoadMapDefinition.Connection(last,n));last=n;}}
 static T Clone<T>(T src,string name)where T:UnityEngine.Object{var a=UnityEngine.Object.Instantiate(src);a.name=name;AssetDatabase.CreateAsset(a,Folder+name+".asset");return a;}
 static FreeMarchMapDefinition.Region Hill(string name,float x,float y,float rx,float ry,TerrainKind kind,int seed){var pts=new Vector2[40];for(int i=0;i<pts.Length;i++){float a=i*Mathf.PI*2/pts.Length;float r=1+.17f*Mathf.Sin(a*3+seed)+.10f*Mathf.Cos(a*5-seed)+.055f*Mathf.Sin(a*9+seed);pts[i]=P(x+Mathf.Cos(a)*rx*r,y+Mathf.Sin(a)*ry*r);}return new FreeMarchMapDefinition.Region{name=name,terrain=kind,polygon=pts};}
 [MenuItem("Tools/态势地图/新建偏移双河谷九倍地图")]
 public static void Build(){
 if(AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/Scenes/DoubleValley9x.unity")!=null)throw new Exception("New map already exists; refusing to overwrite.");
 Directory.CreateDirectory(Folder);EditorSceneManager.OpenScene("Assets/Scenes/TacticalSandbox.unity");EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(),"Assets/Scenes/DoubleValley9x.unity");
 var world=UnityEngine.Object.FindObjectOfType<BattleWorld>();var setup=world.setup;var road=Clone(setup.roadMap.definition,"Roads");var terrain=Clone(setup.freeMarchMap.definition,"Terrain");setup.roadMap.definition=road;setup.freeMarchMap.definition=terrain;
 road.background=null;road.backgroundSize=new Vector2(96,60);road.backgroundCenter=Vector2.zero;road.showRoads=false;terrain.bounds=new Rect(-48,-30,96,60);terrain.defaultTerrain=TerrainKind.Ground;
 nodes.Clear();edges.Clear();
 Road(1,6,2.3f,8.5f,4.3f,9,5.5f,9.4f,7.2f,9.2f,8.2f,10.2f,9.7f,9.8f,10.7f,9.9f,12,10.2f,14,9.6f,15.2f,8.1f,16.5f,7.3f,17,6);
 Road(1,6,2,6.5f,3.3f,5.9f,5.3f,5.6f,6.5f,5.6f,8,6,9.2f,5.8f,10.6f,6,12.3f,5.7f,14,5.7f,15.3f,5.8f,17,6);
 Road(1,6,2.4f,5.7f,3.7f,5.1f,4.7f,4.2f,6.4f,3.6f,7.5f,3.1f,8.9f,2.1f,10.2f,2.6f,11.3f,3.1f,11.8f,3.8f,13.5f,4.2f,15.1f,4.8f,16.4f,5,17,6);
 Road(6.5f,5.6f,6.8f,5,7.7f,4.6f,8.5f,4.2f,9.4f,4.3f,10.5f,4.1f,11.8f,3.8f);
 Road(5.5f,9.4f,5.2f,8.5f,5.7f,7.3f,5.3f,5.6f);Road(4.7f,4.2f,4.3f,2.8f,5,2.1f,7.5f,2.5f,8.9f,2.1f);Road(14,5.7f,14.1f,5,13.5f,4.2f);Road(17,6,16.7f,4.7f,16,3.9f,14.6f,3.3f,14,2.6f);
 road.nodes=nodes.ToArray();road.connections=edges.ToArray();road.spawnNodes=new int[0];road.roadWidth=.9f;road.MarkDirty();
 var local=setup.roadMap.GetComponent<LocalBattleMap>();if(local==null)local=setup.roadMap.gameObject.AddComponent<LocalBattleMap>();local.roads=setup.roadMap;local.terrain=setup.freeMarchMap;local.roadWidth=.9f;
 float[] riverXY={9.9f,12.3f,10.2f,11.3f,9.6f,10.6f,9.7f,9.8f,9.4f,9.2f,8.9f,8.7f,9.1f,8.2f,9.8f,7.8f,9.3f,7.1f,9.15f,6.5f,9.2f,5.8f,9.45f,5.3f,9.15f,4.7f,9.4f,4.3f,10,3.7f,9.9f,3.2f,9.1f,2.5f,8.9f,2.1f,9,1.5f,9.5f,.7f,9.15f,-.3f};var rp=new List<Vector2>();for(int i=0;i<riverXY.Length;i+=2)rp.Add(P(riverXY[i],riverXY[i+1]));local.rivers=new[]{new LocalBattleMap.River{width=.65f,points=StaffMapAuthoring.Spline(rp,5).ToArray()}};
 var regions=new List<FreeMarchMapDefinition.Region>();regions.Add(Hill("苍屏山麓",6.4f,10.3f,1.7f,1.3f,TerrainKind.Mountain,4));regions.Add(Hill("苍屏主峰",6.4f,10.3f,1.03f,.79f,TerrainKind.HighMountain,4));regions.Add(Hill("龙脊山麓",12.8f,2.1f,1.9f,1.55f,TerrainKind.Mountain,7));regions.Add(Hill("龙脊主峰",12.8f,2.1f,1.15f,.94f,TerrainKind.HighMountain,7));regions.Add(Hill("鹿鸣丘陵",12.8f,9,1,.7f,TerrainKind.Mountain,2));regions.Add(Hill("青石岭",7,4.1f,.85f,.6f,TerrainKind.Mountain,5));
 float[] forest={1.2f,10.7f,1.4f,8.9f,3.8f,10.6f,4.6f,7.7f,7.4f,7.8f,10.9f,7.6f,14.3f,7.8f,15,10.9f,16.9f,9.9f,16.8f,8.2f,1.4f,2.5f,2.8f,1.5f,3.8f,.7f,3,3.7f,5,4.7f,5.1f,2.7f,8.2f,3.7f,10.8f,.8f,12.3f,4.9f,15.8f,1.1f,17.2f,2.3f};for(int i=0;i<forest.Length;i+=2)regions.Add(Hill("林区"+(i/2+1),forest[i],forest[i+1],.55f,.62f,TerrainKind.Forest,i));local.baseRegions=regions.ToArray();local.Rebuild();
 var cart=local.GetComponent<StaffMapCartography>();cart.resolution=3072;cart.visualRoadWidth=.24f;cart.wear=.15f;cart.foldStrength=.15f;cart.Rebuild();
 var original=world.points.First();foreach(var p in world.points.Skip(1))UnityEngine.Object.DestroyImmediate(p.gameObject);string[] names={"北麓口","中部桥口","柳湾浅滩","南河谷","北望营地","青石驿","东原驿","南谷前哨","东岭营地","白榆营地","西林营地","松溪营地"};float[] pos={9.7f,9.8f,9.2f,5.8f,9.4f,4.3f,8.9f,2.1f,8.3f,9.5f,7.7f,5.5f,11.2f,5.6f,10.5f,2.2f,15.5f,7.2f,15.6f,4.4f,2.3f,7.3f,2.3f,5};var points=new List<RecoveryPoint>();for(int i=0;i<names.Length;i++){var p=i==0?original:UnityEngine.Object.Instantiate(original,original.transform.parent);p.name=names[i];p.displayName=names[i];p.pointId="DV-"+(i+1).ToString("00");p.ownerId=i>=8?(i>=10?"A":"B"):"";p.transform.position=P(pos[i*2],pos[i*2+1]);points.Add(p);}world.points=points.ToArray();
 for(int i=0;i<world.units.Length;i++){var u=world.units[i];u.transform.position=P(u.ownerId=="A"?1.4f+(i%3)*.25f:16.4f+(i%3)*.25f,5.8f+(i%4)*.3f);var mover=u.GetComponent<ArmyRoadMover>();if(mover!=null)mover.waterMap=null;}
 setup.mapCamera.transform.position=new Vector3(0,0,-10);setup.mapCamera.orthographicSize=32;setup.zoomController.minimumSize=2;setup.zoomController.maximumSize=50;setup.zoomController.startAtMiddle=false;setup.panController.useBounds=true;setup.panController.cameraCenterBounds=terrain.bounds;var focus=world.GetComponent<OpeningCameraFocus>();if(focus!=null)focus.enabled=false;
 var builds=EditorBuildSettings.scenes.ToList();builds.Add(new EditorBuildSettingsScene("Assets/Scenes/DoubleValley9x.unity",true));EditorBuildSettings.scenes=builds.ToArray();EditorUtility.SetDirty(road);EditorUtility.SetDirty(terrain);EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());AssetDatabase.SaveAssets();
 File.WriteAllBytes("C:/Users/ZWQ/Documents/Codex/2026-10-19/c-desktop-wargame-9/outputs/DoubleValley9x.png",cart.Texture.EncodeToPNG());File.WriteAllText("C:/Users/ZWQ/Documents/Codex/2026-10-19/c-desktop-wargame-9/outputs/map-validation.txt","Scene: DoubleValley9x\nBaseline 32x20 = 640\nNew 96x60 = 5760; ratio = 9\nRoad nodes: "+nodes.Count+"\nNamed points: "+points.Count+"\nRegions: "+terrain.regions.Length+"\nBridges: "+cart.Bridges.Count);Debug.Log("DOUBLE_VALLEY_BUILD_OK");
 }
}}
