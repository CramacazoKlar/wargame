using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using CampaignDemo;
[InitializeOnLoad]
public static class CityArchitectureSetup
{
    const string Request="city-architecture-setup.request";
    static CityArchitectureSetup(){EditorApplication.update+=Poll;}
    [MenuItem("Tools/City/Configure City Management")]
    public static void Apply(){File.WriteAllText(Request,"install");Poll();}
    public static void Configure(BattleWorld world)
    {
        if(Application.isPlaying||EditorApplication.isPlayingOrWillChangePlaymode)return;
        var rules=AssetDatabase.LoadAssetAtPath<CityProductionRules>("Assets/Resources/CityProductionRules.asset");
        if(rules==null)
        {
            rules=ScriptableObject.CreateInstance<CityProductionRules>();var commander=world.GetComponent<AIBasicCommander>();var old=commander==null?null:commander.profile;
            if(old!=null){rules.infantryPrefab=old.infantryPrefab;rules.infantrySquadCost=old.infantrySquadCost;rules.infantrySquadSeconds=old.infantrySquadSeconds;rules.infantryCostScale=old.infantryCostScale;rules.infantryTimeScale=old.infantryTimeScale;rules.infantryRequiresFactory=old.infantryRequiresFactory;}
            if(rules.infantryPrefab==null)rules.infantryPrefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Game/UnitLibrary/Prefabs/Enemy/Infantry_Squad.prefab");
            AssetDatabase.CreateAsset(rules,"Assets/Resources/CityProductionRules.asset");
        }
        var production=world.GetComponent<InfantryProduction>();if(production==null)production=Undo.AddComponent<InfantryProduction>(world.gameObject);
        Undo.RecordObject(production,"Use independent city production rules");production.world=world;production.rules=rules;
        var service=world.GetComponent<CityManagement>();if(service==null)service=Undo.AddComponent<CityManagement>(world.gameObject);service.world=world;
        EditorSceneManager.MarkSceneDirty(world.gameObject.scene);AssetDatabase.SaveAssets();
    }
    static void Poll()
    {
        if(!File.Exists(Request)||EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode||Application.isPlaying)return;
        var world=Object.FindObjectOfType<BattleWorld>();if(world==null||world.gameObject.scene.path!="Assets/Scenes/Battle01.unity")return;
        bool dirty=world.gameObject.scene.isDirty;Configure(world);if(!dirty)EditorSceneManager.SaveScene(world.gameObject.scene);
        File.WriteAllText("city-architecture-setup-result.txt","PASS independent city rules and management configured; sceneSaved="+!dirty);File.Delete(Request);
    }
}
