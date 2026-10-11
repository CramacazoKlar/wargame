using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using CampaignDemo;

// Installer runs only on an explicit marker, never merely from opening the project.
[InitializeOnLoad]
public static class BasicCommanderLiveSetup
{
    const string Request="basic-ai-setup.request";
    [MenuItem("Tools/AI/Apply Basic Red Commander")]
    public static void Apply(){File.WriteAllText(Request,"install");Poll();}
    static BasicCommanderLiveSetup(){EditorApplication.delayCall+=Inspect;EditorApplication.update+=Poll;}
    static void Inspect()
    {
        var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        File.WriteAllText("basic-ai-editor-state.txt","playing="+EditorApplication.isPlaying+"\nscene="+scene.path+"\ndirty="+scene.isDirty+"\ncompiling="+EditorApplication.isCompiling);
    }
    static void Poll()
    {
        if(!File.Exists(Request)||EditorApplication.isCompiling||EditorApplication.isUpdating)return;
        if(EditorApplication.isPlayingOrWillChangePlaymode){Inspect();return;}
        var world=UnityEngine.Object.FindObjectOfType<BattleWorld>();
        if(world==null||world.gameObject.scene.path!="Assets/Scenes/Battle01.unity"){Inspect();return;}
        bool alreadyDirty=world.gameObject.scene.isDirty;
        var ai=world.GetComponent<BattleAIController>();
        if(ai==null){File.WriteAllText("basic-ai-live-setup-result.txt","AI controller missing");File.Delete(Request);return;}
        string path="Assets/Game/Data/AI/BasicCityCommander.asset";
        var profile=AssetDatabase.LoadAssetAtPath<AIBasicProfile>(path);
        if(profile==null){profile=ScriptableObject.CreateInstance<AIBasicProfile>();AssetDatabase.CreateAsset(profile,path);}
        string behaviorPath="Assets/Game/Data/AI/BasicCityBehavior.asset";
        var behavior=AssetDatabase.LoadAssetAtPath<AIBehaviorProfile>(behaviorPath);
        if(behavior==null){behavior=ai.profile==null?ScriptableObject.CreateInstance<AIBehaviorProfile>():UnityEngine.Object.Instantiate(ai.profile);AssetDatabase.CreateAsset(behavior,behaviorPath);}
        behavior.recovery=profile.recover;behavior.supply=profile.transportResources;
        Undo.RecordObject(ai,"Configure basic red AI");
        ai.profile=behavior;ai.playerId=BattleFactions.RedPlayerId;ai.enabled=true;
        ai.autoToggleOnPlayerSwitch=true;ai.pauseWhenPlayerControlled=true;ai.aiEnabled=world.currentPlayer!=BattleFactions.RedPlayerId;
        var commander=world.GetComponent<AIBasicCommander>();if(commander==null)commander=Undo.AddComponent<AIBasicCommander>(world.gameObject);
        Undo.RecordObject(commander,"Configure basic commander");commander.controller=ai;commander.profile=profile;ai.basicCommander=commander;
        foreach(var unit in world.units.Where(u=>u!=null&&u.ownerId=="B"))
        {
            var order=unit.GetComponent<AIUnitOrder>();if(order==null)order=Undo.AddComponent<AIUnitOrder>(unit.gameObject);
            Undo.RecordObject(order,"Configure red unit executor");if(order.task==null)order.task=ai.defaultGuard;
            if(order.task.kind==AITaskKind.Guard)order.profileOverride=behavior;
            PrefabUtility.RecordPrefabInstancePropertyModifications(order);
        }
        EditorUtility.SetDirty(behavior);EditorSceneManager.MarkSceneDirty(world.gameObject.scene);AssetDatabase.SaveAssets();
        if(!alreadyDirty)EditorSceneManager.SaveScene(world.gameObject.scene);
        File.WriteAllText("basic-ai-live-setup-result.txt","PASS installed basic commander; sceneSaved="+!alreadyDirty+"; priorUnsavedEdits="+alreadyDirty);
        File.Delete(Request);Inspect();
    }
}
