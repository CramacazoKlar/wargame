using System;
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
namespace CampaignDemo.Editor
{
    public static class CampaignRevisionInstaller
    {
        [MenuItem("Campaign Demo/Configure unified campaign revision")]
        public static void Configure()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop Play first");
            var scene=EditorSceneManager.OpenScene("Assets/Scenes/Battle01.unity");var world=UnityEngine.Object.FindObjectOfType<BattleWorld>();if(world==null)throw new Exception("Battle01 world required");
            Directory.CreateDirectory("Assets/Game/Data/Campaign");var rules=AssetDatabase.LoadAssetAtPath<CampaignRules>("Assets/Game/Data/Campaign/CampaignRules.asset");if(rules==null){rules=ScriptableObject.CreateInstance<CampaignRules>();AssetDatabase.CreateAsset(rules,"Assets/Game/Data/Campaign/CampaignRules.asset");}
            var runtime=world.GetComponent<CampaignRuntime>();if(runtime==null)runtime=world.gameObject.AddComponent<CampaignRuntime>();runtime.world=world;runtime.rules=rules;runtime.Bind();world.campaign=runtime;
            var old=UnityEngine.Object.FindObjectOfType<BattleHUD>();var canvas=old.GetComponent<Canvas>();old.enabled=false;var combinedHUD=old.GetComponent<CombinedArmsHUD>();if(combinedHUD!=null)combinedHUD.enabled=false;
            var hud=canvas.GetComponent<CampaignHUD>();if(hud==null)hud=canvas.gameObject.AddComponent<CampaignHUD>();hud.campaign=runtime;hud.font=old.font;hud.deploymentIcon=runtime.world.unknownUnitSprite;
            foreach(var unit in world.units)if(unit!=null){var state=unit.GetComponent<CampaignUnit>();if(state==null)state=unit.gameObject.AddComponent<CampaignUnit>();state.desiredSpeed=unit.GetComponent<ArmyRoadMover>().Speed;}
            foreach(var point in world.points)if(point!=null&&point.GetComponent<SpriteRenderer>()==null){var sprite=point.gameObject.AddComponent<SpriteRenderer>();sprite.sprite=world.unknownUnitSprite;sprite.color=new Color(.85f,.72f,.4f);sprite.sortingOrder=8;point.transform.localScale=Vector3.one*.3f;}
            foreach(var role in new[]{"PersonnelCarrier","Logistics"})
            {
                string path=role=="Logistics"?"Assets/Game/Data/Logistics/TruckFreeMarch.asset":"Assets/Game/Data/CombinedArms/PersonnelCarrierFreeMarch.asset";var free=AssetDatabase.LoadAssetAtPath<ArmyFreeMarchDefinition>(path);if(free!=null){free.allowedTerrain=TerrainAccess.Ground|TerrainAccess.Road|TerrainAccess.Grass|TerrainAccess.Forest;EditorUtility.SetDirty(free);}
            }
            var artillery=AssetDatabase.LoadAssetAtPath<ArtilleryDefinition>("Assets/Game/Data/CombinedArms/DefaultArtillery.asset");if(artillery!=null){artillery.friendlyFire=true;EditorUtility.SetDirty(artillery);}
            EditorUtility.SetDirty(rules);EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();Debug.Log("CAMPAIGN_REVISION_CONFIGURED");
        }
    }
}
