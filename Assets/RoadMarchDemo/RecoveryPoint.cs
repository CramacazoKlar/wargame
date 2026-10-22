using UnityEngine;
namespace CampaignDemo
{
    public enum FacilityKind { RecoveryPoint, Factory, Mine }
    public sealed class RecoveryPoint : MonoBehaviour
    {
        [Tooltip("城市内兵工厂升级状态，每个城市最多一个。")] public bool hasFactory;
        [Tooltip("小地图常驻显示的大型城市地标；小城市可关闭。")] public bool minimapMajorCity=true;
        public bool isRepairSite;
        public string pointId;
        public string displayName="Recovery point";
        public string ownerId="";
        public FacilityKind kind;
        [Min(.1f)] public float captureRadius=.7f;
        [Min(.1f)] public float interactionRadius=.8f;
        [Min(.1f)] public float captureSeconds=8;
        [Min(0)] public float transitionSeconds=0;
        [Min(1)] public int capacity=3;
        [Range(.001f,.2f)] public float repairFractionPerSecond=.02f;
        [Min(0)] public float resourceStock=400000;
        [Min(0)] public float resourcePerPerson=100;
        public Vector2 exitOffset=new Vector2(.3f,0);
        public string CapturingPlayer { get; internal set; }
        public float CaptureProgress { get; internal set; }
        public Vector3 ExitPosition { get { return transform.position+(Vector3)exitOffset; } }
        public Vector3 ExitPositionFor(BattleUnit unit)
        {
            var world=unit==null||unit.Campaign==null?null:unit.Campaign.world;
            if(world==null)return ExitPosition;
            var map=world.setup==null?null:world.setup.freeMarchMap;
            var free=unit.GetComponent<ArmyFreeMarchMover>();
            for(int i=0;i<65;i++)
            {
                var candidate=ExitPosition;
                if(i>0){int index=i-1;float angle=index%16*Mathf.PI/8;float radius=(index/16+1)*.5f;candidate+=new Vector3(Mathf.Cos(angle),Mathf.Sin(angle))*radius;}
                var point=new RoadPoint(candidate.x,candidate.y);
                if(map!=null&&free!=null&&!map.CanTraverse(point,point,free.AllowedTerrain))continue;
                bool occupied=false;
                foreach(var other in world.units)if(other!=null&&other!=unit&&other.OnMap&&other.ownerId==unit.ownerId&&Vector2.Distance(other.transform.position,candidate)<.4f){occupied=true;break;}
                if(!occupied)return candidate;
            }
            return ExitPosition;
        }
    }
}


