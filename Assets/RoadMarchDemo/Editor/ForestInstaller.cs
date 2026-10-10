using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
namespace CampaignDemo.Editor
{
    public static class ForestInstaller
    {
        [MenuItem("Campaign Demo/Enable forest movement (keeps roads and art)")]
        public static void Configure()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/Battle01.unity");var world=UnityEngine.Object.FindObjectOfType<BattleWorld>();if(world==null)throw new Exception("BattleWorld required");
            foreach(string guid in AssetDatabase.FindAssets("t:ArmyFreeMarchDefinition"))
            {
                var asset=AssetDatabase.LoadAssetAtPath<ArmyFreeMarchDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                if((asset.allowedTerrain&(TerrainAccess.Ground|TerrainAccess.Grass|TerrainAccess.Mountain))==0)continue;
                asset.allowedTerrain|=TerrainAccess.Forest;bool exists=false;foreach(var rule in asset.terrainReductions??new TerrainSpeedPenalty[0])if(rule.terrain==TerrainKind.Forest)exists=true;
                if(!exists){var penalties=new List<TerrainSpeedPenalty>(asset.terrainReductions??new TerrainSpeedPenalty[0]);penalties.Add(new TerrainSpeedPenalty{terrain=TerrainKind.Forest,reductionPercent=20});asset.terrainReductions=penalties.ToArray();}EditorUtility.SetDirty(asset);
            }
            var map=world.setup.freeMarchMap;var data=map.definition;bool present=false;foreach(var r in data.regions??new FreeMarchMapDefinition.Region[0])if(r!=null&&r.terrain==TerrainKind.Forest)present=true;
            if(!present)
            {
                // One editable test patch around the first deployed A army; no road or background is regenerated.
                var pos=map.transform.InverseTransformPoint(world.units[0].transform.position);float half=.55f;var regions=new List<FreeMarchMapDefinition.Region>(data.regions??new FreeMarchMapDefinition.Region[0]);regions.Add(new FreeMarchMapDefinition.Region{name="森林藏匿测试区（可移动或删除）",terrain=TerrainKind.Forest,polygon=new[]{new Vector2(pos.x-half,pos.y-half),new Vector2(pos.x+half,pos.y-half),new Vector2(pos.x+half,pos.y+half),new Vector2(pos.x-half,pos.y+half)}});data.regions=regions.ToArray();data.MarkDirty();EditorUtility.SetDirty(data);
            }
            AssetDatabase.SaveAssets();Debug.Log("FOREST_CONFIGURED: editable test patch; roads and background preserved");
        }
        public static void ConfigureAndVerify(){Configure();ForestVerification.Run();}
    }
}
