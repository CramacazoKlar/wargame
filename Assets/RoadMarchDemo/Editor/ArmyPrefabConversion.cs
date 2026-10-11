using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using CampaignDemo;

public static class ArmyPrefabConversion
{
    const string Scene = "Assets/Scenes/Battle01.unity";
    const string Folder = "Assets/Game/Prefabs/SceneArmies";
    static string Signature(BattleUnit u)
    {
        return u.unitId + "|" + u.ownerId + "|" + u.name + "|" + u.startingPersonnelPercent
            + "|" + AssetDatabase.GetAssetPath(u.combatDefinition)
            + "|" + u.transform.localPosition.ToString("R")
            + "|" + u.transform.localRotation.ToString("R")
            + "|" + u.transform.localScale.ToString("R")
            + "|" + (u.transform.parent == null ? "" : u.transform.parent.name)
            + "|" + string.Join(",", u.GetComponents<Component>().Select(c => c == null ? "Missing" : c.GetType().FullName));
    }
    public static void Run()
    {
        try
        {
            var scene = EditorSceneManager.OpenScene(Scene);
            var units = UnityEngine.Object.FindObjectsOfType<BattleUnit>(true).OrderBy(u => u.unitId).ToArray();
            var before = units.Select(Signature).ToArray();
            var world = UnityEngine.Object.FindObjectOfType<BattleWorld>();
            if (world == null) throw new Exception("BattleWorld missing");
            var registered = world.units.Select(u => u == null ? "NULL" : u.unitId).ToArray();
            var movers = world.setup.sceneArmies.Select(m => m == null ? "NULL" : m.GetComponent<BattleUnit>().unitId).ToArray();
            Directory.CreateDirectory(Folder);
            int converted = 0;
            var paths = new List<string>();
            foreach (var unit in units)
            {
                if (PrefabUtility.IsPartOfPrefabInstance(unit.gameObject)) continue;
                string name = unit.name;
                foreach (char invalid in Path.GetInvalidFileNameChars()) name = name.Replace(invalid, '_');
                string path = AssetDatabase.GenerateUniqueAssetPath(Folder + "/" + name + ".prefab");
                bool success;
                PrefabUtility.SaveAsPrefabAssetAndConnect(unit.gameObject, path, InteractionMode.AutomatedAction, out success);
                if (!success) throw new Exception("Prefab conversion failed: " + unit.name);
                paths.Add(path);
                converted++;
            }
            if (!before.SequenceEqual(units.Select(Signature))) throw new Exception("Army configuration changed during conversion");
            if (world.units.Any(u => u == null) || world.setup.sceneArmies.Any(m => m == null)) throw new Exception("Scene reference lost");
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new Exception("Scene save failed");
            AssetDatabase.SaveAssets();
            EditorSceneManager.OpenScene(Scene);
            var after = UnityEngine.Object.FindObjectsOfType<BattleUnit>(true).OrderBy(u => u.unitId).ToArray();
            world = UnityEngine.Object.FindObjectOfType<BattleWorld>();
            if (!before.SequenceEqual(after.Select(Signature))) throw new Exception("Configuration changed after scene reload");
            if (after.Any(u => !PrefabUtility.IsPartOfPrefabInstance(u.gameObject))) throw new Exception("Unlinked army remains");
            if (!registered.SequenceEqual(world.units.Select(u => u == null ? "NULL" : u.unitId))) throw new Exception("World references changed");
            if (!movers.SequenceEqual(world.setup.sceneArmies.Select(m => m == null ? "NULL" : m.GetComponent<BattleUnit>().unitId))) throw new Exception("Movement references changed");
            string report = "PASS\nScene: " + Scene + "\nArmies: " + after.Length + "\nConverted: " + converted
                + "\nVerified all army prefab links, identities, owners, positions, rotations, scales, component lists, combat definitions, starting personnel, world and movement references after reload.\nAI unchanged.\n"
                + string.Join("\n", paths);
            File.WriteAllText("army-prefab-conversion-result.txt", report);
            Debug.Log(report);
            EditorApplication.Exit(0);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            File.WriteAllText("army-prefab-conversion-result.txt", exception.ToString());
            EditorApplication.Exit(1);
        }
    }
}
