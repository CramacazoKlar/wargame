using UnityEngine;
using UnityEngine.UI;
namespace CampaignDemo
{
    [DefaultExecutionOrder(270)]
    public sealed class BattleOverviewHUD : MonoBehaviour
    {
        public BattleWorld world;
        public Font font;
        public RectTransform OverviewPanel {get;private set;}
        Text statistics,controls;
        public string DisplayedText {get{return statistics==null?"":statistics.text;}}
        public Text StatisticsText {get{return statistics;}}
        void Start()
        {
            if(world==null){var hud=GetComponent<CampaignHUD>();if(hud!=null&&hud.campaign!=null)world=hud.campaign.world;}
            if(world==null){enabled=false;return;}
            BattleServices.Ensure(world);
            if(font==null)font=Font.CreateDynamicFontFromOSFont(new[]{"Microsoft YaHei","SimHei","Arial"},16);
            if(font==null)font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var panel=new GameObject("Army overview",typeof(RectTransform),typeof(Image));panel.transform.SetParent(transform,false);
            panel.GetComponent<Image>().color=new Color(.17f,.13f,.08f,.96f);
            OverviewPanel=panel.GetComponent<RectTransform>();OverviewPanel.anchorMin=Vector2.zero;OverviewPanel.anchorMax=new Vector2(1,0);OverviewPanel.pivot=new Vector2(.5f,0);
            statistics=CreateText("Statistics",panel.transform,16);statistics.alignment=TextAnchor.UpperLeft;
            statistics.rectTransform.anchorMin=new Vector2(0,1);statistics.rectTransform.anchorMax=Vector2.one;
            statistics.rectTransform.offsetMin=new Vector2(12,-50);statistics.rectTransform.offsetMax=new Vector2(-12,-8);
            controls=CreateText("Controls",panel.transform,14);var hint=controls;hint.text="1 沿路行军  |  2 自由行军  |  3 查看与管理  |  WASD 平移 / 右键框选  |  滚轮缩放";
            hint.rectTransform.anchorMin=Vector2.zero;hint.rectTransform.anchorMax=new Vector2(1,0);hint.rectTransform.offsetMin=new Vector2(12,8);hint.rectTransform.offsetMax=new Vector2(-12,30);
            Refresh();
        }
        Text CreateText(string name,Transform parent,int size)
        {
            var obj=new GameObject(name,typeof(RectTransform),typeof(Text));obj.transform.SetParent(parent,false);
            var text=obj.GetComponent<Text>();text.font=font;text.fontSize=size;text.color=new Color(.95f,.87f,.68f);
            text.alignment=TextAnchor.MiddleLeft;text.raycastTarget=false;text.resizeTextForBestFit=true;text.resizeTextMinSize=8;text.resizeTextMaxSize=size;return text;
        }
        void Update(){Refresh();}
        public void Refresh()
        {
            if(world==null||!world.Initialized||OverviewPanel==null)return;
            var main=GetComponent<CampaignHUD>();bool reserve=main!=null&&main.enabled&&main.Inspect&&!world.SimulationEnded;
            OverviewPanel.offsetMin=new Vector2(8,6);OverviewPanel.offsetMax=new Vector2(-(reserve?main.rightPanelWidth:0)-8,88);
            OverviewPanel.gameObject.SetActive(world.setup!=null&&world.setup.modes!=null&&world.setup.modes.CurrentMode==MouseCommandMode.Interaction);
            if(!OverviewPanel.gameObject.activeSelf)return;
            statistics.text=BattleTextFormatter.Overview(world.GetComponent<BattleInformationSource>().GetOverview(world.currentPlayer));
            var selection=world.GetComponent<ArmySelection>();var group=world.GetComponent<GroupMarchController>();controls.text="1 沿路  |  2 自由行军  |  3 管理  |  WASD 平移  |  右键框选  |  滚轮缩放"+(selection!=null&&selection.HasBoxSelection?"   已选 "+selection.SelectedArmies.Count+" 支  "+(group==null?"":group.Status):"");
        }
        void OnDisable(){if(OverviewPanel!=null)OverviewPanel.gameObject.SetActive(false);}
        void OnDestroy(){if(OverviewPanel!=null)Destroy(OverviewPanel.gameObject);}
    }
}
