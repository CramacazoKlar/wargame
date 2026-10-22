using System;
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
namespace CampaignDemo.Editor
{
    public static class CombatEffectsInstaller
    {
        static CombatEffectProfile Asset(string name,Color color)
        {
            Directory.CreateDirectory("Assets/Game/Data/Effects");string path="Assets/Game/Data/Effects/"+name+".asset";
            var asset=AssetDatabase.LoadAssetAtPath<CombatEffectProfile>(path);if(asset==null){asset=ScriptableObject.CreateInstance<CombatEffectProfile>();asset.color=color;AssetDatabase.CreateAsset(asset,path);}return asset;
        }
        [MenuItem("Campaign Demo/Configure replaceable combat effects")]
        public static void Configure()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop Play before configuring.");
            var scene=EditorSceneManager.OpenScene("Assets/Scenes/Battle01.unity");var world=UnityEngine.Object.FindObjectOfType<BattleWorld>();if(world==null)throw new Exception("Battle01 world required");
            var presenter=world.GetComponent<CombatEffectPresenter>();if(presenter==null)presenter=world.gameObject.AddComponent<CombatEffectPresenter>();presenter.world=world;
            var player=world.GetComponent<PrefabCombatEffectPlayer>();if(player==null)player=world.gameObject.AddComponent<PrefabCombatEffectPlayer>();player.presenter=presenter;
            if(presenter.effectPlayer==null)presenter.effectPlayer=player;
            if(presenter.defaultProfile==null)presenter.defaultProfile=Asset("DefaultAttackEffects",new Color(1,.75f,.25f,.9f));
            if(presenter.artilleryProfile==null)presenter.artilleryProfile=Asset("ArtilleryEffects",new Color(1,.45f,.15f,.9f));
            if(presenter.profiles==null||presenter.profiles.Length==0)presenter.profiles=new[]{new CombatEffectPresenter.RoleProfile{role=UnitRole.Tank,profile=Asset("TankAttackEffects",new Color(1,.6f,.2f,.9f))}};
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();Debug.Log("COMBAT_EFFECTS_CONFIGURED");
        }
    }
}
