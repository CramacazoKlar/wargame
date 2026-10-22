using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
namespace CampaignDemo
{
    [DefaultExecutionOrder(-60)]
    public sealed class ArmySelection : MonoBehaviour
    {
        BattleWorld registeredWorld;
        public Camera mapCamera;public RoadMarchSettings settings;public ArmyRoadMover[] armies;
        readonly List<ArmyRoadMover> selected=new List<ArmyRoadMover>();
        bool boxing,boxDragged;Vector2 boxStart,boxEnd;
        public bool HasBoxSelection {get;private set;}
        public bool IsBoxSelecting {get{return boxing;}}
        public ArmyRoadMover Selected {get{Prune();return selected.FirstOrDefault();}}
        public IReadOnlyList<ArmyRoadMover> SelectedArmies {get{Prune();return selected;}}
        void Awake(){if(GetComponent<SelectedArmyHotkeys>()==null)gameObject.AddComponent<SelectedArmyHotkeys>();var commands=GetComponent<GroupMarchController>();if(commands==null)commands=gameObject.AddComponent<GroupMarchController>();commands.selection=this;}
        void OnEnable(){registeredWorld=GetComponent<BattleWorld>();if(registeredWorld!=null)registeredWorld.UnitRegistered+=RefreshRegistry;}
        void OnDisable(){if(registeredWorld!=null)registeredWorld.UnitRegistered-=RefreshRegistry;boxing=false;}
        void RefreshRegistry(BattleUnit unit){if(registeredWorld!=null&&registeredWorld.setup!=null)armies=registeredWorld.setup.sceneArmies;}
        bool Eligible(ArmyRoadMover army)
        {
            if(army==null||!army.isActiveAndEnabled||!army.gameObject.activeInHierarchy)return false;
            var world=GetComponent<BattleWorld>();var unit=army.GetComponent<BattleUnit>();
            return world==null||(unit!=null&&unit.ownerId==world.currentPlayer&&unit.OnMap&&!world.SimulationEnded);
        }
        void Prune(){selected.RemoveAll(a=>!Eligible(a));}
        public bool Contains(ArmyRoadMover army){return Eligible(army)&&selected.Contains(army);}
        public void Clear(){GetComponent<GroupMarchController>()?.Cancel();selected.Clear();HasBoxSelection=false;boxing=false;}
        public void SelectOwned(ArmyRoadMover army){Clear();if(Eligible(army))selected.Add(army);}
        public int SelectBox(Rect screenRect)
        {
            selected.Clear();HasBoxSelection=true;if(mapCamera==null||armies==null)return 0;
            foreach(var army in armies.Where(Eligible).Distinct()){var point=mapCamera.WorldToScreenPoint(army.transform.position);if(point.z>0&&mapCamera.pixelRect.Contains(point)&&screenRect.Contains(point))selected.Add(army);}
            selected.Sort((a,b)=>string.CompareOrdinal(a.GetComponent<BattleUnit>()?.unitId,b.GetComponent<BattleUnit>()?.unitId));return selected.Count;
        }
        public bool SelectAt(Vector3 mouse,bool preserveOnMiss=false)
        {
            if(mapCamera==null||settings==null||armies==null)return false;ArmyRoadMover best=null;float limit=settings.selectionPixels;
            foreach(var army in armies.Where(Eligible)){var p=mapCamera.WorldToScreenPoint(army.transform.position);if(p.z<=0)continue;float distance=Vector2.Distance(p,mouse);if(distance<limit){limit=distance;best=army;}}
            if(best!=null||!preserveOnMiss)SelectOwned(best);return best!=null;
        }
        public void CompleteRightGesture(Vector2 start,Vector2 end,bool dragged=false)
        {
            boxing=false;var hud=FindObjectOfType<CampaignHUD>();
            if(dragged||(end-start).sqrMagnitude>=36){var min=Vector2.Min(start,end);var max=Vector2.Max(start,end);SelectBox(Rect.MinMaxRect(min.x,min.y,max.x,max.y));if(Selected!=null)hud?.SyncFromArmySelection();else hud?.ClearSelection();return;}
            bool detachment=selected.Any(a=>a!=null&&a.GetComponent<TacticalDetachment>()!=null)||(hud!=null&&hud.SelectedUnit!=null&&hud.SelectedUnit.GetComponent<TacticalDetachment>()!=null);
            GetComponent<RoadMarchController>()?.CancelDraft();GetComponent<FreeMarchController>()?.CancelDraft();GetComponent<LocalTacticalBattle>()?.CancelPointerDraft();
            if(hud!=null)hud.ClearSelection();else Clear();
            var world=GetComponent<BattleWorld>();if(detachment&&world!=null)world.setup.modes.SetMode(MouseCommandMode.FreeMarch);
        }
        void Update()
        {if(UnitContextOrders.BlocksInput){boxing=false;return;}
            if(Input.GetKeyDown(KeyCode.Escape))Clear();var world=GetComponent<BattleWorld>();if(world!=null&&world.SimulationEnded){Clear();return;}
            if(Input.GetMouseButtonDown(1)&&!(EventSystem.current!=null&&EventSystem.current.IsPointerOverGameObject())){boxing=true;boxDragged=false;boxStart=boxEnd=Input.mousePosition;}
            if(boxing){boxEnd=Input.mousePosition;if((boxEnd-boxStart).sqrMagnitude>=36)boxDragged=true;}
            if(boxing&&Input.GetMouseButtonUp(1))CompleteRightGesture(boxStart,boxEnd,boxDragged);
        }
        void OnApplicationFocus(bool focused){if(!focused)boxing=false;}
        void OnGUI()
        {
            var old=GUI.color;
            if(boxing){var min=Vector2.Min(boxStart,boxEnd);var max=Vector2.Max(boxStart,boxEnd);var rect=new Rect(min.x,Screen.height-max.y,max.x-min.x,max.y-min.y);GUI.color=new Color(StaffMapStyle.Blue.r,StaffMapStyle.Blue.g,StaffMapStyle.Blue.b,.12f);GUI.DrawTexture(rect,Texture2D.whiteTexture);Outline(rect);}
            if(mapCamera!=null)foreach(var army in SelectedArmies){var p=mapCamera.WorldToScreenPoint(army.transform.position);if(p.z>0)Outline(new Rect(p.x-22,Screen.height-p.y-18,44,36));}
            GUI.color=old;
        }
        static void Outline(Rect r){GUI.color=StaffMapStyle.Blue;GUI.DrawTexture(new Rect(r.x,r.y,r.width,1),Texture2D.whiteTexture);GUI.DrawTexture(new Rect(r.x,r.yMax-1,r.width,1),Texture2D.whiteTexture);GUI.DrawTexture(new Rect(r.x,r.y,1,r.height),Texture2D.whiteTexture);GUI.DrawTexture(new Rect(r.xMax-1,r.y,1,r.height),Texture2D.whiteTexture);}
    }
}
