using System;
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
namespace CampaignDemo.Editor
{
    [InitializeOnLoad]
    public static class MountainRuntimeVerification
    {
        static double start;static bool done;
        static MountainRuntimeVerification(){if(SessionState.GetBool("MountainRuntime",false)){start=EditorApplication.timeSinceStartup;EditorApplication.update+=Poll;}}
        public static void Run(){EditorSceneManager.OpenScene("Assets/Scenes/Battle01.unity");SessionState.SetBool("MountainRuntime",true);start=EditorApplication.timeSinceStartup;EditorApplication.update+=Poll;EditorApplication.isPlaying=true;}
        static void Require(bool value,string name){if(!value)throw new Exception(name);}
        static void Poll()
        {
            try
            {
                if(done)return;if(EditorApplication.timeSinceStartup-start>90)throw new Exception("Mountain runtime timeout");if(!EditorApplication.isPlaying||Time.time<1)return;
                var world=UnityEngine.Object.FindObjectOfType<BattleWorld>();Require(world!=null&&world.Initialized,"World initialized");var hud=UnityEngine.Object.FindObjectOfType<BattleHUD>();Require(hud!=null&&hud.GetComponent<CombinedArmsHUD>()!=null,"Mode3 specialty UI initialized");world.setup.modes.EnterInspectionMode();
                PersonnelCarrier carrier=null;bool mountain=false;foreach(var u in world.units){var free=u.GetComponent<ArmyFreeMarchMover>();Require(free!=null,"Every unit has movement component");Require((free.AllowedTerrain&TerrainAccess.Water)==0,"Water forbidden for every unit");Require(free.AllowedTerrain==MountainTerrainInstaller.Access(u.combatDefinition.role),"Runtime terrain permission matches role");if(u.GetComponent<PersonnelCarrier>()!=null)carrier=u.GetComponent<PersonnelCarrier>();if(u.combatDefinition.role==UnitRole.MountainInfantry)mountain=true;}
                Require(mountain&&carrier!=null,"Mountain infantry and carrier examples available");var road=carrier.GetComponent<ArmyRoadMover>();Require(road.terrainMap==world.setup.freeMarchMap,"Road movement terrain binding present");
                var at=new RoadPoint(road.transform.position.x,road.transform.position.y);var hits=world.setup.roadMap.Network.Candidates(at,.02);Require(hits.Count>0,"Carrier starts on road network");bool moved=false;foreach(var hit in hits){var edge=world.setup.roadMap.Network.Edges[hit.Edge];foreach(var node in new[]{world.setup.roadMap.Network.Nodes[edge.A],world.setup.roadMap.Network.Nodes[edge.B]}){double distance=RoadPoint.Distance(at,node);if(distance<.02)continue;var target=RoadPoint.Lerp(at,node,Math.Min(1,.1/distance));if(road.CommitAutomaticRoute(new[]{at,target})){moved=true;break;}}if(moved)break;}
                Require(moved,"Carrier accepts actual scene road route");road.Motor.Stop();bool forbidden=false;for(int i=0;i<16;i++){float a=i*Mathf.PI/8;var destination=new RoadPoint(at.X+Math.Cos(a),at.Y+Math.Sin(a));if(!road.CommitAutomaticRoute(new[]{at,destination})){forbidden=true;break;}road.Motor.Stop();}Require(forbidden,"Carrier rejects actual off road movement");hud.SelectUnit(carrier.Unit);done=true;
                File.WriteAllText("mountain-runtime-verification.txt","PASS: scene initializes, all units have correct terrain permissions, water forbidden, mountain infantry present, carrier starts on road, road movement works, off-road movement rejected, mode3 UI initialized.");SessionState.SetBool("MountainRuntime",false);EditorApplication.update-=Poll;EditorApplication.Exit(0);
            }
            catch(Exception e){File.WriteAllText("mountain-runtime-verification.txt",e.ToString());Debug.LogException(e);SessionState.SetBool("MountainRuntime",false);EditorApplication.update-=Poll;EditorApplication.Exit(1);}
        }
    }
}
