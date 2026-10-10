using System;
using UnityEngine;
namespace CampaignDemo
{
    public enum MouseCommandMode { None=0, RoadMarch=1, FreeMarch=2, Interaction=3 }
    public enum TacticalMapLayer { Normal=0, ArmyStatus=1, Vision=2, Attack=3, Ownership=4 }
    [DefaultExecutionOrder(-100)]
    public sealed class MouseModeStateMachine : MonoBehaviour
    {
        BattleWorld linkedWorld;
        [SerializeField] MouseCommandMode currentMode;
        public MouseCommandMode CurrentMode { get { return currentMode; } }
        public TacticalMapLayer DisplayLayer { get; private set; }
        public event Action<TacticalMapLayer> DisplayChanged;
        public event Action<MouseCommandMode,MouseCommandMode> Changed;
        public void EnterInspectionMode() { SetMode(MouseCommandMode.Interaction); }
        public void SetMode(MouseCommandMode mode)
        {
            if(linkedWorld==null)linkedWorld=GetComponent<BattleWorld>();if(linkedWorld!=null&&linkedWorld.SimulationEnded)mode=MouseCommandMode.None;
            if(currentMode==mode) return;
            var old=currentMode; currentMode=mode;
            if(Changed!=null) Changed(old,mode);
            SetDisplay(mode==MouseCommandMode.Interaction?TacticalMapLayer.ArmyStatus:TacticalMapLayer.Normal);
        }
        public void SetDisplay(TacticalMapLayer layer)
        { if(DisplayLayer==layer) return; DisplayLayer=layer; if(DisplayChanged!=null) DisplayChanged(layer); }
        void Start() { SetDisplay(currentMode==MouseCommandMode.Interaction?TacticalMapLayer.ArmyStatus:TacticalMapLayer.Normal); }
        void Update()
        {if(linkedWorld==null)linkedWorld=GetComponent<BattleWorld>();if(linkedWorld!=null&&linkedWorld.SimulationEnded)return;
            if(Input.GetKeyDown(KeyCode.Tab)&&!Input.GetKey(KeyCode.LeftAlt)&&!Input.GetKey(KeyCode.RightAlt)){var world=GetComponent<BattleWorld>();if(world!=null&&world.Initialized)world.NextPlayer();}
            if(Input.GetKeyDown(KeyCode.Alpha1)||Input.GetKeyDown(KeyCode.Keypad1)) SetMode(MouseCommandMode.RoadMarch);
            else if(Input.GetKeyDown(KeyCode.Alpha2)||Input.GetKeyDown(KeyCode.Keypad2)) SetMode(MouseCommandMode.FreeMarch);
            else if(Input.GetKeyDown(KeyCode.Alpha3)||Input.GetKeyDown(KeyCode.Keypad3))
            {
                EnterInspectionMode();
            }
        }
    }
}



