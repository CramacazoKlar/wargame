using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
namespace CampaignDemo.Editor
{
    public static class ReferenceMapVerification
    {
        static double start;static int stage;static float committedAt;static Vector3 before;
        static Vector2 P(float x,float y){return new Vector2((x/1920-.5f)*36,(.5f-y/1297)*(36*1348f/1995));}
        [InitializeOnLoadMethod] static void Resume(){if(!SessionState.GetBool("ReferenceVerify",false))return;start=EditorApplication.timeSinceStartup;EditorApplication.update+=Tick;Application.logMessageReceived+=Log;}
        public static void Run(){EditorSceneManager.OpenScene("Assets/Scenes/Battle01.unity");SessionState.SetBool("ReferenceVerify",true);start=EditorApplication.timeSinceStartup;EditorApplication.update+=Tick;Application.logMessageReceived+=Log;EditorApplication.EnterPlaymode();}
        static void Log(string msg,string stack,LogType type){if(type==LogType.Exception||type==LogType.Error){File.WriteAllText("reference-verification-error.txt",msg+"\n"+stack);SessionState.SetBool("ReferenceVerify",false);EditorApplication.Exit(1);}}
        static void Require(bool valid,string label){if(!valid)throw new Exception("Reference map check failed: "+label);}
        static RoadPoint RP(Vector2 p){return new RoadPoint(p.x,p.y);}
        static void Tick()
        {
            if(EditorApplication.timeSinceStartup-start>90){Debug.LogError("Reference verification timeout");return;}
            if(!EditorApplication.isPlaying||Time.time<.4f)return;
            try
            {
                var s=UnityEngine.Object.FindObjectOfType<RoadMarchSceneSetup>();Require(s!=null&&s.sceneArmies[0].Motor!=null,"scene initialization");
                var water=s.freeMarchMap.waterMap;
                if(stage==0)
                {
                    Require(!s.roadMap.definition.showRoads&&!s.showTacticalOverlays&&!s.showCommittedRoutes,"clean presentation");
                    Require(water.IsWater(RP(P(980,460))),"river pixel recognition");Require(!water.CanTraverse(RP(P(915,460)),RP(P(1030,460))),"river crossing blocked");Require(water.CanTraverse(RP(P(934,610)),RP(P(987,625))),"central road bridge");Require(water.CanTraverse(RP(P(580,1145)),RP(P(740,1145))),"southern road bridge");
                    var free=s.sceneArmies[0].GetComponent<ArmyFreeMarchMover>();Require(free.SpeedAt(TerrainKind.Ground)<free.SpeedAt(TerrainKind.Road),"offroad slower");
                    Require(!free.CommitRoute(new[]{RP(new Vector2(s.sceneArmies[0].transform.position.x,s.sceneArmies[0].transform.position.y)),RP(P(980,460))},s.freeMarchMap),"invalid free march rejected");
                    var zoom=s.zoomController;zoom.SetSize(3);Require(Mathf.Abs(zoom.zoomSlider.value-1)<.01,"slider synchronization");zoom.ResetToMiddle();Require(Mathf.Abs(zoom.CurrentSize-14)<.01,"zoom reset");
                    var config=s.GetComponent<GameSettingsController>();Require(config!=null&&config.bgmSource!=null&&config.effectSources.Length>0,"settings and audio sources");config.SetPlanning(.8f);config.SetBgm(.3f);Require(Mathf.Abs(config.bgmSource.volume-.3f)<.01,"BGM volume");config.SetPlanning(1);config.SetBgm(.6f);
                    before=s.sceneArmies[0].transform.position;s.sceneArmies[0].CommitRoute(new[]{new RoadPoint(before.x,before.y),RP(P(481,524))});committedAt=Time.time;stage=1;return;
                }
                if(stage==1&&Time.time-committedAt>.5f)
                {
                    Require(Vector3.Distance(before,s.sceneArmies[0].transform.position)>.1f,"actual road movement");
                    var blocked=new List<string>();foreach(var e in s.roadMap.Network.Edges){var a=s.roadMap.Network.Nodes[e.A];var b=s.roadMap.Network.Nodes[e.B];if(!water.CanTraverse(a,b))blocked.Add(e.A+"->"+e.B);}
                    File.WriteAllText("reference-verification-result.txt","PASS: Play initialization, river rejection, two bridges, offroad slowdown, invalid route rejection, road movement, settings, zoom synchronization.\nRoad segments touching water pixels: "+string.Join(",",blocked));
                    Render(s.mapCamera);s.GetComponent<GameSettingsController>().settingsPanel.SetActive(true);Render(s.mapCamera,"设置面板预览");Debug.Log("REFERENCE_PLAY_PASS; water-touching segments: "+blocked.Count);SessionState.SetBool("ReferenceVerify",false);EditorApplication.Exit(0);
                }
            }
            catch(Exception e){Debug.LogException(e);}
        }
        static void Render(Camera camera,string name="地图接入后的场景")
        {
            var canvas=GameObject.Find("Command UI").GetComponent<Canvas>();var old=canvas.renderMode;canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=1;
            var target=new RenderTexture(1280,720,24);camera.targetTexture=target;camera.aspect=1280f/720;Canvas.ForceUpdateCanvases();camera.Render();var previous=RenderTexture.active;RenderTexture.active=target;var image=new Texture2D(1280,720,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1280,720),0,0);image.Apply();File.WriteAllBytes("C:/Users/ZWQ/Documents/Codex/2026-10-03/x20-mu-q/outputs/"+name+".png",image.EncodeToPNG());camera.targetTexture=null;RenderTexture.active=previous;canvas.renderMode=old;UnityEngine.Object.DestroyImmediate(image);target.Release();UnityEngine.Object.DestroyImmediate(target);
        }
    }
}
