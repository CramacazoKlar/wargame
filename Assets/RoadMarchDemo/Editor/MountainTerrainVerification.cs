using System;
using System.IO;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEditor;
namespace CampaignDemo.Editor
{
    public static class MountainTerrainVerification
    {
        static readonly List<string> results=new List<string>();
        static void Check(bool value,string name){if(!value)throw new Exception(name);results.Add("PASS "+name);}
        static FreeMarchMapDefinition.Region RectRegion(TerrainKind kind,float x0,float x1,float y0,float y1)
        {return new FreeMarchMapDefinition.Region{terrain=kind,polygon=new[]{new Vector2(x0,y0),new Vector2(x1,y0),new Vector2(x1,y1),new Vector2(x0,y1)}};}
        public static void Run(){try{MountainTerrainInstaller.Configure();Tests();File.WriteAllText("mountain-verification.txt",results.Count+" checks passed\n"+string.Join("\n",results));Debug.Log("MOUNTAIN_VERIFY_PASS "+results.Count);EditorApplication.Exit(0);}catch(Exception e){File.WriteAllText("mountain-verification.txt",e+"\n"+string.Join("\n",results));Debug.LogException(e);EditorApplication.Exit(1);}}
        static void Tests()
        {
            Check((int)TerrainKind.Mountain==3&&(int)TerrainKind.Forest==6&&(int)TerrainKind.HighMountain==7,"existing terrain IDs preserved, high mountain appended");
            foreach(UnitRole role in Enum.GetValues(typeof(UnitRole)))foreach(TerrainKind kind in Enum.GetValues(typeof(TerrainKind)))
            {
                var geo=new TerrainGeometry{Min=new RoadPoint(-1,-1),Max=new RoadPoint(1,1),DefaultTerrain=kind};bool expected=kind!=TerrainKind.Water&&kind!=TerrainKind.Blocked;
                if(kind==TerrainKind.HighMountain)expected=role==UnitRole.MountainInfantry;
                else if(role==UnitRole.PersonnelCarrier)expected=kind==TerrainKind.Road;
                else if(kind==TerrainKind.Mountain)expected=role!=UnitRole.Artillery&&role!=UnitRole.Tank;
                Check(geo.CanTraverse(new RoadPoint(-.5,0),new RoadPoint(.5,0),MountainTerrainInstaller.Access(role))==expected,"terrain matrix "+role+" / "+kind);
            }
            var root=new GameObject("Mountain mobility test");var map=root.AddComponent<FreeMarchMapRuntime>();var data=ScriptableObject.CreateInstance<FreeMarchMapDefinition>();data.bounds=new Rect(-10,-10,20,20);data.defaultTerrain=TerrainKind.Ground;data.regions=new[]{RectRegion(TerrainKind.Road,-9,9,-.1f,.1f),RectRegion(TerrainKind.Mountain,2,3,-3,3),RectRegion(TerrainKind.HighMountain,4,4.005f,-3,3),RectRegion(TerrainKind.Water,6,7,-3,3)};map.definition=data;
            var g=new GameObject("test mover");g.transform.SetParent(root.transform);var road=g.AddComponent<ArmyRoadMover>();var movement=ScriptableObject.CreateInstance<ArmyDefinition>();movement.speed=2;movement.freeMarchDefinition=MountainTerrainInstaller.Free(UnitRole.MountainInfantry);road.definition=movement;
            var graph=new RoadNetwork(new[]{new RoadPoint(-8,0),new RoadPoint(8,0)},new[]{new RoadEdge(0,1)});var view=RoadRouteView.Create(g.transform,"test route",.03f,1,Color.white);road.InitializeAt(graph,new RoadPoint(0,1),view);var free=g.AddComponent<ArmyFreeMarchMover>();free.definition=movement.freeMarchDefinition;free.Initialize(road.Motor);free.BindMap(map);
            Check(road.terrainMap==map,"shared terrain bound to road mover");Check(road.CommitAutomaticRoute(new[]{new RoadPoint(0,1),new RoadPoint(5,1)}),"mountain infantry crosses mountain and high mountain in mode1");road.Motor.Stop();
            Check(free.CommitRoute(new[]{new RoadPoint(0,1),new RoadPoint(5,1)},map),"mountain infantry crosses both in mode2");road.Motor.Stop();
            Check(!free.CommitRoute(new[]{new RoadPoint(0,1),new RoadPoint(8,1)},map),"mountain infantry cannot cross water");
            free.definition=MountainTerrainInstaller.Free(UnitRole.Infantry);Check(road.CommitAutomaticRoute(new[]{new RoadPoint(0,1),new RoadPoint(3.5f,1)}),"ordinary infantry crosses ordinary mountain in mode1");road.Motor.Stop();
            Check(!road.CommitAutomaticRoute(new[]{new RoadPoint(0,1),new RoadPoint(5,1)}),"mode1 cannot bypass thin high mountain");Check(!free.CommitRoute(new[]{new RoadPoint(0,1),new RoadPoint(5,1)},map),"mode2 cannot bypass thin high mountain");
            free.definition=MountainTerrainInstaller.Free(UnitRole.Tank);Check(!road.CommitAutomaticRoute(new[]{new RoadPoint(0,1),new RoadPoint(3.5f,1)}),"tank mountain permission enforced in mode1");
            free.definition=MountainTerrainInstaller.Free(UnitRole.Artillery);Check(!free.CommitRoute(new[]{new RoadPoint(0,1),new RoadPoint(3.5f,1)},map),"artillery cannot enter ordinary mountain");
            free.definition=MountainTerrainInstaller.Free(UnitRole.Logistics);Check(free.CommitRoute(new[]{new RoadPoint(0,1),new RoadPoint(3.5f,1)},map),"resource transport crosses mountain in mode2");road.Motor.Stop();Check(!road.CommitAutomaticRoute(new[]{new RoadPoint(0,1),new RoadPoint(5,1)}),"resource transport cannot cross high mountain in mode1");
            free.definition=MountainTerrainInstaller.Free(UnitRole.PersonnelCarrier);g.transform.position=Vector3.zero;Check(road.CommitAutomaticRoute(new[]{new RoadPoint(0,0),new RoadPoint(1,0)}),"carrier follows road in mode1");road.Motor.Stop();Check(free.CommitRoute(new[]{new RoadPoint(0,0),new RoadPoint(1,0)},map),"carrier follows road in mode2");road.Motor.Stop();
            Check(!road.CommitAutomaticRoute(new[]{new RoadPoint(0,0),new RoadPoint(1,.5f)}),"carrier mode1 rejects off road shortcut");Check(!free.CommitRoute(new[]{new RoadPoint(0,0),new RoadPoint(1,.5f)},map),"carrier mode2 rejects off road route");
            free.overrideSettings=true;free.allowedTerrain=TerrainAccess.All;Check((free.AllowedTerrain&TerrainAccess.Water)==0&&!free.CommitRoute(new[]{new RoadPoint(0,0),new RoadPoint(8,1)},map),"even explicit All override cannot grant amphibious movement");free.overrideSettings=false;
            free.definition=MountainTerrainInstaller.Free(UnitRole.Infantry);float ordinary=free.SpeedAt(TerrainKind.Mountain);free.definition=MountainTerrainInstaller.Free(UnitRole.MountainInfantry);float mountain=free.SpeedAt(TerrainKind.Mountain);Check(mountain>ordinary,"mountain infantry has smaller mountain speed penalty");Check(free.SpeedAt(TerrainKind.HighMountain)<mountain,"high mountain slows mountain infantry more");Check(road.RoadLegSpeed(new RoadPoint(2.2,1),new RoadPoint(2.8,1))==1.5f,"mode1 off road mountain speed uses selected unit terrain settings");
            free.definition=MountainTerrainInstaller.Free(UnitRole.Infantry);g.transform.position=Vector3.zero;Check(road.CommitAutomaticRoute(new[]{new RoadPoint(0,0),new RoadPoint(1,0)}),"route accepted before terrain edit");var regions=new List<FreeMarchMapDefinition.Region>(data.regions);regions.Add(RectRegion(TerrainKind.HighMountain,.4f,.5f,-.2f,.2f));data.regions=regions.ToArray();data.MarkDirty();typeof(ArmyRouteMotor).GetMethod("Update",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(road.Motor,null);Check(road.Motor.Blocked&&!road.Motor.IsMoving,"live terrain edits invalidate existing road route");
            var world=UnityEngine.Object.FindObjectOfType<BattleWorld>();bool found=false;foreach(var u in world.units){var mover=u.GetComponent<ArmyRoadMover>();var f=u.GetComponent<ArmyFreeMarchMover>();Check(f!=null&&f.definition.allowedTerrain==MountainTerrainInstaller.Access(u.combatDefinition.role),"scene unit mobility "+u.unitId);if(u.combatDefinition.role==UnitRole.PersonnelCarrier){var p=new RoadPoint(u.transform.position.x,u.transform.position.y);Check(world.setup.freeMarchMap.CanTraverse(p,p,TerrainAccess.Road),"carrier example placed on actual road");}if(u.combatDefinition.role==UnitRole.MountainInfantry)found=true;}Check(found,"mountain infantry example registered");
            var carrierObj=new GameObject("mountain passenger vehicle");carrierObj.transform.SetParent(root.transform);var carrierUnit=carrierObj.AddComponent<BattleUnit>();carrierUnit.combatDefinition=ScriptableObject.CreateInstance<ArmyCombatDefinition>();carrierUnit.ownerId="A";var carrier=carrierObj.AddComponent<PersonnelCarrier>();carrierUnit.Initialize();var passenger=g.AddComponent<BattleUnit>();passenger.combatDefinition=ScriptableObject.CreateInstance<ArmyCombatDefinition>();passenger.combatDefinition.role=UnitRole.MountainInfantry;passenger.ownerId="A";passenger.Initialize();Check(carrier.Load(passenger).accepted,"mountain infantry may board personnel carrier");
            UnityEngine.Object.DestroyImmediate(root);UnityEngine.Object.DestroyImmediate(data);UnityEngine.Object.DestroyImmediate(movement);
        }
    }
}
