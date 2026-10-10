using System.Collections.Generic;
using UnityEngine;
namespace CampaignDemo {
 [DefaultExecutionOrder(250)]
 public sealed class MinimapAlertTracker:MonoBehaviour {
  public BattleWorld world;
  [Min(.1f)] public float duration=3;
  [Min(.1f)] public float repeatSeconds=1;
  public Color alertColor=new Color(1,.55f,.05f);
  public sealed class Alert {public int id;public Vector3 position;public float raised,expires;}
  sealed class Snapshot {public string owner;public float health;public bool contested;}
  readonly Dictionary<int,Snapshot> snapshots=new Dictionary<int,Snapshot>();
  readonly List<Alert> alerts=new List<Alert>();
  string observer;
  public IReadOnlyList<Alert> Alerts {get{return alerts;}}
  void Start(){if(world==null)world=FindObjectOfType<BattleWorld>();if(world!=null)world.PlayerChanged+=ResetObserver;Sample();}
  void ResetObserver(){observer=null;snapshots.Clear();alerts.Clear();Sample();}
  void OnDestroy(){if(world!=null)world.PlayerChanged-=ResetObserver;}
  void Update(){Sample();}
  public void Sample(){
   if(world==null||!world.Initialized)return;if(observer!=world.currentPlayer){observer=world.currentPlayer;snapshots.Clear();alerts.Clear();}
   alerts.RemoveAll(a=>Time.unscaledTime>=a.expires);
   foreach(var u in world.units)if(u!=null&&u.ownerId==observer)Track(u,u.ownerId,u.Personnel,false,u.Carrier==null?u.transform.position:u.Carrier.transform.position);
   foreach(var p in world.points)if(p!=null&&(p.ownerId==observer||snapshots.ContainsKey(p.GetInstanceID())))Track(p,p.ownerId,0,!string.IsNullOrEmpty(p.CapturingPlayer)&&p.CapturingPlayer!=observer,p.transform.position);
   var c=world.campaign;if(c==null)return;
   if(c.combined!=null)foreach(var f in c.combined.fortifications)if(f!=null&&f.ownerId==observer)Track(f,f.ownerId,f.Health,false,f.IsLine?f.PointAtArc(f.WorldLength*.5f):f.transform.position);
   if(c.logistics!=null)foreach(var collector in c.logistics.collectors)if(collector!=null&&collector.ownerId==observer)Track(collector,collector.ownerId,collector.Health,false,collector.transform.position);
   foreach(var depot in c.depots)if(depot!=null&&(depot.ownerId==observer||snapshots.ContainsKey(depot.GetInstanceID())))Track(depot,depot.ownerId,0,!depot.Removed&&!string.IsNullOrEmpty(depot.capturingPlayer)&&depot.capturingPlayer!=observer,depot.transform.position);
  }
  void Track(Component obj,string owner,float health,bool contested,Vector3 position){int id=obj.GetInstanceID();Snapshot old;bool existed=snapshots.TryGetValue(id,out old);bool danger=existed&&old.owner==observer&&(health<old.health||owner!=observer||contested);if(danger){Alert alert=alerts.Find(a=>a.id==id);if(alert==null){alert=new Alert{id=id,position=position,raised=Time.unscaledTime};alerts.Add(alert);}else if(Time.unscaledTime-alert.raised>=repeatSeconds){alert.raised=Time.unscaledTime;alert.position=position;}alert.expires=Time.unscaledTime+duration;}snapshots[id]=new Snapshot{owner=owner,health=health,contested=contested};}
 }
}

