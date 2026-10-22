using System;
using UnityEditor;
using UnityEngine;
namespace CampaignDemo.Editor
{
    public static class MapExtentVerification
    {
        static int checks;
        static void Check(bool value,string name){if(!value)throw new Exception(name);checks++;}
        public static void Run()
        {
            var root=new GameObject("Extent verification");var roadData=ScriptableObject.CreateInstance<RoadMapDefinition>();var terrainData=ScriptableObject.CreateInstance<FreeMarchMapDefinition>();var texture=new Texture2D(16,8);var sprite=Sprite.Create(texture,new Rect(0,0,16,8),Vector2.one*.5f);
            try
            {
                var road=root.AddComponent<RoadMapRuntime>();var terrain=root.AddComponent<FreeMarchMapRuntime>();road.definition=roadData;terrain.definition=terrainData;roadData.background=sprite;roadData.backgroundSize=new Vector2(72,53.45f);
                roadData.nodes=new[]{new Vector2(30,20)};var region=new FreeMarchMapDefinition.Region{terrain=TerrainKind.Blocked,polygon=new[]{new Vector2(25,15),new Vector2(30,15),new Vector2(30,20)}};terrainData.regions=new[]{region};terrainData.bounds=new Rect(-18,-13.3625f,36,26.725f);
                Check(terrain.SyncBoundsToBackground(),"stale half-size bounds updated");Check(terrainData.bounds.width==72&&Mathf.Abs(terrainData.bounds.height-53.45f)<.001f,"full map terrain extent");Check(terrain.TerrainAt(new RoadPoint(32,22))==TerrainKind.Ground,"outer map is playable ground, not false blocked");Check(terrainData.regions[0]==region&&roadData.nodes[0]==new Vector2(30,20),"user regions and road nodes preserved");Check(!terrain.SyncBoundsToBackground(),"stable bounds do not constantly invalidate cache");
                var setup=root.AddComponent<RoadMarchSceneSetup>();setup.roadMap=road;setup.freeMarchMap=terrain;var world=root.AddComponent<BattleWorld>();world.setup=setup;var grid=root.AddComponent<TerritoryControlSystem>();grid.world=world;grid.Initialize();
                Check(grid.bounds==terrainData.bounds,"territory covers complete artwork");Check(grid.mapPixels==new Vector2Int(16,8),"grid uses actual sprite dimensions");Check(grid.CellAt(new Vector3(32,22))>=0,"far map corner participates in territory control");
                var actual=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Game/Art/MapArmyStitched.png");Check(actual!=null&&actual.texture.width==6218&&actual.texture.height==4616,"43-image original resolution retained");
                Debug.Log("MAP_EXTENT_VERIFY_PASS "+checks);EditorApplication.Exit(0);
            }
            catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
            finally{UnityEngine.Object.DestroyImmediate(root);UnityEngine.Object.DestroyImmediate(roadData);UnityEngine.Object.DestroyImmediate(terrainData);UnityEngine.Object.DestroyImmediate(sprite);UnityEngine.Object.DestroyImmediate(texture);}
        }
    }
}
