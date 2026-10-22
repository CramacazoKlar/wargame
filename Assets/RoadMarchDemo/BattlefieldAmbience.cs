using UnityEngine;
namespace CampaignDemo {
 // One global ambience bed; never processed by unit distance or camera filters.
 [DisallowMultipleComponent]
 public sealed class BattlefieldAmbience:MonoBehaviour {
  [Range(0,1)] public float normalVolume=.65f,quietVolume=.25f;
  [Min(.1f)] public float phaseSeconds=10;
  [Range(0,2)] public float transitionSeconds=.4f;
  AudioSource source;BattleWorld world;double started;
  public AudioSource Source=>source;
  public bool QuietPhase=>source!=null&&((long)((Time.unscaledTimeAsDouble-started)/Mathf.Max(.1f,phaseSeconds))&1)==1;
  void OnEnable(){world=GetComponent<BattleWorld>();}
  public static float Level(double elapsed,float period,float transition,float normal,float quiet){
   period=Mathf.Max(.1f,period);elapsed=System.Math.Max(0,elapsed);long phase=(long)(elapsed/period);float local=(float)(elapsed-phase*period);float target=(phase&1)==0?normal:quiet;
   if(phase==0||transition<=0)return target;
   float previous=(phase&1)==0?quiet:normal,t=Mathf.Clamp01(local/Mathf.Min(period,transition));return Mathf.Lerp(previous,target,t*t*(3-2*t));
  }
  void Update(){
   if(world==null||!world.Initialized)return;
   if(source==null){var clip=Resources.Load<AudioClip>("Audio/Ambience/MapAmbience");if(clip==null)return;var g=new GameObject("Continuous map ambience");g.transform.SetParent(transform,false);source=g.AddComponent<AudioSource>();source.playOnAwake=false;source.clip=clip;source.loop=true;source.spatialBlend=0;source.panStereo=0;source.pitch=1;source.dopplerLevel=0;started=Time.unscaledTimeAsDouble;source.volume=normalVolume*Mathf.Clamp01(PlayerPrefs.GetFloat("Campaign.EffectsVolume",.8f));source.Play();}
   source.volume=Level(Time.unscaledTimeAsDouble-started,phaseSeconds,transitionSeconds,normalVolume,quietVolume)*Mathf.Clamp01(PlayerPrefs.GetFloat("Campaign.EffectsVolume",.8f));
  }
  void OnDisable(){if(source!=null){source.Stop();Destroy(source.gameObject);source=null;}}
 }
}
