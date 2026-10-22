using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
namespace CampaignDemo
{
    [DefaultExecutionOrder(900)]
    public sealed class StaffMapPresentation:MonoBehaviour
    {
        public BattleWorld world;
        static readonly int[] IconSizes={16,24,32,44,64,128},BlockSizes={8,12,16};
        public StaffSymbolDetail Detail {get;private set;}=StaffSymbolDetail.Compact;
        public int SymbolPixels
        {
            get
            {
                var camera=world==null||world.setup==null?null:world.setup.mapCamera;
                float scale=camera==null?1:Mathf.Clamp(camera.pixelHeight/900f,.65f,3);
                float desired=StaffMapSymbols.Width(Detail)*scale;
                var sizes=Detail==StaffSymbolDetail.Block?BlockSizes:IconSizes;
                int best=sizes[0];foreach(var size in sizes)if(Mathf.Abs(size-desired)<Mathf.Abs(best-desired))best=size;return best;
            }
        }
        public float CoverageRatio {get;private set;}
        readonly Dictionary<BattleUnit,Text> labels=new Dictionary<BattleUnit,Text>();
        readonly Dictionary<BattleUnit,RectTransform> strengthBars=new Dictionary<BattleUnit,RectTransform>();
        readonly Dictionary<BattleUnit,string> names=new Dictionary<BattleUnit,string>();
        readonly Dictionary<string,int> serials=new Dictionary<string,int>();
        readonly HashSet<int> styled=new HashSet<int>();
        Canvas canvas;RectTransform labelRoot;Font font;CampaignHUD hud;TacticalMinimap minimap;
        RectTransform drawerTab;Text drawerCaption;float drawerReveal=0;bool drawerOpen=false;UnityEngine.Object previousDrawerObject;float nextSkin;
        Text scaleText,zoomText;RectTransform scale,compass,referenceFooter;Image compassNeedle;GameObject referenceRoot;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Install(){SceneManager.sceneLoaded-=SceneLoaded;SceneManager.sceneLoaded+=SceneLoaded;}
        static void SceneLoaded(Scene scene,LoadSceneMode mode){foreach(var w in Object.FindObjectsOfType<BattleWorld>())if(w.gameObject.scene==scene&&w.GetComponent<StaffMapPresentation>()==null)w.gameObject.AddComponent<StaffMapPresentation>().world=w;}
        void Start()
        {
            if(world==null)world=GetComponent<BattleWorld>();if(world.GetComponent<StaffVisibilityShade>()==null)world.gameObject.AddComponent<StaffVisibilityShade>().world=world;if(world.GetComponent<UnitContextOrders>()==null)world.gameObject.AddComponent<UnitContextOrders>().world=world;hud=FindObjectOfType<CampaignHUD>();if(hud==null){enabled=false;return;}
            canvas=hud.GetComponentInParent<Canvas>();canvas.sortingOrder=2000;minimap=hud.GetComponent<TacticalMinimap>();font=hud.font!=null?hud.font:Font.CreateDynamicFontFromOSFont(new[]{"Microsoft YaHei","Arial"},16);
            labelRoot=Rect("Staff unit designations",canvas.transform);Stretch(labelRoot);
            labelRoot.SetAsFirstSibling();
            foreach(var point in world.points)if(point!=null&&point.kind==FacilityKind.RecoveryPoint&&point.GetComponent<StaffObjectiveMarker>()==null)point.gameObject.AddComponent<StaffObjectiveMarker>();
            // Names are non-interactive overlays; all map input continues to existing controls.
            var group=labelRoot.gameObject.AddComponent<CanvasGroup>();group.blocksRaycasts=false;group.interactable=false;
            drawerTab=Rect("Operations drawer handle",hud.transform);drawerTab.gameObject.AddComponent<Image>().color=StaffMapStyle.Panel;
            drawerCaption=Text("Caption",drawerTab,"部队\n›",12);Stretch(drawerCaption.rectTransform);drawerCaption.alignment=TextAnchor.MiddleCenter;
            var handle=drawerTab.gameObject.AddComponent<StaffDrawerHandle>();handle.Toggle=()=>SetDrawerOpen(!drawerOpen);handle.DragDelta=delta=>{drawerReveal=Mathf.Clamp01(drawerReveal-delta/Mathf.Max(1,hud.rightPanelWidth));};handle.Release=()=>SetDrawerOpen(drawerReveal>=.5f);
            AddBorder(drawerTab.gameObject);BuildReference();Skin();
            ArrangeLegacyZoom();
            hud.uiFontSize=13;
            var arrows=world.GetComponent<MergedMovementArrows>();if(arrows!=null)arrows.color=StaffMapStyle.Blue;
            world.setup.mapCamera.backgroundColor=StaffMapStyle.Paper;
            var mapRoot=world.setup.freeMarchMap;var cartography=mapRoot.GetComponent<StaffMapCartography>();if(cartography==null)cartography=mapRoot.gameObject.AddComponent<StaffMapCartography>();cartography.roads=world.setup.roadMap;cartography.terrain=mapRoot;cartography.Invalidate();
            world.UnitRegistered+=Registered;foreach(var unit in world.units)if(unit!=null)Registered(unit);
            if(world.territory!=null){world.territory.ownColor=new Color(StaffMapStyle.Blue.r,StaffMapStyle.Blue.g,StaffMapStyle.Blue.b,.06f);world.territory.enemyColor=new Color(StaffMapStyle.Red.r,StaffMapStyle.Red.g,StaffMapStyle.Red.b,.06f);}
        }
        public void SetDrawerOpen(bool value){drawerOpen=value;if(value&&hud!=null&&hud.RightPanel!=null&&hud.Inspect)hud.RightPanel.gameObject.SetActive(true);}
        public bool DrawerOpen {get{return drawerOpen;}}
        void Registered(BattleUnit unit)
        {
            if(unit==null||names.ContainsKey(unit))return;string key=unit.ownerId;int index;serials.TryGetValue(key,out index);serials[key]=++index;
            names[unit]=unit.ownerId==BattleFactions.BluePlayerId?((index-1)/3+1)+"-"+((index-1)%3+1):"B-"+index;
        }
        public void SetDesignation(BattleUnit unit,string value){if(unit!=null)names[unit]=value;}
        public string Designation(BattleUnit unit){return DesignationFor(unit,world.currentPlayer);}
        public string DesignationFor(BattleUnit unit,string viewer){if(unit==null)return "";if(unit.GetComponent<RadarInstallation>()!=null)return "雷达";var d=unit.GetComponent<TacticalDetachment>();if(d!=null)return d.Label(viewer);Registered(unit);return names[unit];}
        bool knownUnit(BattleUnit unit){return unit.ownerId==world.currentPlayer||world.IsKnown(world.currentPlayer,unit);}
        public RectTransform StrengthBarRoot(BattleUnit unit){RectTransform value;return strengthBars.TryGetValue(unit,out value)?value:null;}
        void RefreshStrengthBars(BattleUnit unit,bool visible,Camera camera,RectTransform root){RectTransform bar;if(!strengthBars.TryGetValue(unit,out bar)||bar==null){bar=Rect("Strength bars "+unit.unitId,labelRoot);bar.sizeDelta=Vector2.zero;for(int i=0;i<4;i++){var cell=Rect("Cell "+i,bar);var image=cell.gameObject.AddComponent<Image>();image.raycastTarget=false;}strengthBars[unit]=bar;}bar.gameObject.SetActive(visible);if(!visible)return;var viewport=camera.WorldToViewportPoint(unit.transform.position);float scale=root.rect.height/Mathf.Max(1,camera.pixelHeight);float height=Mathf.Max(4,Mathf.Floor(SymbolPixels*.44f));float gap=Mathf.Max(1,Mathf.Floor(SymbolPixels/22f)),cellHeight=(height-3*gap)/4;bar.anchoredPosition=new Vector2((viewport.x-.5f)*root.rect.width-(SymbolPixels*.48f+3)*scale,(viewport.y-.5f)*root.rect.height-SymbolPixels*.08f*scale);int count=CombatMath.StrengthBars(unit.StrengthFraction);for(int i=0;i<4;i++){var cell=(RectTransform)bar.GetChild(i);cell.gameObject.SetActive(i<count);cell.sizeDelta=new Vector2(Mathf.Max(2,Mathf.Floor(SymbolPixels*.095f))*scale,cellHeight*scale);cell.anchoredPosition=new Vector2(0,(-height/2+cellHeight/2+i*(cellHeight+gap))*scale);cell.GetComponent<Image>().color=StaffMapStyle.Faction(unit.ownerId==BattleFactions.BluePlayerId);}}
        public void RefreshPresentation()
        {
            if(world==null||!world.Initialized||canvas==null||world.setup.mapCamera==null)return;
            var camera=world.setup.mapCamera;Rect bounds;
            if(MapViewBounds.TryGet(world.setup.roadMap,world.setup.freeMarchMap,out bounds))CoverageRatio=2*camera.orthographicSize/Mathf.Max(.001f,bounds.height);
            Detail=StaffMapSymbols.Detail(CoverageRatio,Detail);
            var root=canvas.transform as RectTransform;var uiCamera=canvas.renderMode==RenderMode.ScreenSpaceOverlay?null:canvas.worldCamera;
            float factor=Mathf.Max(.01f,canvas.scaleFactor);
            foreach(var unit in world.units)
            {
                if(unit==null)continue;Registered(unit);
                if(!labels.TryGetValue(unit,out var label)||label==null){label=Text("Unit designation "+unit.unitId,labelRoot,"",12);label.alignment=TextAnchor.UpperCenter;label.horizontalOverflow=HorizontalWrapMode.Overflow;label.verticalOverflow=VerticalWrapMode.Overflow;label.rectTransform.pivot=new Vector2(.5f,1);label.rectTransform.sizeDelta=new Vector2(112,22);label.gameObject.AddComponent<Outline>().effectColor=StaffMapStyle.Panel;label.GetComponent<Outline>().effectDistance=new Vector2(1,-1);labels[unit]=label;}
                bool visible=!world.SimulationEnded&&unit.gameObject.activeInHierarchy&&unit.OnMap&&world.IsVisible(world.currentPlayer,unit)&&Detail!=StaffSymbolDetail.Block;
                var screen=camera.WorldToScreenPoint(unit.transform.position);visible&=screen.z>0&&camera.pixelRect.Contains(screen);
                label.gameObject.SetActive(visible);RefreshStrengthBars(unit,visible&&knownUnit(unit),camera,root);if(!visible)continue;
                bool known=unit.ownerId==world.currentPlayer||world.IsKnown(world.currentPlayer,unit);
                label.text=known?Designation(unit):"?";label.color=StaffMapStyle.Faction(unit.ownerId==BattleFactions.BluePlayerId);label.fontSize=Detail==StaffSymbolDetail.Full?13:12;label.fontStyle=FontStyle.Bold;
                // Symbol's transparent header is included in height; label follows its lower edge.
                var viewport=camera.WorldToViewportPoint(unit.transform.position);
                label.rectTransform.anchoredPosition=new Vector2((viewport.x-.5f)*root.rect.width,(viewport.y-.5f)*root.rect.height-(SymbolPixels*.85f*.5f+2)*root.rect.height/Mathf.Max(1,camera.pixelHeight));
                var view=unit.View;if(view!=null)view.RefreshStaffSymbol();
            }
            // Labels never intercept clicks or expose units hidden by fog.
            var dead=new List<BattleUnit>();foreach(var pair in labels)if(pair.Key==null){if(pair.Value!=null)Destroy(pair.Value.gameObject);dead.Add(pair.Key);}foreach(var key in dead){labels.Remove(key);names.Remove(key);if(strengthBars.TryGetValue(key,out var bar)&&bar!=null)Destroy(bar.gameObject);strengthBars.Remove(key);}
            UpdateDrawer();UpdateReference();
        }
        void LateUpdate(){RefreshPresentation();if(Time.unscaledTime>=nextSkin){nextSkin=Time.unscaledTime+.25f;Skin();}}
        void UpdateDrawer()
        {
            if(drawerTab==null||hud.RightPanel==null)return;
            UnityEngine.Object current=(!ReferenceEquals(hud.SelectedUnit,null)&&hud.SelectedUnit!=null)?(UnityEngine.Object)hud.SelectedUnit:hud.SelectedBuilding;if(!ReferenceEquals(previousDrawerObject,null)&&current==null)drawerOpen=false;previousDrawerObject=current;
            if(!drawerTab.GetComponent<StaffDrawerHandle>().Dragging)drawerReveal=Mathf.MoveTowards(drawerReveal,drawerOpen?1:0,Time.unscaledDeltaTime/.20f);
            float width=Mathf.Clamp(hud.rightPanelWidth,260,340);var p=hud.RightPanel;
            float height=((RectTransform)hud.transform).rect.height;float top=78;float bottom=24;
            if(height-top-bottom<160)bottom=24; // Compact windows: minimap can independently collapse.
            p.anchorMin=new Vector2(1,0);p.anchorMax=Vector2.one;p.pivot=new Vector2(1,.5f);float shift=(1-drawerReveal)*(width+12);
            p.offsetMin=new Vector2(-width-12+shift,bottom);p.offsetMax=new Vector2(-12+shift,-top);
            p.gameObject.SetActive(hud.Inspect&&!world.SimulationEnded&&drawerReveal>0);
            drawerTab.anchorMin=drawerTab.anchorMax=drawerTab.pivot=new Vector2(1,1);drawerTab.sizeDelta=new Vector2(30,76);drawerTab.anchoredPosition=new Vector2(-width-12+shift,-top-14);
            if(drawerReveal<=0)drawerTab.anchoredPosition=new Vector2(0,-top-14);
            drawerCaption.text="部队\n"+(drawerOpen?"›":"‹");drawerTab.gameObject.SetActive(hud.Inspect&&!world.SimulationEnded);drawerTab.SetAsLastSibling();
            // Older scene panels share the same data; CampaignHUD is the interactive drawer.
            var legacy=hud.transform.Find("Unit and facility details");if(legacy!=null&&hud.Inspect)legacy.gameObject.SetActive(false);
        }
        void BuildReference()
        {
            var old=hud.GetComponent<MapReferenceHUD>();if(old!=null){old.enabled=false;if(old.scaleBar!=null)old.scaleBar.gameObject.SetActive(false);if(old.scaleLabel!=null)old.scaleLabel.gameObject.SetActive(false);if(old.compass!=null)old.compass.gameObject.SetActive(false);}
            referenceRoot=Rect("Staff map references",hud.transform).gameObject;Stretch((RectTransform)referenceRoot.transform);
            var cg=referenceRoot.AddComponent<CanvasGroup>();cg.blocksRaycasts=false;
            var gridRect=Rect("Viewport coordinate grid",referenceRoot.transform);Stretch(gridRect);gridRect.SetAsFirstSibling();var grid=gridRect.gameObject.AddComponent<StaffViewportGrid>();grid.mapCamera=world.setup.mapCamera;grid.labelFont=font;if(world.GetComponent<TabletopPresentation>()!=null)grid.gameObject.SetActive(false);var reference=hud.GetComponent<MapReferenceHUD>();grid.metersPerWorldUnit=reference==null?1000:reference.metersPerWorldUnit;
            compass=Rect("North compass",referenceRoot.transform);compass.anchorMin=compass.anchorMax=compass.pivot=new Vector2(1,1);compass.anchoredPosition=new Vector2(-20,-12);compass.sizeDelta=new Vector2(36,52);
            var north=Text("N",compass,"N",15);north.alignment=TextAnchor.UpperCenter;Stretch(north.rectTransform);
            compassNeedle=Rect("Compass needle",compass).gameObject.AddComponent<Image>();compassNeedle.sprite=StaffMapSymbols.Compass();compassNeedle.color=Color.white;compassNeedle.raycastTarget=false;compassNeedle.rectTransform.anchorMin=compassNeedle.rectTransform.anchorMax=new Vector2(.5f,0);compassNeedle.rectTransform.pivot=new Vector2(.5f,0);compassNeedle.rectTransform.sizeDelta=new Vector2(22,29);
            var footer=Rect("Scale and zoom",referenceRoot.transform);footer.anchorMin=footer.anchorMax=footer.pivot=Vector2.zero;footer.anchoredPosition=new Vector2(24,20);footer.sizeDelta=new Vector2(260,40);
            referenceFooter=footer;
            scale=Rect("Distance bar",footer);scale.anchorMin=scale.anchorMax=scale.pivot=Vector2.zero;scale.sizeDelta=new Vector2(140,5);
            for(int i=0;i<4;i++){var segment=Rect("Scale segment "+i,scale);segment.anchorMin=new Vector2(i*.25f,0);segment.anchorMax=new Vector2((i+1)*.25f,1);segment.offsetMin=segment.offsetMax=Vector2.zero;segment.gameObject.AddComponent<Image>().color=i%2==0?StaffMapStyle.Ink:StaffMapStyle.Panel;segment.gameObject.AddComponent<StaffKeepColor>();}
            AddBorder(scale.gameObject);scaleText=Text("Scale label",footer,"",12);scaleText.rectTransform.pivot=Vector2.zero;scaleText.rectTransform.anchorMin=scaleText.rectTransform.anchorMax=Vector2.zero;scaleText.rectTransform.anchoredPosition=new Vector2(0,10);scaleText.rectTransform.sizeDelta=new Vector2(220,20);
            zoomText=Text("Symbol detail",footer,"",11);zoomText.rectTransform.pivot=Vector2.zero;zoomText.rectTransform.anchorMin=zoomText.rectTransform.anchorMax=Vector2.zero;zoomText.rectTransform.anchoredPosition=new Vector2(164,-1);zoomText.rectTransform.sizeDelta=new Vector2(140,22);
        }
        void UpdateReference()
        {
            if(scale==null)return;var camera=world.setup.mapCamera;float mpu=1000;var old=hud.GetComponent<MapReferenceHUD>();if(old!=null)mpu=old.metersPerWorldUnit;
            var overview=hud.GetComponent<BattleOverviewHUD>();referenceFooter.anchoredPosition=new Vector2(24,minimap!=null&&minimap.expanded?minimap.Bottom+minimap.size+12:overview!=null&&overview.enabled&&hud.Inspect?104:20);referenceRoot.transform.SetAsLastSibling();
            float metersPerUI=2*camera.orthographicSize/Mathf.Max(1,camera.pixelHeight)*mpu*canvas.scaleFactor;
            float desired=140*metersPerUI;float power=Mathf.Pow(10,Mathf.Floor(Mathf.Log10(Mathf.Max(.001f,desired))));float step=desired/power;float distance=(step>=5?5:step>=2?2:1)*power;
            scale.sizeDelta=new Vector2(distance/metersPerUI,5);scaleText.text="0                       "+(distance>=1000?(distance/1000).ToString("0.#")+" km":distance.ToString("0")+" m");
            zoomText.text=Detail==StaffSymbolDetail.Block?"全局态势":Detail==StaffSymbolDetail.Compact?"战术态势":"部队细节";
            compassNeedle.rectTransform.localRotation=Quaternion.Euler(0,0,(old==null?0:old.mapNorthDegrees)-camera.transform.eulerAngles.z);
        }
        void ArrangeLegacyZoom()
        {
            var zoom=world.setup.zoomController;if(zoom==null)return;
            if(zoom.zoomSlider!=null){var r=zoom.zoomSlider.GetComponent<RectTransform>();r.anchorMin=r.anchorMax=r.pivot=Vector2.zero;r.anchoredPosition=new Vector2(minimap==null?20:minimap.size+52,170);r.sizeDelta=new Vector2(14,100);var handle=zoom.zoomSlider.handleRect;if(handle!=null)handle.sizeDelta=new Vector2(18,10);}
            if(zoom.resetNode!=null){var r=zoom.resetNode.GetComponent<RectTransform>();r.anchorMin=r.anchorMax=r.pivot=Vector2.zero;r.anchoredPosition=new Vector2(minimap==null?12:minimap.size+44,138);r.sizeDelta=new Vector2(64,24);var t=zoom.resetNode.GetComponentInChildren<Text>();if(t!=null){t.text="复位";t.fontSize=12;}}
        }
        public void RefreshTheme(){Skin();}
        void Skin()
        {
            if(hud==null)return;foreach(var text in hud.GetComponentsInChildren<Text>(true)){if(styled.Add(text.GetInstanceID())){text.color=StaffMapStyle.Ink;text.font=font;}}
            foreach(var image in hud.GetComponentsInChildren<Image>(true))
            {
                if(!styled.Add(image.GetInstanceID())||image.color.a<.05f||image.GetComponent<Mask>()!=null||image.GetComponent<StaffKeepColor>()!=null)continue;
                if(image.sprite!=null){if(image.sprite.name.StartsWith("Staff"))continue;if(image.name.IndexOf("icon",System.StringComparison.OrdinalIgnoreCase)>=0)continue;}
                if(image.GetComponent<Button>()!=null){image.color=StaffMapStyle.Inset;var b=image.GetComponent<Button>();var c=b.colors;c.normalColor=Color.white;c.highlightedColor=new Color(.92f,.94f,.93f);c.pressedColor=new Color(.76f,.80f,.78f);c.selectedColor=Color.white;c.disabledColor=new Color(1,1,1,.4f);b.colors=c;AddBorder(image.gameObject);}
                else if(image.GetComponent<Slider>()!=null||image.name=="Fill"||image.name=="Handle")image.color=StaffMapStyle.Blue;
                else image.color=image.name.Contains("details")||image.name.Contains("Selection")?StaffMapStyle.Inset:StaffMapStyle.Panel;
                if(image.name.Contains("panel")||image.name.Contains("overview")||image.name.Contains("operations"))AddBorder(image.gameObject);
            }
        }
        public static void AddBorder(GameObject g){var o=g.GetComponent<Outline>();if(o==null)o=g.AddComponent<Outline>();o.effectColor=StaffMapStyle.Rule;o.effectDistance=new Vector2(1,-1);o.useGraphicAlpha=true;}
        static RectTransform Rect(string name,Transform parent){var g=new GameObject(name,typeof(RectTransform));g.transform.SetParent(parent,false);return (RectTransform)g.transform;}
        Text Text(string name,Transform parent,string value,int size){var r=Rect(name,parent);var t=r.gameObject.AddComponent<Text>();t.font=font;t.fontSize=size;t.text=value;t.color=StaffMapStyle.Ink;t.raycastTarget=false;return t;}
        static void Stretch(RectTransform r){r.anchorMin=Vector2.zero;r.anchorMax=Vector2.one;r.offsetMin=r.offsetMax=Vector2.zero;}
        void OnDestroy(){if(world!=null)world.UnitRegistered-=Registered;if(labelRoot!=null)Destroy(labelRoot.gameObject);if(referenceRoot!=null)Destroy(referenceRoot);if(drawerTab!=null)Destroy(drawerTab.gameObject);}
    }
    public sealed class StaffKeepColor:MonoBehaviour{}
    public sealed class StaffDrawerHandle:MonoBehaviour,IPointerClickHandler,IBeginDragHandler,IDragHandler,IEndDragHandler
    {
        public System.Action Toggle,Release;public System.Action<float> DragDelta;public bool Dragging {get;private set;}float lastDragEnd=-1;
        public void OnPointerClick(PointerEventData e){if(!Dragging&&Time.unscaledTime-lastDragEnd>.05f)Toggle?.Invoke();}
        public void OnBeginDrag(PointerEventData e){Dragging=true;}
        public void OnDrag(PointerEventData e){var c=GetComponentInParent<Canvas>();DragDelta?.Invoke(e.delta.x/(c==null?1:Mathf.Max(.01f,c.scaleFactor)));}
        public void OnEndDrag(PointerEventData e){Dragging=false;lastDragEnd=Time.unscaledTime;Release?.Invoke();}
    }
}
