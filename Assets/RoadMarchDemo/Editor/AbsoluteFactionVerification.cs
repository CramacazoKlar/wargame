using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using CampaignDemo;

[InitializeOnLoad]
public static class AbsoluteFactionVerification
{
    const string Key = "AbsoluteFactionVerification";
    static double started;
    static readonly List<string> checks = new List<string>();
    static AbsoluteFactionVerification()
    {
        if (SessionState.GetBool(Key, false)) { started = EditorApplication.timeSinceStartup; EditorApplication.update += Poll; }
    }
    public static void Run()
    {
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/Battle01.unity");
        var world = UnityEngine.Object.FindObjectOfType<BattleWorld>();
        if (world == null || !world.players.Contains(BattleFactions.BluePlayerId) || !world.players.Contains(BattleFactions.RedPlayerId))
            throw new Exception("Expected fixed A/B factions");
        var controllers = UnityEngine.Object.FindObjectsOfType<BattleAIController>(true);
        if (controllers.Length != 1) throw new Exception("Expected one AI commander, found " + controllers.Length);
        var ai = controllers[0];
        Check(UnityEngine.Object.FindObjectsOfType<BattleUnit>(true).All(u => PrefabUtility.IsPartOfPrefabInstance(u.gameObject)), "all saved scene armies retain prefab links");
        ai.playerId = BattleFactions.RedPlayerId;
        ai.autoToggleOnPlayerSwitch = true;
        ai.pauseWhenPlayerControlled = true;
        ai.aiEnabled = world.currentPlayer != BattleFactions.RedPlayerId;
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        SessionState.SetBool(Key, true);
        started = EditorApplication.timeSinceStartup;
        EditorApplication.update += Poll;
        EditorApplication.isPlaying = true;
    }
    static void Check(bool condition, string label)
    {
        if (!condition) throw new Exception(label);
        checks.Add("PASS " + label);
    }
    static void Poll()
    {
        try
        {
            if (EditorApplication.timeSinceStartup - started > 100) throw new Exception("Play mode verification timed out");
            if (!EditorApplication.isPlaying || Time.time < 1) return;
            var world = UnityEngine.Object.FindObjectOfType<BattleWorld>();
            if (world == null || !world.Initialized) return;
            var ai = world.GetComponent<BattleAIController>();
            var ownerIds = world.units.Select(u => u.ownerId).ToArray();
            var red = world.units.First(u => u.ownerId == BattleFactions.RedPlayerId);
            var blue = world.units.First(u => u.ownerId == BattleFactions.BluePlayerId);
            Check(BattleFactions.Absolute("A") == AbsoluteFaction.Blue, "A is absolute blue");
            Check(BattleFactions.Absolute("B") == AbsoluteFaction.Red, "B is absolute red");
            Check(BattleFactions.Relative("", "A") == RelativeFaction.Neutral, "unowned remains neutral");
            world.SetPlayer(BattleFactions.BluePlayerId);
            ai.Refresh();
            Check(ai.CanThink && ai.playerId == "B", "blue player enables only absolute red AI");
            Check(ai.Orders.All(o => o.GetComponent<BattleUnit>().ownerId == "B"), "commander has no blue units");
            Check(world.RelativeToCurrentPlayer("A") == RelativeFaction.Friendly && world.RelativeToCurrentPlayer("B") == RelativeFaction.Enemy, "blue perspective is relative friendly blue / enemy red");
            var redOrder = red.GetComponent<AIUnitOrder>();
            Check(redOrder != null, "red executor exists");
            var blueOrder = blue.GetComponent<AIUnitOrder>();
            if (blueOrder == null) blueOrder = blue.gameObject.AddComponent<AIUnitOrder>();
            blueOrder.task = ai.defaultGuard;
            Check(!ai.IssueCommand(blueOrder, new BattleCommand { kind = BattleCommandKind.StopMovement }), "AI cannot command blue even with an executor");
            ai.reservations.ClaimUnit(redOrder.RuntimeTaskId, red.unitId);
            ai.reservations.Reserve(redOrder.RuntimeTaskId, "handoff-test", 1, 10);
            var free = red.GetComponent<ArmyFreeMarchMover>();
            var from = red.transform.position;
            var destination = from;
            foreach (var offset in new[] { Vector3.right, Vector3.left, Vector3.up, Vector3.down })
            {
                var candidate = from + offset * .1f;
                if (free.CanTraverse(world.setup.freeMarchMap, new RoadPoint(from.x, from.y), new RoadPoint(candidate.x, candidate.y))) { destination = candidate; break; }
            }
            Check(destination != from && ai.IssueCommand(redOrder, new BattleCommand { kind = BattleCommandKind.FreeDestination, destination = destination }) && red.Motor.IsMoving, "AI-owned red movement starts before handoff");
            world.SetPlayer(BattleFactions.RedPlayerId);
            Check(!ai.aiEnabled && !ai.CanThink, "red manual control disables AI immediately");
            Check(!red.Motor.IsMoving, "handoff stops previous AI movement");
            Check(!ai.reservations.HasTask(redOrder.RuntimeTaskId), "manual handoff releases task reservations");
            Check(world.RelativeToCurrentPlayer("B") == RelativeFaction.Friendly && world.RelativeToCurrentPlayer("A") == RelativeFaction.Enemy, "red perspective reverses relative sides");
            var count = redOrder.SubmittedCommands;
            ai.Tick(10);
            Check(count == redOrder.SubmittedCommands, "red manual receives no AI tick commands");
            ai.aiEnabled = true;
            ai.pauseWhenPlayerControlled = false;
            ai.autoToggleOnPlayerSwitch = false;
            Check(!ai.CanThink && !ai.IssueCommand(redOrder, new BattleCommand { kind = BattleCommandKind.StopMovement }), "legacy toggles cannot bypass manual red lock");
            Check(ai.ActivateTask(redOrder.taskKey) == 0, "manual red cannot activate AI tasks");
            ai.autoToggleOnPlayerSwitch = true;
            ai.pauseWhenPlayerControlled = true;
            for (int i = 0; i < 5; i++)
            {
                world.SetPlayer("A");
                Check(ai.CanThink && ai.playerId == "B", "switch back resumes red only " + i);
                world.SetPlayer("B");
                Check(!ai.CanThink && ai.playerId == "B", "repeat red manual lock " + i);
            }
            Check(ownerIds.SequenceEqual(world.units.Select(u => u.ownerId)), "perspective switching never changes unit ownership");
            world.SetPlayer("A");
            ai.playerId = "A";
            Check(!ai.CanThink, "misconfigured blue AI is blocked");
            ai.Refresh();
            Check(ai.playerId == "B" && ai.Orders.All(o => o.GetComponent<BattleUnit>().ownerId == "B"), "refresh restores fixed red identity");
            File.WriteAllText("absolute-faction-verification-result.txt", checks.Count + " checks passed\n" + string.Join("\n", checks));
            Debug.Log("ABSOLUTE_FACTIONS_PASS " + checks.Count);
            SessionState.SetBool(Key, false);
            EditorApplication.update -= Poll;
            EditorApplication.Exit(0);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            File.WriteAllText("absolute-faction-verification-result.txt", exception + "\n" + string.Join("\n", checks));
            SessionState.SetBool(Key, false);
            EditorApplication.update -= Poll;
            EditorApplication.Exit(1);
        }
    }
}
