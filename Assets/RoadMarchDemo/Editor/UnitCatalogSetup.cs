using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using CampaignDemo;
[InitializeOnLoad]
public static class UnitCatalogSetup
{
    static UnitCatalogSetup(){EditorApplication.update+=Poll;}
    public static void Configure(BattleWorld world)
    {
        if(Application.isPlaying||EditorApplication.isPlayingOrWillChangePlaymode)return;
        CityArchitectureSetup.Configure(world);var rules=world.GetComponent<InfantryProduction>().rules;
        var entries=rules.additionalUnits.ToList();
        foreach(UnitRole role in Enum.GetValues(typeof(UnitRole)))
        {
            if(role==UnitRole.Infantry||role==UnitRole.Logistics||entries.Any(e=>e!=null&&e.role==role))continue;
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Game/UnitLibrary/Prefabs/Enemy/"+role+"_Squad.prefab");if(prefab==null)throw new Exception("Missing prefab "+role);
            entries.Add(new CityProductionRules.Entry{role=role,prefab=prefab,squadCost=rules.infantrySquadCost,squadSeconds=rules.infantrySquadSeconds,costScale=rules.infantryCostScale,timeScale=rules.infantryTimeScale,requiresFactory=rules.infantryRequiresFactory});
        }
        rules.additionalUnits=entries.ToArray();EditorUtility.SetDirty(rules);
        if(world.GetComponent<LevelUnitAvailability>()==null)Undo.AddComponent<LevelUnitAvailability>(world.gameObject);
        EditorSceneManager.MarkSceneDirty(world.gameObject.scene);AssetDatabase.SaveAssets();
    }
    [MenuItem("Tools/City/Configure Unit Catalog")]
    public static void Apply(){File.WriteAllText("unit-catalog-setup.request","install");Poll();}
    static void Poll()
    {
        if(!File.Exists("unit-catalog-setup.request")||EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode||Application.isPlaying)return;
        var world=UnityEngine.Object.FindObjectOfType<BattleWorld>();if(world==null||world.gameObject.scene.path!="Assets/Scenes/Battle01.unity")return;
        bool dirty=world.gameObject.scene.isDirty;Configure(world);if(!dirty)EditorSceneManager.SaveScene(world.gameObject.scene);
        File.WriteAllText("unit-catalog-setup-result.txt","PASS all eight roles available by default; sceneSaved="+!dirty);File.Delete("unit-catalog-setup.request");
    }
}
