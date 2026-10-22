using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace CampaignDemo {
 // Local presentation only; randomness never consumes the simulation's random stream.
 [DisallowMultipleComponent]
 public sealed class LocalCombatEffects:MonoBehaviour,ICombatEffectPlayer {
  [Range(32,400)] public int maxParticles=180;
  readonly List<LocalEffectParticle> active=new List<LocalEffectParticle>();
  readonly System.Random random=new System.Random();
  Sprite glow;
  public int ActiveCount { get {active.RemoveAll(p=>p==null);return active.Count;} }
  [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
  static void Install(){SceneManager.sceneLoaded-=Loaded;SceneManager.sceneLoaded+=Loaded;}
  static void Loaded(Scene scene,LoadSceneMode mode){foreach(var world in Object.FindObjectsOfType<BattleWorld>())if(world.gameObject.scene==scene)Attach(world);}
  public static void Attach(BattleWorld world){
   var player=world.GetComponent<LocalCombatEffects>();if(player==null)player=world.gameObject.AddComponent<LocalCombatEffects>();
   var presenter=world.GetComponent<CombatEffectPresenter>();if(presenter==null)presenter=world.gameObject.AddComponent<CombatEffectPresenter>();
   if(presenter.effectPlayer!=null&&presenter.effectPlayer!=player){var old=presenter.effectPlayer as ICombatEffectPlayer;if(old!=null)old.Clear();}
   player.enabled=true;presenter.enabled=true;presenter.effectPlayer=player;presenter.BindWorld(world);
   var marks=world.GetComponent<DeathScorchMarks>();if(marks==null)marks=world.gameObject.AddComponent<DeathScorchMarks>();marks.world=world;
  }
  void Awake(){var t=new Texture2D(32,32,TextureFormat.RGBA32,false){name="Restrained combat glow",wrapMode=TextureWrapMode.Clamp};for(int y=0;y<32;y++)for(int x=0;x<32;x++){float d=Vector2.Distance(new Vector2(x,y),Vector2.one*15.5f)/16;t.SetPixel(x,y,new Color(1,1,1,Mathf.Pow(Mathf.Clamp01(1-d),2)));}t.Apply();glow=Sprite.Create(t,new Rect(0,0,32,32),Vector2.one*.5f,32);}
  float R(float lo,float hi){return Mathf.Lerp(lo,hi,(float)random.NextDouble());}
  Vector3 Disk(float radius){float a=R(0,Mathf.PI*2),r=Mathf.Sqrt(R(0,1))*radius;return new Vector3(Mathf.Cos(a)*r,Mathf.Sin(a)*r);}
  void Particle(Vector3 a,Vector3 b,Color color,float size,float duration,float delay=0,float arc=0,bool smoke=false,bool streak=false){
   active.RemoveAll(p=>p==null);while(active.Count>=Mathf.Max(1,maxParticles)){var oldest=active[0];active.RemoveAt(0);if(oldest!=null){oldest.gameObject.SetActive(false);Destroy(oldest.gameObject);}}
   var g=new GameObject(smoke?"轻烟尘":streak?"短曳光":"短促火光");g.transform.SetParent(transform,false);var s=g.AddComponent<SpriteRenderer>();s.sprite=glow;s.sortingOrder=smoke?30:32;s.color=color;
   var p=g.AddComponent<LocalEffectParticle>();p.Init(a,b,color,size,duration,delay,arc,smoke,streak);active.Add(p);
  }
  void Flash(Vector3 at,float size,float delay=0){Particle(at,at,new Color(1,.86f,.5f,.9f),size,.14f,delay);}
  void Dust(Vector3 at,float size,float delay=0){var drift=Disk(size*.6f);Particle(at,at+drift,new Color(.24f,.22f,.18f,.3f),size,R(.4f,.7f),delay,0,true);}
  void Burst(Vector3 at,float radius,float delay){
   float s=Mathf.Clamp(radius,.16f,.75f);Flash(at,s*.7f,delay);Particle(at,at,new Color(.95f,.52f,.18f,.7f),s,.16f,delay);
   for(int i=0;i<4;i++){var d=Disk(s);Particle(at,at+d,new Color(.75f,.58f,.3f,.6f),.05f+s*.08f,.2f,delay);Dust(at+d*.4f,s*.55f,delay+.05f);}
  }
  public void Play(CombatEffectCue cue){if(!isActiveAndEnabled||glow==null)return;
   if(cue.Stage==CombatEffectStage.DirectAttack){
    bool tank=cue.HasSourceRole&&cue.SourceRole==UnitRole.Tank;int count=tank?1:random.Next(3,6);
    for(int i=0;i<count;i++){
     float delay=tank?0:i*R(.045f,.08f);var source=cue.SourcePosition+Disk(tank?.04f:.16f);var target=cue.TargetPosition+Disk(tank?.1f:.22f);
     if(cue.HasSource){Flash(source,tank?.32f:.12f,delay);if(cue.HasTarget){float travel=tank?.2f:Mathf.Clamp(Vector3.Distance(source,target)/24,.18f,.32f);Particle(source,target,new Color(1,.83f,.42f,.8f),tank?.085f:.035f,travel,delay,0,false,true);}}
     if(cue.HasTarget){if(tank)Burst(target,.3f,delay+.2f);else Dust(target,.12f,delay+.2f);}
    }
   }
   else if(cue.Stage==CombatEffectStage.ArtilleryLaunch&&cue.HasSource){Flash(cue.SourcePosition,.38f);Dust(cue.SourcePosition,.28f,.04f);if(cue.HasTarget)Particle(cue.SourcePosition,cue.TargetPosition,new Color(1,.78f,.38f,.75f),.08f,.7f,0,.3f,false,true);}
   else if(cue.Stage==CombatEffectStage.ArtilleryImpact&&cue.HasTarget)Burst(cue.TargetPosition,Mathf.Max(.3f,cue.Radius*.45f),0);
  }
  public void Clear(){foreach(var p in active)if(p!=null){p.gameObject.SetActive(false);Destroy(p.gameObject);}active.Clear();}
  void OnDisable(){Clear();}
  void OnDestroy(){Clear();if(glow!=null){Destroy(glow.texture);Destroy(glow);}}
 }
 public sealed class LocalEffectParticle:MonoBehaviour {
  public Vector3 From {get;private set;} public Vector3 To {get;private set;}
  public bool IsStreak {get;private set;}
  Color color;float start,duration,size,arc;bool smoke;SpriteRenderer view;Camera mapCamera;
  public void Init(Vector3 a,Vector3 b,Color c,float s,float life,float delay,float curve,bool dust,bool streak=false){From=a;To=b;color=c;size=s;duration=Mathf.Max(.01f,life);start=Time.time+delay;arc=curve;smoke=dust;IsStreak=streak;view=GetComponent<SpriteRenderer>();view.enabled=false;transform.position=a;var world=GetComponentInParent<BattleWorld>();mapCamera=world!=null&&world.setup!=null?world.setup.mapCamera:Camera.main;}
  void Update(){float t=(Time.time-start)/duration;if(t<0)return;if(t>=1){Destroy(gameObject);return;}view.enabled=true;transform.position=Vector3.Lerp(From,To,t)+Vector3.up*Mathf.Sin(t*Mathf.PI)*arc;
   float visibleSize=size;if(mapCamera!=null&&mapCamera.orthographic){float pixel=2*mapCamera.orthographicSize/Mathf.Max(1,mapCamera.pixelHeight);visibleSize=Mathf.Max(size,pixel*(IsStreak?4:smoke?7:10));}
   visibleSize*=3f;
   if(IsStreak){Vector3 tangent=To-From+Vector3.up*(Mathf.Cos(t*Mathf.PI)*Mathf.PI*arc);transform.rotation=Quaternion.Euler(0,0,Mathf.Atan2(tangent.y,tangent.x)*Mathf.Rad2Deg);transform.localScale=new Vector3(Mathf.Max(visibleSize*3,Mathf.Min(Vector3.Distance(From,To)*.2f,visibleSize*5)),visibleSize,1);}
   else transform.localScale=Vector3.one*visibleSize*(smoke?1+t*1.2f:1-t*.5f);
   view.color=new Color(color.r,color.g,color.b,color.a*(1-t));
  }
 }
}



