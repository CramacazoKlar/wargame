using System;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
namespace CampaignDemo.Editor
{
    public static class UISeparationInstaller
    {
        [MenuItem("Campaign Demo/Configure information and commands (keeps layout)")]
        public static void Configure()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/Battle01.unity");var world=UnityEngine.Object.FindObjectOfType<BattleWorld>();if(world==null)throw new Exception("BattleWorld required");var logistics=world.GetComponent<LogisticsWorld>();
            var commands=world.GetComponent<BattleCommandService>();if(commands==null)commands=world.gameObject.AddComponent<BattleCommandService>();commands.world=world;commands.logistics=logistics;
            var info=world.GetComponent<BattleInformationSource>();if(info==null)info=world.gameObject.AddComponent<BattleInformationSource>();info.world=world;info.logistics=logistics;
            var hud=UnityEngine.Object.FindObjectOfType<BattleHUD>();if(hud==null)throw new Exception("Existing HUD required");var actions=hud.GetComponent<BattleUIActions>();if(actions==null)actions=hud.gameObject.AddComponent<BattleUIActions>();actions.commands=commands;hud.information=info;hud.actions=actions;
            var transportUI=hud.GetComponent<LogisticsHUD>();if(transportUI!=null){transportUI.information=info;transportUI.actions=actions;}
            if(logistics!=null){var ids=new System.Collections.Generic.HashSet<string>();foreach(var site in logistics.sites)if(site!=null){if(string.IsNullOrEmpty(site.siteId)||!ids.Add(site.siteId)){site.siteId=Guid.NewGuid().ToString("N");ids.Add(site.siteId);}}}
            EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());Debug.Log("UI_SEPARATED: scene layout and mode visibility kept");
        }
        public static void ConfigureAndVerify(){Configure();UISeparationVerification.Run();}
    }
}
