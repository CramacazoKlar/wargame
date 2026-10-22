using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
namespace CampaignDemo
{
    public sealed class FreeMarchController : MonoBehaviour
    {
        public Camera mapCamera;
        public float planningSensitivity=1; Vector3 strokeOrigin,strokeAnchor;
        [Min(0)] public float startDragPixels=6;
        bool strokeStarted;
        public MouseModeStateMachine modes;
        public ArmySelection selection;
        public FreeMarchMapRuntime map;
        public RoadMarchSettings settings;
        public RoadRouteView preview;
        public float topInterfacePixels=100;
        [Min(0.001f)] public float minimumPointDistance=0.03f;
        public string Status { get; private set; }="E 直线行军：选择部队后点击目的地。";
        readonly List<RoadPoint> draft=new List<RoadPoint>();
        ArmyFreeMarchMover army;
        ArmyRouteMotor motor;
        bool drawing,invalid;
        void OnEnable() { if(modes!=null) modes.Changed+=ModeChanged; }
        public void BindModes(MouseModeStateMachine state) { if(modes!=null) modes.Changed-=ModeChanged; modes=state; if(modes!=null) modes.Changed+=ModeChanged; }
        void ModeChanged(MouseCommandMode oldMode,MouseCommandMode next) { Cancel(); }
        void OnDisable() { if(modes!=null) modes.Changed-=ModeChanged; Cancel(); }
        public void CancelDraft(){Cancel();}
        public bool MoveStraight(Vector3 destination)
        {
            var selected=selection==null?null:selection.Selected;var unit=selected==null?null:selected.GetComponent<BattleUnit>();var service=GetComponent<BattleCommandService>();if(unit==null||service==null){Status="先选择己方部队";return false;}
            var result=service.Submit(new BattleCommand{playerId=service.world.currentPlayer,unitId=unit.unitId,kind=BattleCommandKind.StraightDestination,destination=destination});Status=result.pending?"直线命令已发送":result.accepted?"已下达直线行军":result.reason;if(!result.accepted&&!result.pending)GetComponent<UnitContextOrders>()?.RejectMovement(Status);return result.accepted||result.pending;
        }
        void Update()
        {
            if(UnitContextOrders.BlocksInput||LocalTacticalBattle.EnabledFor(this)||modes==null||modes.CurrentMode!=MouseCommandMode.FreeMarch||selection==null||mapCamera==null)return;
            if(selection.HasBoxSelection&&selection.SelectedArmies.Count>0)return;
            if(GetComponent<SelectedArmyHotkeys>()?.SuppressPointer==true)return;
            if(!Input.GetMouseButtonDown(0)||Input.GetMouseButton(1)||EventSystem.current!=null&&EventSystem.current.IsPointerOverGameObject())return;
            if(selection.SelectAt(Input.mousePosition,true)){Status="已选择部队；点击目的地下达直线行军";return;}
            var target=MapProjection.Ground(mapCamera,new Vector2(Input.mousePosition.x,Input.mousePosition.y));target.z=0;MoveStraight(target);
        }
        bool SubmitRoute(ArmyFreeMarchMover mover,IList<RoadPoint> route)
        {
            var service=GetComponent<BattleCommandService>();if(service==null)return mover.CommitRoute(route,map);
            var unit=mover.GetComponent<BattleUnit>();return unit!=null&&service.Submit(new BattleCommand{playerId=service.world.currentPlayer,unitId=unit.unitId,kind=BattleCommandKind.FreeRoute,route=new List<RoadPoint>(route)}).accepted;
        }
        void Sample(RoadPoint cursor,bool final)
        {
            if(army==null||motor==null||draft.Count==0) { Cancel(); return; }
            // Ignore initial hand jitter; anchor the stroke to the army.
            if(!strokeStarted)
            {
                var delta=Input.mousePosition-strokeOrigin; delta.z=0;
                if(delta.sqrMagnitude<=startDragPixels*startDragPixels) return;
                strokeStarted=true;
            }
            var last=draft[draft.Count-1]; double d=RoadPoint.Distance(last,cursor);
            if(d<0.00001||(!final&&d<minimumPointDistance)) return;
            if(!army.CanTraverse(map,last,cursor)) invalid=true;
            RoadNetwork.Append(draft,cursor);
        }

        void UpdatePreview() { preview.SetColor(invalid?settings.invalidRouteColor:settings.validRouteColor); preview.ShowArrow(draft); if(invalid) Status="Forbidden terrain. Release and redraw."; }
        void Finish() { if(motor!=null) motor.PlanningPaused=false; drawing=false; invalid=false; draft.Clear(); if(preview!=null) preview.Clear(); army=null; motor=null; }
        void Cancel() { Finish(); Status="Draft cancelled. Previous order retained."; }
    }
}




