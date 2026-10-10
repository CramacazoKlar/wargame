using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
namespace CampaignDemo.Editor
{
    [InitializeOnLoad]
    public static class MovementArrowVerification
    {
        static double start;static float phase;static int stage;static BattleUnit unit;static List<RoadPoint> original;static LineRenderer arrow;static readonly List<string> checks=new List<string>();
        static MovementArrowVerification(){if(SessionState.GetBool("MovementArrowVerify",false)){start=EditorApplication.timeSinceStartup;EditorApplication.update+=Poll;}}
        static void Check(bool value,string label){if(!value)throw new Exception(label);checks.Add("PASS "+label);}
        public static void Run()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/Battle01.unity");var world=UnityEngine.Object.FindObjectOfType<BattleWorld>();world.setup.showCommittedRoutes=true;EditorUtility.SetDirty(world.setup.settings);EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());AssetDatabase.SaveAssets();
            var sample=RoadRouteView.Create(world.transform,"Preview check",.09f,4,Color.red);sample.ConfigureArrow(world.setup.settings,world.setup.mapCamera,false);sample.ShowArrow(new[]{new RoadPoint(0,0),new RoadPoint(2,0)});var stroke=sample.GetComponent<LineRenderer>();Check(!stroke.loop&&stroke.positionCount>2,"draft preview renders hollow arrow");Check(stroke.startColor==Color.red,"invalid preview keeps red color");Check(stroke.startWidth+.00001f>=2*world.setup.mapCamera.orthographicSize/Mathf.Max(1,world.setup.mapCamera.pixelHeight)*world.setup.settings.minimumArrowStrokePixels,"zoomed out stroke retains pixel readability");UnityEngine.Object.DestroyImmediate(sample.gameObject);var route=new[]{new RoadPoint(0,0),new RoadPoint(1,0),new RoadPoint(1,1),new RoadPoint(2,1)};var shape=RoadRouteView.ArrowContour(route,.14f,.3f,.28f);Check(shape.Count>=9,"bent route generates closed contour instead of centerline");Check(shape.Contains(new Vector3(2,1)),"arrow tip stays at destination");Check(route[0].X==0&&route[3].X==2,"appearance does not modify route data");foreach(var p in shape)Check(!float.IsNaN(p.x)&&!float.IsNaN(p.y),"contour vertex finite");Check(RoadRouteView.ArrowContour(new[]{new RoadPoint(0,0),new RoadPoint(0,0)},.14f,.3f,.28f).Count==0,"zero length route invisible");Check(RoadRouteView.ArrowContour(new[]{new RoadPoint(0,0),new RoadPoint(.002,0)},.14f,.3f,.28f).Count>0,"short final route still produces bounded arrow");
            SessionState.SetBool("MovementArrowVerify",true);start=EditorApplication.timeSinceStartup;EditorApplication.update+=Poll;EditorApplication.isPlaying=true;
        }
        static void Poll()
        {
            try
            {
                if(EditorApplication.timeSinceStartup-start>100)throw new Exception("Arrow runtime timeout");if(!EditorApplication.isPlaying||Time.time<1)return;var w=UnityEngine.Object.FindObjectOfType<BattleWorld>();if(!w.Initialized)return;
                if(stage==0){foreach(var u in w.units)if(u.ownerId=="A"&&u.combatDefinition.role==UnitRole.Infantry){unit=u;break;}var at=unit.transform.position;original=new List<RoadPoint>{new RoadPoint(at.x,at.y),new RoadPoint(at.x+.2,at.y),new RoadPoint(at.x+.4,at.y+.15),new RoadPoint(at.x+1,at.y+.15)};Check(unit.Motor.Commit(original,()=>.2f),"movement executor accepts existing route structure");w.GetComponent<ArmySelection>().SelectOwned(unit.GetComponent<ArmyRoadMover>());foreach(var line in UnityEngine.Object.FindObjectsOfType<LineRenderer>())if(line.loop&&line.name.Contains(unit.name)&&line.positionCount>3){arrow=line;break;}if(arrow==null){var field=typeof(ArmyRouteMotor).GetField("view",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance);arrow=((RoadRouteView)field.GetValue(unit.Motor)).GetComponent<LineRenderer>();}Check(!arrow.loop&&arrow.positionCount>original.Count,"committed view is hollow outline");stage=1;phase=Time.time;return;}
                if(Time.time-phase<.4f)return;
                if(stage==1){Check(arrow.enabled,"selected own army arrow visible");var remaining=unit.Motor.RemainingRoute();Check(remaining[0].X>original[0].X,"remaining route starts at current position");Check(unit.Motor.CopyRoute().Count==original.Count,"visual shortening preserves complete order data");w.GetComponent<ArmySelection>().Clear();stage=2;phase=Time.time;return;}
                if(stage==2){Check(!arrow.enabled,"unselected route hidden");w.GetComponent<ArmySelection>().SelectOwned(unit.GetComponent<ArmyRoadMover>());w.SetPlayer("B");stage=3;phase=Time.time;return;}
                if(stage==3){Check(!arrow.enabled,"other player route never revealed");w.SetPlayer("A");w.GetComponent<ArmySelection>().SelectOwned(unit.GetComponent<ArmyRoadMover>());unit.Motor.Stop();Check(arrow.positionCount==0,"stopping clears arrow");var at=unit.transform.position;var show=new List<RoadPoint>{new RoadPoint(at.x,at.y),new RoadPoint(at.x+.7,at.y+.25),new RoadPoint(at.x+1.3,at.y+.25),new RoadPoint(at.x+1.9,at.y+.6)};Check(unit.Motor.Commit(show,()=>.05f),"replacement movement order recreates arrow");w.setup.modes.SetMode(MouseCommandMode.FreeMarch);stage=4;phase=Time.time;return;}
                if(stage==4){var cam=w.setup.mapCamera;cam.orthographicSize=2.5f;cam.transform.position=new Vector3(unit.transform.position.x+.9f,unit.transform.position.y,cam.transform.position.z);arrow.GetComponent<RoadRouteView>().ShowArrow(unit.Motor.RemainingRoute());Capture(cam);File.WriteAllText("movement-arrow-verification.txt",checks.Count+" checks passed\n"+string.Join("\n",checks));SessionState.SetBool("MovementArrowVerify",false);EditorApplication.update-=Poll;EditorApplication.Exit(0);}
            }
            catch(Exception e){File.WriteAllText("movement-arrow-verification.txt",e+"\n"+string.Join("\n",checks));Debug.LogException(e);SessionState.SetBool("MovementArrowVerify",false);EditorApplication.update-=Poll;EditorApplication.Exit(1);}
        }
        static void Capture(Camera camera){var rt=new RenderTexture(1600,900,24);camera.targetTexture=rt;foreach(var canvas in UnityEngine.Object.FindObjectsOfType<Canvas>()){canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.sortingOrder=200;canvas.worldCamera=camera;canvas.planeDistance=1;}Canvas.ForceUpdateCanvases();camera.Render();RenderTexture.active=rt;var image=new Texture2D(1600,900,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1600,900),0,0);image.Apply();File.WriteAllBytes("movement-arrow-preview.png",image.EncodeToPNG());camera.targetTexture=null;RenderTexture.active=null;UnityEngine.Object.Destroy(image);UnityEngine.Object.Destroy(rt);}
    }
}




