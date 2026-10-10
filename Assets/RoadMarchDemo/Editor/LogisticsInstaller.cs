using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
namespace CampaignDemo.Editor
{
    public static class LogisticsInstaller
    {
        static T Asset<T>(string name) where T:ScriptableObject{Directory.CreateDirectory("Assets/Game/Data/Logistics");string path="Assets/Game/Data/Logistics/"+name+".asset";var a=AssetDatabase.LoadAssetAtPath<T>(path);if(a==null){a=ScriptableObject.CreateInstance<T>();AssetDatabase.CreateAsset(a,path);}return a;}
        [MenuItem("Campaign Demo/Configure logistics (keeps map)")]
        public static void Configure()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/Battle01.unity");var world=UnityEngine.Object.FindObjectOfType<BattleWorld>();if(world==null)throw new Exception("Battle01 world required");var logistics=world.GetComponent<LogisticsWorld>();if(logistics==null)logistics=world.gameObject.AddComponent<LogisticsWorld>();logistics.world=world;logistics.factoryLimitPerPlayer=3;
            var transport=Asset<TransportDefinition>("DefaultTransport");var combat=Asset<ArmyCombatDefinition>("TruckCombat");combat.unitType="运输车";combat.echelon=ArmyEchelon.Company;combat.fullStrengthAttack=0;combat.defense=10;combat.visionRadius=2;combat.attackRadius=.1f;
            var movement=Asset<ArmyDefinition>("TruckMovement");movement.displayName="运输车";movement.speed=1.5f;var free=Asset<ArmyFreeMarchDefinition>("TruckFreeMarch");free.allowedTerrain=TerrainAccess.Road;free.mode2ReductionPercent=0;movement.freeMarchDefinition=free;
            var g=new GameObject("运输车模板");var sprite=g.AddComponent<SpriteRenderer>();sprite.sprite=world.unknownUnitSprite;sprite.color=new Color(.85f,.65f,.25f);sprite.sortingOrder=10;g.transform.localScale=Vector3.one*.3f;g.AddComponent<ArmyRoadMover>().definition=movement;var unit=g.AddComponent<BattleUnit>();unit.unitId="template";unit.combatDefinition=combat;unit.designation="";g.AddComponent<TransportTruck>().definition=transport;Directory.CreateDirectory("Assets/Game/Prefabs");logistics.truckPrefab=PrefabUtility.SaveAsPrefabAsset(g,"Assets/Game/Prefabs/TransportTruck.prefab");UnityEngine.Object.DestroyImmediate(g);logistics.collectorSprite=world.unknownUnitSprite;
            var root=GameObject.Find("ResourceSites");if(root==null)root=new GameObject("ResourceSites");var list=new List<ResourceSite>();
            for(int i=0;i<2;i++){string name="Resource site "+(i+1);var found=root.transform.Find(name);var obj=found==null?new GameObject(name):found.gameObject;obj.transform.SetParent(root.transform);if(found==null){var city=world.points[i];obj.transform.position=city.transform.position+new Vector3(.8f,0,0);}var site=obj.GetComponent<ResourceSite>();if(site==null)site=obj.AddComponent<ResourceSite>();site.displayName=i==0?"西岸测试资源区":"东岸测试资源区";site.areaSize=new Vector2(1.2f,1.2f);list.Add(site);}logistics.sites=list.ToArray();
            var canvas=UnityEngine.Object.FindObjectOfType<BattleHUD>();var hud=canvas.GetComponent<LogisticsHUD>();if(hud==null)hud=canvas.gameObject.AddComponent<LogisticsHUD>();hud.logistics=logistics;hud.battleHUD=canvas;
            foreach(var a in new ScriptableObject[]{transport,combat,movement,free})EditorUtility.SetDirty(a);EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());AssetDatabase.SaveAssets();Debug.Log("LOGISTICS_CONFIGURED: existing map preserved");
        }
        public static void ConfigureAndVerify(){Configure();LogisticsVerification.Run();}
    }
}
