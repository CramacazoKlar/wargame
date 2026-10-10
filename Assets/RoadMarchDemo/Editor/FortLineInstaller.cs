using System;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
namespace CampaignDemo.Editor
{
    public static class FortLineInstaller
    {
        static bool Vehicle(UnitRole role){return role==UnitRole.PersonnelCarrier||role==UnitRole.Logistics;}
        public static float Retained(UnitRole role,TerrainKind terrain)
        {if(terrain==TerrainKind.Road)return 1;if(terrain==TerrainKind.Water||terrain==TerrainKind.Blocked||terrain==TerrainKind.HighMountain)return 0;if(terrain==TerrainKind.Forest)return .8f;bool armor=role==UnitRole.Tank||role==UnitRole.Artillery;if(terrain==TerrainKind.Mountain)return Vehicle(role)?0:armor?.2f:role==UnitRole.MountainInfantry?.7f:.4f;return Vehicle(role)?.3f:armor?.6f:.9f;}
        public static TerrainAccess Access(UnitRole role){return TerrainAccess.Ground|TerrainAccess.Road|TerrainAccess.Grass|TerrainAccess.Forest|(Vehicle(role)?TerrainAccess.None:TerrainAccess.Mountain);}
        static void SetMovement(ArmyFreeMarchDefinition asset,UnitRole role)
        {if(asset==null)return;asset.allowedTerrain=Access(role);asset.mode2ReductionPercent=0;asset.terrainReductions=new[]{Penalty(role,TerrainKind.Ground),Penalty(role,TerrainKind.Grass),Penalty(role,TerrainKind.Forest),Penalty(role,TerrainKind.Mountain),Penalty(role,TerrainKind.Road)};EditorUtility.SetDirty(asset);}
        static TerrainSpeedPenalty Penalty(UnitRole role,TerrainKind terrain){return new TerrainSpeedPenalty{terrain=terrain,reductionPercent=(1-Retained(role,terrain))*100};}
        [MenuItem("Campaign Demo/Configure drawn fortification lines and terrain")]
        public static void Configure()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop Play before configuring");EditorSceneManager.OpenScene("Assets/Scenes/Battle01.unity");var c=UnityEngine.Object.FindObjectOfType<CampaignRuntime>();if(c==null)throw new Exception("Campaign runtime required");c.Bind();
            foreach(var unit in c.world.units)if(unit!=null){var free=unit.GetComponent<ArmyFreeMarchMover>();if(free!=null){free.overrideSettings=false;SetMovement(free.definition,unit.combatDefinition.role);}SetMovement(unit.GetComponent<ArmyRoadMover>().definition.freeMarchDefinition,unit.combatDefinition.role);}
            foreach(var role in new[]{UnitRole.Infantry,UnitRole.MountainInfantry,UnitRole.Engineer,UnitRole.Scout,UnitRole.Tank,UnitRole.Artillery,UnitRole.PersonnelCarrier,UnitRole.Logistics})
            {string path=role==UnitRole.Infantry?"Assets/Game/Data/InfantryFree.asset":role==UnitRole.Tank?"Assets/Game/Data/TankFree.asset":role==UnitRole.Logistics?"Assets/Game/Data/Logistics/TruckFreeMarch.asset":"Assets/Game/Data/CombinedArms/"+role+"FreeMarch.asset";SetMovement(AssetDatabase.LoadAssetAtPath<ArmyFreeMarchDefinition>(path),role);}
            var reference=UnityEngine.Object.FindObjectOfType<MapReferenceHUD>();if(reference!=null)c.combined.engineering.metersPerWorldUnit=reference.metersPerWorldUnit;
            EditorUtility.SetDirty(c.rules);EditorUtility.SetDirty(c.combined.engineering);EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());AssetDatabase.SaveAssets();Debug.Log("FORT_LINE_CONFIGURED");
        }
    }
}
