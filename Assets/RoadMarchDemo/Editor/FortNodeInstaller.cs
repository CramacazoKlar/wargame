using UnityEngine;
using UnityEditor;
namespace CampaignDemo.Editor
{
    public static class FortNodeInstaller
    {
        [MenuItem("Campaign Demo/Configure point defense lines")]
        public static void Configure(){FortLineInstaller.Configure();var c=Object.FindObjectOfType<CampaignRuntime>();c.combined.engineering.lineColor=Color.black;c.combined.engineering.allowEnemyAutomaticDemolition=false;EditorUtility.SetDirty(c.combined.engineering);AssetDatabase.SaveAssets();Debug.Log("POINT_DEFENSE_LINES_CONFIGURED");}
    }
}
