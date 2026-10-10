using UnityEngine;
namespace CampaignDemo
{
    public sealed class ArmySelection : MonoBehaviour
    {
        BattleWorld registeredWorld;
        void OnEnable(){registeredWorld=GetComponent<BattleWorld>();if(registeredWorld!=null)registeredWorld.UnitRegistered+=RefreshRegistry;}
        void OnDisable(){if(registeredWorld!=null)registeredWorld.UnitRegistered-=RefreshRegistry;}
        void RefreshRegistry(BattleUnit unit){if(registeredWorld!=null&&registeredWorld.setup!=null)armies=registeredWorld.setup.sceneArmies;}
        public Camera mapCamera;
        public void Clear() { Selected=null; }
        public void SelectOwned(ArmyRoadMover army) { var world=GetComponent<BattleWorld>();var unit=army==null?null:army.GetComponent<BattleUnit>();Selected=army!=null&&(world==null||(unit!=null&&unit.ownerId==world.currentPlayer&&unit.OnMap))?army:null; }
        public RoadMarchSettings settings;
        public ArmyRoadMover[] armies;
        public ArmyRoadMover Selected { get; private set; }
        public bool SelectAt(Vector3 mouse)
        {
            if(mapCamera==null||settings==null||armies==null) return false;
            var world=GetComponent<BattleWorld>(); ArmyRoadMover best=null; float limit=settings.selectionPixels;
            foreach(var army in armies)
            {
                if(army==null||!army.isActiveAndEnabled||!army.gameObject.activeInHierarchy) continue;
                var unit=army.GetComponent<BattleUnit>(); if(world!=null&&(unit==null||unit.ownerId!=world.currentPlayer||!unit.OnMap))continue;
                var p=mapCamera.WorldToScreenPoint(army.transform.position);
                float d=Vector2.Distance(new Vector2(p.x,p.y),new Vector2(mouse.x,mouse.y));
                if(d<limit) { limit=d; best=army; }
            }
            if(best==null) { Selected=null;return false; } Selected=best; return true;
        }
    }
}



