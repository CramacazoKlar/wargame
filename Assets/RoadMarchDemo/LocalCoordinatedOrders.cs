using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
namespace CampaignDemo
{
 [DefaultExecutionOrder(-170),DisallowMultipleComponent]
 public sealed class LocalCoordinatedOrders:MonoBehaviour
 {
  public enum PickMode { None,Rotation,Defense }
  public PickMode Picking {get;private set;}
  sealed class Rotation {public BattleUnit front,rear;public Vector3 frontPosition,rearPosition;public uint frontRevision,rearRevision;public bool withdrawing;}
  sealed class Link {public BattleUnit a,b;public LineRenderer line;}
  sealed class Support {public BattleUnit helper,ally,attacker;public uint revision;public float quiet,replan;public bool issued;public Vector3 destination;public object suspendedTask;}
  sealed class Hit {public BattleUnit ally,attacker;}
  readonly List<Rotation> rotations=new List<Rotation>();
  readonly List<Link> links=new List<Link>();
  readonly Dictionary<BattleUnit,Support> support=new Dictionary<BattleUnit,Support>();
  readonly Dictionary<BattleUnit,float> manualPause=new Dictionary<BattleUnit,float>();
  readonly Dictionary<BattleUnit,Hit> hits=new Dictionary<BattleUnit,Hit>();
  BattleWorld world;ArmySelection selection;CampaignHUD hud;BattleUnit anchor,pendingRear;Material material;readonly List<BattleUnit> pendingDefense=new List<BattleUnit>();
  [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]static void Install(){SceneManager.sceneLoaded-=Loaded;SceneManager.sceneLoaded+=Loaded;}
  static void Loaded(Scene scene,LoadSceneMode mode){foreach(var w in FindObjectsOfType<BattleWorld>())if(w.gameObject.scene==scene&&LocalPrototypeOrders.EnabledFor(w)&&w.GetComponent<LocalCoordinatedOrders>()==null)w.gameObject.AddComponent<LocalCoordinatedOrders>();}
  void Awake(){Resolve();}
  void Resolve(){if(world==null)world=GetComponent<BattleWorld>();if(selection==null)selection=GetComponent<ArmySelection>();if(hud==null)hud=FindObjectOfType<CampaignHUD>();}
  void Feedback(string text){hud?.SetCommandFeedback(text);}
  bool Editing(){var g=EventSystem.current==null?null:EventSystem.current.currentSelectedGameObject;return g!=null&&g.GetComponent<InputField>()!=null;}
  bool Eligible(BattleUnit u){return u!=null&&u.OnMap&&u.CanMove&&u.ownerId==NetworkTacticalOrders.Player(world,u.ownerId);}
  public bool InRotation(BattleUnit u){return u!=null&&(!NetworkTacticalOrders.Authority?u.GetComponent<NetworkUnitPresentation>()?.rotating==true:rotations.Any(r=>r.front==u||r.rear==u));}
  public int LinkCount(BattleUnit u){return u==null?0:links.Count(e=>e.a==u||e.b==u);}
  public bool Supporting(BattleUnit u){return u!=null&&(!NetworkTacticalOrders.Authority?u.GetComponent<NetworkUnitPresentation>()?.supporting==true:support.ContainsKey(u));}
  public string Status(BattleUnit u){if(!NetworkTacticalOrders.Authority)return u==null?"":u.GetComponent<NetworkUnitPresentation>()?.coordination??"";var r=rotations.FirstOrDefault(x=>x.front==u||x.rear==u);return r!=null?(r.withdrawing?"交替轮换：前队撤回":"交替轮换：后队接替"):(Supporting(u)?"联防支援中":LinkCount(u)>0?"联防关联 "+LinkCount(u)+" 条":"");}
  public void BeginPicking(PickMode mode){Resolve();if(!LocalPrototypeOrders.EnabledFor(this)||world==null||!world.Initialized)return;GetComponent<UnitContextOrders>()?.CancelSplitPreview();CancelPicking();Picking=mode;anchor=null;hud?.CancelTool();GetComponent<LocalPrototypeOrders>()?.CancelDraft();GetComponent<RoadMarchController>()?.CancelDraft();GetComponent<FreeMarchController>()?.CancelDraft();GetComponent<GroupMarchController>()?.Cancel();GetComponent<LocalTacticalBattle>()?.CancelPointerDraft();Feedback(mode==PickMode.Rotation?"轮换：先点前队，再点后队":"联防：先点一个单位，再点关联单位；H / 右键完成");}
  public void TogglePicking(PickMode mode){if(Picking==mode)EndPicking();else BeginPicking(mode);}
  public void CancelPicking(){Picking=PickMode.None;anchor=null;pendingRear=null;pendingDefense.Clear();}
  public void EndPicking(){var mode=Picking;var front=anchor;var rear=pendingRear;var members=pendingDefense.ToArray();CancelPicking();if(mode==PickMode.Rotation){if(front!=null&&rear!=null)StartRotation(front,rear);else Feedback("轮换未选择完整，未执行");}else if(mode==PickMode.Defense){int added=0;foreach(var u in members)if(LinkDefense(front,u))added++;Feedback(added>0?"联防设置完成":"未新增联防关联");}}
  public bool Pick(BattleUnit u){if(!Eligible(u)){Feedback("请选择地图上可移动的己方单位");return false;}if(anchor==null){anchor=u;hud?.SelectUnit(u);Feedback(Picking==PickMode.Rotation?"再选择后方接替单位":"继续选择联防单位；再次点击联防 / H 执行");return true;}if(anchor==u)return false;if(Picking==PickMode.Rotation){pendingRear=u;hud?.SelectUnit(u);Feedback("前后队已选；再次点击轮换 / K 开始执行");return true;}if(Picking==PickMode.Defense){if(!pendingDefense.Contains(u))pendingDefense.Add(u);hud?.SelectUnit(u);Feedback("已选 "+pendingDefense.Count+" 支联防部队；再次点击联防 / H 执行");return true;}return false;}
  void Update(){Resolve();if(!LocalPrototypeOrders.EnabledFor(this)||world==null||!world.Initialized||world.SimulationEnded||Editing())return;
   if(Input.GetKeyDown(KeyCode.K)){if(Picking==PickMode.Rotation)EndPicking();else BeginPicking(PickMode.Rotation);UnitContextOrders.ConsumePointer();return;}
   if(Input.GetKeyDown(KeyCode.H)){if(Picking==PickMode.Defense)EndPicking();else BeginPicking(PickMode.Defense);UnitContextOrders.ConsumePointer();return;}
   if(Picking==PickMode.None)return;
   if(Input.GetKeyDown(KeyCode.Escape)||Input.GetKeyDown(KeyCode.T)||Input.GetKeyDown(KeyCode.R)){CancelPicking();UnitContextOrders.ConsumePointer();return;}if(Input.GetMouseButtonDown(1)){EndPicking();UnitContextOrders.ConsumePointer();return;}
   if(!Input.GetMouseButtonDown(0)||EventSystem.current!=null&&EventSystem.current.IsPointerOverGameObject())return;UnitContextOrders.ConsumePointer();BattleUnit selected=null;float distance=world.setup.settings.selectionPixels;foreach(var u in world.units){if(!Eligible(u))continue;var p=world.setup.mapCamera.WorldToScreenPoint(u.transform.position);float d=Vector2.Distance(Input.mousePosition,p);if(p.z>0&&d<distance){distance=d;selected=u;}}if(selected!=null)Pick(selected);else Feedback("请选择己方单位");
  }
  void Prepare(BattleUnit u){GetComponent<LocalPrototypeOrders>()?.CancelUnit(u,true);GetComponent<UnitContextOrders>()?.CancelEntriesFor(u,false);GetComponent<TacticalTargetOrders>()?.CancelFor(u,false);CancelSupport(u,true);}
  List<RoadPoint> Route(BattleUnit u,Vector3 destination){var a=u.GetComponent<ArmyRoadMover>();return a==null?null:LocalRoadAStar.Plan(world.setup.freeMarchMap,a.AllowedTerrain,u.transform.position,destination,Vector2.Distance(u.transform.position,destination)>12?.4f:.2f,30000);}
  bool Move(BattleUnit u,Vector3 destination,out uint revision){revision=u.Motor.ArrowOrderRevision;if(Vector2.Distance(u.transform.position,destination)<=.025f){u.Motor.Stop();return true;}var route=Route(u,destination);if(route==null||!u.GetComponent<ArmyRoadMover>().CommitAutomaticRoute(route))return false;revision=u.Motor.ArrowOrderRevision;return true;}
  public bool StartRotation(BattleUnit front,BattleUnit rear){Resolve();if(NetworkTacticalOrders.Forward(world,front,"rotation",out var forwarded,target:rear))return forwarded;if(!LocalPrototypeOrders.EnabledFor(this)||!Eligible(front)||!Eligible(rear)||front==rear||front.ownerId!=rear.ownerId){Feedback("轮换需要两支可移动的己方单位");return false;}var a=front.transform.position;var b=rear.transform.position;
   // Validate both legs before stopping either original order.
   if(Vector2.Distance(a,b)<.025f||Route(rear,a)==null||Route(front,b)==null){Feedback("轮换位置相同或双方路线不可通行");return false;}CancelRotation(front);CancelRotation(rear);Prepare(front);Prepare(rear);var r=new Rotation{front=front,rear=rear,frontPosition=a,rearPosition=b,frontRevision=front.Motor.ArrowOrderRevision};if(!Move(rear,a,out r.rearRevision)){Feedback("后队无法接替，轮换取消");return false;}rotations.Add(r);if(front.ownerId==world.currentPlayer)hud?.SelectUnit(rear);Feedback("轮换已关联：后队到位后，前队再撤回");return true;
  }
  void StopOwned(BattleUnit u,uint revision){if(u!=null&&u.Motor!=null&&u.Motor.ArrowOrderRevision==revision)u.Motor.Stop();}
  void Cancel(Rotation r){StopOwned(r.front,r.frontRevision);StopOwned(r.rear,r.rearRevision);rotations.Remove(r);}
  public void CancelRotation(BattleUnit u){foreach(var r in rotations.Where(x=>x.front==u||x.rear==u).ToArray())Cancel(r);}
  public bool LinkDefense(BattleUnit a,BattleUnit b){Resolve();if(NetworkTacticalOrders.Forward(world,a,"defense",out var forwarded,target:b))return forwarded;if(!LocalPrototypeOrders.EnabledFor(this)||!Eligible(a)||!Eligible(b)||a==b||a.ownerId!=b.ownerId||links.Count>=128)return false;if(links.Any(e=>e.a==a&&e.b==b||e.a==b&&e.b==a))return true;links.Add(new Link{a=a,b=b});Feedback("联防已关联");return true;}
  public string[] NetworkLinks(BattleUnit u){return links.Where(e=>e.a==u||e.b==u).Select(e=>(e.a==u?e.b:e.a).unitId).ToArray();}
  public void ApplyNetworkLinks(){foreach(var e in links.ToArray())Remove(e);foreach(var u in world.units){var state=u==null?null:u.GetComponent<NetworkUnitPresentation>();if(u==null||u.ownerId!=world.currentPlayer||state?.links==null)continue;foreach(var id in state.links){var other=world.units.FirstOrDefault(v=>v!=null&&v.ownerId==u.ownerId&&v.unitId==id);if(other!=null&&!links.Any(e=>e.a==u&&e.b==other||e.a==other&&e.b==u))links.Add(new Link{a=u,b=other});}}}
  void Remove(Link e){if(e.line!=null)Destroy(e.line.gameObject);links.Remove(e);}
  public void CancelDefense(BattleUnit u){foreach(var e in links.Where(x=>x.a==u||x.b==u).ToArray())Remove(e);foreach(var s in support.Values.ToArray())if(s.helper==u||!Connected(s.helper,s.ally))CancelSupport(s.helper,true,true);hits.Remove(u);}
  HashSet<BattleUnit> Group(BattleUnit u){var result=new HashSet<BattleUnit>();if(u==null)return result;var queue=new Queue<BattleUnit>();queue.Enqueue(u);result.Add(u);while(queue.Count>0){var at=queue.Dequeue();foreach(var edge in links){var next=edge.a==at?edge.b:edge.b==at?edge.a:null;if(next!=null&&next.Alive&&next.ownerId==u.ownerId&&result.Add(next))queue.Enqueue(next);}}return result;}
  bool Connected(BattleUnit a,BattleUnit b){return a!=null&&b!=null&&Group(a).Contains(b);}
  public void NotifyHit(BattleUnit attacker,BattleUnit ally,string sourceOwner){if(!LocalPrototypeOrders.EnabledFor(this)||ally==null||!ally.OnMap||string.IsNullOrEmpty(sourceOwner)||sourceOwner==ally.ownerId||LinkCount(ally)==0)return;hits[ally]=new Hit{ally=ally,attacker=attacker};}
  void CancelSupport(BattleUnit u,bool stop,bool resume=false){if(!ReferenceEquals(u,null)&&support.TryGetValue(u,out var s)){if(stop&&s.issued)StopOwned(u,s.revision);support.Remove(u);if(resume)GetComponent<LocalPrototypeOrders>()?.ResumeAfterSupport(u,s.suspendedTask);}}
  public void CancelCommand(BattleCommand c){if(c==null)return;switch(c.kind){case BattleCommandKind.StopSelected:case BattleCommandKind.NudgeSelected:if(c.unitIds!=null)foreach(var id in c.unitIds)Manual(world.units.FirstOrDefault(u=>u!=null&&u.ownerId==c.playerId&&u.unitId==id));return;case BattleCommandKind.StopMovement:case BattleCommandKind.RoadRoute:case BattleCommandKind.FreeRoute:case BattleCommandKind.RoadDestination:case BattleCommandKind.FreeDestination:case BattleCommandKind.StraightDestination:case BattleCommandKind.EnterPoint:case BattleCommandKind.BoardCarrier:case BattleCommandKind.BuildFortification:case BattleCommandKind.SplitFlank:case BattleCommandKind.SplitFeint:case BattleCommandKind.SplitInfiltrate:case BattleCommandKind.MergeDetachment:Manual(world.units.FirstOrDefault(u=>u!=null&&u.ownerId==c.playerId&&u.unitId==c.unitId));return;}}
  public void CancelForNewTask(BattleUnit u){Manual(u);}
  void Manual(BattleUnit u){if(u==null)return;CancelRotation(u);CancelSupport(u,false);manualPause[u]=2;}
  public void Tick(float dt){Resolve();if(!NetworkTacticalOrders.Authority||!LocalPrototypeOrders.EnabledFor(this)||world==null||world.SimulationEnded)return;dt=Mathf.Max(0,dt);
   foreach(var u in manualPause.Keys.ToArray()){if(u==null){manualPause.Remove(u);continue;}manualPause[u]-=dt;if(manualPause[u]<=0&&!u.Motor.IsMoving)manualPause.Remove(u);}
   foreach(var edge in links.ToArray())if(edge.a==null||edge.b==null||!edge.a.Alive||!edge.b.Alive||edge.a.ownerId!=edge.b.ownerId)Remove(edge);
   foreach(var r in rotations.ToArray()){
    if(r.front==null||r.rear==null||!r.front.OnMap||!r.rear.OnMap||!r.front.CanMove||!r.rear.CanMove||r.front.ownerId!=r.rear.ownerId||r.front.Motor.ArrowOrderRevision!=r.frontRevision||r.rear.Motor.ArrowOrderRevision!=r.rearRevision){Cancel(r);continue;}
    var moving=r.withdrawing?r.front:r.rear;var goal=r.withdrawing?r.rearPosition:r.frontPosition;if(moving.Motor.IsMoving)continue;if(Vector2.Distance(moving.transform.position,goal)>.03f){Cancel(r);Feedback("轮换途中受阻，已取消");continue;}
    if(r.withdrawing){rotations.Remove(r);continue;}r.rear.Motor.Stop();if(!Move(r.front,r.rearPosition,out r.frontRevision)){Cancel(r);Feedback("前队撤回路线不可达，轮换已停止");continue;}r.withdrawing=true;
   }
   var requested=new Dictionary<BattleUnit,Hit>();foreach(var h in hits.Values){if(h.ally==null||!h.ally.OnMap)continue;foreach(var helper in Group(h.ally)){if(helper==h.ally||!helper.CanMove||InRotation(helper)||manualPause.ContainsKey(helper))continue;if(!requested.TryGetValue(helper,out var old)||Vector2.Distance(helper.transform.position,h.ally.transform.position)<Vector2.Distance(helper.transform.position,old.ally.transform.position))requested[helper]=h;}}hits.Clear();
   foreach(var pair in requested){if(!support.TryGetValue(pair.Key,out var s)){var saved=GetComponent<LocalPrototypeOrders>()?.SuspendForSupport(pair.Key);Prepare(pair.Key);s=new Support{helper=pair.Key,ally=pair.Value.ally,attacker=pair.Value.attacker,suspendedTask=saved};support[pair.Key]=s;}else if(s.ally!=pair.Value.ally){if(s.issued)StopOwned(s.helper,s.revision);s.ally=pair.Value.ally;s.issued=false;}s.attacker=pair.Value.attacker;s.quiet=0;}
   foreach(var s in support.Values.ToArray()){
    s.quiet+=dt;s.replan-=dt;var u=s.helper;if(u==null||!u.OnMap||s.ally==null||!s.ally.OnMap||!Connected(u,s.ally)||s.quiet>6){CancelSupport(u,true,true);continue;}if(!u.CanMove)continue;if(s.issued&&u.Motor.ArrowOrderRevision!=s.revision){CancelSupport(u,false);continue;}
    bool inRange=s.attacker!=null&&s.attacker.OnMap&&world.IsVisible(u.ownerId,s.attacker)&&Vector2.Distance(u.transform.position,s.attacker.transform.position)<=UnitRanges.Attack(u)*.9f;
    if(inRange||Vector2.Distance(u.transform.position,s.ally.transform.position)<=.3f){if(s.issued)StopOwned(u,s.revision);continue;}
    if(s.issued&&u.Motor.IsMoving&&Vector2.Distance(s.destination,s.ally.transform.position)<.5f)continue;if(s.replan>0)continue;s.replan=1;var target=s.ally.transform.position;if(!Move(u,target,out s.revision)){CancelSupport(u,true,true);Feedback("联防支援路线不可达");continue;}s.issued=true;s.destination=target;
   }
  }
  void LateUpdate(){if(world==null||selection==null)return;foreach(var e in links){bool show=e.a!=null&&e.b!=null&&e.a.OnMap&&e.b.OnMap&&e.a.ownerId==world.currentPlayer&&(selection.Contains(e.a.GetComponent<ArmyRoadMover>())||selection.Contains(e.b.GetComponent<ArmyRoadMover>()));if(!show){if(e.line!=null)e.line.enabled=false;continue;}if(e.line==null){var g=new GameObject("联防关联线");g.transform.SetParent(transform,false);e.line=g.AddComponent<LineRenderer>();if(material==null)material=new Material(Shader.Find("Sprites/Default"));e.line.sharedMaterial=material;e.line.positionCount=2;e.line.sortingOrder=24;e.line.startColor=e.line.endColor=new Color(.15f,.42f,.58f,.45f);}e.line.enabled=true;e.line.startWidth=e.line.endWidth=2*world.setup.mapCamera.orthographicSize/Mathf.Max(1,world.setup.mapCamera.pixelHeight);e.line.SetPosition(0,e.a.transform.position);e.line.SetPosition(1,e.b.transform.position);}}
  void OnDestroy(){foreach(var e in links)if(e.line!=null)Destroy(e.line.gameObject);if(material!=null)Destroy(material);}
 }
}
