using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
namespace CampaignDemo
{
    [DefaultExecutionOrder(260)]
    public sealed class CampaignHUD:MonoBehaviour
    {
        public CampaignRuntime campaign;
        public Font font;
        public Sprite deploymentIcon;
        [Min(200)] public float rightPanelWidth=280;
        [Range(10,18)] public int uiFontSize=13;
        public RectTransform RightPanel {get{return panel==null?null:panel.GetComponent<RectTransform>();}}
        public enum Tool { None,ReservedLegacy1,ReservedLegacy,Artillery,FortBuild,FortAttack,DepotBuild,FortCut,RadarBuild }
        public Tool ActiveTool {get;private set;}
        public BattleUnit SelectedUnit {get;private set;}
        public UnityEngine.Object SelectedBuilding {get;private set;}
        public bool PreviewValid {get;private set;}
        GameObject panel,detailsObject;
        RectTransform detailsContent;
        Text status,overview,details;
        Image staffThumbnail;
        Button switchPlayer;
        public Button PlayerSwitchButton {get{return switchPlayer;}}
        readonly List<Action> refreshers=new List<Action>();
        readonly List<Text> compactLabels=new List<Text>();
        readonly List<GameObject> compactRows=new List<GameObject>();
        bool uiSuppressed;
        readonly Dictionary<Transform,bool> otherUI=new Dictionary<Transform,bool>();
        readonly List<GameObject> grayAreas=new List<GameObject>();
        SpriteRenderer ghost;
        LineRenderer ring;
        Material ringMaterial;
        Sprite square;
        string toolPlayer;BattleUnit toolUnit;
        float nextGray;
        bool drawingFort;readonly List<Vector3> fortStroke=new List<Vector3>();Vector3 fortOrigin;
        string feedback="";
        BattleWorld World {get{return campaign.world;}}
        public bool Inspect {get{return campaign!=null&&(LocalTacticalBattle.EnabledFor(this)||World.setup.modes.CurrentMode==MouseCommandMode.Interaction);}}
        void Start()
        {
            if(campaign==null||campaign.rules==null){enabled=false;return;}
            if(font==null)font=Font.CreateDynamicFontFromOSFont(new[]{"Microsoft YaHei","SimHei","Arial"},16);
            foreach(Transform child in transform)otherUI[child]=child.gameObject.activeSelf;
            if(World.GetComponent<LevelUnitAvailability>()==null)World.gameObject.AddComponent<LevelUnitAvailability>();
            var footer=GetComponent<BattleOverviewHUD>();if(footer==null)footer=gameObject.AddComponent<BattleOverviewHUD>();footer.world=World;footer.font=font;if(LocalTacticalBattle.EnabledFor(this))footer.enabled=false;
            panel=UI("Campaign right panel",transform);var pr=panel.GetComponent<RectTransform>();pr.anchorMin=new Vector2(1,0);pr.anchorMax=Vector2.one;pr.pivot=new Vector2(1,.5f);pr.anchoredPosition=Vector2.zero;pr.sizeDelta=new Vector2(rightPanelWidth,0);panel.AddComponent<Image>().color=StaffMapStyle.Panel;
            var upper=UI("Global operations",panel.transform);var ur=upper.GetComponent<RectTransform>();ur.anchorMin=new Vector2(0,1);ur.anchorMax=Vector2.one;ur.pivot=new Vector2(.5f,1);ur.anchoredPosition=Vector2.zero;ur.sizeDelta=new Vector2(0,38);var layout=upper.AddComponent<VerticalLayoutGroup>();Layout(layout);
            overview=Label(upper.transform,"",26);
            detailsObject=UI("Selection details",panel.transform);var dr=detailsObject.GetComponent<RectTransform>();dr.anchorMin=Vector2.zero;dr.anchorMax=Vector2.one;dr.offsetMin=new Vector2(6,6);dr.offsetMax=new Vector2(-6,-44);
            detailsObject.AddComponent<Image>().color=StaffMapStyle.Inset;var viewport=UI("Viewport",detailsObject.transform);Stretch(viewport.GetComponent<RectTransform>());viewport.AddComponent<Image>().color=Color.white;viewport.AddComponent<Mask>().showMaskGraphic=false;
            var content=UI("Content",viewport.transform);detailsContent=content.GetComponent<RectTransform>();detailsContent.anchorMin=new Vector2(0,1);detailsContent.anchorMax=Vector2.one;detailsContent.pivot=new Vector2(.5f,1);detailsContent.anchoredPosition=Vector2.zero;detailsContent.sizeDelta=Vector2.zero;Layout(content.AddComponent<VerticalLayoutGroup>());var fitter=content.AddComponent<ContentSizeFitter>();fitter.verticalFit=ContentSizeFitter.FitMode.PreferredSize;var scroll=detailsObject.AddComponent<ScrollRect>();scroll.viewport=viewport.GetComponent<RectTransform>();scroll.content=detailsContent;scroll.horizontal=false;scroll.movementType=ScrollRect.MovementType.Clamped;
            var thumbnail=UI("Staff unit thumbnail",detailsObject.transform);staffThumbnail=thumbnail.AddComponent<Image>();staffThumbnail.preserveAspect=true;staffThumbnail.raycastTarget=false;var tr=staffThumbnail.rectTransform;tr.anchorMin=tr.anchorMax=tr.pivot=Vector2.one;tr.anchoredPosition=new Vector2(-10,-6);tr.sizeDelta=new Vector2(44,38);thumbnail.SetActive(false);
            var g=new GameObject("Deployment cursor preview");ghost=g.AddComponent<SpriteRenderer>();ghost.sprite=deploymentIcon==null?World.unknownUnitSprite:deploymentIcon;ghost.sortingOrder=30;
            var rg=new GameObject("Target area preview");ring=rg.AddComponent<LineRenderer>();ring.loop=true;ring.useWorldSpace=true;ring.positionCount=64;ringMaterial=new Material(Shader.Find("Sprites/Default"));ring.sharedMaterial=ringMaterial;ring.startWidth=ring.endWidth=.025f;ring.sortingOrder=30;
            square=Sprite.Create(Texture2D.whiteTexture,new Rect(0,0,1,1),new Vector2(.5f,.5f),1);
            var ranges=World.GetComponent<CampaignRangeDisplay>();if(ranges==null)ranges=World.gameObject.AddComponent<CampaignRangeDisplay>();ranges.world=World;
            World.PlayerChanged+=ClearSelection;ClearSelection();CancelTool();
        }
        static void Layout(VerticalLayoutGroup layout){layout.padding=new RectOffset(6,6,5,5);layout.spacing=3;layout.childControlHeight=true;layout.childControlWidth=true;layout.childForceExpandHeight=false;layout.childForceExpandWidth=true;}
        void LateUpdate()
        {
            for(int i=compactRows.Count-1;i>=0;i--){var row=compactRows[i];if(row==null){compactRows.RemoveAt(i);continue;}bool active=false;foreach(Transform child in row.transform)if(child.gameObject.activeSelf){active=true;break;}row.SetActive(active);}
            // Text rows use their actual wrapped height instead of large empty fixed slots.
            for(int i=compactLabels.Count-1;i>=0;i--){var text=compactLabels[i];if(text==null){compactLabels.RemoveAt(i);continue;}var element=text.GetComponent<LayoutElement>();if(element==null)continue;float height=string.IsNullOrEmpty(text.text)?0:Mathf.Ceil(text.preferredHeight+2);if(Mathf.Abs(element.preferredHeight-height)>.5f)element.preferredHeight=height;}
        }
        public void SetCommandFeedback(string message){feedback=message;}
        public void RefreshStaffDetails(){foreach(var refresh in refreshers)refresh();LateUpdate();}
        public void ToggleTool(Tool tool)
        {if(World.SimulationEnded)return;if(ActiveTool==tool){CancelTool();return;}CancelTool();ActiveTool=tool;toolPlayer=World.currentPlayer;toolUnit=SelectedUnit;if((tool==Tool.FortBuild||tool==Tool.FortCut)&&toolUnit!=null){if(toolUnit.Motor!=null)toolUnit.Motor.Stop();fortOrigin=toolUnit.transform.position;}feedback=tool==Tool.FortBuild||tool==Tool.FortCut?(LocalTacticalBattle.EnabledFor(this)?"按住左键自由划线，松开施工；Esc取消":"左键逐点连接防线或拆除段，点击确认按钮施工；Esc取消"):"点击地图确认；右键或 Esc 取消";}
        public void CancelTool(){drawingFort=false;fortStroke.Clear();feedback="";ActiveTool=Tool.None;toolUnit=null;ClearGray();if(ghost!=null)ghost.enabled=false;if(ring!=null)ring.enabled=false;if(World.territory!=null)World.territory.suppressPresentation=false;}
        public void ClearSelection(){CancelTool();SelectedUnit=null;SelectedBuilding=null;World.GetComponent<ArmySelection>()?.Clear();World.GetComponent<StaffMapPresentation>()?.SetDrawerOpen(false);Rebuild();}
        public void SelectUnit(BattleUnit u){CancelTool();SelectedUnit=u;SelectedBuilding=null;World.GetComponent<StaffMapPresentation>()?.SetDrawerOpen(u!=null);var selection=World.GetComponent<ArmySelection>();if(selection!=null){if(u!=null&&u.ownerId==World.currentPlayer)selection.SelectOwned(u.GetComponent<ArmyRoadMover>());else selection.Clear();}Rebuild();}
        public void SyncFromArmySelection(){var s=World.GetComponent<ArmySelection>();if(s==null)return;if(s.Selected==null){if(SelectedUnit!=null&&SelectedUnit.ownerId==World.currentPlayer&&SelectedUnit.OnMap)ClearSelection();return;}var u=s.Selected.GetComponent<BattleUnit>();if(SelectedUnit!=u){CancelTool();SelectedUnit=u;SelectedBuilding=null;World.GetComponent<StaffMapPresentation>()?.SetDrawerOpen(u!=null);Rebuild();}}
        public void SelectBuilding(UnityEngine.Object obj){CancelTool();SelectedUnit=null;SelectedBuilding=obj;World.GetComponent<StaffMapPresentation>()?.SetDrawerOpen(obj!=null);World.GetComponent<ArmySelection>()?.Clear();Rebuild();}
        public CommandResult Send(BattleCommand command)
        {CancelTool();if(!Inspect)return CommandResult.No("请按 V");command.playerId=World.currentPlayer;var result=World.GetComponent<BattleCommandService>().Submit(command);feedback=result.pending?result.reason:result.accepted?"操作完成":result.reason;return result;}
        void Update()
        {
            if(panel==null||!World.Initialized)return;if(World.SimulationEnded){CancelTool();if(uiSuppressed)RestoreOtherUI();panel.SetActive(false);return;}panel.SetActive(Inspect);var minimap=GetComponent<TacticalMinimap>();var panelRect=panel.GetComponent<RectTransform>();panelRect.offsetMin=new Vector2(-rightPanelWidth,8);panelRect.offsetMax=new Vector2(0,-(LocalTacticalBattle.EnabledFor(this)?8:minimap==null?8:minimap.ReservedTop));if(LocalTacticalBattle.EnabledFor(this))panelRect.offsetMin=new Vector2(-rightPanelWidth,8);if(Inspect&&!uiSuppressed){foreach(var child in new List<Transform>(otherUI.Keys))if(child!=null&&(!LocalTacticalBattle.EnabledFor(this)&&child.name!="Army overview"||LocalTacticalBattle.EnabledFor(this))&&!SharedControl(child)){otherUI[child]=child.gameObject.activeSelf;child.gameObject.SetActive(false);}uiSuppressed=true;}else if(!Inspect&&uiSuppressed)RestoreOtherUI();
            if(!Inspect){CancelTool();return;}
            if(LocalTacticalBattle.EnabledFor(this)&&!UnitContextOrders.Consumed&&Input.GetKeyDown(KeyCode.Escape)){ClearSelection();return;}
            if(SelectedUnit!=null&&(!SelectedUnit.Alive||SelectedUnit.State==ArmyBattleState.EnteringPoint||(!SelectedUnit.OnMap&&SelectedUnit.Point==null)))ClearSelection();
            if(SelectedBuilding is ResourceDepot removed&&removed.Removed)ClearSelection();if(SelectedBuilding is Fortification selectedFort&&selectedFort.Destroyed)ClearSelection();
            overview.text="己方控制 "+(World.territory==null?0:World.territory.Percent(World.currentPlayer)).ToString("0.0")+"%";
            foreach(var refresh in refreshers)refresh();
            if(ActiveTool!=Tool.None)
            {
                if(toolPlayer!=World.currentPlayer||(toolUnit!=SelectedUnit||toolUnit==null||!toolUnit.OnMap)){CancelTool();return;}
                if(Input.GetKeyDown(KeyCode.Escape)){CancelTool();feedback="已取消";return;}if(Input.GetMouseButtonDown(1)){CancelTool();feedback="已取消";return;}
                var p=MouseWorld();if(ActiveTool==Tool.FortBuild||ActiveTool==Tool.FortCut){UpdateFortDrawing(p);return;}Preview(p);if(Input.GetMouseButtonDown(0)&&!PointerOnUI())Confirm(p);return;
            }
            if(!UnitContextOrders.Consumed&&!LocalTacticalBattle.EnabledFor(this)&&Input.GetMouseButtonDown(0)&&!PointerOnUI())Pick(Input.mousePosition);
        }
        bool PointerOnUI(){return (EventSystem.current!=null&&EventSystem.current.IsPointerOverGameObject())||LocalMapSettings.CapturesPointer(World);}
        Vector3 FortDrawingPoint(Vector3 point)
        {
            if(!LocalTacticalBattle.EnabledFor(this))point=FortificationLineWorks.ClampToVision(toolUnit,fortOrigin,point);else point.z=0;
            if(ActiveTool!=Tool.FortBuild)return point;
            var camera=World.setup.mapCamera;var screen=camera.WorldToScreenPoint(point);float closestPixels=8;var result=point;
            foreach(var fort in campaign.combined.fortifications)
            {
                if(fort==null||fort.Destroyed||!fort.IsLine||fort.ownerId!=World.currentPlayer)continue;
                var candidate=fort.ClosestPoint(point);
                if(!LocalTacticalBattle.EnabledFor(this)&&Vector2.Distance(fortOrigin,candidate)>toolUnit.combatDefinition.visionRadius)continue;
                float pixels=Vector2.Distance(screen,camera.WorldToScreenPoint(candidate));
                if(pixels<closestPixels){closestPixels=pixels;result=candidate;}
            }
            return result;
        }
        public void BeginFortStroke(Vector3 point)
        {if((ActiveTool!=Tool.FortBuild&&ActiveTool!=Tool.FortCut)||toolUnit==null)return;fortStroke.Clear();drawingFort=true;fortOrigin=toolUnit.transform.position;fortStroke.Add(FortDrawingPoint(point));}
        public void AppendFortPoint(Vector3 point,bool force=false)
        {if(!drawingFort||toolUnit==null)return;var clamped=FortDrawingPoint(point);float distance=Vector2.Distance(clamped,fortStroke[fortStroke.Count-1]);if(distance<=Mathf.Epsilon||(!force&&distance<campaign.combined.engineering.drawingSampleWorldDistance)||fortStroke.Count>=campaign.combined.engineering.maximumLinePoints)return;fortStroke.Add(clamped);}
        public Vector3[] FortPreviewPoints {get{return fortStroke.ToArray();}}
        public void AddFortNode(Vector3 point){if(!drawingFort)BeginFortStroke(point);else AppendFortPoint(point,true);}
        void UpdateFortDrawing(Vector3 point)
        {
            ghost.enabled=false;if(LocalTacticalBattle.EnabledFor(this)){if(Input.GetMouseButtonDown(0)&&!PointerOnUI())BeginFortStroke(point);if(drawingFort&&Input.GetMouseButton(0)&&!PointerOnUI())AppendFortPoint(point);if(drawingFort&&Input.GetMouseButtonUp(0)){if(!PointerOnUI()){AppendFortPoint(point,true);ConfirmFortStroke();}else CancelTool();return;}}else if(Input.GetMouseButtonDown(0)&&!PointerOnUI())AddFortNode(point);
            var preview=new List<Vector3>(fortStroke);if(drawingFort&&!PointerOnUI()){var cursor=FortDrawingPoint(point);if(Vector2.Distance(cursor,preview[preview.Count-1])>Mathf.Epsilon)preview.Add(cursor);}
            ring.startWidth=ring.endWidth=LocalPrototypeOrders.EnabledFor(this)?Mathf.Max(.10f,campaign.combined.engineering.lineVisualWidth*1.5f):campaign.combined.engineering.lineVisualWidth;ring.numCornerVertices=0;ring.numCapVertices=0;ring.loop=false;ring.positionCount=preview.Count;ring.enabled=preview.Count>1;if(preview.Count>0)ring.SetPositions(preview.ToArray());
            string reason;FortificationLineWorks.BreachPlan demolition=null;PreviewValid=ActiveTool==Tool.FortCut?campaign.lineWorks.PlanBreach(toolUnit,preview,out demolition,out reason):campaign.lineWorks.CanBuild(toolUnit,preview,out reason);ring.startColor=ring.endColor=PreviewValid?Color.green:Color.red;if(demolition!=null){ring.positionCount=demolition.effectivePoints.Length;ring.SetPositions(demolition.effectivePoints);}
            if(drawingFort&&ActiveTool==Tool.FortCut){feedback=demolition!=null?"拆除长度 "+demolition.lengthMeters.ToString("0.0")+"米\n预计时间 "+demolition.time.ToString("0.0")+"秒\n目标："+(demolition.own?"己方防线（用时×"+campaign.combined.engineering.ownLineDemolitionTimeMultiplier.ToString("0.##")+"）":"敌方防线")+"；松手拆除":reason;}else if(drawingFort){float length=FortificationLineWorks.Length(preview)*campaign.combined.engineering.metersPerWorldUnit;feedback="已选 "+fortStroke.Count+" 个点；松手施工（红色松手取消）\n长度 "+length.ToString("0.0")+"米 · 时间 "+(length*campaign.combined.engineering.lineBuildSecondsPerMeter).ToString("0.0")+"秒 · 费用 "+(length*campaign.combined.engineering.lineCostPerMeter).ToString("0.0")+(PreviewValid?"":"\n"+reason);}
        }
        public CommandResult ConfirmFortStroke()
        {
            if((ActiveTool!=Tool.FortBuild&&ActiveTool!=Tool.FortCut)||toolUnit==null)return CommandResult.No("没有防线绘制操作");bool cut=ActiveTool==Tool.FortCut;var command=new BattleCommand{kind=cut?BattleCommandKind.DismantleFortSection:BattleCommandKind.BuildFortification,playerId=World.currentPlayer,unitId=toolUnit.unitId,fortLine=new List<Vector3>(fortStroke)};var result=World.GetComponent<BattleCommandService>().Submit(command);
            if(result.accepted){CancelTool();Rebuild();feedback=cut?"已安排工兵到拆除段中点；完成后一次性开口":LocalTacticalBattle.EnabledFor(this)?"已安排工兵到防线起点施工；其他工兵可 Shift＋左键协助":"已安排工兵到起终点中点施工；取消退还未完成费用";}else{if(LocalPrototypeOrders.EnabledFor(this))CancelTool();feedback=result.reason;}return result;
        }
        Vector3 MouseWorld(){var camera=World.setup.mapCamera;var p=MapProjection.Ground(camera,new Vector2(Input.mousePosition.x,Input.mousePosition.y));p.z=0;return p;}
        public bool Pick(Vector3 mouse)
        {
            UnityEngine.Object best=null;float distance=World.setup.settings.selectionPixels;var cam=World.setup.mapCamera;
            Action<UnityEngine.Object,Vector3> candidate=(obj,p)=>{float d=Vector2.Distance(mouse,cam.WorldToScreenPoint(p));if(d<distance){distance=d;best=obj;}};
            foreach(var u in World.units)if(u!=null&&World.IsVisible(World.currentPlayer,u))candidate(u,u.transform.position);
            foreach(var p in World.points)if(p!=null&&(!World.fogEnabled||p.ownerId==World.currentPlayer||World.VisibleAt(World.currentPlayer,p.transform.position)))candidate(p,p.transform.position);
            foreach(var d in campaign.depots)if(d!=null&&!d.Removed&&(!World.fogEnabled||d.ownerId==World.currentPlayer||World.VisibleAt(World.currentPlayer,d.transform.position)))candidate(d,d.transform.position);
            foreach(var f in campaign.combined.fortifications)if(f!=null&&!f.Destroyed&&f.VisibleTo(World,World.currentPlayer))candidate(f,f.ClosestPoint(MouseWorld()));
            if(LocalTacticalBattle.EnabledFor(this)&&best==null)foreach(var site in campaign.logistics.sites)if(site!=null&&site.Area.Contains(MouseWorld()))candidate(site,MouseWorld());
            if(best is BattleUnit unit)SelectUnit(unit);else if(best!=null)SelectBuilding(best);else if(!LocalTacticalBattle.EnabledFor(this))ClearSelection();return best!=null;
        }
        public void Preview(Vector3 p)
        {
            bool over=PointerOnUI();PreviewValid=false;float radius=campaign.rules.placementPreviewRadius;
            if(ActiveTool==Tool.Artillery){var b=toolUnit.GetComponent<ArtilleryBattery>();radius=b.definition.blastRadius;PreviewValid=b.Ready&&Vector2.Distance(toolUnit.transform.position,p)<=b.definition.range;}
            else if(ActiveTool==Tool.RadarBuild){radius=.35f;PreviewValid=RadarInstallation.CanDeploy(World,toolUnit,p);}
            else if(ActiveTool==Tool.DepotBuild)PreviewValid=campaign.CanBuildDepot(toolUnit,p);
            else if(ActiveTool==Tool.FortBuild){radius=campaign.combined.engineering.fortificationRadius;PreviewValid=campaign.combined.CanBuildPreview(toolUnit,p);}
            else if(ActiveTool==Tool.FortAttack){foreach(var f in campaign.combined.fortifications)if(f!=null&&!f.Destroyed&&f.DistanceTo(p)<=f.radius&&f.VisibleTo(World,World.currentPlayer))PreviewValid=true;}
            Color color=ActiveTool==Tool.Artillery?new Color(.78f,.10f,.07f,PreviewValid?.95f:.5f):PreviewValid?Color.green:Color.red;ghost.enabled=!over&&(ActiveTool==Tool.DepotBuild);ghost.transform.position=p;ghost.color=color;ghost.transform.localScale=Vector3.one*radius;
            ring.startWidth=ring.endWidth=.025f;ring.loop=true;ring.positionCount=64;ring.enabled=!over;ring.startColor=ring.endColor=color;for(int i=0;i<64;i++){float a=i*Mathf.PI*2/64;ring.SetPosition(i,p+new Vector3(Mathf.Cos(a)*radius,Mathf.Sin(a)*radius));}
        }
        public CommandResult Confirm(Vector3 p)
        {
            var tool=ActiveTool;var actor=toolUnit;if(tool==Tool.None)return CommandResult.No("没有选点操作");
            var kind=tool==Tool.RadarBuild?BattleCommandKind.DeployRadar:tool==Tool.Artillery?BattleCommandKind.ArtilleryFire:tool==Tool.FortBuild?BattleCommandKind.BuildFortification:tool==Tool.FortAttack?BattleCommandKind.AttackFortification:BattleCommandKind.BuildResourceDepot;
            var command=new BattleCommand{kind=kind,playerId=World.currentPlayer,unitId=actor==null?null:actor.unitId,destination=p,enabled=true};var result=World.GetComponent<BattleCommandService>().Submit(command);if(result.accepted){CancelTool();Rebuild();feedback=tool==Tool.Artillery?"持续炮击已开始，共 "+actor.GetComponent<ArtilleryBattery>().BarrageDuration.ToString("0.0")+" 秒；首发 "+actor.GetComponent<ArtilleryBattery>().definition.delaySeconds.ToString("0.0")+" 秒后落弹":"命令已接受";}else feedback=result.reason;return result;
        }
        void RefreshGray()
        {
            ClearGray();if(World.territory==null)return;foreach(var cell in World.territory.OwnedCells(World.currentPlayer))foreach(var s in campaign.logistics.sites)if(s!=null&&s.Area.Overlaps(cell)){var area=Rect.MinMaxRect(Mathf.Max(cell.xMin,s.Area.xMin),Mathf.Max(cell.yMin,s.Area.yMin),Mathf.Min(cell.xMax,s.Area.xMax),Mathf.Min(cell.yMax,s.Area.yMax));var g=new GameObject("合法领土资源区预览");g.transform.position=area.center;g.transform.localScale=new Vector3(area.width,area.height,1);var sprite=g.AddComponent<SpriteRenderer>();sprite.sprite=square;sprite.sortingOrder=-4;sprite.color=new Color(.7f,.7f,.7f,.35f);grayAreas.Add(g);}
        }
        void ClearGray(){foreach(var g in grayAreas)if(g!=null)Destroy(g);grayAreas.Clear();}
        void Rebuild()
        {
            if(detailsContent==null)return;foreach(Transform child in detailsContent){child.gameObject.SetActive(false);Destroy(child.gameObject);}refreshers.Clear();bool selected=SelectedUnit!=null||SelectedBuilding!=null;detailsObject.SetActive(selected);if(!selected)return;
            if(staffThumbnail!=null){staffThumbnail.gameObject.SetActive(SelectedUnit!=null);if(SelectedUnit!=null){bool known=SelectedUnit.ownerId==World.currentPlayer||World.IsKnown(World.currentPlayer,SelectedUnit);staffThumbnail.sprite=StaffMapSymbols.Unit(SelectedUnit.combatDefinition.role,SelectedUnit.combatDefinition.echelon,SelectedUnit.ownerId==BattleFactions.BluePlayerId,44,!known);}}
            details=Label(detailsContent,"",SelectedUnit!=null||SelectedBuilding is Fortification?140:90);
            if(SelectedUnit!=null)UnitDetails(SelectedUnit);else BuildingDetails(SelectedBuilding);
        }
        void UnitDetails(BattleUnit u)
        {
            bool own=u.ownerId==World.currentPlayer;refreshers.Add(()=>{if(u==null)return;details.text=own?u.DisplayDesignation+"\n"+u.combatDefinition.unitType+" · "+BattleTextFormatter.Echelon(u.combatDefinition.echelon)+"\n完整度 "+(u.StrengthFraction*100).ToString("0.0")+"%\n"+(campaign.Vehicle(u)?"运输等效份数 "+(campaign.rules.At(campaign.rules.transportEquivalents,u.combatDefinition.echelon)*u.Fraction*TacticalDetachment.Scale(u)).ToString("0.##"):"兵力 "+u.Personnel.ToString("0")+" / "+u.StrengthCapacity.ToString("0"))+(u.Point==null?"":"\n驻留："+u.Point.displayName):World.IsKnown(World.currentPlayer,u)?"敌军\n"+u.combatDefinition.unitType+" · "+BattleTextFormatter.Echelon(u.combatDefinition.echelon)+"\n完整度 "+(u.StrengthFraction*100).ToString("0.0")+"%":"未知敌军";});if(!own)return;if(u.GetComponent<RadarInstallation>()!=null){Label(detailsContent,"固定雷达 · 侦察半径 10 km\n不可移动，不可藏匿",54);return;}
            if(LocalPrototypeOrders.EnabledFor(this)){var experience=Label(detailsContent,UnitExperience.Ensure(u)?.Description??"",72);refreshers.Add(()=>experience.text=UnitExperience.Ensure(u)?.Description??"");}
            if(LocalPrototypeOrders.EnabledFor(this)){var co=World.GetComponent<LocalCoordinatedOrders>();if(co!=null){var info=Label(detailsContent,co.Status(u),40);var rotate=Button(detailsContent,"取消轮换",()=>co.CancelRotation(u));var defend=Button(detailsContent,"取消联防",()=>co.CancelDefense(u));rotate.gameObject.SetActive(co.InRotation(u));defend.gameObject.SetActive(co.LinkCount(u)>0);info.gameObject.SetActive(co.InRotation(u)||co.LinkCount(u)>0);rotate.transform.parent.gameObject.SetActive(co.InRotation(u)||co.LinkCount(u)>0);refreshers.Add(()=>{rotate.gameObject.SetActive(co.InRotation(u));defend.gameObject.SetActive(co.LinkCount(u)>0);info.gameObject.SetActive(co.InRotation(u)||co.LinkCount(u)>0);info.text=co.Status(u);rotate.transform.parent.gameObject.SetActive(co.InRotation(u)||co.LinkCount(u)>0);});}}
            if(u.Point!=null&&!u.OnMap){var recover=Button(detailsContent,"恢复单位",()=>{Send(new BattleCommand{kind=BattleCommandKind.ToggleRepair,unitId=u.unitId});});refreshers.Add(()=>recover.GetComponentInChildren<Text>().text=FacilityRecoveryQueue.For(World)?.Contains(u)==true?"取消恢复":"恢复单位");Button(detailsContent,"离开",()=>{if(Send(new BattleCommand{kind=BattleCommandKind.ExitPoint,unitId=u.unitId}).accepted)ClearSelection();});return;}
            var state=u.GetComponent<CampaignUnit>();var road=u.GetComponent<ArmyRoadMover>();if(!LocalPrototypeOrders.EnabledFor(this)){var speedLabel=Label(detailsContent,"",36);var speed=Slider(detailsContent,road.Speed,state.desiredSpeed);speed.maxValue=Mathf.Max(.000001f,road.Speed);speed.SetValueWithoutNotify(Mathf.Min(state.desiredSpeed,speed.maxValue));speed.onValueChanged.AddListener(value=>{Send(new BattleCommand{kind=BattleCommandKind.SetAbsoluteSpeed,unitId=u.unitId,amount=value});});refreshers.Add(()=>speedLabel.text="指定速度 "+Mathf.Min(state.desiredSpeed,road.Speed).ToString("0.#####")+"   实际 "+(u.Motor==null?0:u.Motor.CurrentSpeed).ToString("0.#####"));}
            if(LocalTacticalBattle.EnabledFor(this)){Button(detailsContent,"Z 徐进",()=>World.GetComponent<LocalTacticalBattle>().SetOrder(LocalMarchStyle.Cautious));Button(detailsContent,"X 突击",()=>World.GetComponent<LocalTacticalBattle>().SetOrder(LocalMarchStyle.Assault));Button(detailsContent,"C 强行军",()=>World.GetComponent<LocalTacticalBattle>().SetOrder(LocalMarchStyle.Forced));}
            Button(detailsContent,"停止移动",()=>Send(new BattleCommand{kind=BattleCommandKind.StopMovement,unitId=u.unitId}));
            var battery=u.GetComponent<ArtilleryBattery>();if(battery!=null){var fireInfo=Label(detailsContent,"",76);refreshers.Add(()=>fireInfo.text="射程 "+battery.definition.range.ToString("0.##")+"   覆盖半径 "+battery.definition.blastRadius.ToString("0.##")+"\n落弹延迟 "+battery.definition.delaySeconds.ToString("0.0")+" 秒\n一轮炮击 "+battery.BarrageDuration.ToString("0.0")+" 秒"+(battery.Firing?" · 剩余 "+battery.BarrageRemaining.ToString("0.0")+" 秒":""));Label(detailsContent,"F 炮击瞄准 · 静止即可开火",25);}
            var carrier=u.GetComponent<PersonnelCarrier>();if(carrier!=null){var manifest=Label(detailsContent,"",65);refreshers.Add(()=>manifest.text="可载人数 "+carrier.PassengerCapacity.ToString("0")+"\n已载人数 "+carrier.Used.ToString("0")+"\n速度比例 "+(carrier.SpeedMultiplier*100).ToString("0")+"%");foreach(var passenger in carrier.Passengers){var capture=passenger;Button(detailsContent,capture.DisplayDesignation+" · 下车",()=>{if(Send(new BattleCommand{kind=BattleCommandKind.UnloadCarrier,unitId=u.unitId,targetUnitId=capture.unitId}).accepted)Rebuild();});}}
            var truck=u.GetComponent<TransportTruck>();if(truck!=null){TransportDetails(truck);return;}
            if(carrier!=null)return;
            if(u.combatDefinition.role==UnitRole.Engineer){var grade=Button(detailsContent,"工事等级："+(state.fortBuildLevel+1),null);grade.onClick.AddListener(()=>{CancelTool();Send(new BattleCommand{kind=BattleCommandKind.SetFortBuildLevel,unitId=u.unitId,count=(state.fortBuildLevel+1)%Mathf.Max(1,campaign.rules.fortLevelCount)});grade.GetComponentInChildren<Text>().text="工事等级："+(state.fortBuildLevel+1);});var build=Button(detailsContent,"修建防线",()=>{if(campaign.lineWorks.Working(u))Send(new BattleCommand{kind=BattleCommandKind.CancelEngineering,unitId=u.unitId});else ToggleTool(Tool.FortBuild);});var confirmLine=Button(detailsContent,"确认修建",()=>ConfirmFortStroke());
            refreshers.Add(()=>{bool active=ActiveTool==Tool.FortBuild||ActiveTool==Tool.FortCut;confirmLine.gameObject.SetActive(active&&!LocalPrototypeOrders.EnabledFor(this));confirmLine.GetComponentInChildren<Text>().text=ActiveTool==Tool.FortCut?"确认拆除":"确认修建";string reason;FortificationLineWorks.BreachPlan plan;confirmLine.interactable=active&&fortStroke.Count>=2&&(ActiveTool==Tool.FortCut?campaign.lineWorks.PlanBreach(toolUnit,fortStroke,out plan,out reason):campaign.lineWorks.CanBuild(toolUnit,fortStroke,out reason));});
            var radarButton=Button(detailsContent,"部署雷达",()=>ToggleTool(Tool.RadarBuild));refreshers.Add(()=>radarButton.GetComponentInChildren<Text>().text=ActiveTool==Tool.RadarBuild?"取消":"部署雷达（"+ResourceEconomy.Money(RadarInstallation.Settings.buildCost)+"）");
            var warehouse=Button(detailsContent,"部署修复点",()=>ToggleTool(Tool.DepotBuild));refreshers.Add(()=>{build.GetComponentInChildren<Text>().text=ActiveTool==Tool.FortBuild||campaign.lineWorks.IsBuilding(u)?"取消":"修建防线";warehouse.GetComponentInChildren<Text>().text=ActiveTool==Tool.DepotBuild?"取消":"部署仓库（"+ResourceEconomy.Money(campaign.rules.warehouseBuildCost)+"）";});}
            if(u.combatDefinition.canHideInForest){var hide=Button(detailsContent,"藏匿",()=>Send(new BattleCommand{kind=BattleCommandKind.ToggleForestHide,unitId=u.unitId}));refreshers.Add(()=>{hide.GetComponentInChildren<Text>().text=u.HiddenInForest?"取消藏匿":"藏匿";hide.interactable=u.HiddenInForest||World.CanHide(u);});var ambush=Button(detailsContent,"切换伏击待命",()=>Send(new BattleCommand{kind=BattleCommandKind.SetForestAttack,unitId=u.unitId,enabled=!u.ForestAttackEnabled}));refreshers.Add(()=>ambush.gameObject.SetActive(u.HiddenInForest));}
            var remaining=Label(detailsContent,"",24);refreshers.Add(()=>remaining.text=campaign.lineWorks.Working(u)?(campaign.lineWorks.Approaching(u)?"正在前往中点；施工剩余 ":"作业剩余 ")+campaign.lineWorks.Remaining(u).ToString("0.0")+"秒":"");var work=Button(detailsContent,"取消施工",()=>Send(new BattleCommand{kind=BattleCommandKind.CancelEngineering,unitId=u.unitId}));refreshers.Add(()=>work.gameObject.SetActive(campaign.lineWorks.Working(u)&&battery==null));
        }
        PersonnelCarrier NearbyCarrier(BattleUnit u)
        {
            if(!u.OnMap||u.Working||u.combatDefinition.role==UnitRole.Tank)return null;PersonnelCarrier best=null;float min=float.MaxValue;
            foreach(var unit in World.units)if(unit!=null&&unit.OnMap&&unit.ownerId==u.ownerId){var c=unit.GetComponent<PersonnelCarrier>();if(c==null||c.Unit==u||c.Unit.Working||c.Unit.Point!=null||u.Point!=null||c.Used+u.Personnel>c.PassengerCapacity)continue;float distance=Vector2.Distance(unit.transform.position,u.transform.position);if(distance<=c.BoardingRadius&&distance<min){min=distance;best=c;}}return best;
        }
        void TransportDetails(TransportTruck truck)
        {
            if(LocalPrototypeOrders.EnabledFor(this)){var cargo=Label(detailsContent,"",80);var auto=Button(detailsContent,"设置自动运输",()=>LocalPrototypeOrders.For(World)?.ToggleDraft(LocalPrototypeOrders.DraftKind.Patrol));var stop=Button(detailsContent,"停止运输",()=>{LocalPrototypeOrders.For(World)?.CancelDraft();LocalPrototypeOrders.For(World)?.CancelUnit(truck.Unit,true);});refreshers.Add(()=>{var orders=LocalPrototypeOrders.For(World);cargo.text="货物 "+ResourceEconomy.Money(truck.Cargo)+" / "+ResourceEconomy.Money(truck.Capacity)+"\n"+(orders?.Status(truck.Unit)??"待命")+"\n先点装货设施，再点卸货设施";auto.GetComponentInChildren<Text>().text=orders!=null&&orders.Drafting&&orders.CurrentDraft==LocalPrototypeOrders.DraftKind.Patrol?"完成设置并运输":truck.Automatic?"重设自动运输":"设置自动运输";stop.interactable=truck.Automatic||orders!=null&&orders.Drafting;});return;}
            var label=Label(detailsContent,"",50);refreshers.Add(()=>label.text="当前货物 "+truck.Cargo.ToString("0.##")+"\n最大容量 "+truck.Capacity.ToString("0.##"));
            var near=new List<UnityEngine.Object>();int targetIndex=0;UnityEngine.Object chosen=null;var targetButton=Button(detailsContent,"交互目标",()=>{CancelTool();if(near.Count>0){targetIndex=(targetIndex+1)%near.Count;chosen=near[targetIndex];}});
            var loadLabel=Label(detailsContent,"",25);var loadSlider=Slider(detailsContent,1,0);var load=Button(detailsContent,"填装",()=>Transfer(truck,chosen,true,loadSlider.value));var unloadLabel=Label(detailsContent,"",25);var unloadSlider=Slider(detailsContent,1,0);var unload=Button(detailsContent,"卸载",()=>Transfer(truck,chosen,false,unloadSlider.value));loadSlider.onValueChanged.AddListener(value=>CancelTool());unloadSlider.onValueChanged.AddListener(value=>CancelTool());
            refreshers.Add(()=>{near.Clear();foreach(var obj in campaign.StorageTargets(World.currentPlayer))if(Vector2.Distance(truck.transform.position,campaign.Position(obj))<=UnitRanges.Interaction(truck.Unit))near.Add(obj);if(chosen==null||!near.Contains(chosen)){targetIndex=0;chosen=near.Count>0?near[0]:null;}targetButton.interactable=near.Count>1;targetButton.GetComponentInChildren<Text>().text=chosen==null?"附近无装卸目标":"目标："+campaign.Name(chosen)+"（点击切换）";float lm=campaign.TransferMaximum(truck,chosen,true),um=campaign.TransferMaximum(truck,chosen,false);loadSlider.maxValue=Mathf.Max(1,truck.Capacity);unloadSlider.maxValue=Mathf.Max(1,truck.Capacity);if(loadSlider.value>lm)loadSlider.SetValueWithoutNotify(lm);if(unloadSlider.value>um)unloadSlider.SetValueWithoutNotify(um);loadSlider.interactable=lm>0;unloadSlider.interactable=um>0;load.interactable=chosen!=null&&lm>0&&loadSlider.value>0;unload.interactable=chosen!=null&&um>0&&unloadSlider.value>0;loadLabel.text="本次填装 "+loadSlider.value.ToString("0.##")+" / "+lm.ToString("0.##");unloadLabel.text="本次卸载 "+unloadSlider.value.ToString("0.##")+" / "+um.ToString("0.##");});
        }
        void Transfer(TransportTruck truck,UnityEngine.Object target,bool loading,float amount)
        {var c=new BattleCommand{kind=loading?BattleCommandKind.LoadResources:BattleCommandKind.UnloadResources,unitId=truck.Unit.unitId,amount=amount};if(target is RecoveryPoint p)c.pointId=p.pointId;else if(target is ResourceDepot d)c.depotId=d.depotId;Send(c);}
        void BuildingDetails(UnityEngine.Object obj)
        {
            if(obj is ResourceDepot repair&&repair.recovery!=null){BuildingDetails(repair.recovery);return;}
            if(obj is ResourceSite site){refreshers.Add(()=>details.text=site.displayName+"\n资源储量："+site.stock.ToString("0")+"\n派运输车前往资源区即可自动装载");return;}
            if(obj is Fortification fort)
            {
                refreshers.Add(()=>details.text="防御线\n归属："+fort.ownerId+"\n等级："+(fort.level+1)+"\n长度："+fort.builtLengthMeters.ToString("0.0")+"米\n血量："+fort.Health.ToString("0.0")+" / "+fort.MaximumHealth.ToString("0.0")+(campaign.lineWorks.Removing(fort)?"\n拆除剩余："+campaign.lineWorks.RemovalRemaining(fort).ToString("0.0")+"秒":""));
                Label(detailsContent,"选中工兵后点击防线：敌方优先攻击，己方执行拆除。",50);
                return;
            }
            refreshers.Add(()=>details.text=campaign.Name(obj)+"\n归属："+(campaign.Owner(obj)??"中立")+"\n库存："+(campaign.Owner(obj)==World.currentPlayer||obj is ResourceDepot pile&&pile.resourcePile?campaign.Stock(obj).ToString("0.##"):"未知"));
            if(obj is RecoveryPoint city&&city.kind==FacilityKind.RecoveryPoint&&city.ownerId==World.currentPlayer)
            {
                var service=World.GetComponent<CityManagement>();if(service==null)service=World.gameObject.AddComponent<CityManagement>();service.world=World;
                details.gameObject.SetActive(false);refreshers.Add(()=>details.gameObject.SetActive(city.ownerId!=World.currentPlayer));var cityView=CityPanelView.Attach(this,detailsContent,service,city);refreshers.Add(()=>{if(cityView!=null)cityView.Refresh();});if(city.isRepairSite){var repairDepot=city.GetComponent<ResourceDepot>();if(repairDepot!=null)Button(detailsContent,"拆除修复点",()=>{if(Send(new BattleCommand{kind=BattleCommandKind.RemoveResourceDepot,depotId=repairDepot.depotId}).accepted)ClearSelection();});}
            }
            if(obj is ResourceDepot depot&&depot.ownerId==World.currentPlayer&&!depot.resourcePile)Button(detailsContent,"自行拆除仓库（库存留作资源堆）",()=>{if(Send(new BattleCommand{kind=BattleCommandKind.RemoveResourceDepot,depotId=depot.depotId}).accepted)ClearSelection();});
        }
        GameObject UI(string name,Transform parent){var g=new GameObject(name,typeof(RectTransform));g.transform.SetParent(parent,false);return g;}
        static void Stretch(RectTransform rect){rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=rect.offsetMax=Vector2.zero;}
        Text Label(Transform parent,string text,float height){var g=UI("Text",parent);var t=g.AddComponent<Text>();t.font=font;t.fontSize=uiFontSize;t.text=text;t.color=StaffMapStyle.Ink;t.alignment=TextAnchor.UpperLeft;t.raycastTarget=false;g.AddComponent<LayoutElement>().preferredHeight=height*uiFontSize/16f;compactLabels.Add(t);return t;}
        Button Button(Transform parent,string title,UnityEngine.Events.UnityAction callback){if(parent==detailsContent){bool wide=title.Length>14;Transform row=null;if(!wide&&parent.childCount>0){var last=parent.GetChild(parent.childCount-1);if(last.name=="Action row"&&last.childCount<2)row=last;}if(row==null){var obj=UI(wide?"Full action row":"Action row",parent);compactRows.Add(obj);obj.AddComponent<LayoutElement>().preferredHeight=22;var layout=obj.AddComponent<HorizontalLayoutGroup>();layout.spacing=4;layout.childControlHeight=layout.childControlWidth=true;layout.childForceExpandWidth=true;layout.childForceExpandHeight=false;row=obj.transform;}parent=row;}
            var g=UI(title,parent);var item=g.AddComponent<LayoutElement>();item.preferredHeight=22;item.flexibleWidth=1;g.AddComponent<Image>().color=StaffMapStyle.Inset;var b=g.AddComponent<Button>();var text=Label(g.transform,title,36);Destroy(text.GetComponent<LayoutElement>());Stretch(text.rectTransform);text.alignment=TextAnchor.MiddleCenter;text.resizeTextForBestFit=true;text.resizeTextMinSize=9;text.resizeTextMaxSize=uiFontSize;if(callback!=null)b.onClick.AddListener(callback);return b;}
        Slider Slider(Transform parent,float maximum,float value){var g=UI("Amount slider",parent);g.AddComponent<LayoutElement>().preferredHeight=20;var bg=g.AddComponent<Image>();bg.color=new Color(.1f,.09f,.07f);var fill=UI("Fill",g.transform);Stretch(fill.GetComponent<RectTransform>());fill.AddComponent<Image>().color=new Color(.6f,.48f,.27f);var handle=UI("Handle",g.transform);handle.GetComponent<RectTransform>().sizeDelta=new Vector2(14,20);var image=handle.AddComponent<Image>();image.color=new Color(.9f,.8f,.5f);var slider=g.AddComponent<Slider>();slider.fillRect=fill.GetComponent<RectTransform>();slider.handleRect=handle.GetComponent<RectTransform>();slider.targetGraphic=image;slider.minValue=0;slider.maxValue=Mathf.Max(1,maximum);slider.SetValueWithoutNotify(value);return slider;}
        static bool ContainsControl(Transform root,Transform control){return control!=null&&(control==root||control.IsChildOf(root));}
        bool SharedControl(Transform root)
        {
            // Settings and map controls belong to every mode; preserve their current state.
            if(campaign==null||World==null||World.setup==null)return false;if(LocalTacticalBattle.EnabledFor(this)&&root.name=="离线战术指令")return true;
            var settings=World.GetComponent<GameSettingsController>();
            if(settings!=null&&settings.settingsPanel!=null&&ContainsControl(root,settings.settingsPanel.transform))return true;
            var zoom=World.setup.zoomController;
            if(zoom!=null&&((zoom.zoomSlider!=null&&ContainsControl(root,zoom.zoomSlider.transform))||(zoom.resetNode!=null&&ContainsControl(root,zoom.resetNode.transform))))return true;
            if(settings!=null)foreach(var button in root.GetComponentsInChildren<Button>(true))
                for(int i=0;i<button.onClick.GetPersistentEventCount();i++)
                    if(button.onClick.GetPersistentTarget(i)==settings&&button.onClick.GetPersistentMethodName(i)==nameof(GameSettingsController.TogglePanel))return true;
            return false;
        }
        void RestoreOtherUI(){foreach(var pair in otherUI)if(pair.Key!=null&&!SharedControl(pair.Key))pair.Key.gameObject.SetActive(pair.Value);uiSuppressed=false;}
        void OnDisable(){if(uiSuppressed)RestoreOtherUI();}
        void OnDestroy(){if(campaign!=null&&World!=null)World.PlayerChanged-=ClearSelection;CancelTool();if(ghost!=null)Destroy(ghost.gameObject);if(ring!=null)Destroy(ring.gameObject);if(ringMaterial!=null)Destroy(ringMaterial);if(square!=null)Destroy(square);}
    }
}




















