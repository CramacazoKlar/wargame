using UnityEngine;
namespace CampaignDemo
{
    public sealed class ResourceDepot:MonoBehaviour
    {
        public string depotId,ownerId,displayName="资源仓库";
        [Min(0)] public float stock;
        public bool resourcePile;
        public float captureProgress;
        public string capturingPlayer;
        public bool Removed {get;private set;}
        public void Remove(){Removed=true;gameObject.SetActive(false);}
    }
}
