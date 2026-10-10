using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using CampaignDemo;
public static class MinimapInstaller {
 public static void Run(){var scene=EditorSceneManager.OpenScene("Assets/Scenes/Battle01.unity");var hud=Object.FindObjectOfType<CampaignHUD>();var minimap=hud.GetComponent<TacticalMinimap>();if(minimap==null)minimap=hud.gameObject.AddComponent<TacticalMinimap>();minimap.world=Object.FindObjectOfType<BattleWorld>();var alerts=hud.GetComponent<MinimapAlertTracker>();if(alerts==null)alerts=hud.gameObject.AddComponent<MinimapAlertTracker>();alerts.world=minimap.world;minimap.size=260;minimap.margin=8;minimap.expanded=true;hud.rightPanelWidth=280;hud.uiFontSize=13;EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);Debug.Log("MINIMAP_INSTALLED square collapsible minimap and compact state 3 panel");}
}

