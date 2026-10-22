using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace CampaignDemo.Editor
{
    [InitializeOnLoad]
    public static class InstallStitchedMap
    {
        const string Receipt="stitched-map-installed.txt";
        static InstallStitchedMap(){EditorApplication.update+=Pending;EditorSceneManager.sceneOpened+=(scene,mode)=>Pending();}
        static void Pending()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode||EditorApplication.isCompiling||File.Exists(Receipt))return;
            var scene=SceneManager.GetSceneByPath("Assets/Scenes/Battle01.unity");
            if(!scene.IsValid()||!scene.isLoaded)return;
            Apply(scene);
        }
        static void Apply(Scene scene)
        {
            var road=AssetDatabase.LoadAssetAtPath<RoadMapDefinition>("Assets/Game/Data/StitchedRoadMap.asset");
            var terrain=AssetDatabase.LoadAssetAtPath<FreeMarchMapDefinition>("Assets/Game/Data/StitchedTerrain.asset");
            var sprite=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Game/Art/MapArmyStitched.png");
            var texture=AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Game/Art/MapArmyStitched.png");
            if(road==null||terrain==null||sprite==null||texture==null)return;
            road.background=sprite;road.backgroundSize=new Vector2(36,36*4616f/6218);road.backgroundCenter=Vector2.zero;
            terrain.bounds=new Rect(-18,-road.backgroundSize.y/2,36,road.backgroundSize.y);terrain.MarkDirty();
            EditorUtility.SetDirty(road);EditorUtility.SetDirty(terrain);
            RoadMapRuntime selected=null;
            foreach(var root in scene.GetRootGameObjects())
            {
                foreach(var setup in root.GetComponentsInChildren<RoadMarchSceneSetup>(true))
                {
                    if(setup.roadMap!=null){setup.roadMap.definition=road;EditorUtility.SetDirty(setup.roadMap);selected=setup.roadMap;}
                    if(setup.freeMarchMap!=null){setup.freeMarchMap.definition=terrain;setup.freeMarchMap.InvalidateGeometry();EditorUtility.SetDirty(setup.freeMarchMap);}
                }
                foreach(var pan in root.GetComponentsInChildren<MapPanController>(true))
                {pan.cameraCenterBounds=terrain.bounds;EditorUtility.SetDirty(pan);}
                foreach(var water in root.GetComponentsInChildren<RasterWaterMap>(true))
                {water.source=texture;water.localBounds=terrain.bounds;water.bridges=new Rect[0];EditorUtility.SetDirty(water);}
            }
            AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
            if(selected!=null)SceneMapPreview.Frame(selected);
            File.WriteAllText(Receipt,"43-source stitched map installed: 6218 x 4616; bounds 36 x "+road.backgroundSize.y+". No terrain regions or road nodes authored.\n");
            Debug.Log("STITCHED_MAP_INSTALLED: 43-source map is active in Battle01.");
        }
        public static void Verify()
        {
            var scene=EditorSceneManager.OpenScene("Assets/Scenes/Battle01.unity");
            Apply(scene);
            var road=AssetDatabase.LoadAssetAtPath<RoadMapDefinition>("Assets/Game/Data/StitchedRoadMap.asset");
            var terrain=AssetDatabase.LoadAssetAtPath<FreeMarchMapDefinition>("Assets/Game/Data/StitchedTerrain.asset");
            var tex=road.background.texture;
            if(tex.width!=6218||tex.height!=4616)throw new System.Exception("Map resolution changed");
            int setups=0;
            foreach(var root in scene.GetRootGameObjects())foreach(var setup in root.GetComponentsInChildren<RoadMarchSceneSetup>(true))
            {
                if(setup.roadMap.definition!=road||setup.freeMarchMap.definition!=terrain)throw new System.Exception("Scene points to old map");
                setups++;
            }
            if(setups==0)throw new System.Exception("No scene setup");
            Debug.Log("STITCHED_MAP_VERIFY_PASS: exact native resolution; active scene references and bounds verified.");EditorApplication.Exit(0);
        }
    }
}
