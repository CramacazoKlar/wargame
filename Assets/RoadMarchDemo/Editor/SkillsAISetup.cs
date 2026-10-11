using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using CampaignDemo;

[InitializeOnLoad]
public static class SkillsAISetup
{
    const string Request="skills-ai-setup.request";
    static SkillsAISetup(){EditorApplication.update+=Poll;}
    [MenuItem("Tools/AI/Configure Skills and Production")]
    public static void RequestSetup(){File.WriteAllText(Request,"install");Poll();}
    public static bool Configure(BattleWorld world)
    {
        if(Application.isPlaying||EditorApplication.isPlayingOrWillChangePlaymode)return false;
        var commander=world.GetComponent<AIBasicCommander>();
        if(commander==null||commander.profile==null)return false;
        var profile=commander.profile;
        if(profile.infantryPrefab==null)
        {
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Game/UnitLibrary/Prefabs/Enemy/Infantry_Squad.prefab");
            if(prefab==null)throw new Exception("Ordinary infantry prefab missing");
            Undo.RecordObject(profile,"Configure infantry production");profile.infantryPrefab=prefab;EditorUtility.SetDirty(profile);
        }
        var production=world.GetComponent<InfantryProduction>();
        if(production==null)production=Undo.AddComponent<InfantryProduction>(world.gameObject);
        Undo.RecordObject(production,"Bind infantry production world");production.world=world;
        EditorSceneManager.MarkSceneDirty(world.gameObject.scene);return true;
    }
    static void Poll()
    {
        if(!File.Exists(Request)||EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode||Application.isPlaying)return;
        var world=UnityEngine.Object.FindObjectOfType<BattleWorld>();
        if(world==null||world.gameObject.scene.path!="Assets/Scenes/Battle01.unity")return;
        bool dirty=world.gameObject.scene.isDirty;
        try
        {
            if(!Configure(world))return;
            AssetDatabase.SaveAssets();if(!dirty)EditorSceneManager.SaveScene(world.gameObject.scene);
            File.WriteAllText("skills-ai-setup-result.txt","PASS skills inspector and infantry production configured; sceneSaved="+!dirty);
            File.Delete(Request);
        }
        catch(Exception exception){Debug.LogException(exception);File.WriteAllText("skills-ai-setup-result.txt",exception.ToString());File.Delete(Request);}
    }
}
