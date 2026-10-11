using UnityEngine;
namespace CampaignDemo
{
    public sealed class ResourceCollector : MonoBehaviour
    {
        [System.NonSerialized] public CampaignRuntime Campaign;
        public bool unlimitedStorage=true;
        public string collectorId;
        public string ownerId="A";
        public ResourceSite site;
        public ResourceCollectorSettings settings=new ResourceCollectorSettings();
        public float Health {get;private set;}
        public float Stock {get;private set;}
        public bool Alive {get{return Health>0;}}
        float timer;
        public Rect Footprint {get{return new Rect((Vector2)transform.position-settings.footprint/2,settings.footprint);}}
        public void Initialize(){if(string.IsNullOrEmpty(collectorId))collectorId=System.Guid.NewGuid().ToString("N");Health=Mathf.Max(.1f,settings.health);Stock=0;timer=0;}
        public void Tick(float dt){if(!Alive)return;if(!unlimitedStorage&&Stock>=settings.storageLimit){timer=0;return;}timer+=Mathf.Max(0,dt);float interval=Mathf.Max(.1f,settings.collectionInterval);int batches=Mathf.FloorToInt(timer/interval);if(batches==0)return;timer-=batches*interval;Stock=unlimitedStorage?Stock+batches*Mathf.Max(0,settings.amountPerInterval)*AICheatRules.Income(Campaign==null?null:Campaign.world,ownerId):Mathf.Min(Mathf.Max(0,settings.storageLimit),Stock+batches*Mathf.Max(0,settings.amountPerInterval)*AICheatRules.Income(Campaign==null?null:Campaign.world,ownerId));if(!unlimitedStorage&&Stock>=settings.storageLimit)timer=0;}
        public void Store(float amount){Stock+=Mathf.Max(0,amount);}
        public float Take(float amount){float value=Mathf.Min(Stock,Mathf.Max(0,amount));Stock-=value;return value;}
        public void Damage(float amount){Health=Mathf.Max(0,Health-Mathf.Max(0,amount));if(!Alive){float left=Stock;Stock=0;timer=0;if(Campaign!=null&&left>0)Campaign.CreateDepot(transform.position,"",left,true);}}
    }
}



