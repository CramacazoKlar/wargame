using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
namespace CampaignDemo.Editor
{
    public static class CombinedArmsInstaller
    {
        static T Asset<T>(string name) where T:ScriptableObject
        {string folder="Assets/Game/Data/CombinedArms";Directory.CreateDirectory(folder);string path=folder+"/"+name+".asset";var a=AssetDatabase.LoadAssetAtPath<T>(path);if(a==null){a=ScriptableObject.CreateInstance<T>();AssetDatabase.CreateAsset(a,path);}return a;}
        [MenuItem("Campaign Demo/Configure artillery carriers and engineering")]
        public static void Configure()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop Play before configuring.");
            var scene=EditorSceneManager.OpenScene("Assets/Scenes/Battle01.unity");var world=UnityEngine.Object.FindObjectOfType<BattleWorld>();if(world==null)throw new Exception("Battle01 world required");
            var system=world.GetComponent<CombinedArmsWorld>();if(system==null)system=world.gameObject.AddComponent<CombinedArmsWorld>();system.world=world;
            var engineering=Asset<EngineeringDefinition>("DefaultEngineering");system.engineering=engineering;
            var artillery=Asset<ArtilleryDefinition>("DefaultArtillery");
            var support=Asset<ArtilleryDefinition>("DefaultSupport");if(support.cooldownSeconds==20){support.cooldownSeconds=60;support.range=100;support.damage=60;}system.supportDefinition=support;
            var hud=UnityEngine.Object.FindObjectOfType<BattleHUD>();var special=hud.GetComponent<CombinedArmsHUD>();if(special==null)special=hud.gameObject.AddComponent<CombinedArmsHUD>();special.hud=hud;
            foreach(var guid in AssetDatabase.FindAssets("t:ArmyCombatDefinition")){var d=AssetDatabase.LoadAssetAtPath<ArmyCombatDefinition>(AssetDatabase.GUIDToAssetPath(guid));if(d.unitType=="Armor"||d.unitType=="装甲"||d.unitType=="坦克")d.role=UnitRole.Tank;else if(d.unitType=="运输车")d.role=UnitRole.Logistics;EditorUtility.SetDirty(d);}
            var template=world.units[0];var roadTemplate=template.GetComponent<ArmyRoadMover>();var root=GameObject.Find("ArmyRoot").transform;
            var all=new List<BattleUnit>(world.units);var movers=new List<ArmyRoadMover>(world.setup.sceneArmies);
            foreach(var role in new[]{UnitRole.Artillery,UnitRole.PersonnelCarrier,UnitRole.Engineer,UnitRole.Scout})
            {
                string title=role==UnitRole.Artillery?"炮兵":role==UnitRole.PersonnelCarrier?"运兵车":role==UnitRole.Engineer?"工兵":"侦察兵";
                var combat=Asset<ArmyCombatDefinition>(role+"Combat");combat.role=role;combat.unitType=title;combat.echelon=ArmyEchelon.Squad;combat.fullStrengthAttack=role==UnitRole.Engineer?5:0;combat.visionRadius=role==UnitRole.Scout?5:2;combat.canHideInForest=role!=UnitRole.PersonnelCarrier;
                var move=Asset<ArmyDefinition>(role+"Movement");move.displayName=title;move.speed=role==UnitRole.Artillery?.8f:1.5f;if(move.freeMarchDefinition==null)move.freeMarchDefinition=roadTemplate.definition.freeMarchDefinition;
                var obj=new GameObject(title+"模板");var renderer=obj.AddComponent<SpriteRenderer>();var original=template.GetComponent<SpriteRenderer>();renderer.sprite=original==null?world.unknownUnitSprite:original.sprite;renderer.sortingOrder=10;renderer.color=role==UnitRole.Artillery?new Color(1,.6f,.25f):role==UnitRole.PersonnelCarrier?new Color(.6f,.8f,1):role==UnitRole.Engineer?new Color(1,.85f,.3f):new Color(.5f,1,.65f);obj.transform.localScale=template.transform.localScale;
                var road=obj.AddComponent<ArmyRoadMover>();road.definition=move;road.waterMap=roadTemplate.waterMap;var unit=obj.AddComponent<BattleUnit>();unit.combatDefinition=combat;unit.unitId=role+"-template";unit.designation=title;
                if(role==UnitRole.Artillery)obj.AddComponent<ArtilleryBattery>().definition=artillery;
                if(role==UnitRole.PersonnelCarrier)obj.AddComponent<PersonnelCarrier>();
                Directory.CreateDirectory("Assets/Game/Prefabs");var prefab=PrefabUtility.SaveAsPrefabAsset(obj,"Assets/Game/Prefabs/"+role+".prefab");UnityEngine.Object.DestroyImmediate(obj);
                string id="A-demo-"+role;bool exists=all.Exists(u=>u!=null&&u.unitId==id);
                if(!exists){var instance=(GameObject)PrefabUtility.InstantiatePrefab(prefab,root);instance.name="A "+title;instance.transform.position=template.transform.position+new Vector3(.25f*(int)role,.55f,0);var newUnit=instance.GetComponent<BattleUnit>();newUnit.unitId=id;newUnit.ownerId=template.ownerId;all.Add(newUnit);movers.Add(instance.GetComponent<ArmyRoadMover>());}
                EditorUtility.SetDirty(combat);EditorUtility.SetDirty(move);
            }
            world.units=all.ToArray();world.setup.sceneArmies=movers.ToArray();
            foreach(var a in new ScriptableObject[]{engineering,artillery,support})EditorUtility.SetDirty(a);
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();Debug.Log("COMBINED_ARMS_CONFIGURED");
        }
    }
}

