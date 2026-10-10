using System;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Events;
namespace CampaignDemo.Editor
{
    public static class ProjectBootstrap
    {
        const string Data="Assets/Game/Data/";
        static Vector2[] Box(float x,float y,float w,float h) { return new[]{new Vector2(x,y),new Vector2(x+w,y),new Vector2(x+w,y+h),new Vector2(x,y+h)}; }
        static T Asset<T>(string name) where T:ScriptableObject { var existing=AssetDatabase.LoadAssetAtPath<T>(Data+name+".asset"); if(existing!=null)return existing; var a=ScriptableObject.CreateInstance<T>(); AssetDatabase.CreateAsset(a,Data+name+".asset"); return a; }
        [MenuItem("Campaign Demo/Create configured Battle01")]
        public static void Build()
        {
            Directory.CreateDirectory(Data); Directory.CreateDirectory("Assets/Scenes"); Directory.CreateDirectory("Assets/Game/Art");
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var road=Asset<RoadMapDefinition>("Map01");
            road.nodes=new[]{new Vector2(-8,-2),new Vector2(-4,-2),new Vector2(0,-2),new Vector2(4,-2),new Vector2(8,-2),new Vector2(0,2),new Vector2(0,4),new Vector2(-4,2),new Vector2(4,2)};
            road.connections=new[]{new RoadMapDefinition.Connection(0,1),new RoadMapDefinition.Connection(1,2),new RoadMapDefinition.Connection(2,3),new RoadMapDefinition.Connection(3,4),new RoadMapDefinition.Connection(2,5),new RoadMapDefinition.Connection(5,6),new RoadMapDefinition.Connection(1,7),new RoadMapDefinition.Connection(7,5),new RoadMapDefinition.Connection(5,8),new RoadMapDefinition.Connection(8,3)};
            road.roadColor=new Color(.47f,.35f,.22f); road.roadWidth=.12f; road.backgroundSize=new Vector2(20,12); road.background=MapSprite();
            var terrain=Asset<FreeMarchMapDefinition>("Map01Terrain"); terrain.bounds=new Rect(-10,-6,20,12);
            terrain.regions=new[]{new FreeMarchMapDefinition.Region{name="Mountain",terrain=TerrainKind.Mountain,polygon=Box(-8,1,3,3)},new FreeMarchMapDefinition.Region{name="Grass",terrain=TerrainKind.Grass,polygon=Box(2,1,5,3)},new FreeMarchMapDefinition.Region{name="Water",terrain=TerrainKind.Water,polygon=Box(5,-6,1,12)},new FreeMarchMapDefinition.Region{name="Bridge",terrain=TerrainKind.Road,polygon=Box(5,-2.3f,1,.6f)}};
            var ownership=Asset<OwnershipMapDefinition>("Map01Ownership"); ownership.regions=new[]{new OwnershipMapDefinition.Region{name="West sector",polygon=Box(-10,-6,10,12),state=OwnershipState.Occupied,ownerId="A"},new OwnershipMapDefinition.Region{name="East sector",polygon=Box(0,-6,10,12),state=OwnershipState.Unoccupied,color=new Color(.8f,.5f,.2f)}};
            var settings=Asset<RoadMarchSettings>("DefaultSettings");
            var infantryFree=Asset<ArmyFreeMarchDefinition>("InfantryFree"); infantryFree.allowedTerrain=TerrainAccess.Ground|TerrainAccess.Road|TerrainAccess.Grass|TerrainAccess.Mountain; infantryFree.mode2ReductionPercent=25; infantryFree.terrainReductions=new[]{new TerrainSpeedPenalty{terrain=TerrainKind.Mountain,reductionPercent=40},new TerrainSpeedPenalty{terrain=TerrainKind.Grass,reductionPercent=10}};
            var tankFree=Asset<ArmyFreeMarchDefinition>("TankFree"); tankFree.allowedTerrain=TerrainAccess.Ground|TerrainAccess.Road|TerrainAccess.Grass; tankFree.mode2ReductionPercent=35; tankFree.terrainReductions=new[]{new TerrainSpeedPenalty{terrain=TerrainKind.Grass,reductionPercent=30}};
            var infantry=Asset<ArmyDefinition>("Infantry"); infantry.speed=1.2f; infantry.displayName="Infantry"; infantry.freeMarchDefinition=infantryFree; infantry.icon=Icon(false);
            var tank=Asset<ArmyDefinition>("Tank"); tank.speed=2; tank.displayName="Armor"; tank.freeMarchDefinition=tankFree; tank.icon=Icon(true);
            var cam=new GameObject("Main Camera").AddComponent<Camera>(); cam.tag="MainCamera"; cam.orthographic=true; cam.orthographicSize=5; cam.transform.position=new Vector3(0,0,-10); cam.backgroundColor=new Color(.15f,.13f,.1f);
            var root=new GameObject("GameRoot"); var map=new GameObject("MapRoot"); var armies=new GameObject("ArmyRoot");
            var setup=root.AddComponent<RoadMarchSceneSetup>(); setup.mapCamera=cam; setup.roadMap=map.AddComponent<RoadMapRuntime>(); setup.roadMap.definition=road; setup.freeMarchMap=map.AddComponent<FreeMarchMapRuntime>(); setup.freeMarchMap.definition=terrain; setup.ownershipMap=map.AddComponent<OwnershipMapRuntime>(); setup.ownershipMap.definition=ownership; setup.settings=settings;
            setup.sceneArmies=new[]{Army(armies.transform,"1st Infantry",infantry,new Vector2(-4,-2)),Army(armies.transform,"2nd Infantry",infantry,new Vector2(-7,-3)),Army(armies.transform,"1st Armor",tank,new Vector2(0,-2))};
            setup.modes=root.AddComponent<MouseModeStateMachine>(); setup.modes.SetMode(MouseCommandMode.RoadMarch);
            setup.zoomController=root.AddComponent<MapZoomController>(); setup.zoomController.mapCamera=cam; setup.zoomController.minimumSize=2; setup.zoomController.maximumSize=8;
            setup.panController=root.AddComponent<MapPanController>(); setup.panController.mapCamera=cam;
            var canvas=new GameObject("Command UI",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster)).GetComponent<Canvas>(); canvas.renderMode=RenderMode.ScreenSpaceOverlay; var scaler=canvas.GetComponent<CanvasScaler>(); scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution=new Vector2(1280,720);
            new GameObject("EventSystem",typeof(EventSystem),typeof(StandaloneInputModule));
            var panel=UI("Command bar",canvas.transform,new Vector2(.5f,0),new Vector2(0,42),new Vector2(1280,84)); panel.AddComponent<Image>().color=new Color(.23f,.17f,.11f,.98f);
            Label(panel.transform,"1 ROAD   |   2 FREE MARCH   |   3 TACTICAL LAYERS",new Vector2(-340,20),new Vector2(560,26)); Label(panel.transform,"WASD / RMB: pan    LMB: select + draw    Wheel: zoom",new Vector2(-310,-16),new Vector2(620,26));
            setup.zoomController.zoomSlider=Slider(panel.transform,"Zoom",new Vector2(270,0)); setup.zoomController.resetNode=Button(panel.transform,"RESET",new Vector2(470,0));
            var sensitivity=Slider(canvas.transform,"Mouse sensitivity",new Vector2(0,0)); var sr=sensitivity.GetComponent<RectTransform>(); sr.anchorMin=sr.anchorMax=new Vector2(1,1); sr.anchoredPosition=new Vector2(-150,-28); sensitivity.minValue=.2f; sensitivity.maxValue=3; sensitivity.value=1; UnityEventTools.AddPersistentListener(sensitivity.onValueChanged,setup.panController.SetMouseSensitivity);
            var title=UI("Sensitivity label",canvas.transform,new Vector2(1,1),new Vector2(-150,-60),new Vector2(240,24)); Text(title,"Mouse sensitivity");
            foreach(var a in new UnityEngine.Object[]{road,terrain,ownership,settings,infantryFree,tankFree,infantry,tank}) EditorUtility.SetDirty(a);
            EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(),"Assets/Scenes/Battle01.unity"); EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene("Assets/Scenes/Battle01.unity",true)}; PlayerSettings.productName="Wargame Command Demo"; PlayerSettings.companyName="Campaign"; AssetDatabase.SaveAssets(); Debug.Log("WARGAME_BUILD_OK");
        }
        static ArmyRoadMover Army(Transform parent,string name,ArmyDefinition type,Vector2 pos) { var g=new GameObject(name); g.transform.SetParent(parent); g.transform.position=pos; var sprite=g.AddComponent<SpriteRenderer>(); sprite.sprite=type.icon; sprite.sortingOrder=10; g.transform.localScale=Vector3.one*.7f; var a=g.AddComponent<ArmyRoadMover>(); a.definition=type; g.AddComponent<ArmyFreeMarchMover>().definition=type.freeMarchDefinition; g.AddComponent<ArmyTacticalInfo>().armyLabel=name; return a; }
        static GameObject UI(string name,Transform parent,Vector2 anchor,Vector2 at,Vector2 size) { var g=new GameObject(name,typeof(RectTransform)); g.transform.SetParent(parent,false); var r=(RectTransform)g.transform; r.anchorMin=r.anchorMax=anchor; r.sizeDelta=size; r.anchoredPosition=at; return g; }
        static void Text(GameObject g,string value) { var t=g.AddComponent<Text>(); t.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); t.text=value; t.fontSize=17; t.color=new Color(.94f,.88f,.71f); t.alignment=TextAnchor.MiddleCenter; t.raycastTarget=false; }
        static void Label(Transform p,string v,Vector2 at,Vector2 size) { Text(UI(v,p,new Vector2(.5f,.5f),at,size),v); }
        static Button Button(Transform p,string label,Vector2 at) { var g=UI(label,p,new Vector2(.5f,.5f),at,new Vector2(110,36)); g.AddComponent<Image>().color=new Color(.4f,.3f,.2f); var b=g.AddComponent<Button>(); Text(UI("Label",g.transform,new Vector2(.5f,.5f),Vector2.zero,new Vector2(110,36)),label); return b; }
        static Slider Slider(Transform p,string name,Vector2 at) { var g=UI(name,p,new Vector2(.5f,.5f),at,new Vector2(210,24)); g.AddComponent<Image>().color=new Color(.23f,.18f,.13f); var s=g.AddComponent<Slider>(); var h=UI("Handle",g.transform,new Vector2(.5f,.5f),Vector2.zero,new Vector2(16,24)); var im=h.AddComponent<Image>(); im.color=new Color(.85f,.72f,.48f); s.handleRect=(RectTransform)h.transform; s.targetGraphic=im; return s; }
        static Sprite SaveTexture(Texture2D t,string file) { File.WriteAllBytes(file,t.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(t); AssetDatabase.ImportAsset(file); var importer=(TextureImporter)AssetImporter.GetAtPath(file); importer.textureType=TextureImporterType.Sprite; importer.spritePixelsPerUnit=100; importer.SaveAndReimport(); return AssetDatabase.LoadAssetAtPath<Sprite>(file); }
        static Sprite Icon(bool armor) { var t=new Texture2D(100,64); for(int y=0;y<64;y++) for(int x=0;x<100;x++) { bool border=x<4||x>95||y<4||y>59; float nx=(x-50)/34f,ny=(y-32)/18f; bool mark=armor?Mathf.Abs(nx*nx+ny*ny-1)<.17f:Mathf.Abs((x-12)*40f/76-(y-12))<2||Mathf.Abs((x-12)*40f/76+(y-12)-40)<2; t.SetPixel(x,y,border||mark?new Color(.08f,.14f,.17f):new Color(.36f,.77f,.87f)); } t.Apply(); return SaveTexture(t,"Assets/Game/Art/"+(armor?"Armor":"Infantry")+".png"); }
        static Sprite MapSprite() { var t=new Texture2D(1000,600); for(int y=0;y<600;y++) for(int x=0;x<1000;x++) { float wx=x/50f-10,wy=y/50f-6; var c=new Color(.78f,.76f,.63f); if(wx>=-8&&wx<=-5&&wy>=1&&wy<=4) { float contour=Mathf.Sin((Mathf.Pow((wx+6.5f)*1.1f,2)+Mathf.Pow(wy-2.5f,2))*14); c=contour>.85f?new Color(.52f,.48f,.36f):new Color(.68f,.66f,.53f); } if(wx>=2&&wx<=7&&wy>=1&&wy<=4)c=new Color(.48f,.59f,.39f); if(wx>=5&&wx<=6)c=new Color(.45f,.63f,.67f); if(wx>=5&&wx<=6&&Mathf.Abs(wy+2)<.3f)c=new Color(.54f,.43f,.28f); if(x%100==0||y%100==0)c=Color.Lerp(c,new Color(.3f,.3f,.2f),.14f); t.SetPixel(x,y,c); } t.Apply(); return SaveTexture(t,"Assets/Game/Art/Map01.png"); }
        static double started; static bool checkedScene;
        [InitializeOnLoadMethod] static void ResumeVerification() { if(!SessionState.GetBool("WargameVerify",false))return; started=EditorApplication.timeSinceStartup; Application.logMessageReceived+=OnLog; EditorApplication.update+=Verify; }
        public static void BuildAndVerify() { Build(); SessionState.SetBool("WargameVerify",true); Application.logMessageReceived+=OnLog; started=EditorApplication.timeSinceStartup; EditorApplication.update+=Verify; EditorApplication.EnterPlaymode(); }
        static void OnLog(string msg,string stack,LogType type) { if(type==LogType.Exception||type==LogType.Error) { File.WriteAllText("verification-error.txt",msg+"\n"+stack); EditorApplication.Exit(1); } }
        static void Verify() { if(EditorApplication.timeSinceStartup-started>90) { Debug.LogError("Play verification timed out"); return; } if(!EditorApplication.isPlaying||Time.time<.3f||checkedScene)return; checkedScene=true; var s=UnityEngine.Object.FindObjectOfType<RoadMarchSceneSetup>(); if(s==null||s.sceneArmies[0].Motor==null||s.roadMap.Network==null) { Debug.LogError("Scene did not initialize"); return; } s.sceneArmies[0].CommitRoute(new[]{new RoadPoint(-4,-2),new RoadPoint(0,-2)}); s.modes.SetMode(MouseCommandMode.Interaction); s.zoomController.SetSize(2); s.zoomController.ResetToMiddle(); if(Mathf.Abs(s.mapCamera.orthographicSize-5)>.01f) { Debug.LogError("Zoom reset failed");return; } Debug.Log("WARGAME_PLAY_OK: scene, armies, road route, tactical mode, zoom reset"); File.WriteAllText("verification-result.txt","Unity 2022.3.62f3c1: compile and Play initialization PASS; armies, road command, tactical mode, zoom reset PASS."); EditorApplication.Exit(0); }
    }
}
