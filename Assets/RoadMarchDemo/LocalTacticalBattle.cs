using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
namespace CampaignDemo {
 public enum LocalMarchStyle { Cautious,Assault,Forced }
 // This controller is deliberately confined to the shared tactical map scenes.
 [DefaultExecutionOrder(-40)] public sealed class LocalTacticalBattle:MonoBehaviour {
  public static bool EnabledFor(Component c){return c!=null&&(c.gameObject.scene.name=="TacticalSandbox"||c.gameObject.scene.name=="DoubleValley9x");}
  public BattleWorld world;public LocalMarchStyle style=LocalMarchStyle.Assault;
  public string Status {get;private set;}="选择部队后点击目的地";
  ArmySelection selection;CampaignHUD hud;readonly List<RoadPoint> stroke=new List<RoadPoint>();Vector3 down;bool drawing;RoadRouteView preview;
  void Start(){world=GetComponent<BattleWorld>();world.setup.modes.SetMode(MouseCommandMode.RoadMarch);}
  bool Editing(){var g=EventSystem.current==null?null:EventSystem.current.currentSelectedGameObject;return g!=null&&g.GetComponent<InputField>()!=null;}
  public void SetOrder(LocalMarchStyle value){style=value;if(selection==null)return;foreach(var a in selection.SelectedArmies){var unit=a.GetComponent<BattleUnit>();if(NetworkTacticalOrders.Forward(world,unit,"march",out var forwarded,style:(int)value))continue;var order=a.GetComponent<LocalMarchOrder>();if(order==null)order=a.gameObject.AddComponent<LocalMarchOrder>();order.style=value;order.world=world;}Status=value==LocalMarchStyle.Cautious?(LocalPrototypeOrders.EnabledFor(this)?"徐进：以步兵速度为上限，较慢单位保持原速":"徐进：慢速，交战中仍可移动"):value==LocalMarchStyle.Assault?"突击：快速，交战中仍可移动":"强行军：快速，遇敌继续前进";}
  void Cancel(){drawing=false;stroke.Clear();if(preview!=null)preview.Clear();}
  public void CancelPointerDraft(){Cancel();}
  void Update(){
   if(selection==null)selection=GetComponent<ArmySelection>();if(hud==null)hud=FindObjectOfType<CampaignHUD>();
   if(world==null||!world.Initialized||world.SimulationEnded||hud==null||selection==null)return;
   if(LocalPvpSession.Simulate)TickResources(Time.deltaTime);if(UnitContextOrders.BlocksInput)return;
   if(!Editing()){if(Input.GetKeyDown(KeyCode.Z))SetOrder(LocalMarchStyle.Cautious);if(Input.GetKeyDown(KeyCode.X))SetOrder(LocalMarchStyle.Assault);if(Input.GetKeyDown(KeyCode.C))SetOrder(LocalMarchStyle.Forced);}
   if(Input.GetKeyDown(KeyCode.Escape)){Cancel();return;}
   bool ui=(EventSystem.current!=null&&EventSystem.current.IsPointerOverGameObject())||LocalMapSettings.CapturesPointer(world);
   if(hud.ActiveTool!=CampaignHUD.Tool.None){Cancel();return;}
   if(Input.GetMouseButtonDown(0)&&!ui&&(Input.GetKey(KeyCode.LeftShift)||Input.GetKey(KeyCode.RightShift))){Assist(MouseWorld());Cancel();return;}
   if(world.setup.modes.CurrentMode==MouseCommandMode.FreeMarch){if(Input.GetMouseButtonDown(0)&&!ui){if(!hud.Pick(Input.mousePosition))Move(MouseWorld(),null);Cancel();}return;}
   if(Input.GetMouseButtonDown(0)&&!ui){down=Input.mousePosition;if(hud.Pick(down)){Cancel();if(world.setup.modes.CurrentMode==MouseCommandMode.FreeMarch&&selection.Selected!=null){drawing=true;var at=selection.Selected.transform.position;stroke.Add(new RoadPoint(at.x,at.y));}return;}if(selection.SelectedArmies.Count==0)return;
    if(world.setup.modes.CurrentMode==MouseCommandMode.RoadMarch){Move(MouseWorld(),null);return;}
    drawing=true;stroke.Clear();var a=selection.Selected.transform.position;stroke.Add(new RoadPoint(a.x,a.y));var p=MouseWorld();RoadNetwork.Append(stroke,new RoadPoint(p.x,p.y));
   }
   if(drawing&&Input.GetMouseButton(0)&&!ui){var p=MouseWorld();if(RoadPoint.Distance(stroke[stroke.Count-1],new RoadPoint(p.x,p.y))>.06&&stroke.Count<400)stroke.Add(new RoadPoint(p.x,p.y));if(preview==null){preview=RoadRouteView.Create(transform,"手动路线",.045f,24,Color.cyan);preview.ConfigureArrow(world.setup.settings,world.setup.mapCamera,false);}preview.ShowArrow(stroke);}
   if(drawing&&Input.GetMouseButtonUp(0)){if(!ui&&(Input.mousePosition-down).sqrMagnitude>36){var p=MouseWorld();RoadNetwork.Append(stroke,new RoadPoint(p.x,p.y));Move(p,new List<RoadPoint>(stroke));}Cancel();}
  }
  public void TickResources(float dt){if(LocalPrototypeOrders.EnabledFor(this)){GetComponent<LocalPrototypeOrders>()?.Tick(dt);return;}foreach(var u in world.units){if(u==null||!u.Alive)continue;var truck=u.GetComponent<TransportTruck>();if(truck==null)continue;if(!u.OnMap){if(u.Point!=null&&u.Point.ownerId==u.ownerId&&truck.Cargo>0){float amount=Mathf.Min(truck.Cargo,dt*ResourceEconomy.UnloadPerSecond);u.Point.resourceStock+=amount;truck.SetCargo(truck.Cargo-amount);}continue;}if(u.Motor!=null&&u.Motor.IsMoving)continue;foreach(var city in world.points)if(city!=null&&city.ownerId==u.ownerId&&Vector2.Distance(u.transform.position,city.transform.position)<=UnitRanges.Interaction(u)&&truck.Cargo>0){float amount=Mathf.Min(truck.Cargo,dt*ResourceEconomy.UnloadPerSecond);city.resourceStock+=amount;truck.SetCargo(truck.Cargo-amount);}foreach(var site in world.campaign.logistics.sites)if(site!=null&&site.Area.Contains(u.transform.position)&&site.stock>0){float amount=Mathf.Max(0,Mathf.Min(site.stock,Mathf.Min(truck.Capacity-truck.Cargo,dt*ResourceEconomy.LoadPerSecond)));site.stock-=amount;truck.SetCargo(truck.Cargo+amount);}}}
  Vector3 MouseWorld(){var c=world.setup.mapCamera;var p=MapProjection.Ground(c,new Vector2(Input.mousePosition.x,Input.mousePosition.y));p.z=0;return p;}
  public int Move(Vector3 destination,List<RoadPoint> manual){int accepted=0;var service=world.GetComponent<BattleCommandService>();var members=new List<ArmyRoadMover>(selection.SelectedArmies);var center=Vector3.zero;foreach(var a in members)center+=a.transform.position;if(members.Count==0)return 0;center/=members.Count;
   foreach(var army in members){var u=army.GetComponent<BattleUnit>();if(u==null)continue;if(world.campaign.lineWorks.Working(u)){world.campaign.lineWorks.Cancel(u);Status="已取消该工兵施工；请再次点击下达移动";continue;}if(!u.CanMove)continue;
    var free=army.GetComponent<ArmyFreeMarchMover>();var target=destination+(members.Count>1?army.transform.position-center:Vector3.zero);List<RoadPoint> route;
    if(world.setup.modes.CurrentMode==MouseCommandMode.FreeMarch){route=new List<RoadPoint>{new RoadPoint(army.transform.position.x,army.transform.position.y),new RoadPoint(target.x,target.y)};if(!free.CanTraverse(world.setup.freeMarchMap,route[0],route[1])){Status="直线穿过不可通行地形";world.GetComponent<UnitContextOrders>()?.RejectMovement(Status);continue;}}
    else if(manual==null)route=LocalRoadAStar.Plan(world.setup.freeMarchMap,free.AllowedTerrain,army.transform.position,target,.2f,100000);
    else {route=new List<RoadPoint>{new RoadPoint(army.transform.position.x,army.transform.position.y)};var offset=army.transform.position-new Vector3((float)manual[0].X,(float)manual[0].Y);for(int i=1;i<manual.Count;i++)RoadNetwork.Append(route,new RoadPoint(manual[i].X+offset.x,manual[i].Y+offset.y));}
    if(route==null){Status="目的地不可达";world.GetComponent<UnitContextOrders>()?.RejectMovement(Status);continue;}var result=service.Submit(new BattleCommand{kind=BattleCommandKind.FreeRoute,playerId=world.currentPlayer,unitId=u.unitId,route=route});if(result.accepted){var order=u.GetComponent<LocalMarchOrder>();if(order==null)order=u.gameObject.AddComponent<LocalMarchOrder>();order.style=style;order.world=world;accepted++;}else{Status=result.reason;world.GetComponent<UnitContextOrders>()?.RejectMovement(Status);}
   }if(accepted>0)Status="已向 "+accepted+" 支部队下达"+(world.setup.modes.CurrentMode==MouseCommandMode.FreeMarch?"直线行军":manual==null?"优先道路的 A* 路线":"手动路线");if(accepted==0)world.GetComponent<UnitContextOrders>()?.RejectMovement(Status??"当前无法移动");return accepted;
  }
  void Assist(Vector3 point){foreach(var a in selection.SelectedArmies){var u=a.GetComponent<BattleUnit>();if(u==null||u.combatDefinition.role!=UnitRole.Engineer)continue;if(NetworkTacticalOrders.Forward(world,u,"assist",out var forwarded,destination:point))continue;var result=world.campaign.lineWorks.Assist(u,point);Status=result.reason??(result.accepted?"工兵已加入施工":"附近没有施工任务");}}
 }
 public sealed class LocalMarchOrder:MonoBehaviour {
  public BattleWorld world;public LocalMarchStyle style;
  public float SpeedFactor {get{return style==LocalMarchStyle.Cautious?.45f:1;}}
  ArmyDefinition infantryMovement;MapReferenceHUD distanceReference;
  // Cap final terrain-adjusted speed; never accelerate a slower unit.
  public float ApplySpeed(float normalSpeed,RoadPoint from,RoadPoint to,MouseCommandMode mode,float movementMultiplier=1){
   if(!LocalPrototypeOrders.EnabledFor(this))return normalSpeed*SpeedFactor;
   if(style!=LocalMarchStyle.Cautious)return normalSpeed;
   if(world==null)world=GetComponent<BattleUnit>()?.Campaign?.world;
   if(infantryMovement==null&&world!=null){foreach(var u in world.units){if(u==null||u.combatDefinition==null||u.combatDefinition.role!=UnitRole.Infantry)continue;var mover=u.GetComponent<ArmyRoadMover>();if(mover!=null&&mover.definition!=null){infantryMovement=mover.definition;break;}}}
   if(infantryMovement==null)return normalSpeed;
   if(distanceReference==null&&world!=null)distanceReference=world.GetComponent<MapReferenceHUD>();
   float road=infantryMovement.useReferenceTravelTime?ArmyDefinition.ReferenceRoadSpeed(infantryMovement.referenceDistanceMeters,infantryMovement.referenceTravelMinutes,distanceReference==null?1000:distanceReference.metersPerWorldUnit,infantryMovement.roadSpeedRatio):.5f*Mathf.Max(.01f,infantryMovement.speed);
   var map=world==null||world.setup==null?null:world.setup.freeMarchMap;var terrain=map==null?TerrainKind.Road:map.TerrainAt(RoadPoint.Lerp(from,to,.5));var free=infantryMovement.freeMarchDefinition;
   float cap=terrain==TerrainKind.Road||free==null?road:TerrainSpeedRules.Calculate(road,mode==MouseCommandMode.FreeMarch?free.mode2ReductionPercent:0,TerrainSpeedRules.TerrainReduction(terrain,free.terrainReductions));
   return Mathf.Min(normalSpeed,cap*Mathf.Max(0,movementMultiplier));
  }
  public bool StopForContact(){return false;}
 }
 public static class LocalRoadAStar {
  // Merge grid stair steps inside the original route corridor; every shortcut must be traversable.
  public static List<RoadPoint> Straighten(FreeMarchMapRuntime map,TerrainAccess access,IList<RoadPoint> route,float tolerance){
   if(route==null||route.Count<3)return route==null?null:new List<RoadPoint>(route);
   var keep=new bool[route.Count];keep[0]=keep[route.Count-1]=true;var work=new Stack<int>();work.Push(0);work.Push(route.Count-1);double limit=tolerance*tolerance;
   while(work.Count>0){int end=work.Pop(),start=work.Pop();if(end<=start+1)continue;var a=route[start];var b=route[end];double dx=b.X-a.X,dy=b.Y-a.Y,length=dx*dx+dy*dy,max=-1;int split=(start+end)/2;
    for(int i=start+1;i<end;i++){var p=route[i];double t=length>1e-12?Math.Max(0,Math.Min(1,((p.X-a.X)*dx+(p.Y-a.Y)*dy)/length)):0;double x=p.X-a.X-dx*t,y=p.Y-a.Y-dy*t,dist=x*x+y*y;if(dist>max){max=dist;split=i;}}
    if(max<=limit&&map.CanTraverse(a,b,access))continue;keep[split]=true;work.Push(start);work.Push(split);work.Push(split);work.Push(end);
   }
   var result=new List<RoadPoint>();for(int i=0;i<route.Count;i++)if(keep[i])result.Add(route[i]);return result;
  }
  struct Node:IComparable<Node>{public int id;public float score;public long seq;public int CompareTo(Node b){int c=score.CompareTo(b.score);return c==0?seq.CompareTo(b.seq):c;}}
  public static List<RoadPoint> Plan(FreeMarchMapRuntime map,TerrainAccess access,Vector3 from,Vector3 to,float step,int limit){
   if(map==null)return null;var a=new RoadPoint(from.x,from.y);var b=new RoadPoint(to.x,to.y);if(!map.CanTraverse(a,a,access)||!map.CanTraverse(b,b,access))return null;
   var bounds=map.definition.bounds;var min=map.transform.TransformPoint(new Vector3(bounds.xMin,bounds.yMin));var max=map.transform.TransformPoint(new Vector3(bounds.xMax,bounds.yMax));int cols=Mathf.CeilToInt((max.x-min.x)/step),rows=Mathf.CeilToInt((max.y-min.y)/step);
   Func<Vector3,int> index=p=>Mathf.Clamp((int)((p.y-min.y)/step),0,rows-1)*cols+Mathf.Clamp((int)((p.x-min.x)/step),0,cols-1);int start=index(from),goal=index(to);if(start==goal)return map.CanTraverse(a,b,access)?new List<RoadPoint>{a,b}:null;
   int gridCount=cols*rows;var navigation=LocalRoadNavigationCache.For(map,access,step,cols,rows,index);var anchors=navigation.anchors;Func<int,RoadPoint> point=i=>i>=gridCount?anchors.points[i-gridCount]:i==start?a:i==goal?b:new RoadPoint(min.x+(i%cols+.5f)*step,min.y+(i/cols+.5f)*step);
   var open=new SortedSet<Node>();var cost=new Dictionary<int,float>{{start,0}};var parent=new Dictionary<int,int>();var closed=new HashSet<int>();long seq=0;open.Add(new Node{id=start,score=0,seq=seq++});
   while(open.Count>0&&closed.Count<limit){var n=open.Min;open.Remove(n);if(!closed.Add(n.id))continue;if(n.id==goal){var path=new List<RoadPoint>();for(int i=goal;;i=parent[i]){path.Add(point(i));if(i==start)break;}path.Reverse();var result=new List<RoadPoint>{path[0]};for(int i=1;i<path.Count-1;i++){var u=path[i-1];var v=path[i];var w=path[i+1];if(Math.Abs((v.X-u.X)*(w.Y-v.Y)-(v.Y-u.Y)*(w.X-v.X))>1e-6)result.Add(v);}result.Add(path[path.Count-1]);return Straighten(map,access,result,step*1.05f);}
    void Visit(int id){if(closed.Contains(id))return;var p=point(n.id);var q=point(id);bool stable=n.id!=start&&n.id!=goal&&id!=start&&id!=goal;LocalRoadNavigationCache.Edge edge;if(!stable||!navigation.TryGet(n.id,id,out edge)){bool allowed=map.CanTraverse(p,q,access);var terrain=allowed?map.TerrainAt(RoadPoint.Lerp(p,q,.5)):TerrainKind.Blocked;edge=new LocalRoadNavigationCache.Edge{allowed=allowed,weight=terrain==TerrainKind.Road?.28f:terrain==TerrainKind.Mountain?1.8f:terrain==TerrainKind.Forest?1.4f:1};if(stable)navigation.Store(n.id,id,edge);}if(!edge.allowed)return;float weight=edge.weight;float next=cost[n.id]+(float)RoadPoint.Distance(p,q)*weight;if(cost.TryGetValue(id,out var prior)&&prior<=next)return;cost[id]=next;parent[id]=n.id;open.Add(new Node{id=id,score=next+(float)RoadPoint.Distance(q,b)*.28f,seq=seq++});}if(n.id<gridCount){int x=n.id%cols,y=n.id/cols;for(int dy=-1;dy<=1;dy++)for(int dx=-1;dx<=1;dx++){if(dx==0&&dy==0)continue;int nx=x+dx,ny=y+dy;if(nx>=0&&ny>=0&&nx<cols&&ny<rows)Visit(ny*cols+nx);}}if(anchors.links.TryGetValue(n.id,out var neighbours))foreach(int id in neighbours)Visit(id);
   }return null;
  }
 }
}





