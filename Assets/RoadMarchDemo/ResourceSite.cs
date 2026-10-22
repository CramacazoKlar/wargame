using UnityEngine;
namespace CampaignDemo {
    public sealed class ResourceSite : MonoBehaviour
    {
        public float stock=2000000;
        public string siteId;
        void Awake(){if(string.IsNullOrEmpty(siteId))siteId=System.Guid.NewGuid().ToString("N");}
        public string displayName="资源区";
        [Tooltip("允许部署的世界坐标矩形，以该对象为中心，不随贴图缩放。")]
        public Vector2 areaSize=new Vector2(1,1);
        public Rect Area { get {return new Rect((Vector2)transform.position-areaSize/2,areaSize);} }
        void OnDrawGizmosSelected(){Gizmos.color=Color.yellow;Gizmos.DrawWireCube(transform.position,areaSize);}
    }
}


