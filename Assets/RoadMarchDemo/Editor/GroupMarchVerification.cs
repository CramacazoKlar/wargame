using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using CampaignDemo;
[InitializeOnLoad]
public static class GroupMarchVerification
{
    const string Key="GroupMarchVerification";static double start;static int stage;static float mark;static Vector3[] positions;
    static readonly List<string> checks=new List<string>();
    static GroupMarchVerification(){if(SessionState.GetBool(Key,false)){start=EditorApplication.timeSinceStartup;EditorApplication.update+=Poll;}}
    public static void Run(){EditorSceneManager.OpenScene("Assets/Scenes/Battle01.unity");SessionState.SetBool(Key,true);start=EditorApplication.timeSinceStartup;EditorApplication.update+=Poll;EditorApplication.isPlaying=true;}
    static void Check(bool ok,string label){if(!ok)throw new Exception(label);checks.Add("PASS "+label);}
    static void Poll(){try{
        if(EditorApplication.timeSinceStartup-start>120)throw new Exception("Group test timeout");if(!EditorApplication.isPlaying||Time.time<1)return;
        var world=UnityEngine.Object.FindObjectOfType<BattleWorld>();if(world==null||!world.Initialized)return;
        var selection=world.GetComponent<ArmySelection>();var group=world.GetComponent<GroupMarchController>();var blue=world.units.Where(u=>u.ownerId=="A").ToArray();
        if(stage==0)
        {
            var ai=world.GetComponent<BattleAIController>();if(ai!=null)ai.aiEnabled=false;world.enabled=false;world.SetPlayer("A");if(ai!=null)ai.aiEnabled=false;
            var camera=selection.mapCamera;camera.transform.position=new Vector3(-12,0,camera.transform.position.z);camera.orthographicSize=20;
            var road=world.setup.roadMap.Network;
            for(int i=0;i<blue.Length;i++){blue[i].Motor.Stop();var p=road.Nodes[i+1];blue[i].transform.position=new Vector3((float)p.X,(float)p.Y);blue[i].UnderFire=false;}
            var rect=camera.pixelRect;int expected=world.setup.sceneArmies.Count(a=>a!=null&&a.GetComponent<BattleUnit>().ownerId=="A"&&a.GetComponent<BattleUnit>().OnMap&&rect.Contains(camera.WorldToScreenPoint(a.transform.position)));
            Check(selection.SelectBox(rect)==expected&&expected>1,"box selects multiple visible owned units");Check(selection.SelectedArmies.All(a=>a.GetComponent<BattleUnit>().ownerId=="A"),"box excludes enemies");
            world.setup.modes.SetMode(MouseCommandMode.RoadMarch);world.setup.modes.SetMode(MouseCommandMode.FreeMarch);Check(selection.HasBoxSelection&&selection.SelectedArmies.Count==expected,"mode switches preserve box selection");
            var goal=(blue[0].transform.position+blue[1].transform.position)*.5f;
            var members=selection.SelectedArmies.ToArray();var starts=members.Select(a=>a.transform.position).ToArray();var center=Vector3.zero;foreach(var p in starts)center+=p;center/=starts.Length;
            Check(group.Begin(goal,MouseCommandMode.FreeMarch),"group free order starts without clicking a unit");int accepted=group.Commit();Check(accepted==expected,"same free movement command reaches every selected unit");
            for(int i=0;i<members.Length;i++){var route=members[i].Motor.CopyRoute();var end=route[route.Count-1];var expectedEnd=goal+starts[i]-center;Check(Vector2.Distance(new Vector2((float)end.X,(float)end.Y),expectedEnd)<.001f,"group free endpoint preserves member offset "+i);}
            Check(selection.SelectedArmies.All(a=>!a.PlanningPaused&&a.Motor.IsMoving),"group command releases planning pause for all units");
            positions=blue.Select(u=>u.transform.position).ToArray();mark=Time.time;stage=1;return;
        }
        if(Time.time-mark<.5f)return;
        Check(blue.Where((u,i)=>Vector2.Distance(u.transform.position,positions[i])>.000001f).Count()>1,"multiple selected units physically advance");
        foreach(var u in blue)u.Motor.Stop();
        world.setup.modes.SetMode(MouseCommandMode.RoadMarch);
        Check(group.Begin(new Vector3(100000,100000),MouseCommandMode.RoadMarch),"road group accepts arbitrarily distant cursor");int selectedCount=selection.SelectedArmies.Count;Check(group.Commit()==selectedCount,"distant road cursor snaps and commits for all selected units");
        var net=world.setup.roadMap.Network;
        foreach(var a in selection.SelectedArmies){var route=a.Motor.CopyRoute();Check(RoadOrderPlanner.Validate(net,route,false),"committed group route follows road graph");}
        var startHit=net.Candidates(new RoadPoint(blue[0].transform.position.x,blue[0].transform.position.y),double.PositiveInfinity)[0];RoadHit next;List<RoadPoint> path;
        Check(net.SnapConnected(startHit,new RoadPoint(-100000,-100000),out next,out path),"single road sampler has no cursor distance cutoff");
        var disconnected=new RoadNetwork(new[]{new RoadPoint(0,0),new RoadPoint(10,0),new RoadPoint(100,0),new RoadPoint(110,0)},new[]{new RoadEdge(0,1),new RoadEdge(2,3)});
        Check(disconnected.SnapConnected(disconnected.Candidates(new RoadPoint(0,0),1)[0],new RoadPoint(110,1000),out next,out path)&&next.Edge==0,"snapping chooses reachable roads instead of teleporting between disconnected components");
        group.Begin(new Vector3(0,0),MouseCommandMode.RoadMarch);world.setup.modes.SetMode(MouseCommandMode.FreeMarch);Check(selection.SelectedArmies.All(a=>!a.PlanningPaused),"mode switch cancels draft and releases all members");
        group.Begin(new Vector3(0,0),MouseCommandMode.FreeMarch);selection.Clear();Check(blue.All(u=>!u.Motor.PlanningPaused),"clear selection releases draft pause");
        var cameraController=world.setup.panController;cameraController.useBounds=false;var before=selection.mapCamera.transform.position;cameraController.MoveKeyboard(Vector2.zero,1);Check(selection.mapCamera.transform.position==before,"no key axis leaves map unchanged");cameraController.MoveKeyboard(Vector2.right,.1f);Check(selection.mapCamera.transform.position.x>before.x,"keyboard pan still moves map");
        selection.SelectBox(selection.mapCamera.pixelRect);world.SetPlayer("B");Check(selection.SelectedArmies.Count==0,"faction change clears old side's selection");
        File.WriteAllText("group-march-verification-result.txt",checks.Count+" checks passed\n"+string.Join("\n",checks));Debug.Log("GROUP_MARCH_PASS "+checks.Count);SessionState.SetBool(Key,false);EditorApplication.update-=Poll;EditorApplication.Exit(0);
    }catch(Exception e){Debug.LogException(e);File.WriteAllText("group-march-verification-result.txt",e+"\n"+string.Join("\n",checks));SessionState.SetBool(Key,false);EditorApplication.update-=Poll;EditorApplication.Exit(1);}}
}
