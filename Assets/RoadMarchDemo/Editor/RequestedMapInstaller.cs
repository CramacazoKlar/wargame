using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
namespace CampaignDemo.Editor
{
    public static class RequestedMapInstaller
    {
        const float W=36,H=36*1348f/1995;
        static Vector2 P(float x,float y) { return new Vector2((x/1920-.5f)*W,(.5f-y/1297)*H); }
        static readonly List<Vector2> nodes=new List<Vector2>(); static readonly List<RoadMapDefinition.Connection> edges=new List<RoadMapDefinition.Connection>();
        static int Node(Vector2 p) { int i=nodes.FindIndex(n=>Vector2.Distance(n,p)<.015f);if(i>=0)return i;nodes.Add(p);return nodes.Count-1; }
        static void Road(params float[] xy) { int last=-1;for(int i=0;i<xy.Length;i+=2){int n=Node(P(xy[i],xy[i+1]));if(last>=0&&last!=n)edges.Add(new RoadMapDefinition.Connection(last,n));last=n;} }
        static T Asset<T>(string name)where T:ScriptableObject { string path="Assets/Game/Data/"+name+".asset";var a=AssetDatabase.LoadAssetAtPath<T>(path);if(a!=null)return a;a=ScriptableObject.CreateInstance<T>();AssetDatabase.CreateAsset(a,path);return a; }
        [MenuItem("Campaign Demo/Apply requested reference map (explicit only)")]
        public static void Apply()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/Battle01.unity"); var setup=UnityEngine.Object.FindObjectOfType<RoadMarchSceneSetup>();if(setup==null)throw new Exception("Existing scene setup missing. No scene was rebuilt.");
            string image="Assets/Game/Art/ReferenceMap.png";var importer=(TextureImporter)AssetImporter.GetAtPath(image);importer.textureType=TextureImporterType.Sprite;importer.spritePixelsPerUnit=100;importer.isReadable=true;importer.mipmapEnabled=false;importer.textureCompression=TextureImporterCompression.Uncompressed;importer.maxTextureSize=4096;importer.SaveAndReimport();
            nodes.Clear();edges.Clear();
            // Pixel coordinates digitized along the brown principal roads in the supplied image.
            Road(0,635,75,628,132,603,182,594,240,590,287,578,337,565,379,552,405,533,446,518,481,524,530,540,590,540,660,529,711,530,770,538,809,548,850,560,900,582,967,609,1020,631,1080,656,1112,684,1130,718,1143,765,1170,840,1220,900,1270,979,1320,1020,1400,1070,1490,1120,1506,1173,1510,1220,1490,1297);
            Road(850,560,875,520,915,477,942,442,946,416,941,387,939,350,943,318,952,300,977,275,986,249,1020,235,1065,221,1110,199,1155,178,1190,141,1205,135,1220,132,1230,136,1240,143,1250,144,1260,150,1270,156,1310,169,1340,163,1370,154,1388,136,1400,111,1407,75,1408,31,1402,0);
            Road(446,518,483,496,516,493,540,475,551,428,570,382,580,365,604,333,620,307,627,276,637,243,641,217,638,180,650,145,669,112,649,57,630,0);
            Road(590,540,603,574,610,625,647,638,690,646,715,650,769,675,805,691,839,704);
            Road(809,548,830,565,840,594,840,627,836,659,830,690,839,704,824,729,812,754);
            Road(105,1297,125,1260,148,1225,167,1200,190,1170,219,1150,250,1134,298,1125,350,1120,395,1124,438,1134,503,1137,550,1142,580,1144,620,1145,650,1145,690,1145,730,1145,755,1153,772,1163,818,1168,865,1169,906,1160,960,1150,998,1156,1050,1168,1083,1192,1120,1220,1154,1255,1180,1297);
            Road(446,518,418,550,387,595,350,651,303,713,270,761,234,807,225,840,250,878,276,908,250,952,221,985,197,1020,170,1060,155,1099,179,1128,219,1150);
            // Omit the uncertain minor branch west of Yingxi.
            Road(250,878,307,911,346,958,367,1009,383,1069,395,1124);
            Road(395,1124,423,1079,438,1030,461,991,490,959,530,927,550,890,577,866,610,871,667,900,711,931);
            Road(730,1145,770,1200,807,1239,834,1267,852,1297);
            Road(1270,979,1285,1000,1300,1037,1330,1060,1370,1093,1400,1103,1490,1120);
            Road(1490,1120,1530,1080,1570,1030,1600,980,1630,914,1670,848,1710,788,1750,726,1770,668,1790,630,1830,579,1880,539,1920,511);
            SnapToImageRoad(AssetDatabase.LoadAssetAtPath<Texture2D>(image));
            var road=Asset<RoadMapDefinition>("ReferenceRoadMap");road.nodes=nodes.ToArray();road.connections=edges.ToArray();road.background=AssetDatabase.LoadAssetAtPath<Sprite>(image);road.backgroundCenter=Vector2.zero;road.backgroundSize=new Vector2(W,H);road.showRoads=false;road.spawnNodes=new int[0];
            var terrain=Asset<FreeMarchMapDefinition>("ReferenceTerrain");terrain.bounds=new Rect(-W/2,-H/2,W,H);terrain.defaultTerrain=TerrainKind.Ground;
            var corridors=new List<FreeMarchMapDefinition.Region>(); foreach(var e in edges){Vector2 a=nodes[e.a],b=nodes[e.b];var side=new Vector2(-(b-a).y,(b-a).x).normalized*.065f;corridors.Add(new FreeMarchMapDefinition.Region{name="Road corridor",terrain=TerrainKind.Road,polygon=new[]{a-side,b-side,b+side,a+side}});}terrain.regions=corridors.ToArray();terrain.MarkDirty();
            setup.roadMap.definition=road;setup.freeMarchMap.definition=terrain;
            var water=setup.roadMap.GetComponent<RasterWaterMap>();if(water==null)water=setup.roadMap.gameObject.AddComponent<RasterWaterMap>();water.source=AssetDatabase.LoadAssetAtPath<Texture2D>(image);water.localBounds=terrain.bounds;
            water.bridges=new[]{PixelRect(934,593,988,640),PixelRect(594,1132,727,1161)};AddMinorRoadCrossings(water);setup.freeMarchMap.waterMap=water;
            setup.showTacticalOverlays=false;setup.showCommittedRoutes=false;setup.showDebugUI=false;
            var placements=new[]{P(446,518),P(660,529),P(1080,656)};
            for(int i=0;i<setup.sceneArmies.Length;i++){var army=setup.sceneArmies[i];if(army==null)continue;army.transform.position=placements[i%placements.Length];army.waterMap=water;var f=army.definition.freeMarchDefinition;if(f!=null){f.allowedTerrain=TerrainAccess.Ground|TerrainAccess.Road|TerrainAccess.Grass|TerrainAccess.Mountain;f.mode2ReductionPercent=25;f.terrainReductions=new[]{new TerrainSpeedPenalty{terrain=TerrainKind.Ground,reductionPercent=25},new TerrainSpeedPenalty{terrain=TerrainKind.Grass,reductionPercent=35},new TerrainSpeedPenalty{terrain=TerrainKind.Mountain,reductionPercent=50}};EditorUtility.SetDirty(f);}}
            setup.mapCamera.transform.position=new Vector3(0,0,-10);setup.mapCamera.clearFlags=CameraClearFlags.SolidColor;setup.mapCamera.backgroundColor=new Color(.15f,.12f,.08f);setup.mapCamera.orthographicSize=14;setup.zoomController.minimumSize=3;setup.zoomController.maximumSize=25;setup.panController.useBounds=true;setup.panController.cameraCenterBounds=terrain.bounds;
            var old=GameObject.Find("Command UI");if(old!=null)UnityEngine.Object.DestroyImmediate(old);
            var canvas=new GameObject("Command UI",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster)).GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;var cs=canvas.GetComponent<CanvasScaler>();cs.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;cs.referenceResolution=new Vector2(1280,720);
            var config=setup.GetComponent<GameSettingsController>();if(config==null)config=setup.gameObject.AddComponent<GameSettingsController>();config.pan=setup.panController;
            var settings=Panel(canvas.transform,"Settings panel",new Vector2(0,1),new Vector2(185,-200),new Vector2(340,275));config.settingsPanel=settings;
            Label(settings.transform,"SETTINGS",new Vector2(0,110),new Vector2(300,24));config.panSlider=Setting(settings.transform,"Map pan sensitivity",65,.2f,3,1);config.planningSlider=Setting(settings.transform,"Route sensitivity",10,.25f,2,1);config.bgmSlider=Setting(settings.transform,"BGM volume",-45,0,1,.6f);config.effectsSlider=Setting(settings.transform,"Effects volume",-100,0,1,.8f);settings.SetActive(false);
            var button=Button(canvas.transform,"SETTINGS",new Vector2(0,1),new Vector2(68,-26),new Vector2(116,36));UnityEventTools.AddPersistentListener(button.onClick,config.TogglePanel);
            var zoom=Slider(canvas.transform,"Zoom",new Vector2(0,.5f),new Vector2(28,0),new Vector2(18,235),true);setup.zoomController.zoomSlider=zoom;setup.zoomController.resetNode=Button(canvas.transform,"RESET",new Vector2(0,.5f),new Vector2(45,-151),new Vector2(78,30));
            var refHud=canvas.gameObject.AddComponent<MapReferenceHUD>();refHud.mapCamera=setup.mapCamera;refHud.metersPerWorldUnit=1000;
            var scale=UI("Scale bar",canvas.transform,new Vector2(0,1),new Vector2(158,-39),new Vector2(130,4));scale.GetComponent<RectTransform>().pivot=new Vector2(0,.5f);scale.AddComponent<Image>().color=new Color(.87f,.79f,.6f);refHud.scaleBar=(RectTransform)scale.transform;
            var scaleText=UI("Scale distance",canvas.transform,new Vector2(0,1),new Vector2(224,-21),new Vector2(135,22));refHud.scaleLabel=Text(scaleText,"",16);
            var north=UI("North",canvas.transform,new Vector2(0,1),new Vector2(322,-28),new Vector2(40,42));Text(north,"N\n▲",17);refHud.compass=(RectTransform)north.transform;
            var bgm=setup.transform.Find("BGM");if(bgm==null){var g=new GameObject("BGM");g.transform.SetParent(setup.transform);bgm=g.transform;}config.bgmSource=bgm.GetComponent<AudioSource>();if(config.bgmSource==null)config.bgmSource=bgm.gameObject.AddComponent<AudioSource>();config.bgmSource.loop=true;config.bgmSource.playOnAwake=false;config.bgmSource.spatialBlend=0;
            var effects=setup.transform.Find("Effects");if(effects==null){var g=new GameObject("Effects");g.transform.SetParent(setup.transform);effects=g.transform;}var audio=effects.GetComponent<AudioSource>();if(audio==null)audio=effects.gameObject.AddComponent<AudioSource>();audio.playOnAwake=false;audio.spatialBlend=0;config.effectSources=new[]{audio};
            EditorUtility.SetDirty(road);EditorUtility.SetDirty(terrain);EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());AssetDatabase.SaveAssets();
            File.WriteAllText("reference-map-result.txt","Reference image mapped 1:1 to navigation coordinate system; "+nodes.Count+" road nodes, "+edges.Count+" segments. Water sampled from source pixels; 2 road bridges. No persistent navigation overlays.");Debug.Log("REFERENCE_MAP_APPLIED");
        }
        public static void ApplyAndVerify(){Apply();ReferenceMapVerification.Run();}
        static Rect PixelRect(float x,float y,float right,float bottom){var a=P(x,bottom);var b=P(right,y);return Rect.MinMaxRect(a.x,a.y,b.x,b.y);}
        static void SnapToImageRoad(Texture2D image)
        {
            var pixels=image.GetPixels32();
            for(int i=0;i<nodes.Count;i++)
            {
                var p=nodes[i];int x=Mathf.RoundToInt((p.x/W+.5f)*image.width),y=Mathf.RoundToInt((p.y/H+.5f)*image.height);int best=1000,bx=x,by=y;
                for(int dy=-10;dy<=10;dy++)for(int dx=-10;dx<=10;dx++)
                {
                    int xx=x+dx,yy=y+dy,d=dx*dx+dy*dy;if(xx<0||yy<0||xx>=image.width||yy>=image.height||d>=best)continue;
                    var c=pixels[yy*image.width+xx];if(c.r>70&&c.r<185&&c.r>c.g+20&&c.g>c.b+25){best=d;bx=xx;by=yy;}
                }
                if(best<1000)nodes[i]=new Vector2(((bx+.5f)/image.width-.5f)*W,((by+.5f)/image.height-.5f)*H);
            }
        }
        static void AddMinorRoadCrossings(RasterWaterMap water)
        {
            // A continuous mapped road crossing a thin stream implies a small road bridge.
            // Never exempts an entire road or a broad river crossing.
            var bridges=new List<Rect>(water.bridges);
            foreach(var edge in edges)
            {
                Vector2 a=nodes[edge.a],b=nodes[edge.b];int count=Mathf.CeilToInt(Vector2.Distance(a,b)/.008f);int begin=-1;
                for(int i=0;i<=count+1;i++)
                {
                    var p=Vector2.Lerp(a,b,Mathf.Min(1,(float)i/count));bool wet=i<=count&&water.IsWater(new RoadPoint(p.x,p.y));
                    if(wet&&begin<0)begin=i;
                    if(!wet&&begin>=0)
                    {
                        var first=Vector2.Lerp(a,b,Mathf.Max(0,(float)(begin-1)/count));var last=Vector2.Lerp(a,b,Mathf.Min(1,(float)i/count));
                        if(Vector2.Distance(first,last)<.45f)bridges.Add(Rect.MinMaxRect(Mathf.Min(first.x,last.x)-.025f,Mathf.Min(first.y,last.y)-.025f,Mathf.Max(first.x,last.x)+.025f,Mathf.Max(first.y,last.y)+.025f));
                        begin=-1;
                    }
                }
            }
            water.bridges=bridges.ToArray();
        }
        static GameObject UI(string n,Transform p,Vector2 anchor,Vector2 at,Vector2 size){var g=new GameObject(n,typeof(RectTransform));g.transform.SetParent(p,false);var r=(RectTransform)g.transform;r.anchorMin=r.anchorMax=anchor;r.anchoredPosition=at;r.sizeDelta=size;return g;}
        static GameObject Panel(Transform p,string n,Vector2 anchor,Vector2 at,Vector2 size){var g=UI(n,p,anchor,at,size);g.AddComponent<Image>().color=new Color(.23f,.17f,.1f,.97f);return g;}
        static Text Text(GameObject g,string value,int size){var t=g.AddComponent<Text>();t.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");t.fontSize=size;t.text=value;t.alignment=TextAnchor.MiddleCenter;t.color=new Color(.97f,.88f,.67f);t.raycastTarget=false;return t;}
        static void Label(Transform p,string value,Vector2 at,Vector2 size){Text(UI(value,p,new Vector2(.5f,.5f),at,size),value,16);}
        static Button Button(Transform p,string label,Vector2 anchor,Vector2 at,Vector2 size){var g=Panel(p,label,anchor,at,size);var b=g.AddComponent<Button>();Text(UI("Label",g.transform,new Vector2(.5f,.5f),Vector2.zero,size),label,15);return b;}
        static Slider Slider(Transform p,string n,Vector2 anchor,Vector2 at,Vector2 size,bool vertical=false){var g=Panel(p,n,anchor,at,size);g.GetComponent<Image>().color=new Color(.44f,.33f,.19f);var s=g.AddComponent<Slider>();var area=UI("Handle travel",g.transform,new Vector2(.5f,.5f),Vector2.zero,size-(vertical?new Vector2(0,18):new Vector2(18,0)));var h=UI("Handle",area.transform,new Vector2(.5f,.5f),Vector2.zero,vertical?new Vector2(25,18):new Vector2(18,22));var im=h.AddComponent<Image>();im.color=new Color(.78f,.64f,.4f);s.handleRect=(RectTransform)h.transform;s.targetGraphic=im;s.direction=vertical?UnityEngine.UI.Slider.Direction.BottomToTop:UnityEngine.UI.Slider.Direction.LeftToRight;return s;}
        static Slider Setting(Transform p,string name,float y,float min,float max,float value){Label(p,name,new Vector2(0,y+17),new Vector2(300,22));var s=Slider(p,name,new Vector2(.5f,.5f),new Vector2(0,y-8),new Vector2(270,14));s.minValue=min;s.maxValue=max;s.value=value;return s;}
    }
}

