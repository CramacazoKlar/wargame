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
        public static MouseCommandMode MarchHotkey(KeyCode key){return key==KeyCode.Q?MouseCommandMode.RoadMarch:key==KeyCode.E?MouseCommandMode.FreeMarch:MouseCommandMode.None;}
        public event Action<TacticalMapLayer> DisplayChanged;
        public event Action<MouseCommandMode,MouseCommandMode> Changed;
        public void EnterInspectionMode() { SetMode(MouseCommandMode.Interaction); }
        public void SetMode(MouseCommandMode mode)
        {
            if(linkedWorld==null)linkedWorld=GetComponent<BattleWorld>();if(linkedWorld!=null&&linkedWorld.SimulationEnded)mode=MouseCommandMode.None;
            if(LocalTacticalBattle.EnabledFor(this)&&mode==MouseCommandMode.Interaction)mode=MouseCommandMode.RoadMarch;
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
            var editing=UnityEngine.EventSystems.EventSystem.current==null?null:UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject;if(editing!=null&&editing.GetComponentInParent<UnityEngine.UI.InputField>()!=null)return;
            if(Input.GetKeyDown(KeyCode.Q)) SelectMarch(MarchHotkey(KeyCode.Q));
            else if(Input.GetKeyDown(KeyCode.E)) SelectMarch(MarchHotkey(KeyCode.E));
            else if(Input.GetKeyDown(KeyCode.V))
            {
                if(!LocalTacticalBattle.EnabledFor(this))EnterInspectionMode();
            }
        }
        public void SelectMarch(MouseCommandMode mode){GetComponent<UnitContextOrders>()?.CancelSplitPreview();GetComponent<LocalPrototypeOrders>()?.CancelDraft();GetComponent<LocalCoordinatedOrders>()?.CancelPicking();SetMode(mode);GetComponent<CampaignHUD>()?.SetCommandFeedback(mode==MouseCommandMode.RoadMarch?"A* 自动寻路：点击目的地立即出发，优先道路并绕开不可通行地形":"直线行军：点击目的地立即出发");UnitContextOrders.ConsumePointer();}
    }
}



