using UnityEngine;
namespace CampaignDemo
{
    [CreateAssetMenu(menuName="Campaign Demo/City Production Rules")]
    public sealed class CityProductionRules : ScriptableObject
    {
        [Tooltip("玩家和AI共用的普通步兵预制体。")]
        public GameObject infantryPrefab;
        [System.Serializable] public sealed class Entry {public UnitRole role;public GameObject prefab;[Min(0)] public float squadCost=100;[Min(1)] public float squadSeconds=30,costScale=3.3f,timeScale=3;public bool requiresFactory;}
        public Entry[] additionalUnits=new Entry[0];
        public Entry Find(UnitRole role){foreach(var entry in additionalUnits)if(entry!=null&&entry.role==role)return entry;return null;}
        [Min(0)] public float infantrySquadCost=100;
        [Min(1)] public float infantrySquadSeconds=30;
        [Min(1)] public float infantryCostScale=3.3f,infantryTimeScale=3;
        public bool infantryRequiresFactory;
        [Range(1,10)] public int maximumQueuedPerCity=5;
    }
    public struct CityActionQuote
    {
        public bool allowed;public string reason;public float cost,seconds;
        public static CityActionQuote No(string reason){return new CityActionQuote{reason=reason};}
        public static CityActionQuote Yes(float cost=0,float seconds=0){return new CityActionQuote{allowed=true,reason="",cost=cost,seconds=seconds};}
    }
}
