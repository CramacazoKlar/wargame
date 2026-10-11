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
        bool boxing;Vector2 boxStart,boxEnd;
        public bool HasBoxSelection {get;private set;}
        public bool IsBoxSelecting {get{return boxing;}}
        public ArmyRoadMover Selected {get{Prune();return selected.FirstOrDefault();}}
        public IReadOnlyList<ArmyRoadMover> SelectedArmies {get{Prune();return selected;}}
        void Awake(){var commands=GetComponent<GroupMarchController>();if(commands==null)commands=gameObject.AddComponent<GroupMarchController>();commands.selection=this;}
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
        public bool SelectAt(Vector3 mouse)
        {
            if(mapCamera==null||settings==null||armies==null)return false;ArmyRoadMover best=null;float limit=settings.selectionPixels;
            foreach(var army in armies.Where(Eligible)){var p=mapCamera.WorldToScreenPoint(army.transform.position);if(p.z<=0)continue;float distance=Vector2.Distance(p,mouse);if(distance<limit){limit=distance;best=army;}}
            SelectOwned(best);return best!=null;
        }
        void Update()
        {
            if(Input.GetKeyDown(KeyCode.Escape))Clear();var world=GetComponent<BattleWorld>();if(world!=null&&world.SimulationEnded){Clear();return;}
            if(Input.GetMouseButtonDown(1)&&!(EventSystem.current!=null&&EventSystem.current.IsPointerOverGameObject())){boxing=true;boxStart=boxEnd=Input.mousePosition;}
            if(boxing)boxEnd=Input.mousePosition;
            if(boxing&&Input.GetMouseButtonUp(1)){boxing=false;var min=Vector2.Min(boxStart,boxEnd);var max=Vector2.Max(boxStart,boxEnd);SelectBox(Rect.MinMaxRect(min.x,min.y,max.x,max.y));}
        }
        void OnApplicationFocus(bool focused){if(!focused)boxing=false;}
        void OnGUI()
        {
            var old=GUI.color;
            if(boxing){var min=Vector2.Min(boxStart,boxEnd);var max=Vector2.Max(boxStart,boxEnd);var rect=new Rect(min.x,Screen.height-max.y,max.x-min.x,max.y-min.y);GUI.color=new Color(.2f,.85f,1,.12f);GUI.DrawTexture(rect,Texture2D.whiteTexture);Outline(rect);}
            if(mapCamera!=null)foreach(var army in SelectedArmies){var p=mapCamera.WorldToScreenPoint(army.transform.position);if(p.z>0)Outline(new Rect(p.x-22,Screen.height-p.y-18,44,36));}
            GUI.color=old;
        }
        static void Outline(Rect r){GUI.color=new Color(.2f,.9f,1,.9f);GUI.DrawTexture(new Rect(r.x,r.y,r.width,1),Texture2D.whiteTexture);GUI.DrawTexture(new Rect(r.x,r.yMax-1,r.width,1),Texture2D.whiteTexture);GUI.DrawTexture(new Rect(r.x,r.y,1,r.height),Texture2D.whiteTexture);GUI.DrawTexture(new Rect(r.xMax-1,r.y,1,r.height),Texture2D.whiteTexture);}
    }
}
