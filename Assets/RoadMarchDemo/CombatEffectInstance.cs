using UnityEngine;
namespace CampaignDemo
{
    public sealed class CombatEffectInstance : MonoBehaviour
    {
        float expires,travel,startTime;
        Vector3 from,to;
        public void Configure(float lifetime,Vector3 source,Vector3 target,float travelSeconds=0)
        {startTime=Time.time;expires=startTime+Mathf.Max(.01f,lifetime);from=source;to=target;travel=travelSeconds;}
        void Update(){if(travel>0)transform.position=Vector3.Lerp(from,to,Mathf.Clamp01((Time.time-startTime)/travel));if(Time.time>=expires)Destroy(gameObject);}
    }
}
