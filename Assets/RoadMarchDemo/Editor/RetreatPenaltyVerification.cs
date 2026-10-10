using System;
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
namespace CampaignDemo.Editor
{
    public static class RetreatPenaltyVerification
    {
        static int checks;
        static void Check(bool condition,string message){if(!condition)throw new Exception(message);checks++;}
        public static void Run()
        {
            try
            {
                EditorSceneManager.OpenScene("Assets/Scenes/Battle01.unity");var sceneRules=UnityEngine.Object.FindObjectOfType<CampaignRuntime>().rules;EditorUtility.SetDirty(sceneRules);AssetDatabase.SaveAssets();var root=new GameObject("retreat tests");var runtime=root.AddComponent<CampaignRuntime>();runtime.enabled=false;runtime.rules=ScriptableObject.CreateInstance<CampaignRules>();runtime.rules.troopStrength=new[]{100f};
                var victim=Unit(root,"victim","A",new Vector3(1,0),runtime);var attacker=Unit(root,"attacker","B",Vector3.zero,runtime);var state=victim.GetComponent<CampaignUnit>();
                Check(state.ResolveRetreat(.1f,runtime.rules,1,Vector2.right)==0,"unattacked movement has no penalty");state.ObserveAttack(attacker);
                Check(state.ResolveRetreat(.1f,runtime.rules,0,Vector2.right)==0,"stationary facing cannot trigger penalty");Check(state.ResolveRetreat(.1f,runtime.rules,.01f,Vector2.right)==0,"below minimum speed cannot trigger");Check(state.ResolveRetreat(.1f,runtime.rules,1,Vector2.left)==0,"advancing toward attacker cannot trigger");Check(state.ResolveRetreat(.1f,runtime.rules,1,Vector2.up)==0,"pure perpendicular movement cannot trigger");
                float before=victim.Personnel;float loss=state.ResolveRetreat(.1f,runtime.rules,1,Vector2.right);Check(Mathf.Abs(loss-before*.075f)<.001f,"configured current strength percentage lost");Check(Mathf.Abs(victim.Personnel-92.5f)<.001f,"one retreat charge");for(int i=0;i<20;i++){state.BeginCombatObservation();state.ObserveAttack(attacker);Check(state.ResolveRetreat(.1f,runtime.rules,1,i%2==0?Vector2.right:Vector2.left)==0,"turn stop resume cannot rearm active encounter");}
                Check(state.RetreatCharges==1,"one charge for all active opponents");state.BeginCombatObservation();state.ResolveRetreat(runtime.rules.retreatEncounterResetSeconds+.1f,runtime.rules,0,Vector2.zero);state.ObserveAttack(attacker);Check(state.ResolveRetreat(.1f,runtime.rules,1,Vector2.right)>0&&state.RetreatCharges==2,"new encounter rearms after configured quiet interval");
                victim.Initialize();state.ObserveAttack(attacker);var ally=Unit(root,"ally","A",Vector3.zero,runtime);state.ObserveAttack(ally);Check(state.ResolveRetreat(.1f,runtime.rules,1,Vector2.right)>0,"enemy still triggers with ally nearby");victim.Initialize();state.ObserveAttack(ally);Check(state.ResolveRetreat(.1f,runtime.rules,1,Vector2.right)==0,"friendly fire cannot cause retreat penalty");
                victim.Initialize();state.ObserveAttack(attacker);attacker.Eliminate();Check(state.ResolveRetreat(.1f,runtime.rules,1,Vector2.right)==0,"dead attacker cannot trigger penalty");attacker.Initialize();victim.Initialize();runtime.rules.retreatLossFraction=.05f;state.ObserveAttack(attacker);Check(Mathf.Abs(state.ResolveRetreat(.1f,runtime.rules,1,Vector2.right)-5)<.001f,"5 percent configurable");victim.Initialize();runtime.rules.retreatLossFraction=.10f;state.ObserveAttack(attacker);Check(Mathf.Abs(state.ResolveRetreat(.1f,runtime.rules,1,Vector2.right)-10)<.001f,"10 percent configurable");
                victim.Initialize();state.ObserveAttack(attacker);var motor=victim.gameObject.AddComponent<ArmyRouteMotor>();typeof(ArmyRouteMotor).GetField("<CurrentSpeed>k__BackingField",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).SetValue(motor,1f);state.RecordMovement(Vector3.right*.01f);motor.Stop();state.ResolveRetreatMovement(.1f,runtime.rules);Check(state.RetreatCharges==1&&Mathf.Abs(victim.Personnel-90)<.001f,"brief movement then stop between combat ticks still penalized");
                UnityEngine.Object.DestroyImmediate(root);CampaignRevisionVerification.Validate();File.WriteAllText("retreat-verification.txt",checks+" retreat checks passed; campaign regression passed");Debug.Log("RETREAT_VERIFY_PASS "+checks);EditorApplication.Exit(0);
            }
            catch(Exception e){File.WriteAllText("retreat-verification.txt",e.ToString());Debug.LogException(e);EditorApplication.Exit(1);}
        }
        static BattleUnit Unit(GameObject root,string id,string owner,Vector3 position,CampaignRuntime runtime)
        {var g=new GameObject(id);g.transform.SetParent(root.transform);g.transform.position=position;var unit=g.AddComponent<BattleUnit>();unit.unitId=id;unit.ownerId=owner;unit.combatDefinition=ScriptableObject.CreateInstance<ArmyCombatDefinition>();unit.combatDefinition.echelon=ArmyEchelon.Squad;runtime.ConfigureUnit(unit);unit.Initialize();return unit;}
    }
}

