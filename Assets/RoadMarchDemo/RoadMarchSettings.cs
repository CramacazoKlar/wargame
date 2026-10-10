using UnityEngine;

namespace CampaignDemo
{
    [CreateAssetMenu(menuName="Campaign Demo/Road March Settings",fileName="MarchSettings")]
    public sealed class RoadMarchSettings : ScriptableObject
    {
        [Min(1)] public float snapPixels=32;
        [Min(1)] public float selectionPixels=30;
        [Min(1)] public float startPixels=48;
        [Min(1)] public float connectorMultiplier=2.2f;
        [Tooltip("Mode 1: remove only small overshoot-and-return segments at a turning junction, in screen pixels.")]
        [Min(0)] public float junctionTurnTolerancePixels=16;
        [Header("已确认移动命令：空心军事箭头")][Min(.001f)] public float arrowShaftWidth=.14f,arrowHeadWidth=.30f,arrowHeadLength=.28f,arrowStrokeWidth=.05f;
        public bool arrowOpenTail=true;
        public Color movementArrowColor=new Color(.08f,.35f,.70f,.95f);
        public Color validRouteColor=Color.cyan;
        public Color invalidRouteColor=Color.red;
        [Min(0)] public float minimumArrowShaftPixels=10,minimumArrowHeadPixels=20,minimumArrowHeadLengthPixels=18,minimumArrowStrokePixels=3f;
    }
}



