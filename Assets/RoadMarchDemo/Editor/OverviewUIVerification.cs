using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using CampaignDemo;
[InitializeOnLoad]
public static class OverviewUIVerification
{
    const string Key="OverviewUIVerification";static double start;static int stage;static float next;
    static readonly List<string> checks=new List<string>();
    static OverviewUIVerification(){if(SessionState.GetBool(Key,false)){start=EditorApplication.timeSinceStartup;EditorApplication.update+=Poll;}}
    public static void Run(){EditorSceneManager.OpenScene("Assets/Scenes/Battle01.unity");SessionState.SetBool(Key,true);start=EditorApplication.timeSinceStartup;EditorApplication.update+=Poll;EditorApplication.isPlaying=true;}
    static void Check(bool ok,string label){if(!ok)throw new Exception(label);checks.Add("PASS "+label);}
    static void Poll(){try{
        if(EditorApplication.timeSinceStartup-start>100)throw new Exception("Overview test timeout");if(!EditorApplication.isPlaying||Time.time<1||Time.time<next)return;
        var world=UnityEngine.Object.FindObjectOfType<BattleWorld>();if(world==null||!world.Initialized)return;
        var hud=UnityEngine.Object.FindObjectOfType<BattleOverviewHUD>();var campaignHUD=UnityEngine.Object.FindObjectOfType<CampaignHUD>();var ai=world.GetComponent<BattleAIController>();
        if(stage==0){world.enabled=false;ai.aiEnabled=false;world.SetPlayer("A");ai.aiEnabled=false;world.setup.modes.EnterInspectionMode();stage=1;next=Time.time+.3f;return;}
        var footer=hud.OverviewPanel;var text=footer.GetComponentsInChildren<Text>(true).First(t=>t.name=="Statistics");
        Canvas.ForceUpdateCanvases();
        if(stage==1){Check(footer.gameObject.activeInHierarchy,"statistics footer remains visible in mode 3");Check(!campaignHUD.GetComponent<BattleHUD>().enabled,"legacy HUD remains disabled");Check(text.text==BattleTextFormatter.Overview(world.GetComponent<BattleInformationSource>().GetOverview("A")),"footer uses actual blue population, resources and cities");Check(footer.offsetMax.x<=-campaignHUD.rightPanelWidth,"footer reserves space for right panel");Check(text.rectTransform.rect.width>0&&text.resizeTextForBestFit,"statistics text scales with available width");world.setup.modes.SetMode(MouseCommandMode.RoadMarch);stage=2;next=Time.time+.3f;return;}
        if(stage==2){Check(!footer.gameObject.activeInHierarchy,"statistics hidden in road movement mode");Check(footer.offsetMax.x==-8,"footer uses full width outside management mode");world.setup.modes.SetMode(MouseCommandMode.FreeMarch);stage=3;next=Time.time+.3f;return;}
        if(stage==3){Check(!footer.gameObject.activeInHierarchy,"statistics hidden in free movement mode");world.setup.modes.EnterInspectionMode();world.SetPlayer("B");stage=4;next=Time.time+.3f;return;}
        if(stage==4){Check(footer.gameObject.activeInHierarchy,"return to management and switch faction do not hide footer");Check(text.text==BattleTextFormatter.Overview(world.GetComponent<BattleInformationSource>().GetOverview("B")),"perspective switch refreshes statistics to red");world.SetPlayer("A");ai.aiEnabled=false;world.units.First(u=>u.ownerId=="A").Damage(1);world.points.First(p=>p.ownerId=="A").resourceStock=1234;stage=5;next=Time.time+.3f;return;}
        Check(text.text==BattleTextFormatter.Overview(world.GetComponent<BattleInformationSource>().GetOverview("A")),"casualty and resource changes update live statistics");
        Check(text.text.Contains("人数")&&text.text.Contains("资源")&&text.text.Contains("据点"),"requested population resource and city fields retained");
        File.WriteAllText("overview-ui-verification-result.txt",checks.Count+" checks passed\n"+string.Join("\n",checks));Debug.Log("OVERVIEW_UI_PASS "+checks.Count);SessionState.SetBool(Key,false);EditorApplication.update-=Poll;EditorApplication.Exit(0);
    }catch(Exception e){Debug.LogException(e);File.WriteAllText("overview-ui-verification-result.txt",e+"\n"+string.Join("\n",checks));SessionState.SetBool(Key,false);EditorApplication.update-=Poll;EditorApplication.Exit(1);}}
}
