using System;
using System.IO;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
namespace CampaignDemo.Editor
{
    [InitializeOnLoad]
    public static class CombatEffectsRuntimeVerification
    {
        static double start;static float phaseStart;static int stage;static readonly List<string> results=new List<string>();
        static CombatEffectProfile testProfile;static GameObject prefab;
        static CombatEffectsRuntimeVerification(){if(SessionState.GetBool("CombatEffectsRuntime",false)){start=EditorApplication.timeSinceStartup;EditorApplication.update+=Poll;}}
        public static void Run(){EditorSceneManager.OpenScene("Assets/Scenes/Battle01.unity");CombatEffectsVerification.Validate();CombinedArmsVerification.Validate();SessionState.SetBool("CombatEffectsRuntime",true);start=EditorApplication.timeSinceStartup;EditorApplication.update+=Poll;EditorApplication.isPlaying=true;}
        static void Check(bool value,string label){if(!value)throw new Exception(label);results.Add("PASS "+label);}
        static CombatEffectEvent Direct(BattleUnit u){return CombatEffectEvent.Direct(u,null,"B",u.transform.position+new Vector3(.5f,.15f));}
        static void Poll()
        {
            try
            {
                if(EditorApplication.timeSinceStartup-start>90)throw new Exception("Effects runtime timeout");if(!EditorApplication.isPlaying||Time.time<1)return;
                var w=UnityEngine.Object.FindObjectOfType<BattleWorld>();if(w==null||!w.Initialized)return;var presenter=w.GetComponent<CombatEffectPresenter>();var player=w.GetComponent<PrefabCombatEffectPlayer>();var u=w.units[0];
                if(stage==0)
                {
                    Check(presenter!=null&&player!=null&&presenter.effectPlayer==player,"scene presentation bound to replaceable player");testProfile=UnityEngine.Object.Instantiate(presenter.defaultProfile);testProfile.lifetime=.7f;testProfile.directAttackInterval=.4f;presenter.defaultProfile=testProfile;
                    prefab=new GameObject("test fire prefab");prefab.transform.position=new Vector3(100,100);var sprite=prefab.AddComponent<SpriteRenderer>();sprite.sprite=w.unknownUnitSprite;sprite.color=Color.yellow;sprite.sortingOrder=26;prefab.transform.localScale=Vector3.one*.1f;testProfile.firePrefab=prefab;
                    float health=u.Personnel;w.NotifyCombatEffect(Direct(u));var all=UnityEngine.Object.FindObjectsOfType<CombatEffectInstance>();Check(all.Length>=3,"fire prefab, placeholder trajectory and impact spawn");Check(u.Personnel==health,"effect publication does not change health");int count=all.Length;for(int i=0;i<20;i++)w.NotifyCombatEffect(Direct(u));Check(UnityEngine.Object.FindObjectsOfType<CombatEffectInstance>().Length==count,"many combat ticks respect visual interval");
                    stage=1;phaseStart=Time.time;return;
                }
                if(stage==1&&Time.time-phaseStart>.9f)
                {
                    Check(UnityEngine.Object.FindObjectsOfType<CombatEffectInstance>().Length==0,"temporary effects expire without accumulating");
                    testProfile.showPlaceholder=false;w.NotifyCombatEffect(Direct(u));Check(UnityEngine.Object.FindObjectsOfType<CombatEffectInstance>().Length==1,"real prefab works without placeholder graphics");
                    player.enabled=false;stage=2;phaseStart=Time.time;return;
                }
                if(stage==2&&Time.time-phaseStart>.1f)
                {
                    Check(UnityEngine.Object.FindObjectsOfType<CombatEffectInstance>().Length==0,"disabling effect player clears objects");w.NotifyCombatEffect(Direct(u));Check(UnityEngine.Object.FindObjectsOfType<CombatEffectInstance>().Length==0,"disabled player spawns no effects");
                    player.enabled=true;testProfile.showPlaceholder=true;presenter.artilleryProfile=testProfile;player.maxActiveObjects=4;
                    for(int i=0;i<50;i++)w.NotifyCombatEffect(new CombatEffectEvent(CombatEffectStage.ArtilleryImpact,null,"A",UnitRole.Artillery,false,Vector3.zero,null,null,u.transform.position,.3f));
                    var active=(List<GameObject>)typeof(PrefabCombatEffectPlayer).GetField("active",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(player);Check(active.Count<=4,"effect object budget bounded under heavy attacks");stage=3;phaseStart=Time.time;return;
                }
                if(stage==3&&Time.time-phaseStart>.1f)
                {
                    Check(UnityEngine.Object.FindObjectsOfType<CombatEffectInstance>().Length<=4,"over budget objects removed on next frame");w.SetPlayer("B");stage=4;phaseStart=Time.time;return;
                }
                if(stage==4&&Time.time-phaseStart>.1f)
                {
                    Check(UnityEngine.Object.FindObjectsOfType<CombatEffectInstance>().Length==0,"switching viewer clears visible old effects");w.SetPlayer("A");player.maxActiveObjects=128;presenter.Reset();testProfile.showPlaceholder=true;w.setup.modes.EnterInspectionMode();w.NotifyCombatEffect(Direct(u));stage=5;phaseStart=Time.time;return;
                }
                if(stage==5&&Time.time-phaseStart>.1f)
                {
                    Capture(w.setup.mapCamera);
                    File.WriteAllText("combat-effects-runtime-verification.txt",results.Count+" runtime checks passed\n"+string.Join("\n",results));SessionState.SetBool("CombatEffectsRuntime",false);EditorApplication.update-=Poll;EditorApplication.Exit(0);
                }
            }
            catch(Exception e){File.WriteAllText("combat-effects-runtime-verification.txt",e+"\n"+string.Join("\n",results));Debug.LogException(e);SessionState.SetBool("CombatEffectsRuntime",false);EditorApplication.update-=Poll;EditorApplication.Exit(1);}
        }
        static void Capture(Camera camera)
        {
            var rt=new RenderTexture(1600,900,24);camera.targetTexture=rt;foreach(var canvas in UnityEngine.Object.FindObjectsOfType<Canvas>()){canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=1;}Canvas.ForceUpdateCanvases();camera.Render();RenderTexture.active=rt;var image=new Texture2D(1600,900,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1600,900),0,0);image.Apply();File.WriteAllBytes("combat-effects-preview.png",image.EncodeToPNG());camera.targetTexture=null;RenderTexture.active=null;UnityEngine.Object.Destroy(image);UnityEngine.Object.Destroy(rt);
        }
    }
}
