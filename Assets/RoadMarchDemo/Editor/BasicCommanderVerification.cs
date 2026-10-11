using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using CampaignDemo;

[InitializeOnLoad]
public static class BasicCommanderVerification
{
    const string Key="BasicCommanderVerification";
    static double started;static int stage;static float mark;
    static Vector3[] initialRed,initialBlue;
    static readonly List<string> checks=new List<string>();
    static BasicCommanderVerification(){if(SessionState.GetBool(Key,false)){started=EditorApplication.timeSinceStartup;EditorApplication.update+=Poll;}}
    public static void Run()
    {
        var scene=EditorSceneManager.OpenScene("Assets/Scenes/Battle01.unity");
        var world=UnityEngine.Object.FindObjectOfType<BattleWorld>();
        var ai=world.GetComponent<BattleAIController>();
        if(ai==null)throw new Exception("AI controller missing");
        string path="Assets/Game/Data/AI/BasicCityCommander.asset";
        var profile=AssetDatabase.LoadAssetAtPath<AIBasicProfile>(path);
        if(profile==null){profile=ScriptableObject.CreateInstance<AIBasicProfile>();AssetDatabase.CreateAsset(profile,path);}
        string behaviorPath="Assets/Game/Data/AI/BasicCityBehavior.asset";
        var behavior=AssetDatabase.LoadAssetAtPath<AIBehaviorProfile>(behaviorPath);
        if(behavior==null){behavior=ai.profile==null?ScriptableObject.CreateInstance<AIBehaviorProfile>():UnityEngine.Object.Instantiate(ai.profile);AssetDatabase.CreateAsset(behavior,behaviorPath);}
        behavior.recovery=profile.recover;behavior.supply=profile.transportResources;
        ai.profile=behavior;ai.playerId=BattleFactions.RedPlayerId;ai.autoToggleOnPlayerSwitch=true;ai.pauseWhenPlayerControlled=true;
        ai.enabled=true;ai.aiEnabled=world.currentPlayer!=BattleFactions.RedPlayerId;
        var commander=world.GetComponent<AIBasicCommander>();if(commander==null)commander=world.gameObject.AddComponent<AIBasicCommander>();
        commander.controller=ai;commander.profile=profile;ai.basicCommander=commander;
        foreach(var u in world.units)if(u!=null&&u.ownerId==BattleFactions.RedPlayerId)
        {
            var order=u.GetComponent<AIUnitOrder>();if(order==null)order=u.gameObject.AddComponent<AIUnitOrder>();
            if(order.task==null)order.task=ai.defaultGuard;
            if(order.task.kind==AITaskKind.Guard){order.profileOverride=behavior;PrefabUtility.RecordPrefabInstancePropertyModifications(order);}
        }
        EditorUtility.SetDirty(profile);EditorUtility.SetDirty(behavior);
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
        SessionState.SetBool(Key,true);started=EditorApplication.timeSinceStartup;EditorApplication.update+=Poll;EditorApplication.isPlaying=true;
    }
    static void Check(bool ok,string label){if(!ok)throw new Exception(label);checks.Add("PASS "+label);}
    static void Poll()
    {
        try
        {
            if(EditorApplication.timeSinceStartup-started>140)throw new Exception("Runtime verification timeout");
            if(!EditorApplication.isPlaying||Time.time<1)return;
            var world=UnityEngine.Object.FindObjectOfType<BattleWorld>();if(world==null||!world.Initialized)return;
            var ai=world.GetComponent<BattleAIController>();var commander=ai.basicCommander;
            var reds=world.units.Where(u=>u.ownerId=="B").ToArray();var blues=world.units.Where(u=>u.ownerId=="A").ToArray();
            if(stage==0)
            {
                checks.Add("INFO cities: "+string.Join("; ",world.points.Select(p=>p.displayName+" owner="+p.ownerId+" stock="+p.resourceStock+" pos="+p.transform.position)));
                checks.Add("INFO commander: "+commander.Status+" defenders="+commander.DefenseAllocated+" required="+commander.DefenseRequirement+" attack="+commander.AttackStrength);
                Check(world.currentPlayer=="A"&&ai.CanThink,"scene starts with blue player and red AI active");
                Check(commander.target!=null&&commander.Attackers.Count>0,"actual map launches attack against a city: "+commander.Status);
                Check(commander.DefenseAllocated>=commander.DefenseRequirement,"whole units satisfy fixed and proportional defense floor");
                Check(commander.Attackers.All(u=>u.ownerId=="B"),"attack squad contains only absolute red units");
                Check(reds.Any(u=>u.Motor.IsMoving),"red has accepted real movement commands");
                Check(world.campaign.logistics.collectors.Any(c=>c!=null&&c.Alive&&c.ownerId=="B"),"resource production deploys a real red collector through game rules");
                initialRed=reds.Select(u=>u.transform.position).ToArray();initialBlue=blues.Select(u=>u.transform.position).ToArray();
                mark=Time.time;stage=1;return;
            }
            if(Time.time-mark<3)return;
            Check(reds.Where((u,i)=>Vector2.Distance(u.transform.position,initialRed[i])>.000001f).Any(),"red physically moves in live simulation");
            Check(blues.Where((u,i)=>Vector2.Distance(u.transform.position,initialBlue[i])>.000001f).Count()==0,"blue receives no automated movement");
            var profile=commander.profile;float floor=profile.defenseRatio;
            world.SetPlayer("B");Check(!ai.CanThink&&commander.target==null,"manual red cancels commander mission");
            Check(reds.All(u=>!u.Motor.IsMoving),"manual red stops all AI movement");
            var counts=reds.Select(u=>u.GetComponent<AIUnitOrder>().SubmittedCommands).ToArray();ai.Tick(10);
            Check(counts.SequenceEqual(reds.Select(u=>u.GetComponent<AIUnitOrder>().SubmittedCommands)),"manual red receives no AI commands");
            world.SetPlayer("A");ai.Tick(2);Check(ai.CanThink&&commander.target!=null,"switch back restarts red mission");
            var attacking=commander.Attackers.First();var response=attacking.GetComponent<AIRecoveryResponse>();
            attacking.Damage(attacking.Personnel*.7f);ai.Tick(2);
            Check(!commander.Attackers.Contains(attacking),"low-health individual leaves attack squad");
            Check(response!=null&&response.IsRecovering,"damaged attacker starts recovery response");
            Check(commander.phase==AIBasicPhase.Withdrawing||commander.target==null,"remaining squad withdraws after losses");
            attacking.Restore(attacking.Capacity);response.CancelResponse();
            world.SetPlayer("B");var stock=world.points.Select(p=>p.resourceStock).ToArray();foreach(var city in world.points)if(city.ownerId=="B")city.resourceStock=0;
            world.SetPlayer("A");ai.Tick(2);
            Check(commander.target==null&&commander.Status.Contains("资源"),"insufficient resources prevent a new attack");
            for(int i=0;i<world.points.Length;i++)world.points[i].resourceStock=stock[i];
            world.SetPlayer("B");profile.defenseRatio=1;world.SetPlayer("A");ai.Tick(2);
            Check(commander.target==null,"100 percent defense leaves no attack troops");
            profile.defenseRatio=floor;
            world.SetPlayer("B");world.SetPlayer("A");ai.Tick(2);
            Check(commander.target!=null,"restored thresholds launch a new mission");
            var target=commander.target;target.ownerId="B";commander.Plan(2);
            Check(commander.target==null,"capture ends old mission before assigning next city");
            Check(Mathf.Abs(commander.Strength(reds[0])-reds[0].Personnel/world.campaign.rules.troopStrength[0])<.001f,"strength uses current personnel in squad equivalents");
            File.WriteAllText("basic-commander-verification-result.txt",checks.Count+" entries\n"+string.Join("\n",checks));
            Debug.Log("BASIC_COMMANDER_PASS "+checks.Count);SessionState.SetBool(Key,false);EditorApplication.update-=Poll;EditorApplication.Exit(0);
        }
        catch(Exception exception)
        {
            Debug.LogException(exception);File.WriteAllText("basic-commander-verification-result.txt",exception+"\n"+string.Join("\n",checks));
            SessionState.SetBool(Key,false);EditorApplication.update-=Poll;EditorApplication.Exit(1);
        }
    }
}
