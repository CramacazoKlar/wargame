using System;
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
namespace CampaignDemo.Editor {
 public static class MapInkRevision {
  public static void Run(){try{
   var scene=EditorSceneManager.OpenScene("Assets/Scenes/DoubleValley9x.unity");var cart=UnityEngine.Object.FindObjectOfType<StaffMapCartography>();
   float wear=cart.wear,fold=cart.foldStrength;int seed=cart.seed;string path=AssetDatabase.GetAssetPath(cart.bakedBackground);if(string.IsNullOrEmpty(path))throw new Exception("Missing original map cache");
   var roads=JsonUtility.ToJson(cart.roads.definition);var terrain=JsonUtility.ToJson(cart.terrain.definition);
   cart.Rebuild();File.WriteAllBytes(path,cart.Texture.EncodeToPNG());AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceUpdate);cart.bakedBackground=AssetDatabase.LoadAssetAtPath<Sprite>(path);cart.ShowBakedBackground();
   if(cart.wear!=wear||cart.foldStrength!=fold||cart.seed!=seed||JsonUtility.ToJson(cart.roads.definition)!=roads||JsonUtility.ToJson(cart.terrain.definition)!=terrain)throw new Exception("Map geometry or ink wear settings changed");
   var colors=(Color[])typeof(MapDetailBaker).GetField("crops",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic).GetValue(null);foreach(var color in colors){float h,s,v;Color.RGBToHSV(color,out h,out s,out v);if(color.r<color.g||color.g<color.b||s>.40f)throw new Exception("Unexpected cool or saturated farm color");}
   Debug.Log("INK_REVISION_PASS muted warm field palette; paper wear seed folds and map geometry preserved");EditorUtility.SetDirty(cart);EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();MapDetailVerification.Run();
  }catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}}
 }
}
