using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
namespace CampaignDemo.Editor
{
    public static class MountainTerrainInstaller
    {
        public const TerrainAccess Land=TerrainAccess.Ground|TerrainAccess.Road|TerrainAccess.Grass|TerrainAccess.Forest;
        public static TerrainAccess Access(UnitRole role)
        {
            if(role==UnitRole.PersonnelCarrier)return TerrainAccess.Road;
            if(role==UnitRole.MountainInfantry)return Land|TerrainAccess.Mountain|TerrainAccess.HighMountain;
            if(role==UnitRole.Tank||role==UnitRole.Artillery)return Land;
            return Land|TerrainAccess.Mountain;
        }
        static T Asset<T>(string path) where T:ScriptableObject
        {Directory.CreateDirectory(Path.GetDirectoryName(path));var a=AssetDatabase.LoadAssetAtPath<T>(path);if(a==null){a=ScriptableObject.CreateInstance<T>();AssetDatabase.CreateAsset(a,path);}return a;}
        public static ArmyFreeMarchDefinition Free(UnitRole role)
        {
            string path=role==UnitRole.Infantry?"Assets/Game/Data/InfantryFree.asset":role==UnitRole.Tank?"Assets/Game/Data/TankFree.asset":role==UnitRole.Logistics?"Assets/Game/Data/Logistics/TruckFreeMarch.asset":"Assets/Game/Data/CombinedArms/"+role+"FreeMarch.asset";
            var asset=Asset<ArmyFreeMarchDefinition>(path);asset.allowedTerrain=Access(role);
            if(role==UnitRole.PersonnelCarrier){asset.mode2ReductionPercent=0;asset.terrainReductions=new TerrainSpeedPenalty[0];}
            else if(role==UnitRole.MountainInfantry){asset.mode2ReductionPercent=15;asset.terrainReductions=new[]{Penalty(TerrainKind.Ground,10),Penalty(TerrainKind.Grass,20),Penalty(TerrainKind.Forest,15),Penalty(TerrainKind.Mountain,25),Penalty(TerrainKind.HighMountain,40)};}
            else if(role==UnitRole.Artillery||role==UnitRole.Scout||role==UnitRole.Engineer){asset.mode2ReductionPercent=25;asset.terrainReductions=new[]{Penalty(TerrainKind.Ground,25),Penalty(TerrainKind.Grass,35),Penalty(TerrainKind.Forest,20),Penalty(TerrainKind.Mountain,50)};}
            EditorUtility.SetDirty(asset);return asset;
        }
        static TerrainSpeedPenalty Penalty(TerrainKind terrain,float reduction){return new TerrainSpeedPenalty{terrain=terrain,reductionPercent=reduction};}
        static void ConfigureMovement(string path,UnitRole role){var asset=AssetDatabase.LoadAssetAtPath<ArmyDefinition>(path);if(asset==null)return;asset.freeMarchDefinition=Free(role);EditorUtility.SetDirty(asset);}
        [MenuItem("Campaign Demo/Configure mountain terrain and mobility")]
        public static void Configure()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop Play before configuring.");
            var scene=EditorSceneManager.OpenScene("Assets/Scenes/Battle01.unity");var world=UnityEngine.Object.FindObjectOfType<BattleWorld>();if(world==null)throw new Exception("Battle01 world required");
            ConfigureMovement("Assets/Game/Data/Infantry.asset",UnitRole.Infantry);ConfigureMovement("Assets/Game/Data/Tank.asset",UnitRole.Tank);ConfigureMovement("Assets/Game/Data/Logistics/TruckMovement.asset",UnitRole.Logistics);
            foreach(var role in new[]{UnitRole.Artillery,UnitRole.PersonnelCarrier,UnitRole.Engineer,UnitRole.Scout})ConfigureMovement("Assets/Game/Data/CombinedArms/"+role+"Movement.asset",role);
            foreach(var name in new[]{"Artillery","PersonnelCarrier","Engineer","Scout","TransportTruck"})
            {
                string path="Assets/Game/Prefabs/"+name+".prefab";var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(path);if(prefab==null)continue;
                var obj=PrefabUtility.LoadPrefabContents(path);try{var unit=obj.GetComponent<BattleUnit>();var road=obj.GetComponent<ArmyRoadMover>();var role=name=="TransportTruck"?UnitRole.Logistics:unit.combatDefinition.role;if(name=="TransportTruck"){unit.combatDefinition.role=role;EditorUtility.SetDirty(unit.combatDefinition);}var free=obj.GetComponent<ArmyFreeMarchMover>();if(free==null)free=obj.AddComponent<ArmyFreeMarchMover>();free.definition=Free(role);free.overrideSettings=false;road.definition.freeMarchDefinition=free.definition;EditorUtility.SetDirty(road.definition);PrefabUtility.SaveAsPrefabAsset(obj,path);}finally{PrefabUtility.UnloadPrefabContents(obj);}
            }
            BattleUnit template=null;foreach(var u in world.units)if(u!=null&&u.combatDefinition.role==UnitRole.Infantry){template=u;break;}if(template==null)throw new Exception("Infantry template required");
            var combat=Asset<ArmyCombatDefinition>("Assets/Game/Data/CombinedArms/MountainInfantryCombat.asset");combat.unitType="山地步兵";combat.role=UnitRole.MountainInfantry;combat.echelon=ArmyEchelon.Squad;combat.fullStrengthAttack=template.combatDefinition.fullStrengthAttack;combat.defense=template.combatDefinition.defense;combat.visionRadius=template.combatDefinition.visionRadius;combat.attackRadius=template.combatDefinition.attackRadius;
            var movement=Asset<ArmyDefinition>("Assets/Game/Data/CombinedArms/MountainInfantryMovement.asset");movement.displayName="山地步兵";movement.speed=template.GetComponent<ArmyRoadMover>().definition.speed;movement.freeMarchDefinition=Free(UnitRole.MountainInfantry);
            var objTemplate=new GameObject("山地步兵模板");var sprite=objTemplate.AddComponent<SpriteRenderer>();var original=template.GetComponent<SpriteRenderer>();sprite.sprite=original.sprite;sprite.color=new Color(.8f,.7f,1);sprite.sortingOrder=10;objTemplate.transform.localScale=template.transform.localScale;objTemplate.AddComponent<ArmyRoadMover>().definition=movement;objTemplate.AddComponent<ArmyFreeMarchMover>().definition=movement.freeMarchDefinition;var newUnit=objTemplate.AddComponent<BattleUnit>();newUnit.combatDefinition=combat;newUnit.unitId="mountain-template";newUnit.designation="山地步兵";
            var mountainPrefab=PrefabUtility.SaveAsPrefabAsset(objTemplate,"Assets/Game/Prefabs/MountainInfantry.prefab");UnityEngine.Object.DestroyImmediate(objTemplate);EditorUtility.SetDirty(combat);EditorUtility.SetDirty(movement);
            var units=new List<BattleUnit>(world.units);var movers=new List<ArmyRoadMover>(world.setup.sceneArmies);
            if(!units.Exists(u=>u!=null&&u.unitId=="A-demo-MountainInfantry")){var g=(GameObject)PrefabUtility.InstantiatePrefab(mountainPrefab,template.transform.parent);g.name="A 山地步兵";g.transform.position=template.transform.position+new Vector3(-.4f,.6f);var u=g.GetComponent<BattleUnit>();u.unitId="A-demo-MountainInfantry";u.ownerId=template.ownerId;units.Add(u);movers.Add(g.GetComponent<ArmyRoadMover>());}
            world.units=units.ToArray();world.setup.sceneArmies=movers.ToArray();
            // Road-only vehicles must start on an actual traversable road corridor.
            var map=world.setup.freeMarchMap;var roadMap=world.setup.roadMap;var roadPoints=new RoadPoint[roadMap.definition.nodes.Length];for(int i=0;i<roadPoints.Length;i++){var p=roadMap.transform.TransformPoint(roadMap.definition.nodes[i]);roadPoints[i]=new RoadPoint(p.x,p.y);}var edges=new RoadEdge[roadMap.definition.connections.Length];for(int i=0;i<edges.Length;i++)edges[i]=new RoadEdge(roadMap.definition.connections[i].a,roadMap.definition.connections[i].b);var network=new RoadNetwork(roadPoints,edges);
            foreach(var u in world.units)if(u!=null){var road=u.GetComponent<ArmyRoadMover>();var free=u.GetComponent<ArmyFreeMarchMover>();if(free==null)free=u.gameObject.AddComponent<ArmyFreeMarchMover>();free.definition=Free(u.combatDefinition.role);free.overrideSettings=false;road.definition.freeMarchDefinition=free.definition;road.terrainMap=map;road.waterMap=map.waterMap;EditorUtility.SetDirty(road.definition);if(u.combatDefinition.role==UnitRole.PersonnelCarrier){var at=new RoadPoint(u.transform.position.x,u.transform.position.y);if(!map.CanTraverse(at,at,TerrainAccess.Road)){bool placed=false;foreach(var hit in network.Candidates(at,double.PositiveInfinity))if(map.CanTraverse(hit.Position,hit.Position,TerrainAccess.Road)){u.transform.position=new Vector3((float)hit.Position.X,(float)hit.Position.Y);placed=true;break;}if(!placed)throw new Exception("No traversable road for carrier");}}}
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();Debug.Log("MOUNTAIN_MOBILITY_CONFIGURED");
        }
    }
}
