using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
namespace CampaignDemo
{
    public sealed class GroupMarchController : MonoBehaviour
    {
        public ArmySelection selection;
        public string Status {get;private set;}
        readonly List<RoadPoint> points=new List<RoadPoint>();readonly List<ArmyRoadMover> planning=new List<ArmyRoadMover>();
        readonly Dictionary<ArmyRoadMover,Vector3> formationOffsets=new Dictionary<ArmyRoadMover,Vector3>();
        RoadRouteView preview;bool drawing;MouseCommandMode mode;int requestedCount;
        MouseModeStateMachine subscribed;
        BattleWorld World {get{return GetComponent<BattleWorld>();}}
        void OnEnable(){subscribed=GetComponent<MouseModeStateMachine>();if(subscribed!=null)subscribed.Changed+=ModeChanged;}
        void ModeChanged(MouseCommandMode oldMode,MouseCommandMode next){if(drawing&&next!=mode)Cancel();}
        public bool Begin(Vector3 point,MouseCommandMode requestedMode)
        {
            Cancel();if(selection==null||!selection.HasBoxSelection||World==null||World.SimulationEnded)return false;
            mode=requestedMode;if(mode!=MouseCommandMode.RoadMarch&&mode!=MouseCommandMode.FreeMarch)return false;
            requestedCount=selection.SelectedArmies.Count;planning.AddRange(selection.SelectedArmies.Where(a=>a.Motor!=null&&a.GetComponent<BattleUnit>().CanMove));if(planning.Count==0){Status="框选部队当前无法移动";GetComponent<UnitContextOrders>()?.RejectMovement(Status);return false;}
            // Freeze each member's offset before drawing so routes translate the formation.
            var center=Vector3.zero;foreach(var army in planning)center+=army.transform.position;center/=planning.Count;
            foreach(var army in planning){formationOffsets[army]=army.transform.position-center;army.PlanningPaused=true;}points.Add(new RoadPoint(point.x,point.y));drawing=true;Status="松开左键确认框选部队路线";ShowPreview();return true;
        }
        public void Append(Vector3 point,bool final=false){if(!drawing)return;var next=new RoadPoint(point.x,point.y);if(final||RoadPoint.Distance(points[points.Count-1],next)>.1){if(points.Count<64)points.Add(next);else if(final)points[points.Count-1]=next;ShowPreview();}}
        public int Commit()
        {
            if(!drawing)return 0;int accepted=0;var commands=GetComponent<BattleCommandService>();
            foreach(var army in planning.ToArray())
            {
                var unit=army==null?null:army.GetComponent<BattleUnit>();if(unit==null||unit.ownerId!=World.currentPlayer||!unit.CanMove)continue;
                var path=BuildPath(army,mode,points);if(path==null||path.Count<2)continue;
                var result=commands.Submit(new BattleCommand{playerId=World.currentPlayer,unitId=unit.unitId,kind=mode==MouseCommandMode.RoadMarch?BattleCommandKind.RoadRoute:BattleCommandKind.FreeRoute,route=path});if(result.accepted)accepted++;
            }
            int total=requestedCount;Cancel();Status="已向 "+accepted+" / "+total+" 支部队下达移动命令";if(accepted<total)GetComponent<UnitContextOrders>()?.RejectMovement(Status+"，部分路线不可通行");return accepted;
        }
        public List<RoadPoint> BuildPath(ArmyRoadMover army,MouseCommandMode requestedMode,IList<RoadPoint> cursorPoints)
        {
            if(World==null||cursorPoints==null||cursorPoints.Count==0)return null;
            var at=army.transform.position;Vector3 offset;
            if(!formationOffsets.TryGetValue(army,out offset))
            {
                // BuildPath may also be called outside an active mouse stroke.
                var members=selection==null?new List<ArmyRoadMover>():selection.SelectedArmies.Where(a=>a!=null&&a.Motor!=null&&a.GetComponent<BattleUnit>()!=null&&a.GetComponent<BattleUnit>().CanMove).ToList();
                if(members.Count==0||!members.Contains(army))return null;
                var center=Vector3.zero;foreach(var member in members)center+=member.transform.position;center/=members.Count;offset=at-center;
            }
            var destinations=new List<RoadPoint>();foreach(var point in cursorPoints)destinations.Add(new RoadPoint(point.X+offset.x,point.Y+offset.y));
            var result=new List<RoadPoint>{new RoadPoint(at.x,at.y)};
            if(requestedMode==MouseCommandMode.RoadMarch)
            {
                var network=World.setup.roadMap.Network;var hits=network.Candidates(result[0],double.PositiveInfinity);if(hits.Count==0)return null;var tail=hits[0];RoadNetwork.Append(result,tail.Position);
                foreach(var point in destinations){RoadHit next;List<RoadPoint> connector;if(!network.SnapConnected(tail,point,out next,out connector))return null;foreach(var step in connector)RoadNetwork.Append(result,step);tail=next;}
                for(int i=1;i<result.Count;i++)if(!army.CanTraverse(result[i-1],result[i]))return null;
            }
            else
            {
                var free=army.GetComponent<ArmyFreeMarchMover>();if(free==null)return null;
                var target=destinations[destinations.Count-1];if(!free.CanTraverse(World.setup.freeMarchMap,result[0],target))return null;RoadNetwork.Append(result,target);
            }
            return result;
        }
        void ShowPreview()
        {
            if(preview==null){preview=RoadRouteView.Create(transform,"Group preview",.08f,4,World.setup.settings.validRouteColor);preview.ConfigureArrow(World.setup.settings,selection.mapCamera,false);}
            var path=BuildPath(planning[0],mode,points);if(path!=null)preview.ShowArrow(path);else preview.Clear();
        }
        public void Cancel(){foreach(var army in planning)if(army!=null)army.PlanningPaused=false;planning.Clear();formationOffsets.Clear();points.Clear();drawing=false;if(preview!=null)preview.Clear();Status="路线草稿已取消";}
        void Update()
        {if(UnitContextOrders.BlocksInput)return; if(LocalTacticalBattle.EnabledFor(this))return;
            if(selection==null)selection=GetComponent<ArmySelection>();var world=World;if(world==null||!world.Initialized||selection==null)return;var current=world.setup.modes.CurrentMode;
            if(drawing&&(!selection.HasBoxSelection||current!=mode||selection.IsBoxSelecting||Input.GetMouseButton(1)||world.SimulationEnded)){Cancel();return;}
            if(!selection.HasBoxSelection||selection.SelectedArmies.Count==0||world.SimulationEnded)return;
            if(current!=MouseCommandMode.RoadMarch&&current!=MouseCommandMode.FreeMarch)return;
            bool ui=EventSystem.current!=null&&EventSystem.current.IsPointerOverGameObject();var mouse=Input.mousePosition;var cursor=MapProjection.Ground(selection.mapCamera,new Vector2(mouse.x,mouse.y));cursor.z=0;
            if(GetComponent<SelectedArmyHotkeys>()?.SuppressPointer==true)return;
            if(current==MouseCommandMode.FreeMarch){if(Input.GetMouseButtonDown(0)&&!ui&&Begin(cursor,current))Commit();return;}
            if(Input.GetMouseButtonDown(0)&&!ui)Begin(cursor,current);
            if(drawing&&Input.GetMouseButton(0)&&!ui)Append(cursor);
            if(drawing&&Input.GetMouseButtonUp(0)){if(ui)Cancel();else{Append(cursor,true);Commit();}}
        }
        void OnDisable(){if(subscribed!=null)subscribed.Changed-=ModeChanged;Cancel();}void OnDestroy(){if(preview!=null)Destroy(preview.gameObject);}
    }
}

