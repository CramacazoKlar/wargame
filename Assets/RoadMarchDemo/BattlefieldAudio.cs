using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace CampaignDemo {
 public enum BattlefieldSound { Telegraph, Bird, Footstep, Car, Tank, Rifle, Cannon, Explosion, Construction, CommandVoice, TankAttack, RocketBarrage }
 [Serializable] public sealed class BattlefieldSoundProfile {
  public BattlefieldSound kind; public AudioClip[] clips=new AudioClip[0];
  [Min(.01f)] public float nearDistance=.2f,farDistance=4;
  [Range(0,1)] public float gain=.5f;
  [Min(.1f)] public float falloff=1.7f;
  public BattlefieldSoundProfile(BattlefieldSound k,float near,float far,float volume){kind=k;nearDistance=near;farDistance=far;gain=volume;}
 }
 // All distances are XY map units. Camera height is deliberately excluded.
 [DefaultExecutionOrder(200),DisallowMultipleComponent]
 public sealed class BattlefieldAudio:MonoBehaviour {
  public BattleWorld world;
  [Header("Map distances, not camera height (map reference: 1 unit = 1 km)")]
  public BattlefieldSoundProfile[] profiles={
   new BattlefieldSoundProfile(BattlefieldSound.Telegraph,.1f,1.5f,.22f),
   new BattlefieldSoundProfile(BattlefieldSound.Bird,.5f,6,.24f),
   new BattlefieldSoundProfile(BattlefieldSound.Footstep,.8f,8,.58f),
   new BattlefieldSoundProfile(BattlefieldSound.Car,1.5f,18,.5f),
   new BattlefieldSoundProfile(BattlefieldSound.Tank,2,24,.6f),
   new BattlefieldSoundProfile(BattlefieldSound.Rifle,2,28,.68f),
   new BattlefieldSoundProfile(BattlefieldSound.Cannon,3,45,.8f),
   new BattlefieldSoundProfile(BattlefieldSound.Explosion,3,55,.75f),
   new BattlefieldSoundProfile(BattlefieldSound.Construction,.6f,10,.4f),
   new BattlefieldSoundProfile(BattlefieldSound.TankAttack,3,45,.8f),
   new BattlefieldSoundProfile(BattlefieldSound.RocketBarrage,3,45,.6f)};
  [Min(1)] public int maxVoices=32;
  [Min(.1f)] public float referenceZoom=5;
  [Range(0,1)] public float overviewGain=.28f;
  public bool forestBirds=false;
  [Header("Bird calls, real-time seconds")]
  [Min(.5f)] public float birdMinimumInterval=3,birdMaximumInterval=7;
  [Header("Periodic command-post radio, independent of clicks and orders")]
  public bool periodicTelegraph=true;
  [Min(1)] public float telegraphMinimumInterval=16,telegraphMaximumInterval=28;
  sealed class Voice {public AudioSource source;public AudioLowPassFilter filter;public BattleUnit unit;public bool unitBound;public string owner;public Vector3 position;public BattlefieldSoundProfile profile;public bool loop;public float until;}
  readonly List<Voice> voices=new List<Voice>();
  readonly Dictionary<BattleUnit,Vector3> positions=new Dictionary<BattleUnit,Vector3>();
  readonly Dictionary<BattleUnit,float> nextStep=new Dictionary<BattleUnit,float>();
  readonly Dictionary<BattleUnit,float> nextHammer=new Dictionary<BattleUnit,float>();
  AudioClip[] constructionRecordings;
  readonly Dictionary<string,float> nextShot=new Dictionary<string,float>();
  readonly Dictionary<BattlefieldSound,AudioClip[]> generated=new Dictionary<BattlefieldSound,AudioClip[]>();
  readonly Dictionary<BattlefieldSound,int> previousVariant=new Dictionary<BattlefieldSound,int>();
  readonly Dictionary<BattlefieldSound,AudioClip> recordings=new Dictionary<BattlefieldSound,AudioClip>();
  readonly System.Random random=new System.Random(731);
  float nextMovement,nextBird,nextTelegraph;string viewer;
  public Vector3 ListeningCenter {get;private set;}
  Camera View=>world!=null&&world.setup!=null?world.setup.mapCamera:null;
  public int ActiveVoices {get{int n=0;foreach(var v in voices)if(v.source.isPlaying)n++;return n;}}
  [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
  static void Install(){SceneManager.sceneLoaded-=Loaded;SceneManager.sceneLoaded+=Loaded;}
  static void Loaded(Scene scene,LoadSceneMode mode){foreach(var w in FindObjectsOfType<BattleWorld>())if(w.gameObject.scene==scene&&w.GetComponent<BattlefieldAudio>()==null)w.gameObject.AddComponent<BattlefieldAudio>();}
  void OnEnable(){if(GetComponent<BattlefieldAmbience>()==null)gameObject.AddComponent<BattlefieldAmbience>();if(world==null)world=GetComponent<BattleWorld>();if(world!=null){world.CombatEffectOccurred+=OnCombat;world.PlayerChanged+=Clear;}viewer=world==null?null:world.currentPlayer;ScheduleTelegraph();}
  void OnDisable(){if(world!=null){world.CombatEffectOccurred-=OnCombat;world.PlayerChanged-=Clear;}Clear();}
  void OnDestroy(){foreach(var bank in generated.Values)foreach(var clip in bank)if(clip!=null)Destroy(clip);}
  public BattlefieldSoundProfile Profile(BattlefieldSound kind){foreach(var p in profiles)if(p!=null&&p.kind==kind)return p;return null;}
  public int ActiveVoicesOf(BattlefieldSound kind){int n=0;foreach(var v in voices)if(v.source.isPlaying&&v.profile.kind==kind)n++;return n;}
  public static float DistanceGain(float distance,BattlefieldSoundProfile p){if(p==null)return 0;float near=Mathf.Max(0,p.nearDistance),far=Mathf.Max(near+.001f,p.farDistance);return Mathf.Pow(1-Mathf.InverseLerp(near,far,Mathf.Max(0,distance)),Mathf.Max(.1f,p.falloff));}
  public static bool TryCenter(Camera camera,out Vector3 center){center=Vector3.zero;if(camera==null)return false;var ray=camera.ViewportPointToRay(new Vector3(.5f,.5f));var plane=new Plane(Vector3.forward,Vector3.zero);if(!plane.Raycast(ray,out float distance)||distance>camera.farClipPlane)return false;center=ray.GetPoint(distance);center.z=0;return true;}
  public static float StereoPan(Camera camera,Vector3 center,Vector3 point,float distance){Vector3 right=camera.transform.right;right.z=0;if(right.sqrMagnitude<.001f)return 0;return Mathf.Clamp(Vector3.Dot(point-center,right.normalized)/Mathf.Max(.2f,distance),-.9f,.9f);}
  public float ZoomGain(){var c=View;if(c==null)return 0;return Mathf.Lerp(1,overviewGain,Mathf.InverseLerp(referenceZoom,65,c.orthographicSize));}
  public bool Audible(BattleUnit unit){return world!=null&&unit!=null&&world.IsVisible(world.currentPlayer,unit);}
  bool Allowed(Voice v){if(v.profile.kind==BattlefieldSound.RocketBarrage&&v.unit==null)return false;if(v.unitBound){if(v.unit==null||!Audible(v.unit))return false;if(v.profile.kind==BattlefieldSound.RocketBarrage){if(v.unit.Motor!=null&&v.unit.Motor.IsMoving)return false;var battery=v.unit.GetComponent<ArtilleryBattery>();return (battery!=null&&battery.Firing)||Time.time<v.until;}if(v.profile.kind==BattlefieldSound.Construction)return v.unit.Working&&world.campaign!=null&&world.campaign.lineWorks!=null&&world.campaign.lineWorks.IsBuilding(v.unit)&&!world.campaign.lineWorks.Approaching(v.unit);return true;}return string.IsNullOrEmpty(v.owner)||v.owner==world.currentPlayer||world.VisibleAt(world.currentPlayer,v.position);}
  float Master=>Mathf.Clamp01(PlayerPrefs.GetFloat("Campaign.EffectsVolume",.8f));
  public void PlayAt(BattlefieldSound kind,Vector3 position,BattleUnit unit=null,string owner=null,bool loop=false){
   if(kind==BattlefieldSound.CommandVoice||kind==BattlefieldSound.Bird)return;
   var p=Profile(kind);if(p==null||world==null||world.SimulationEnded||Time.timeScale<=0||View==null||!TryCenter(View,out var center))return;
   var v=new Voice{unit=unit,unitBound=unit!=null,owner=owner,position=position,profile=p,loop=loop,until=Time.time+.8f};
   if(!Allowed(v)||DistanceGain(Vector2.Distance(center,position),p)*ZoomGain()*Master*p.gain<.001f)return;
   Voice slot=null;foreach(var candidate in voices)if(!candidate.source.isPlaying){slot=candidate;break;}
   if(slot==null){if(voices.Count>=Mathf.Max(1,maxVoices))return;var g=new GameObject("Battlefield sound");g.transform.SetParent(transform,false);slot=new Voice{source=g.AddComponent<AudioSource>(),filter=g.AddComponent<AudioLowPassFilter>()};slot.source.playOnAwake=false;slot.source.spatialBlend=0;slot.source.dopplerLevel=0;voices.Add(slot);}
   slot.unit=v.unit;slot.unitBound=v.unitBound;slot.owner=owner;slot.position=position;slot.profile=p;slot.loop=loop;slot.until=v.until;
   var clip=Clip(kind,p,unit);if(clip==null)return;slot.source.clip=clip;slot.source.loop=loop;slot.source.pitch=loop||kind==BattlefieldSound.CommandVoice?1:(float)(.95+random.NextDouble()*.1);Apply(slot,center);slot.source.Play();
   if(loop&&kind!=BattlefieldSound.RocketBarrage&&clip.length>0)slot.source.time=(float)random.NextDouble()*clip.length;
  }
  int ChooseVariant(BattlefieldSound kind,int count){if(count<=1)return 0;int index=random.Next(count);if(previousVariant.TryGetValue(kind,out var previous)&&index==previous)index=(index+1+random.Next(count-1))%count;previousVariant[kind]=index;return index;}
  AudioClip Recording(BattlefieldSound kind){
   if(recordings.TryGetValue(kind,out var clip))return clip;
   string path=kind==BattlefieldSound.Footstep?"Movement/InfantryMarch":kind==BattlefieldSound.Tank?"Movement/TankMarch":kind==BattlefieldSound.TankAttack?"Combat/TankAttack":kind==BattlefieldSound.Explosion?"Combat/ArtilleryImpact":kind==BattlefieldSound.Cannon?"Combat/ArtilleryLaunch":kind==BattlefieldSound.RocketBarrage?"Combat/RocketBarrage":null;
   clip=path==null?null:Resources.Load<AudioClip>("Audio/"+path);recordings[kind]=clip;return clip;
  }
  AudioClip Clip(BattlefieldSound kind,BattlefieldSoundProfile p,BattleUnit unit){if(p.clips!=null&&p.clips.Length>0){var clip=p.clips[ChooseVariant(kind,p.clips.Length)];if(clip!=null)return clip;}if(kind==BattlefieldSound.Construction){if(constructionRecordings==null)constructionRecordings=Resources.LoadAll<AudioClip>("Audio/Construction");if(constructionRecordings.Length>0)return constructionRecordings[ChooseVariant(kind,constructionRecordings.Length)];}var recording=kind==BattlefieldSound.Cannon&&unit!=null&&unit.combatDefinition!=null&&unit.combatDefinition.role==UnitRole.RocketArtillery?null:Recording(kind);if(recording!=null)return recording;if(kind==BattlefieldSound.CommandVoice)return null;if(!generated.TryGetValue(kind,out var bank)){bank=new AudioClip[BattlefieldAudioSynthesis.VariantCount(kind)];for(int i=0;i<bank.Length;i++)bank[i]=BattlefieldAudioSynthesis.Create(kind,i);generated.Add(kind,bank);}return bank[ChooseVariant(kind,bank.Length)];}
  void Apply(Voice v,Vector3 center){if(v.loop&&v.unit!=null)v.position=v.unit.transform.position;float d=Vector2.Distance(center,v.position);float gain=DistanceGain(d,v.profile);v.source.volume=Mathf.Clamp01(v.profile.gain)*gain*ZoomGain()*Master;v.source.panStereo=StereoPan(View,center,v.position,Mathf.Max(.4f,Mathf.Min(v.profile.farDistance*.65f,View.orthographicSize)));v.filter.cutoffFrequency=Mathf.Lerp(19000,1600,Mathf.Clamp01(d/Mathf.Max(.001f,v.profile.farDistance)));}
  void LateUpdate(){
   if(world==null||!world.Initialized||View==null)return;
   if(viewer!=world.currentPlayer){Clear();viewer=world.currentPlayer;}
   if(world.SimulationEnded||Time.timeScale<=0||!TryCenter(View,out var center)){Clear();return;}ListeningCenter=center;
   foreach(var v in voices){if(!v.source.isPlaying)continue;if(!Allowed(v)){v.source.Stop();continue;}Apply(v,center);if(v.source.volume<.001f)v.source.Stop();}
   if(Time.unscaledTime>=nextMovement){Movement();nextMovement=Time.unscaledTime+.12f;}
   if(periodicTelegraph&&Time.unscaledTime>=nextTelegraph){PlayAt(BattlefieldSound.Telegraph,center);ScheduleTelegraph();}
  }
  void Movement(){
   if(world.units==null)return;
   var live=new HashSet<BattleUnit>();
   foreach(var u in world.units){if(u==null)continue;live.Add(u);Vector3 pos=u.transform.position;bool moved=positions.TryGetValue(u,out var old)&&Vector2.Distance(old,pos)>.00001f;positions[u]=pos;
    bool moving=moved&&u.CanMove&&Audible(u);bool vehicle=u.GetComponent<TransportTruck>()!=null||u.GetComponent<PersonnelCarrier>()!=null||u.combatDefinition!=null&&(u.combatDefinition.role==UnitRole.Tank||u.combatDefinition.role==UnitRole.Logistics);
    var works=world.campaign==null?null:world.campaign.lineWorks;
    bool building=u.Working&&u.combatDefinition!=null&&u.combatDefinition.role==UnitRole.Engineer&&works!=null&&works.IsBuilding(u)&&!works.Approaching(u)&&Audible(u);
    if(building&&(!nextHammer.TryGetValue(u,out var hammer)||Time.unscaledTime>=hammer)){PlayAt(BattlefieldSound.Construction,pos,u);nextHammer[u]=Time.unscaledTime+.7f+(float)random.NextDouble()*.6f;}
    bool recordedFootsteps=!vehicle&&u.combatDefinition!=null&&!ArtilleryBattery.IsArtilleryRole(u.combatDefinition.role)&&u.combatDefinition.role!=UnitRole.Radar&&Recording(BattlefieldSound.Footstep)!=null;
    if(vehicle||recordedFootsteps){bool exists=false;foreach(var v in voices)if(v.loop&&v.unit==u&&v.source.isPlaying){exists=true;if(!moving)v.source.Stop();else v.source.pitch=recordedFootsteps?1:Mathf.Clamp(.85f+Vector2.Distance(old,pos)/.12f*.8f,.85f,1.3f);}if(moving&&!exists)PlayAt(recordedFootsteps?BattlefieldSound.Footstep:u.combatDefinition!=null&&u.combatDefinition.role==UnitRole.Tank?BattlefieldSound.Tank:BattlefieldSound.Car,pos,u,loop:true);}
    else if(moving&&u.combatDefinition!=null&&!ArtilleryBattery.IsArtilleryRole(u.combatDefinition.role)&&u.combatDefinition.role!=UnitRole.Radar&&(!nextStep.TryGetValue(u,out var next)||Time.unscaledTime>=next)){PlayAt(BattlefieldSound.Footstep,pos,u);nextStep[u]=Time.unscaledTime+.6f+(float)random.NextDouble()*.18f;}
   }
   var removed=new List<BattleUnit>();foreach(var u in positions.Keys)if(u==null||!live.Contains(u))removed.Add(u);foreach(var u in removed){positions.Remove(u);nextStep.Remove(u);nextHammer.Remove(u);}
  }
  void Bird(){var map=world.setup.freeMarchMap;var p=Profile(BattlefieldSound.Bird);if(map==null||p==null)return;for(int i=0;i<12;i++){float angle=(float)random.NextDouble()*Mathf.PI*2;float radius=Mathf.Lerp(.3f,p.farDistance*.55f,(float)random.NextDouble());var at=ListeningCenter+new Vector3(Mathf.Cos(angle),Mathf.Sin(angle))*radius;var terrain=map.TerrainAt(new RoadPoint(at.x,at.y));if(terrain==TerrainKind.Forest||terrain==TerrainKind.Grass||terrain==TerrainKind.Ground){PlayAt(BattlefieldSound.Bird,at);return;}}}
  BattleUnit Find(string id){if(world.units!=null)foreach(var u in world.units)if(u!=null&&u.unitId==id)return u;return null;}
  void OnCombat(CombatEffectEvent e){
   // Require the firing enemy itself to be visible, even when its target is friendly.
   var source=Find(e.SourceId);if(source!=null){if(!Audible(source))return;}else if(e.SourceOwner!=world.currentPlayer)return;
   if(e.Stage==CombatEffectStage.ArtilleryImpact){if(world.VisibleAt(world.currentPlayer,e.TargetPosition))PlayAt(BattlefieldSound.Explosion,e.TargetPosition,source,e.SourceOwner);return;}
   if(e.Stage==CombatEffectStage.ArtilleryLaunch&&e.SourceRole==UnitRole.RocketArtillery&&source!=null){foreach(var voice in voices)if(voice.source.isPlaying&&voice.unit==source&&voice.profile.kind==BattlefieldSound.RocketBarrage){voice.until=Time.time+.8f;return;}PlayAt(BattlefieldSound.RocketBarrage,e.SourcePosition,source,e.SourceOwner,true);return;}
   if(!e.HasSource)return;string key=(e.SourceId??e.SourceOwner??"")+":"+e.Stage;if(nextShot.TryGetValue(key,out var next)&&Time.time<next)return;nextShot[key]=Time.time+.25f;
   if(e.Stage==CombatEffectStage.DirectAttack&&e.SourceRole==UnitRole.Tank&&world.VisibleAt(world.currentPlayer,e.TargetPosition))PlayAt(BattlefieldSound.Explosion,e.TargetPosition,source,e.SourceOwner);
   PlayAt(e.SourceRole==UnitRole.Tank?BattlefieldSound.TankAttack:e.Stage==CombatEffectStage.ArtilleryLaunch?BattlefieldSound.Cannon:BattlefieldSound.Rifle,e.SourcePosition,source,e.SourceOwner);
  }
  void ScheduleTelegraph(){float min=Mathf.Max(1,telegraphMinimumInterval),max=Mathf.Max(min,telegraphMaximumInterval);nextTelegraph=Time.unscaledTime+Mathf.Lerp(min,max,(float)random.NextDouble());}
  public static bool CommandVoiceRoll(int roll){return roll==0;}
  // Reserved compatibility entry point; movement speech has been removed.
  public void MovementCommandAccepted(BattleCommand c){}
  public void Clear(){foreach(var v in voices)if(v.source!=null)v.source.Stop();positions.Clear();nextStep.Clear();nextHammer.Clear();nextShot.Clear();}
 }
}
