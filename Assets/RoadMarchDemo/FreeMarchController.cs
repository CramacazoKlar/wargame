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
        public string Status { get; private set; }="FREE MARCH: select an army and draw a route.";
        readonly List<RoadPoint> draft=new List<RoadPoint>();
        ArmyFreeMarchMover army;
        ArmyRouteMotor motor;
        bool drawing,invalid;
        void OnEnable() { if(modes!=null) modes.Changed+=ModeChanged; }
        public void BindModes(MouseModeStateMachine state) { if(modes!=null) modes.Changed-=ModeChanged; modes=state; if(modes!=null) modes.Changed+=ModeChanged; }
        void ModeChanged(MouseCommandMode oldMode,MouseCommandMode next) { Cancel(); }
        void OnDisable() { if(modes!=null) modes.Changed-=ModeChanged; Cancel(); }
        void Update()
        {
            if(mapCamera==null||selection==null||map==null||map.definition==null||settings==null||preview==null||modes==null)
            { Cancel(); Debug.LogError("FreeMarchController: camera, selection, terrain map, settings, preview or modes missing.",this); enabled=false; return; }
            if (modes==null||modes.CurrentMode!=MouseCommandMode.FreeMarch) return;
            if(selection.HasBoxSelection&&selection.SelectedArmies.Count>0){if(drawing)Cancel();return;}
            if(Input.GetMouseButton(1)) { if(drawing) Cancel(); return; }
            if(drawing&&(army==null||motor==null||!army.isActiveAndEnabled||!motor.isActiveAndEnabled||motor.MovementLocked)) { Cancel(); return; }
            if(Input.GetKeyDown(KeyCode.Escape)) Cancel();
            var raw=Input.mousePosition; var mouse=drawing?strokeAnchor+(raw-strokeOrigin)*Mathf.Clamp(planningSensitivity,.25f,2):raw; bool blocked=raw.y>Screen.height-topInterfacePixels||
                (EventSystem.current!=null&&EventSystem.current.IsPointerOverGameObject());
            var w=mapCamera.ScreenToWorldPoint(new Vector3(mouse.x,mouse.y,-mapCamera.transform.position.z)); var cursor=new RoadPoint(w.x,w.y);
            if(Input.GetMouseButtonDown(0)&&!blocked)
            {
                selection.SelectAt(mouse); var selected=selection.Selected;
                if(selected==null) { Status="Select an army first."; return; }
                army=selected.GetComponent<ArmyFreeMarchMover>();
                if(army==null) { Status="Army has no mode-2 component."; return; }
                var p=mapCamera.WorldToScreenPoint(selected.transform.position);
                if(Vector2.Distance(new Vector2(p.x,p.y),new Vector2(mouse.x,mouse.y))>settings.startPixels) { Status="Start near the army."; return; }
                motor=selected.Motor;
                if(motor==null||!motor.isActiveAndEnabled||!army.isActiveAndEnabled||motor.MovementLocked) { Status="Army movement is unavailable."; return; }
                strokeOrigin=raw; strokeAnchor=p; strokeStarted=false; motor.PlanningPaused=true; drawing=true; invalid=false; draft.Clear();
                var at=selected.transform.position; RoadNetwork.Append(draft,new RoadPoint(at.x,at.y));
                preview.SetColor(settings.validRouteColor); preview.ShowArrow(draft); Status="Draw freely. Forbidden terrain turns red.";
            }
            if(drawing&&Input.GetMouseButtonUp(0))
            {
                if(blocked) invalid=true; else Sample(cursor,true);
                for(int i=1;i<draft.Count&&!invalid;i++) if(!army.CanTraverse(map,draft[i-1],draft[i])) invalid=true;
                if(!invalid&&draft.Count>1) { Status=SubmitRoute(army,draft)?"Free route confirmed.":"Route rejected. Previous order retained."; }
                else Status=invalid?"Forbidden terrain: previous order retained.":"Army selected.";
                Finish();
            }
            else if(drawing&&!Input.GetMouseButtonDown(0)&&Input.GetMouseButton(0)) { if(blocked) invalid=true; else Sample(cursor,false); UpdatePreview(); }
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




