using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
namespace CampaignDemo.Editor {
 public static class StaffMapCacheEditor {
  [MenuItem("Tools/态势地图/保存当前地图运行缓存")]
  public static void Bake(){
   if(EditorApplication.isPlaying)return;var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
   foreach(var map in Object.FindObjectsOfType<StaffMapCartography>()){
    if(map.gameObject.scene!=scene)continue;var local=map.GetComponent<LocalBattleMap>();if(local!=null){local.Rebuild();local.useSavedGeometry=true;EditorUtility.SetDirty(local);}
    map.Rebuild();const string folder="Assets/Game/PerformanceCache";Directory.CreateDirectory(folder);string path=folder+"/"+scene.name+".png";
    File.WriteAllBytes(path,map.Texture.EncodeToPNG());AssetDatabase.ImportAsset(path);var importer=(TextureImporter)AssetImporter.GetAtPath(path);importer.textureType=TextureImporterType.Sprite;importer.spritePixelsPerUnit=100;importer.mipmapEnabled=false;importer.isReadable=false;importer.maxTextureSize=4096;importer.textureCompression=TextureImporterCompression.Compressed;importer.SaveAndReimport();map.bakedBackground=AssetDatabase.LoadAssetAtPath<Sprite>(path);map.ShowBakedBackground();EditorUtility.SetDirty(map);
   }
   EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
  }
  [MenuItem("Tools/态势地图/解除当前地图缓存以编辑地形")]
  public static void Edit(){if(EditorApplication.isPlaying)return;var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();foreach(var map in Object.FindObjectsOfType<StaffMapCartography>()){if(map.gameObject.scene!=scene)continue;map.bakedBackground=null;map.Invalidate();EditorUtility.SetDirty(map);}EditorSceneManager.MarkSceneDirty(scene);}
 }
}
