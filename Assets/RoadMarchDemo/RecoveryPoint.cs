using UnityEngine;
namespace CampaignDemo
{
    public enum FacilityKind { RecoveryPoint, Factory, Mine }
    public sealed class RecoveryPoint : MonoBehaviour
    {
        [Tooltip("城市内兵工厂升级状态，每个城市最多一个。")] public bool hasFactory;
        [Tooltip("小地图常驻显示的大型城市地标；小城市可关闭。")] public bool minimapMajorCity=true;
        public string pointId;
        public string displayName="Recovery point";
        public string ownerId="";
        public FacilityKind kind;
        [Min(.1f)] public float captureRadius=.7f;
        [Min(.1f)] public float interactionRadius=.8f;
        [Min(.1f)] public float captureSeconds=8;
        [Min(.1f)] public float transitionSeconds=2;
        [Min(1)] public int capacity=3;
        [Range(.001f,.2f)] public float repairFractionPerSecond=.02f;
        [Min(0)] public float resourceStock=4000;
        [Min(0)] public float resourcePerPerson=1;
        public Vector2 exitOffset=new Vector2(.3f,0);
        public string CapturingPlayer { get; internal set; }
        public float CaptureProgress { get; internal set; }
        public Vector3 ExitPosition { get { return transform.position+(Vector3)exitOffset; } }
    }
}


