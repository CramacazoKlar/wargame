using UnityEngine;
namespace CampaignDemo
{
    [CreateAssetMenu(menuName="Campaign Demo/Engineering",fileName="Engineering")]
    public sealed class EngineeringDefinition : ScriptableObject
    {
        [Min(.1f)] public float buildSeconds=6;
        [Range(.01f,1)] public float engineerDismantleMultiplier=.2f;
        [Min(.01f)] public float interactionRadius=.8f;
        [Min(.02f)] public float fortificationRadius=.3f;
        [Min(.1f)] public float ordinaryDismantleSeconds=15;
        [Header("整条防线；按米计费/计时")]
        [Min(.01f)] public float metersPerWorldUnit=1000;
        [Min(.001f)] public float lineBuildSecondsPerMeter=.01f;
        [Min(0)] public float lineCostPerMeter=.1f;
        [Min(.001f)] public float lineDismantleSecondsPerMeter=.02f;
        [Min(.001f)] public float lineHealthPerMeter=.1f;
        [Min(.001f)] public float minimumLineWorldLength=.03f;
        [Min(.001f)] public float drawingSampleWorldDistance=.03f;
        [Min(2)] public int maximumLinePoints=512;
        [Min(.01f)] public float maximumLineWorldLength=4;
        [Min(.001f)] public float parapetSpacing=.15f,parapetDepth=.08f;
        [Header("军事地图防线外观，不影响防御宽度")][Min(.001f)] public float lineVisualWidth=.06f,lineOutlineWidth=.10f;
        public Color lineColor=Color.black,lineOutlineColor=new Color(.12f,.10f,.07f);
        [Header("逐点施工与自动拆除")]
        [Min(.01f)] public float constructionArrivalDistance=.05f,navigationCellSize=.15f;
        [Min(1)] public int navigationSearchLimit=60000;
        public bool allowEnemyAutomaticDemolition=false;
        [Header("工兵开口拆除")]
        [Min(.001f)] public float demolitionSnapDistance=.2f,demolitionSampleDistance=.04f,minimumBreachWorldLength=.05f;
        [Range(.01f,1)] public float ownLineDemolitionTimeMultiplier=.5f;
    }
}



