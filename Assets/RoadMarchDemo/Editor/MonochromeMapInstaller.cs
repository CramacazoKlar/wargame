using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
namespace CampaignDemo.Editor {
 public static class MonochromeMapInstaller {
  [MenuItem("Campaign Demo/Use simple white tactical map")]
  public static void Apply(){EditorSceneManager.OpenScene("Assets/Scenes/TacticalSandbox.unity");const string path="Assets/Game/TacticalSandbox/TacticalMap.png";var texture=new Texture2D(32,20);var pixels=new Color[640];for(int i=0;i<pixels.Length;i++)pixels[i]=Color.white;texture.SetPixels(pixels);texture.Apply();File.WriteAllBytes(path,texture.EncodeToPNG());Object.DestroyImmediate(texture);AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceUpdate);var map=Object.FindObjectOfType<LocalBattleMap>();map.roadWidth=.24f;map.Rebuild();if(!map.terrain.CanTraverse(new RoadPoint(-1,-5),new RoadPoint(5,-5),TerrainAccess.Ground|TerrainAccess.Road))throw new System.Exception("Road crossing water no longer traversable");if(LocalRoadAStar.Plan(map.terrain,TerrainAccess.Ground|TerrainAccess.Road,new Vector3(-7,-5),new Vector3(7,-5),.2f,100000)==null)throw new System.Exception("Road A-star crossing no longer reachable");var world=Object.FindObjectOfType<BattleWorld>();world.setup.mapCamera.backgroundColor=Color.white;var minimap=Object.FindObjectOfType<TacticalMinimap>();if(minimap!=null)minimap.showControlGrid=false;EditorSceneManager.MarkSceneDirty(world.gameObject.scene);EditorSceneManager.SaveScene(world.gameObject.scene);AssetDatabase.SaveAssets();Debug.Log("MONOCHROME_MAP_APPLIED");}
  public static void ApplyAndVerify(){Apply();SessionState.SetBool("WhiteMapOverview",true);MergedArrowVerification.Run();}
 }
}
