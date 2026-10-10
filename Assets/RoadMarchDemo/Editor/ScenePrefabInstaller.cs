using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using CampaignDemo;
public static class ScenePrefabInstaller {
 const string Folder="Assets/Game/Prefabs/SceneObjects";
 public static void Run(){
  var scene=EditorSceneManager.OpenScene("Assets/Scenes/Battle01.unity");Directory.CreateDirectory(Folder);int converted=0;
  foreach(string side in new[]{"Friendly","Enemy"}){string path="Assets/Game/UnitLibrary/Prefabs/"+side+"/City.prefab";var prefab=PrefabUtility.LoadPrefabContents(path);prefab.transform.localScale=Vector3.one*.5f;PrefabUtility.SaveAsPrefabAsset(prefab,path);PrefabUtility.UnloadPrefabContents(prefab);}
  var objects=new HashSet<GameObject>();foreach(var component in UnityEngine.Object.FindObjectsOfType<MonoBehaviour>())if(component is BattleUnit||component is RecoveryPoint||component is Fortification||component is ResourceCollector||component is ResourceDepot||component is ResourceSite)objects.Add(component.gameObject);
  foreach(var obj in objects){var city=obj.GetComponent<RecoveryPoint>();if(city!=null&&city.kind==FacilityKind.RecoveryPoint)obj.transform.localScale=Vector3.one*.5f;if(PrefabUtility.IsPartOfPrefabInstance(obj)){if(city!=null)PrefabUtility.RecordPrefabInstancePropertyModifications(obj.transform);continue;}string name=obj.name;foreach(char invalid in Path.GetInvalidFileNameChars())name=name.Replace(invalid,'_');string prefix=city!=null?"City_":obj.GetComponent<ResourceSite>()!=null?"ResourceSite_":obj.GetComponent<BattleUnit>()!=null?"Army_":"Facility_";string path=AssetDatabase.GenerateUniqueAssetPath(Folder+"/"+prefix+name+".prefab");PrefabUtility.SaveAsPrefabAssetAndConnect(obj,path,InteractionMode.AutomatedAction);converted++;}
  EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();File.WriteAllText("scene-prefab-conversion.txt",objects.Count+" gameplay objects linked to prefabs; "+converted+" plain objects converted; city scale .5; existing names, positions, resources and references preserved.");Debug.Log("SCENE_PREFABS_CONFIGURED "+objects.Count);
 }
}
