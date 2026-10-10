using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
namespace CampaignDemo.Editor
{
    public static class BattleSceneInstaller
    {
        static T Asset<T>(string name) where T:ScriptableObject
        { Directory.CreateDirectory("Assets/Game/Data/Combat");string path="Assets/Game/Data/Combat/"+name+".asset";var a=AssetDatabase.LoadAssetAtPath<T>(path);if(a==null){a=ScriptableObject.CreateInstance<T>();AssetDatabase.CreateAsset(a,path);}return a; }
        static Vector2 P(float x,float y){return new Vector2((x/1920-.5f)*36,(.5f-y/1297)*(36*1348f/1995));}
        [MenuItem("Campaign Demo/Configure battle systems (keeps map)")]
        public static void Configure()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/Battle01.unity");var setup=UnityEngine.Object.FindObjectOfType<RoadMarchSceneSetup>();if(setup==null)throw new Exception("Existing setup required; no map is generated.");
            var infantry=Asset<ArmyCombatDefinition>("InfantryRegiment");infantry.unitType="步兵";infantry.echelon=ArmyEchelon.Regiment;infantry.fullStrengthAttack=18;infantry.defense=20;infantry.visionRadius=3.5f;infantry.attackRadius=1.05f;
            var armor=Asset<ArmyCombatDefinition>("ArmorRegiment");armor.unitType="装甲";armor.echelon=ArmyEchelon.Regiment;armor.fullStrengthAttack=27;armor.defense=40;armor.visionRadius=3.5f;armor.attackRadius=1.3f;
            var world=setup.GetComponent<BattleWorld>();if(world==null)world=setup.gameObject.AddComponent<BattleWorld>();world.setup=setup;world.players=new[]{"A","B"};world.currentPlayer="A";
            var list=new List<ArmyRoadMover>(setup.sceneArmies);
            for(int i=0;i<Mathf.Min(3,list.Count);i++){var army=list[i];var u=army.GetComponent<BattleUnit>();if(u==null)u=army.gameObject.AddComponent<BattleUnit>();u.ownerId="A";u.unitId="A-"+(i+1);u.combatDefinition=i==2?armor:infantry;u.startingPersonnelPercent=i==0?70:100;army.name=i==2?"A 装甲团":"A 步兵团 "+(i+1);}
            for(int i=0;i<2;i++)
            {
                string id="B-"+(i+1);ArmyRoadMover army=null;foreach(var candidate in list){var b=candidate.GetComponent<BattleUnit>();if(b!=null&&b.unitId==id){army=candidate;break;}}
                if(army==null){var g=UnityEngine.Object.Instantiate(list[i==0?1:2].gameObject,list[0].transform.parent);army=g.GetComponent<ArmyRoadMover>();list.Add(army);}
                army.name=i==0?"B 步兵团":"B 装甲团";army.transform.position=i==0?P(770,538):P(1170,840);var u=army.GetComponent<BattleUnit>();if(u==null)u=army.gameObject.AddComponent<BattleUnit>();u.unitId=id;u.ownerId="B";u.combatDefinition=i==0?infantry:armor;u.startingPersonnelPercent=100;
            }
            setup.sceneArmies=list.ToArray();var units=new List<BattleUnit>();foreach(var a in list){var u=a.GetComponent<BattleUnit>();if(u!=null)units.Add(u);}world.units=units.ToArray();
            var facilityRoot=GameObject.Find("FacilityRoot");if(facilityRoot==null)facilityRoot=new GameObject("FacilityRoot");
            var points=new List<RecoveryPoint>();points.Add(Point(facilityRoot.transform,"West recovery","西部恢复据点","A",P(405,533)));points.Add(Point(facilityRoot.transform,"East recovery","东部恢复据点","B",P(1270,979)));points.Add(Point(facilityRoot.transform,"Neutral recovery","中立恢复据点","",P(730,1145)));world.points=points.ToArray();
            var territory=setup.GetComponent<TerritoryControlSystem>();if(territory==null)territory=setup.gameObject.AddComponent<TerritoryControlSystem>();territory.world=world;territory.bounds=setup.freeMarchMap.definition.bounds;territory.cellSize=2;world.territory=territory;
            setup.showTacticalOverlays=false;setup.showDebugUI=false;setup.showCommittedRoutes=false;
            world.unknownUnitSprite=Unknown();var canvas=GameObject.Find("Command UI");if(canvas==null)throw new Exception("Existing UGUI canvas required");var hud=canvas.GetComponent<BattleHUD>();if(hud==null)hud=canvas.AddComponent<BattleHUD>();hud.world=world;
            if(setup.mapCamera.GetComponent<AudioListener>()==null)setup.mapCamera.gameObject.AddComponent<AudioListener>();
            EditorUtility.SetDirty(infantry);EditorUtility.SetDirty(armor);EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());AssetDatabase.SaveAssets();Debug.Log("BATTLE_CONFIGURED: existing map unchanged");
        }
        static RecoveryPoint Point(Transform parent,string name,string title,string owner,Vector2 position)
        { var found=parent.Find(name);var g=found==null?new GameObject(name):found.gameObject;g.transform.SetParent(parent);g.transform.position=position;var p=g.GetComponent<RecoveryPoint>();if(p==null)p=g.AddComponent<RecoveryPoint>();p.pointId=name;p.displayName=title;p.ownerId=owner;p.interactionRadius=1;p.captureRadius=.9f;var renderer=g.GetComponent<SpriteRenderer>();if(renderer==null)renderer=g.AddComponent<SpriteRenderer>();renderer.sprite=Unknown();renderer.color=new Color(.9f,.77f,.4f);renderer.sortingOrder=9;g.transform.localScale=new Vector3(.32f,.32f,1);return p; }
        static Sprite Unknown()
        {
            const string path="Assets/Game/Art/UnknownUnit.png";var existing=AssetDatabase.LoadAssetAtPath<Sprite>(path);if(existing!=null)return existing;
            var t=new Texture2D(100,64);for(int y=0;y<64;y++)for(int x=0;x<100;x++)t.SetPixel(x,y,x<4||x>95||y<4||y>59?new Color(.12f,.12f,.12f):new Color(.72f,.72f,.67f));t.Apply();File.WriteAllBytes(path,t.EncodeToPNG());UnityEngine.Object.DestroyImmediate(t);AssetDatabase.ImportAsset(path);var i=(TextureImporter)AssetImporter.GetAtPath(path);i.textureType=TextureImporterType.Sprite;i.spritePixelsPerUnit=100;i.SaveAndReimport();return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
    }
}
