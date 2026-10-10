using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
namespace CampaignDemo
{
    [DefaultExecutionOrder(200)]
    public sealed class BattleHUD : MonoBehaviour
    {
        public BattleWorld world;
        public BattleInformationSource information;
        public BattleUIActions actions;
        public Font font;
        [Header("模式3地图显示")]
        [Range(12,28)] public int mapLabelFontSize=16;
        [Range(32,160)] public int rangeCircleSegments=80;
        [Min(.005f)] public float rangeCircleWidth=.025f;
        public Color visionCircleColor=new Color(.35f,.8f,1,.55f);
        public Color attackCircleColor=new Color(1,.55f,.25f,.8f);
        readonly Dictionary<BattleUnit,LineRenderer[]> rangeCircles=new Dictionary<BattleUnit,LineRenderer[]>();
        Material rangeMaterial;
        public BattleUnit SelectedUnit { get; private set; }
        public RecoveryPoint SelectedPoint { get; private set; }
        RectTransform canvasRect;GameObject rightPanel;Text details,statistics,hint,playerLabel;
        Button forestHideButton,forestAttackButton;
        Button escapeButton,enterButton,repairButton,exitButton;Text repairLabel;
        readonly List<Button> slots=new List<Button>();
        readonly Dictionary<BattleUnit,Text> unitLabels=new Dictionary<BattleUnit,Text>();
        readonly Dictionary<RecoveryPoint,Button> pointLabels=new Dictionary<RecoveryPoint,Button>();
        readonly Dictionary<RecoveryPoint,List<Button>> pointIcons=new Dictionary<RecoveryPoint,List<Button>>();
        readonly Dictionary<Button,BattleUnit> iconUnits=new Dictionary<Button,BattleUnit>();
        float nextRefresh;
        public void RefreshPresentation() { LateUpdate(); }
        void Start()
        {
            if(world==null){enabled=false;return;}BattleServices.Ensure(world);if(information==null)information=world.GetComponent<BattleInformationSource>();if(actions==null)actions=GetComponent<BattleUIActions>();if(actions==null)actions=gameObject.AddComponent<BattleUIActions>();if(actions.commands==null)actions.commands=world.GetComponent<BattleCommandService>();if(information==null||actions==null){Debug.LogError("BattleHUD: bind information and actions.",this);enabled=false;return;}canvasRect=transform as RectTransform;
            if(font==null)font=Font.CreateDynamicFontFromOSFont(new[]{"Microsoft YaHei","SimHei","Arial"},18);
            if(font==null)font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            rightPanel=Panel("Unit and facility details",transform,new Vector2(1,.5f),new Vector2(-145,0),new Vector2(270,580));
            details=Text(UI("Details",rightPanel.transform,new Vector2(.5f,1),new Vector2(0,-145),new Vector2(242,270)),"",17,TextAnchor.UpperLeft);
            escapeButton=Button(rightPanel.transform,"逃跑",new Vector2(-61,-100),new Vector2(112,34));escapeButton.onClick.AddListener(Escape);
            enterButton=Button(rightPanel.transform,"进入恢复点",new Vector2(61,-100),new Vector2(112,34));enterButton.onClick.AddListener(Enter);
            repairButton=Button(rightPanel.transform,"开始恢复",new Vector2(-61,-144),new Vector2(112,34));repairButton.onClick.AddListener(Repair);repairLabel=repairButton.GetComponentInChildren<Text>();
            exitButton=Button(rightPanel.transform,"出点",new Vector2(61,-144),new Vector2(112,34));exitButton.onClick.AddListener(Exit);
            forestHideButton=Button(rightPanel.transform,"进入藏匿",new Vector2(-61,-185),new Vector2(112,32));forestHideButton.onClick.AddListener(()=>{Send(BattleCommandKind.ToggleForestHide);nextRefresh=0;});
            forestAttackButton=Button(rightPanel.transform,"进入攻击状态",new Vector2(61,-185),new Vector2(112,32));forestAttackButton.onClick.AddListener(()=>{if(SelectedUnit!=null)Send(BattleCommandKind.SetForestAttack,!information.GetArmy(world.currentPlayer,SelectedUnit.unitId).ambushEnabled.GetValueOrDefault());nextRefresh=0;});
            for(int i=0;i<3;i++){int index=i;var b=Button(rightPanel.transform,"",new Vector2(0,-185-i*36),new Vector2(244,30));b.onClick.AddListener(()=>SelectSlot(index));slots.Add(b);}
            var bottom=Panel("Army overview",transform,new Vector2(.5f,0),new Vector2(0,41),new Vector2(1280,82));
            statistics=Text(UI("Statistics",bottom.transform,new Vector2(.5f,.5f),new Vector2(0,13),new Vector2(1240,38)),"",16);
            hint=Text(UI("Controls",bottom.transform,new Vector2(.5f,.5f),new Vector2(0,-22),new Vector2(1240,24)),"1 沿路行军  |  2 自由行军  |  3 查看与管理  |  WASD / 右键平移  |  滚轮缩放",15);
            var switchButton=Button(transform,"切换玩家",new Vector2(0,0),new Vector2(120,34));var sr=switchButton.GetComponent<RectTransform>();sr.anchorMin=sr.anchorMax=new Vector2(1,1);sr.anchoredPosition=new Vector2(-85,-25);switchButton.onClick.AddListener(world.NextPlayer);
            playerLabel=Text(UI("Player",transform,new Vector2(1,1),new Vector2(-220,-25),new Vector2(120,30)),"",17);
            foreach(var u in world.units)RegisterUnit(u);
            foreach(var p in world.points)if(p!=null){var b=Button(transform,p.displayName,Vector2.zero,new Vector2(126,28));b.GetComponent<Image>().color=Color.clear;b.transition=Selectable.Transition.None;b.onClick.AddListener(()=>{if(Inspect)SelectPoint(p);});pointLabels.Add(p,b);var icons=new List<Button>();for(int i=0;i<p.capacity;i++){var icon=Button(transform,"",Vector2.zero,new Vector2(28,22));var glyph=UI("Army icon",icon.transform,new Vector2(.5f,.5f),Vector2.zero,new Vector2(24,18)).AddComponent<Image>();glyph.preserveAspect=true;glyph.raycastTarget=false;icon.onClick.AddListener(()=>{if(Inspect&&iconUnits.TryGetValue(icon,out var unit))SelectUnit(unit);});icons.Add(icon);}pointIcons.Add(p,icons);}
            rightPanel.transform.SetAsLastSibling();bottom.transform.SetAsLastSibling();world.PlayerChanged+=ClearSelection;world.UnitRegistered+=RegisterUnit;
        }
        public void RegisterUnit(BattleUnit u){if(u!=null&&!unitLabels.ContainsKey(u))unitLabels.Add(u,Text(UI("Personnel "+u.unitId,transform,new Vector2(.5f,.5f),Vector2.zero,new Vector2(200,46)),"",mapLabelFontSize));}
        bool Inspect { get { return world.setup.modes.CurrentMode==MouseCommandMode.Interaction; } }
        void Update()
        {
            if(world==null||!world.Initialized||!Inspect||!Input.GetMouseButtonDown(0))return;var specialized=GetComponent<CombinedArmsHUD>();if(specialized!=null&&specialized.CapturingInput)return;
            if(EventSystem.current!=null&&EventSystem.current.IsPointerOverGameObject())return;
            Vector3 mouse=Input.mousePosition;BattleUnit best=null;float distance=world.setup.settings.selectionPixels;
            foreach(var u in world.units)if(u!=null&&information.GetArmy(world.currentPlayer,u.unitId)?.visible==true)
            { var s=world.setup.mapCamera.WorldToScreenPoint(u.transform.position);float d=Vector2.Distance(mouse,s);if(d<distance){distance=d;best=u;} }
            if(best!=null)SelectUnit(best);else ClearSelection();
        }
        public void SelectUnit(BattleUnit u)
        {
            if(u==null||!u.Alive)return;
            var info=information.GetArmy(world.currentPlayer,u.unitId);if(info==null)return;
            SelectedUnit=u;SelectedPoint=null;var selection=world.GetComponent<ArmySelection>();if(selection!=null){if(u.ownerId==world.currentPlayer)selection.SelectOwned(u.GetComponent<ArmyRoadMover>());else selection.Clear();}nextRefresh=0;
        }
        public void SelectPoint(RecoveryPoint p) { SelectedPoint=p;SelectedUnit=null;var selection=world.GetComponent<ArmySelection>();if(selection!=null)selection.Clear();nextRefresh=0; }
        public void ClearSelection() { SelectedUnit=null;SelectedPoint=null;nextRefresh=0; }
        CommandResult Send(BattleCommandKind kind,bool enabled=false,string pointId=null)
        {return actions.Execute(new BattleCommand{kind=kind,unitId=SelectedUnit==null?null:SelectedUnit.unitId,pointId=pointId,enabled=enabled});}
        void Escape(){if(Send(BattleCommandKind.Escape).accepted)nextRefresh=0;}
        RecoveryPoint NearbyPoint()
        {if(SelectedUnit==null)return null;var info=information.GetArmy(world.currentPlayer,SelectedUnit.unitId);if(info==null||info.entryPointId==null)return null;foreach(var p in world.points)if(p!=null&&p.pointId==info.entryPointId)return p;return null;}
        void Enter(){var p=NearbyPoint();if(p!=null&&Send(BattleCommandKind.EnterPoint,false,p.pointId).accepted)nextRefresh=0;}
        void Repair(){if(Send(BattleCommandKind.ToggleRepair).accepted)nextRefresh=0;}
        void Exit(){if(Send(BattleCommandKind.ExitPoint).accepted)nextRefresh=0;}
        void SelectSlot(int index)
        {if(SelectedPoint==null)return;var info=information.GetFacility(world.currentPlayer,SelectedPoint.pointId);if(info==null||!info.own||index<0||index>=info.stationedUnitIds.Length)return;foreach(var u in world.units)if(u!=null&&u.unitId==info.stationedUnitIds[index]){SelectUnit(u);return;}}
        void LateUpdate()
        {
            if(world==null||!world.Initialized||rightPanel==null)return;rightPanel.SetActive(Inspect);
            if(SelectedUnit!=null&&information.GetArmy(world.currentPlayer,SelectedUnit.unitId)==null)ClearSelection();
            foreach(var pair in unitLabels)
            {
                var u=pair.Key;var label=pair.Value;var info=information.GetArmy(world.currentPlayer,u.unitId);bool visible=info!=null&&info.visible;bool known=info!=null&&info.known;
                label.gameObject.SetActive(Inspect&&visible&&OverlayAllowed(u.transform.position));if(!visible)continue;
                label.fontSize=mapLabelFontSize;label.text=known?(info.own?info.designation+"\n":"")+Mathf.CeilToInt(info.personnelPercent.Value)+"%":"?";var battery=u.GetComponent<ArtilleryBattery>();if(info.own&&battery!=null&&battery.Ready)label.text+=" · 炮击就绪";Place(label.rectTransform,u.transform.position,new Vector2(0,22));
            }
            foreach(var pair in pointLabels)
            {
                var p=pair.Key;var b=pair.Value;bool allowed=Inspect&&OverlayAllowed(p.transform.position);b.gameObject.SetActive(allowed);Place(b.GetComponent<RectTransform>(),p.transform.position,new Vector2(0,-23));
                var data=information.GetFacility(world.currentPlayer,p.pointId);if(data==null)continue;var title=data.name;bool own=data.own;bool observed=data.observed;
                if(own){int n=0;foreach(var id in data.stationedUnitIds)if(information.GetArmy(world.currentPlayer,id)?.onMap==false)n++;if(n>0)title+="  ["+n+"]";}
                var text=b.GetComponentInChildren<Text>();text.text=title;text.fontSize=mapLabelFontSize;text.color=own?new Color(.45f,1,.5f):observed&&!string.IsNullOrEmpty(data.ownerId)?new Color(1,.55f,.45f):new Color(.9f,.86f,.76f);
                var icons=pointIcons[p];int slotIndex=0;foreach(var u in world.units)if(own&&u!=null&&u.Alive&&u.Point==p&&!u.OnMap&&slotIndex<icons.Count){var icon=icons[slotIndex];iconUnits[icon]=u;icon.gameObject.SetActive(allowed);var sprite=u.GetComponentInChildren<SpriteRenderer>();icon.transform.Find("Army icon").GetComponent<Image>().sprite=sprite==null?null:sprite.sprite;Place(icon.GetComponent<RectTransform>(),p.transform.position,new Vector2(82+slotIndex*30,-23));slotIndex++;}for(int i=slotIndex;i<icons.Count;i++){icons[i].gameObject.SetActive(false);iconUnits.Remove(icons[i]);}
            }
            RefreshRanges();if(Time.unscaledTime<nextRefresh)return;nextRefresh=Time.unscaledTime+.15f;Refresh();
        }
        void Place(RectTransform rect,Vector3 worldPosition,Vector2 offset)
        { var s=world.setup.mapCamera.WorldToScreenPoint(worldPosition);var canvas=GetComponent<Canvas>();var camera=canvas.renderMode==RenderMode.ScreenSpaceOverlay?null:canvas.worldCamera;RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect,s,camera,out var p);rect.anchoredPosition=p+offset; }
        bool OverlayAllowed(Vector3 position)
        { var s=world.setup.mapCamera.WorldToScreenPoint(position);var canvas=GetComponent<Canvas>();var camera=canvas.renderMode==RenderMode.ScreenSpaceOverlay?null:canvas.worldCamera;RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect,s,camera,out var p);return s.z>0&&Mathf.Abs(p.x)<canvasRect.rect.width/2&&p.y<canvasRect.rect.height/2-50&&p.y>-canvasRect.rect.height/2+105&&(!Inspect||p.x<canvasRect.rect.width/2-285||Mathf.Abs(p.y)>280); }
        void Refresh()
        {
            playerLabel.text="当前玩家 "+world.currentPlayer;statistics.text=BattleTextFormatter.Overview(information.GetOverview(world.currentPlayer));
            escapeButton.gameObject.SetActive(false);enterButton.gameObject.SetActive(false);repairButton.gameObject.SetActive(false);exitButton.gameObject.SetActive(false);forestHideButton.gameObject.SetActive(false);forestAttackButton.gameObject.SetActive(false);foreach(var slot in slots)slot.gameObject.SetActive(false);
            if(SelectedUnit!=null)
            {
                var info=information.GetArmy(world.currentPlayer,SelectedUnit.unitId);details.text=BattleTextFormatter.Army(info);if(info==null||!info.own)return;var extra=GetComponent<CombinedArmsHUD>();if(extra!=null)details.text+=extra.Description(SelectedUnit);
                escapeButton.gameObject.SetActive(info.canEscape);enterButton.gameObject.SetActive(info.canEnter);repairButton.gameObject.SetActive(info.canRepair);exitButton.gameObject.SetActive(info.canExit);forestHideButton.gameObject.SetActive(info.canHide);forestAttackButton.gameObject.SetActive(info.canToggleAmbush);
                forestHideButton.GetComponentInChildren<Text>().text=info.hidden==true?"取消藏匿":"进入藏匿";forestAttackButton.GetComponentInChildren<Text>().text=info.ambushEnabled==true?"保持藏匿":"进入攻击状态";repairLabel.text=info.state==ArmyBattleState.Repairing?"取消恢复":"开始恢复";
            }
            else if(SelectedPoint!=null)
            {
                var info=information.GetFacility(world.currentPlayer,SelectedPoint.pointId);details.text=BattleTextFormatter.Facility(info);if(info==null||!info.own)return;
                for(int i=0;i<Mathf.Min(info.stationedUnitIds.Length,slots.Count);i++){var unit=information.GetArmy(world.currentPlayer,info.stationedUnitIds[i]);if(unit==null)continue;slots[i].gameObject.SetActive(true);slots[i].GetComponentInChildren<Text>().text=unit.designation+"  "+Mathf.CeilToInt(unit.personnelPercent.Value)+"%";}
            }
            else details.text="查看与管理\n\n点击军队查看信息。\n点击据点查看物资与内部部队。\n\n原移动命令继续执行。\n重复按 3 不切换显示层。";
        }
        static string Echelon(ArmyEchelon e) { return new[]{"班","排","连","营","团","旅","师","军"}[(int)e]; }
        static string State(ArmyBattleState s) { return new[]{"地图行动","交战","逃跑","进入据点","驻点","恢复中","出点","被消灭"}[(int)s]; }
        GameObject UI(string n,Transform parent,Vector2 anchor,Vector2 at,Vector2 size){var g=new GameObject(n,typeof(RectTransform));g.transform.SetParent(parent,false);var r=(RectTransform)g.transform;r.anchorMin=r.anchorMax=anchor;r.anchoredPosition=at;r.sizeDelta=size;return g;}
        GameObject Panel(string n,Transform p,Vector2 a,Vector2 at,Vector2 size){var g=UI(n,p,a,at,size);g.AddComponent<Image>().color=new Color(.22f,.16f,.09f,.96f);return g;}
        Text Text(GameObject g,string value,int size,TextAnchor alignment=TextAnchor.MiddleCenter){var t=g.AddComponent<Text>();t.font=font;t.text=value;t.fontSize=size;t.alignment=alignment;t.color=new Color(.95f,.87f,.68f);t.raycastTarget=false;return t;}
        Button Button(Transform p,string title,Vector2 at,Vector2 size){var g=Panel(title,p,new Vector2(.5f,.5f),at,size);g.GetComponent<Image>().color=new Color(.38f,.28f,.16f,.97f);var b=g.AddComponent<Button>();Text(UI("Label",g.transform,new Vector2(.5f,.5f),Vector2.zero,size),title,15);return b;}
        void RefreshRanges()
        {
            foreach(var u in world.units)
            {
                if(u==null)continue;
                var info=information.GetArmy(world.currentPlayer,u.unitId);bool show=Inspect&&info!=null&&info.own&&info.onMap&&info.visionRadius.HasValue;
                if(!rangeCircles.TryGetValue(u,out var lines))
                {
                    if(!show)continue;
                    if(rangeMaterial==null){var shader=Shader.Find("Sprites/Default");if(shader==null)continue;rangeMaterial=new Material(shader);}
                    lines=new LineRenderer[2];
                    for(int i=0;i<2;i++){var g=new GameObject(i==0?"Vision range":"Attack range");g.transform.SetParent(u.transform,false);var line=g.AddComponent<LineRenderer>();line.useWorldSpace=true;line.loop=true;line.sharedMaterial=rangeMaterial;line.sortingOrder=1;lines[i]=line;}
                    rangeCircles.Add(u,lines);
                }
                for(int i=0;i<2;i++)
                {
                    var line=lines[i];if(line==null)continue;line.enabled=show;if(!show)continue;
                    var battery=u.GetComponent<ArtilleryBattery>();float radius=i==0?info.visionRadius.Value:battery!=null&&battery.definition!=null?battery.definition.range:info.attackRadius.Value;if(i==1&&u.GetComponent<PersonnelCarrier>()!=null){line.enabled=false;continue;}
                    int count=Mathf.Clamp(rangeCircleSegments,32,160);line.positionCount=count;
                    line.startWidth=line.endWidth=Mathf.Max(.005f,rangeCircleWidth);
                    line.startColor=line.endColor=i==0?visionCircleColor:attackCircleColor;
                    Vector3 center=info.position;center.z=0;
                    for(int n=0;n<count;n++){float angle=n*Mathf.PI*2/count;line.SetPosition(n,center+new Vector3(Mathf.Cos(angle),Mathf.Sin(angle),0)*radius);}
                }
            }
        }
        void OnDisable(){foreach(var lines in rangeCircles.Values)foreach(var line in lines)if(line!=null)line.enabled=false;}
        void OnDestroy(){if(world!=null){world.PlayerChanged-=ClearSelection;world.UnitRegistered-=RegisterUnit;}foreach(var lines in rangeCircles.Values)foreach(var line in lines)if(line!=null)Destroy(line.gameObject);if(rangeMaterial!=null)Destroy(rangeMaterial);}
    }
}










