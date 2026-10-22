using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace CampaignDemo
{
    // Road-march input only. Does not create the map, armies, camera or configuration.
    public sealed class RoadMarchController : MonoBehaviour
    {
        public Camera mapCamera;
        public float planningSensitivity=1; Vector3 strokeOrigin,strokeAnchor;
        [Min(0)] public float startDragPixels=6;
        bool strokeStarted;
        public RoadMapRuntime map;
        public RoadMarchSettings settings;
        public RoadRouteView preview;
        public ArmySelection selection;
        public MouseModeStateMachine modes;
        [Min(0)] public float topInterfacePixels=100;
        public ArmyRoadMover Selected { get { return selection==null?null:selection.Selected; } }
        public string Status { get; private set; }="Press Q, click an army and draw along roads.";
        public bool ModeEnabled { get { return modes!=null&&modes.CurrentMode==MouseCommandMode.RoadMarch; } }
        readonly List<RoadPoint> draft=new List<RoadPoint>();
        bool drawing,invalid;
        bool extended;
        RoadHit tail;
        RoadPoint lastCursor;
        ArmyRoadMover planningArmy;

        public void Initialize()
        {
            if(mapCamera==null||map==null||map.Network==null||settings==null||preview==null||selection==null||modes==null)
                throw new ArgumentException("Controller references are incomplete.");
        }
        public void BindModes(MouseModeStateMachine state)
        { if(modes!=null) modes.Changed-=ModeChanged; modes=state; if(modes!=null) modes.Changed+=ModeChanged; }
        void OnEnable() { if(modes!=null) modes.Changed+=ModeChanged; }
        void ModeChanged(MouseCommandMode oldMode,MouseCommandMode next) { if(drawing) Cancel(); }
        public void CancelDraft(){Cancel();}
        void Update()
        {if(UnitContextOrders.BlocksInput)return; if(LocalTacticalBattle.EnabledFor(this))return;
            if(mapCamera==null||map==null||map.Network==null||settings==null||preview==null||selection==null||modes==null)
            { Cancel(); Debug.LogError("RoadMarchController: required references unavailable.",this); enabled=false; return; }
            if(drawing&&(planningArmy==null||planningArmy.Motor==null||!planningArmy.isActiveAndEnabled||!planningArmy.Motor.isActiveAndEnabled||planningArmy.Motor.MovementLocked)) { Cancel(); return; }
            if(!ModeEnabled) return;
            if(selection.HasBoxSelection&&selection.SelectedArmies.Count>0){if(drawing)Cancel();return;}
            if(Input.GetMouseButton(1)) { if(drawing) Cancel(); return; }
            if(GetComponent<SelectedArmyHotkeys>()?.SuppressPointer==true)return;
            if(Input.GetKeyDown(KeyCode.Escape)) Cancel();
            var raw=Input.mousePosition; var mouse=drawing?strokeAnchor+(raw-strokeOrigin)*Mathf.Clamp(planningSensitivity,.25f,2):raw;
            bool blocked=raw.y>Screen.height-topInterfacePixels||
                (EventSystem.current!=null&&EventSystem.current.IsPointerOverGameObject());
            var world=MapProjection.Ground(mapCamera,new Vector2(mouse.x,mouse.y));
            var cursor=new RoadPoint(world.x,world.y);
            double scale=2*mapCamera.orthographicSize/Math.Max(1,Screen.height);
            double tolerance=settings.snapPixels*scale;
            if(Input.GetMouseButtonDown(0)&&!blocked)
            {
                bool clickedArmy=selection.SelectAt(mouse,true);
                if(Selected==null) { Status="Select a blue army first."; return; }
                var p=mapCamera.WorldToScreenPoint(Selected.transform.position);
                if(!clickedArmy)
                {
                    var service=GetComponent<BattleCommandService>();var unit=Selected.GetComponent<BattleUnit>();
                    if(service!=null&&unit!=null){var result=service.Submit(new BattleCommand{playerId=service.world.currentPlayer,unitId=unit.unitId,kind=BattleCommandKind.RoadDestination,destination=world});Status=result.pending?"目的地已发送，等待确认":result.accepted?"已沿道路规划路线":result.reason;if(!result.accepted&&!result.pending)GetComponent<UnitContextOrders>()?.RejectMovement(Status);}
                    return;
                }
                if(Selected.Motor==null||!Selected.isActiveAndEnabled||!Selected.Motor.isActiveAndEnabled||Selected.Motor.MovementLocked) { Status="Army movement is unavailable.";GetComponent<UnitContextOrders>()?.RejectMovement("当前部队无法移动"); return; }
                planningArmy=Selected;
                strokeOrigin=raw; strokeAnchor=p; strokeStarted=false; drawing=true; invalid=false; extended=false; planningArmy.PlanningPaused=true;
                planningArmy.RefreshRoad();
                draft.Clear(); tail=planningArmy.CurrentRoad; lastCursor=tail.Position;
                var actual=planningArmy.transform.position;
                RoadNetwork.Append(draft,new RoadPoint(actual.x,actual.y));
                RoadNetwork.Append(draft,tail.Position); preview.SetColor(settings.validRouteColor); preview.ShowArrow(draft);
                Status="Release to confirm. Esc cancels.";
            }
            if(drawing&&!Input.GetMouseButtonDown(0)&&Input.GetMouseButton(0))
            { if(blocked) Fail(); else if(!invalid) Sample(cursor,tolerance); }
            if(drawing&&Input.GetMouseButtonUp(0))
            {
                if(blocked) Fail(); else Sample(cursor,tolerance);
                if(!invalid&&extended&&draft.Count>1)
                { Status=SubmitRoute(planningArmy,draft)?"Route confirmed.":"Route blocked. Previous order retained."; }
                else Status=invalid?"Invalid stroke. Previous order retained.":"Army selected. Draw a route.";if(invalid)GetComponent<UnitContextOrders>()?.RejectMovement(Status);
                Finish();
            }
        }
        bool SubmitRoute(ArmyRoadMover mover,IList<RoadPoint> route)
        {
            var service=GetComponent<BattleCommandService>();if(service==null)return mover.CommitRoute(route);
            var unit=mover.GetComponent<BattleUnit>();return unit!=null&&service.Submit(new BattleCommand{playerId=service.world.currentPlayer,unitId=unit.unitId,kind=BattleCommandKind.RoadRoute,route=new List<RoadPoint>(route)}).accepted;
        }
        void Sample(RoadPoint cursor,double tolerance)
        {
            if(planningArmy==null||draft.Count==0) { Cancel(); return; }
            // Ignore initial hand jitter; anchor the stroke to the army.
            if(!strokeStarted)
            {
                var delta=Input.mousePosition-strokeOrigin; delta.z=0;
                if(delta.sqrMagnitude<=startDragPixels*startDragPixels) return;
                strokeStarted=true;
            }
            double d=RoadPoint.Distance(lastCursor,cursor); if(d<0.015) return;
            RoadHit hit;List<RoadPoint> connector;
            if(!map.Network.SnapConnected(tail,cursor,out hit,out connector)){Fail();return;}
            if(RoadPoint.Distance(tail.Position,hit.Position)>0.015) extended=true;
            double scale=2*mapCamera.orthographicSize/Math.Max(1,mapCamera.pixelHeight);
            double turnTolerance=Math.Min(settings.snapPixels,Math.Max(0,settings.junctionTurnTolerancePixels))*scale;
            map.Network.AppendTurn(draft,connector,tail.Edge,hit.Edge,turnTolerance);
            if(planningArmy.waterMap!=null)for(int i=1;i<draft.Count;i++)if(!planningArmy.waterMap.CanTraverse(draft[i-1],draft[i])){preview.ShowArrow(draft);Fail();return;}
            tail=hit; lastCursor=cursor; preview.SetColor(settings.validRouteColor); preview.ShowArrow(draft);
        }
        void Fail() { invalid=true; preview.SetColor(settings.invalidRouteColor); Status="该路线包含不可通行地形或缺少道路连接。"; }
        void Finish()
        { if(planningArmy!=null) planningArmy.PlanningPaused=false; planningArmy=null; drawing=false; invalid=false; draft.Clear(); if(preview!=null) preview.Clear(); }
        void Cancel() { Finish(); Status="Draft cancelled. Previous order retained."; }
        void OnDisable() { if(modes!=null) modes.Changed-=ModeChanged; Finish(); }
    }
}




