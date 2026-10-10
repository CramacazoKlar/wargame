using UnityEngine;

namespace CampaignDemo
{
    // Replace or remove this without changing input, movement or map data.
    public sealed class RoadMarchDebugUI : MonoBehaviour
    {
        public RoadMarchController controller;
        public FreeMarchController freeController;
        public MouseModeStateMachine modes;
        void OnGUI()
        {
            if(controller==null) return;
            var mode=modes==null?MouseCommandMode.None:modes.CurrentMode;
            GUI.Box(new Rect(10,10,Mathf.Max(320,Screen.width-20),80),"MARCH DEMO | 1 Road | 2 Free | 3 Interaction");
            GUI.Label(new Rect(25,35,Screen.width-50,25),mode==MouseCommandMode.FreeMarch?(freeController==null?"Mode 2 unavailable.":freeController.Status):
                mode==MouseCommandMode.Interaction?"Interaction mode selected (reserved).":controller.Status);
            var unit=controller.Selected;
            var free=unit==null?null:unit.GetComponent<ArmyFreeMarchMover>();
            float speed=unit==null?0:mode==MouseCommandMode.FreeMarch&&free!=null?free.Speed:unit.Speed;
            GUI.Label(new Rect(25,60,Screen.width-50,25),"Selected: "+(unit==null?"none":unit.name)+
                (unit==null?"":" | Speed: "+speed.ToString("0.0")+" | "+(unit.IsMoving?"Marching":"Idle")));
        }
    }
}
