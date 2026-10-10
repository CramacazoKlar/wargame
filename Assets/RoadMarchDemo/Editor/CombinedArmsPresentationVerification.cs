using System;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
namespace CampaignDemo.Editor
{
    [InitializeOnLoad]
    public static class CombinedArmsPresentationVerification
    {
        static double start;static int stage;
        static CombinedArmsPresentationVerification(){if(SessionState.GetBool("CombinedArmsPresentation",false)){start=EditorApplication.timeSinceStartup;EditorApplication.update+=Poll;}}
        public static void Run(){CombinedArmsInstaller.Configure();CombinedArmsVerification.Validate();SessionState.SetBool("CombinedArmsPresentation",true);start=EditorApplication.timeSinceStartup;EditorApplication.update+=Poll;EditorApplication.isPlaying=true;}
        static void Require(bool value,string message){if(!value)throw new Exception(message);}
        static void Poll()
        {
            try
            {
                if(EditorApplication.timeSinceStartup-start>90)throw new Exception("Presentation timeout");
                if(!EditorApplication.isPlaying)return;var world=UnityEngine.Object.FindObjectOfType<BattleWorld>();if(world==null||!world.Initialized)return;
                var hud=UnityEngine.Object.FindObjectOfType<BattleHUD>();var special=hud.GetComponent<CombinedArmsHUD>();if(special==null||hud.transform.Find("Unit and facility details")==null)return;
                if(stage==0){world.setup.modes.EnterInspectionMode();BattleUnit vehicle=null,artillery=null;foreach(var u in world.units){if(u.GetComponent<PersonnelCarrier>()!=null)vehicle=u;if(u.GetComponent<ArtilleryBattery>()!=null)artillery=u;}
                    Require(vehicle!=null&&artillery!=null,"Demo units present");vehicle.transform.position=artillery.transform.position;hud.SelectUnit(artillery);stage=1;start=EditorApplication.timeSinceStartup;return;}
                if(EditorApplication.timeSinceStartup-start<.7)return;
                var panel=hud.transform.Find("Unit and facility details");
                if(stage==1){Require(panel.Find("发射（点击目标）").gameObject.activeInHierarchy,"Fire button visible in mode3");Require(panel.Find("上车").gameObject.activeInHierarchy,"Board button visible inside carrier radius");Require(panel.Find("发射（点击目标）").GetComponent<Button>().interactable,"Fire button ready");
                    var a=hud.SelectedUnit;var carrier=UnityEngine.Object.FindObjectOfType<PersonnelCarrier>();var result=carrier.Load(a);Require(result.accepted,"Artillery boards from runtime UI context");hud.SelectUnit(carrier.Unit);stage=2;start=EditorApplication.timeSinceStartup;return;}
                if(stage==2){Require(panel.Find("车内乘员").gameObject.activeInHierarchy,"Passenger scroll list visible");Require(panel.Find("Details").GetComponent<Text>().text.Contains("血量"),"Vehicle health displayed");Require(panel.Find("Details").GetComponent<Text>().text.Contains("50%"),"Loaded artillery speed displayed");
                    Require(panel.Find("车内乘员").GetComponent<ScrollRect>().content.GetComponentsInChildren<Button>().Length>0,"Passenger unload button created");
                    var carrier=hud.SelectedUnit.GetComponent<PersonnelCarrier>();Require(carrier.Unload(carrier.Passengers[0],world).accepted,"Runtime unloading works");
                    world.setup.modes.SetMode(MouseCommandMode.RoadMarch);stage=3;start=EditorApplication.timeSinceStartup;return;}
                if(stage==3){Require(!panel.gameObject.activeInHierarchy,"Mode1 hides right panel");world.setup.modes.EnterInspectionMode();var carrier=UnityEngine.Object.FindObjectOfType<PersonnelCarrier>();hud.SelectUnit(carrier.Unit);stage=4;start=EditorApplication.timeSinceStartup;return;}
                if(stage==4){var carrier=hud.SelectedUnit.GetComponent<PersonnelCarrier>();foreach(var u in world.units)if(u.GetComponent<ArtilleryBattery>()!=null){u.transform.position=carrier.transform.position;carrier.Load(u);break;}stage=5;start=EditorApplication.timeSinceStartup;return;}
                if(stage==5){Capture(world.setup.mapCamera);File.WriteAllText("combined-arms-presentation.txt","10 runtime presentation checks passed\nFire, ready, boarding, passengers, health, speed, unload, mode visibility verified.");SessionState.SetBool("CombinedArmsPresentation",false);EditorApplication.update-=Poll;EditorApplication.Exit(0);}
            }
            catch(Exception e){File.WriteAllText("combined-arms-presentation.txt",e.ToString());Debug.LogException(e);SessionState.SetBool("CombinedArmsPresentation",false);EditorApplication.update-=Poll;EditorApplication.Exit(1);}
        }
        static void Capture(Camera camera)
        {
            var rt=new RenderTexture(1600,900,24);camera.targetTexture=rt;
            foreach(var canvas in UnityEngine.Object.FindObjectsOfType<Canvas>()){canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=1;}
            Canvas.ForceUpdateCanvases();camera.Render();RenderTexture.active=rt;var image=new Texture2D(1600,900,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1600,900),0,0);image.Apply();File.WriteAllBytes("combined-arms-preview.png",image.EncodeToPNG());camera.targetTexture=null;RenderTexture.active=null;UnityEngine.Object.Destroy(image);UnityEngine.Object.Destroy(rt);
        }
    }
}
