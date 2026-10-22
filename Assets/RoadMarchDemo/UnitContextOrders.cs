using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
namespace CampaignDemo
{
    [DefaultExecutionOrder(-110)]
    public sealed class UnitContextOrders:MonoBehaviour
    {
        static UnitContextOrders current;static int consumed=-1;
        public static bool Consumed {get{return consumed==Time.frameCount;}}
        public static void ConsumePointer(){consumed=Time.frameCount;}
        public static bool BlocksInput {get{return Consumed||current!=null&&current.hud!=null&&current.hud.ActiveTool!=CampaignHUD.Tool.None;}}
        public BattleWorld world;CampaignHUD hud;ArmySelection selection;
        sealed class Entry {public bool straight;public BattleUnit actor;public RecoveryPoint city;public PersonnelCarrier carrier;public BattleUnit merge;public uint revision;public Vector3 destination;public float next;public Vector3 Position {get{return merge!=null?merge.transform.position:city!=null?city.transform.position:carrier.transform.position;}}}
        readonly List<Entry> entries=new List<Entry>();
        public UnityEngine.Object EntryTarget {get;private set;}
        public int PendingCount {get{return entries.Count;}}
        BattleUnit splitActor;DetachmentKind splitKind;float splitPeople,splitCapacity;uint splitRevision;GameObject splitPanel;Text splitText;Button splitConfirm;
        public bool SplitPreviewActive {get{return splitActor!=null;}}
        public float PreviewOriginalPeople {get;private set;}
        public float PreviewDetachedPeople {get;private set;}
        public string SplitPreviewText {get{return splitText==null?"":splitText.text;}}
        public void CancelSplitPreview(){splitActor=null;if(splitPanel!=null)splitPanel.SetActive(false);}
        public void CancelSplitPreviewFor(BattleCommand command){if(splitActor!=null&&command!=null&&(command.unitId==splitActor.unitId||command.unitIds!=null&&command.unitIds.Contains(splitActor.unitId)))CancelSplitPreview();}
        bool SplitSnapshotValid(){return splitActor!=null&&Actor()==splitActor&&splitActor.ownerId==world.currentPlayer&&splitActor.CanMove&&splitActor.Personnel==splitPeople&&splitActor.Capacity==splitCapacity&&(splitActor.Motor==null||splitActor.Motor.ArrowOrderRevision==splitRevision);}
        static int SplitKey(DetachmentKind kind){return kind==DetachmentKind.Flank?1:kind==DetachmentKind.Feint?2:3;}
        public static bool SplitHotkey(KeyCode key,out DetachmentKind kind){kind=DetachmentKind.Flank;switch(key){case KeyCode.Alpha1:case KeyCode.Keypad1:return true;case KeyCode.Alpha2:case KeyCode.Keypad2:kind=DetachmentKind.Feint;return true;case KeyCode.Alpha3:case KeyCode.Keypad3:kind=DetachmentKind.Infiltrate;return true;default:return false;}}
        public bool PreviewSplit(DetachmentKind kind){
            Resolve();if(world==null||!world.Initialized||world.SimulationEnded||hud==null)return false;var actor=Actor();
            if(splitActor==actor&&splitKind==kind&&SplitSnapshotValid()){CancelSplitPreview();return SplitSelected(kind);}
            CancelSplitPreview();if(actor==null||actor.ownerId!=world.currentPlayer){hud.SetCommandFeedback("先选择己方部队");return false;}
            float share,child;var plan=TacticalDetachment.PlanSplit(actor,kind,out share,out child);if(!plan.accepted){RejectMovement(plan.reason);return false;}
            hud.CancelTool();GetComponent<LocalPrototypeOrders>()?.CancelDraft();GetComponent<LocalCoordinatedOrders>()?.CancelPicking();
            splitActor=actor;splitKind=kind;splitPeople=actor.Personnel;splitCapacity=actor.Capacity;splitRevision=actor.Motor==null?0:actor.Motor.ArrowOrderRevision;PreviewDetachedPeople=child;PreviewOriginalPeople=actor.Personnel-child;
            EnsureSplitPanel();splitPanel.SetActive(true);splitPanel.transform.SetAsLastSibling();int key=SplitKey(kind);splitText.text=actor.DisplayDesignation+" · "+(kind==DetachmentKind.Flank?"对半分兵":kind==DetachmentKind.Feint?"佯攻分队":"渗透分队")+"\n本队保留 "+PreviewOriginalPeople.ToString("0.##")+" · 分出 "+child.ToString("0.##")+"（等效兵力）\n再次按 "+key+" 确认；Esc 取消";splitConfirm.GetComponentInChildren<Text>().text="确认分兵（"+key+"）";return true;
        }
        void EnsureSplitPanel(){if(splitPanel!=null)return;splitPanel=new GameObject("分兵预览",typeof(RectTransform),typeof(Image));splitPanel.transform.SetParent(hud.transform,false);var rect=splitPanel.GetComponent<RectTransform>();rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(.5f,0);rect.anchoredPosition=new Vector2(-100,24);rect.sizeDelta=new Vector2(390,130);splitPanel.GetComponent<Image>().color=StaffMapStyle.Panel;var text=new GameObject("分兵人数预览",typeof(RectTransform),typeof(Text));text.transform.SetParent(splitPanel.transform,false);splitText=text.GetComponent<Text>();splitText.font=hud.font;splitText.fontSize=14;splitText.color=StaffMapStyle.Ink;splitText.alignment=TextAnchor.UpperLeft;splitText.raycastTarget=false;splitText.rectTransform.anchorMin=new Vector2(0,.35f);splitText.rectTransform.anchorMax=Vector2.one;splitText.rectTransform.offsetMin=new Vector2(10,0);splitText.rectTransform.offsetMax=new Vector2(-10,-8);splitConfirm=SplitButton("确认分兵",new Vector2(.03f,.05f),new Vector2(.49f,.3f),()=>PreviewSplit(splitKind));SplitButton("取消（Esc）",new Vector2(.51f,.05f),new Vector2(.97f,.3f),()=>CancelSplitPreview());}
        Button SplitButton(string title,Vector2 min,Vector2 max,UnityEngine.Events.UnityAction action){var obj=new GameObject(title,typeof(RectTransform),typeof(Image),typeof(Button));obj.transform.SetParent(splitPanel.transform,false);var rect=obj.GetComponent<RectTransform>();rect.anchorMin=min;rect.anchorMax=max;rect.offsetMin=rect.offsetMax=Vector2.zero;obj.GetComponent<Image>().color=StaffMapStyle.Inset;var label=new GameObject("Text",typeof(RectTransform),typeof(Text));label.transform.SetParent(obj.transform,false);var t=label.GetComponent<Text>();t.font=hud.font;t.fontSize=13;t.text=title;t.color=StaffMapStyle.Ink;t.alignment=TextAnchor.MiddleCenter;t.raycastTarget=false;t.rectTransform.anchorMin=Vector2.zero;t.rectTransform.anchorMax=Vector2.one;t.rectTransform.offsetMin=t.rectTransform.offsetMax=Vector2.zero;var button=obj.GetComponent<Button>();button.navigation=new Navigation{mode=Navigation.Mode.None};button.onClick.AddListener(action);return button;}
        public bool BombardAiming {get{return hud!=null&&(hud.ActiveTool==CampaignHUD.Tool.Artillery);}}
        public Vector2 NormalHotspot {get;private set;}
        public bool ShowingNormalCursor {get;private set;}
        Texture2D normalPointer,rejectedCursor;float rejectedUntil;
        public bool ShowingBlockedCursor {get{return !BombardAiming&&Time.realtimeSinceStartup<rejectedUntil;}}
        public float BlockedCursorRemaining {get{return Mathf.Max(0,rejectedUntil-Time.realtimeSinceStartup);}}
        public void RejectMovement(string reason){rejectedUntil=Time.realtimeSinceStartup+1f;Resolve();if(hud!=null)hud.SetCommandFeedback(reason);RefreshCursor(Input.mousePosition);}
        LineRenderer targetBox;Material material;float highlightUntil;RawImage cursor;Texture2D cursorTexture;bool cursorHidden,oldCursorVisible;bool nativeCursorOwned;Texture2D nativeCursorTexture;
        void Awake(){current=this;if(world==null)world=GetComponent<BattleWorld>();selection=GetComponent<ArmySelection>();}
        bool Editing(){var g=EventSystem.current==null?null:EventSystem.current.currentSelectedGameObject;return g!=null&&g.GetComponent<InputField>()!=null||LocalPvpSession.Active!=null&&LocalPvpSession.Active.KeyboardCaptured;}
        readonly List<RaycastResult> uiHits=new List<RaycastResult>();
        bool OverUI(){if(LocalMapSettings.CapturesPointer(world))return true;if(EventSystem.current==null)return false;uiHits.Clear();EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current){position=Input.mousePosition},uiHits);return uiHits.Count>0;}
        BattleUnit Actor(){var selected=selection==null?null:selection.Selected;return selected!=null?selected.GetComponent<BattleUnit>():hud==null?null:hud.SelectedUnit;}
        public void Resolve(){if(hud==null)hud=FindObjectOfType<CampaignHUD>();if(selection==null)selection=GetComponent<ArmySelection>();}
        public bool ArmBombardment()
        {
            Resolve();if(world==null||!world.Initialized||world.SimulationEnded||hud==null)return false;
            var actor=Actor();
            if(actor==null||actor.ownerId!=world.currentPlayer||!actor.OnMap||actor.GetComponent<ArtilleryBattery>()==null){hud.SetCommandFeedback("先选中己方炮兵，再按 F");return false;}
            if(!hud.Inspect)world.setup.modes.SetMode(MouseCommandMode.Interaction);
            if(hud.SelectedUnit!=actor)hud.SelectUnit(actor);
            hud.ToggleTool(CampaignHUD.Tool.Artillery);return true;
        }
        void Update()
        {
            Resolve();if(world==null||!world.Initialized||world.SimulationEnded||hud==null){CancelSplitPreview();entries.Clear();return;}
            TickEntries();TickSplitSelection();if(SplitPreviewActive&&!SplitSnapshotValid())CancelSplitPreview();
            if(SplitPreviewActive&&(Input.GetKeyDown(KeyCode.Escape)||Input.GetMouseButtonDown(1))){CancelSplitPreview();ConsumePointer();return;}
            if(SplitPreviewActive&&(Input.GetKeyDown(KeyCode.T)||Input.GetKeyDown(KeyCode.F)||Input.GetMouseButtonDown(0)&&!OverUI()))CancelSplitPreview();
            if(!Editing()){
                foreach(var key in new[]{KeyCode.Alpha1,KeyCode.Alpha2,KeyCode.Alpha3,KeyCode.Keypad1,KeyCode.Keypad2,KeyCode.Keypad3}){DetachmentKind kind;if(Input.GetKeyDown(key)&&SplitHotkey(key,out kind)){PreviewSplit(kind);consumed=Time.frameCount;return;}}


                if(Input.GetKeyDown(KeyCode.F)){ArmBombardment();consumed=Time.frameCount;return;}
            }
            if(BombardAiming&&(Input.GetMouseButtonDown(1)||Input.GetKeyDown(KeyCode.Escape))){hud.CancelTool();consumed=Time.frameCount;return;}
            if(Input.GetKeyDown(KeyCode.Escape)){CancelEntries(true);return;}
            if(Consumed||hud.ActiveTool!=CampaignHUD.Tool.None||Editing()||!Input.GetMouseButtonDown(0)||OverUI())return;
            if(TryClick(Input.mousePosition))consumed=Time.frameCount;
        }
        BattleUnit awaitingSplit,mergingActor,mergingTarget;float splitUntil,mergeUntil;
        public bool SplitSelected(DetachmentKind kind){Resolve();var actor=Actor();if(actor==null||actor.ownerId!=world.currentPlayer){hud.SetCommandFeedback("先选中一支己方部队");return false;}hud.CancelTool();CancelEntries(false);var command=kind==DetachmentKind.Flank?BattleCommandKind.SplitFlank:kind==DetachmentKind.Feint?BattleCommandKind.SplitFeint:BattleCommandKind.SplitInfiltrate;var result=world.GetComponent<BattleCommandService>().Submit(new BattleCommand{playerId=world.currentPlayer,unitId=actor.unitId,kind=command});if(result.accepted||result.pending){world.setup.modes.SetMode(MouseCommandMode.FreeMarch);if(result.pending){awaitingSplit=actor;splitUntil=Time.realtimeSinceStartup+5;}hud.SetCommandFeedback("分出部队已选中，使用直线行军");return true;}RejectMovement(result.reason);return false;}
        void TickSplitSelection(){if(mergingActor!=null&&mergingTarget!=null){if(!mergingActor.gameObject.activeInHierarchy){hud.SelectUnit(mergingTarget);mergingActor=mergingTarget=null;}else if(Time.realtimeSinceStartup>mergeUntil)mergingActor=mergingTarget=null;}if(awaitingSplit==null)return;if(Time.realtimeSinceStartup>splitUntil){awaitingSplit=null;return;}foreach(var u in world.units)if(u!=null&&u.gameObject.activeInHierarchy&&TacticalDetachment.Family(awaitingSplit,u)){hud.SelectUnit(u);awaitingSplit=null;return;}}
        public bool QueueMerge(BattleUnit actor,BattleUnit target){Resolve();if(NetworkTacticalOrders.Forward(world,actor,"merge",out var forwarded,target:target))return forwarded;var e=new Entry{straight=NetworkTacticalOrders.Straight(world),actor=actor,merge=target};string reason;if(!Validate(e,out reason)){hud.SetCommandFeedback(reason);return false;}entries.RemoveAll(x=>x.actor==actor);EntryTarget=target;highlightUntil=Time.time+2;entries.Add(e);if(Execute(e)){entries.Remove(e);return true;}if(!Approach(e)){entries.Remove(e);return false;}return true;}
        public bool TryClick(Vector2 screen)
        {
            Resolve();var actor=Actor();if(actor==null||!actor.OnMap||actor.ownerId!=world.currentPlayer)return false;
            float nearest=world.setup.settings.selectionPixels;RecoveryPoint city=null;PersonnelCarrier carrier=null;
            foreach(var u in world.units)if(u!=null&&u.OnMap&&u.gameObject.activeInHierarchy&&TacticalDetachment.Family(actor,u)){var at=world.setup.mapCamera.WorldToScreenPoint(u.transform.position);if(at.z>0&&Vector2.Distance(screen,at)<nearest){QueueMerge(actor,u);return true;}}
            foreach(var p in world.points)if(p!=null&&p.kind==FacilityKind.RecoveryPoint){var at=world.setup.mapCamera.WorldToScreenPoint(p.transform.position);float d=Vector2.Distance(screen,at);if(at.z>0&&d<nearest){nearest=d;city=p;carrier=null;}}
            foreach(var u in world.units)if(u!=null&&u!=actor&&u.OnMap&&u.ownerId==actor.ownerId){var c=u.GetComponent<PersonnelCarrier>();if(c==null)continue;var at=world.setup.mapCamera.WorldToScreenPoint(u.transform.position);float d=Vector2.Distance(screen,at);if(at.z>0&&d<nearest){nearest=d;carrier=c;city=null;}}
            if(city==null&&carrier==null)return false;if(carrier!=null)return SelectCarrierOnClick(actor,carrier);QueueEntry(actor,city,null);return true;
        }
        public bool SelectCarrierOnClick(BattleUnit actor,PersonnelCarrier carrier)
        {
            Resolve();if(carrier==null||!carrier.Unit.OnMap||carrier.Unit.ownerId!=world.currentPlayer)return false;
            // Boarding and selecting the vehicle are independent; an empty vehicle is selectable.
            if(actor!=null&&actor!=carrier.Unit&&actor.combatDefinition!=null){var role=actor.combatDefinition.role;
                if(role==UnitRole.Infantry||role==UnitRole.MountainInfantry||role==UnitRole.Scout||ArtilleryBattery.IsArtilleryRole(role)||role==UnitRole.Engineer)QueueEntry(actor,null,carrier);
            }
            hud.SelectUnit(carrier.Unit);return true;
        }
        bool Validate(Entry e,out string reason)
        {
            reason="目标不可进入";var u=e.actor;if(u==null||!u.Alive||!u.OnMap||!u.CanMove||u.ownerId!=NetworkTacticalOrders.Player(world,u.ownerId))return false;
            if(e.merge!=null){if(!TacticalDetachment.Family(u,e.merge)||!e.merge.CanMove||!e.merge.gameObject.activeInHierarchy){reason="合并目标不可用";return false;}return true;}
            if(e.city!=null){if(e.city.kind!=FacilityKind.RecoveryPoint)return false;int count=0;foreach(var other in world.units)if(other!=null&&other.Alive&&other.Point==e.city&&other.ownerId==u.ownerId)count++;if(count>=e.city.capacity){reason="据点驻军已满";return false;}return true;}
            var c=e.carrier;if(c==null||!c.Unit.OnMap||c.Unit.ownerId!=u.ownerId||c.Unit.Working||u.Working||c.Unit.Point!=null||u.Point!=null)return false;
            var role=u.combatDefinition.role;if(role!=UnitRole.Infantry&&role!=UnitRole.MountainInfantry&&role!=UnitRole.Scout&&!ArtilleryBattery.IsArtilleryRole(role)&&role!=UnitRole.Engineer){reason="该兵种无法搭乘运兵车";return false;}
            if(c.Used+u.Personnel>c.PassengerCapacity){reason="运兵车容量不足";return false;}return true;
        }
        public bool QueueEntry(BattleUnit actor,RecoveryPoint city,PersonnelCarrier carrier)
        {
            Resolve();if(NetworkTacticalOrders.Forward(world,actor,"entry",out var forwarded,target:carrier==null?null:carrier.Unit,city:city))return forwarded;var e=new Entry{straight=NetworkTacticalOrders.Straight(world),actor=actor,city=city,carrier=carrier};string reason;
            if(!Validate(e,out reason)){RejectMovement(reason);return false;}
            world.GetComponent<RoadMarchController>()?.CancelDraft();world.GetComponent<FreeMarchController>()?.CancelDraft();world.GetComponent<GroupMarchController>()?.Cancel();world.GetComponent<LocalTacticalBattle>()?.CancelPointerDraft();entries.RemoveAll(x=>x.actor==actor);EntryTarget=city!=null?(UnityEngine.Object)city:carrier;highlightUntil=Time.time+2;
            entries.Add(e);if(Execute(e)){entries.Remove(e);return true;}
            if(!Approach(e)){entries.Remove(e);return false;}hud.SetCommandFeedback(city!=null?"正在靠近据点":"正在靠近运兵车");return true;
        }
        bool Execute(Entry e)
        {
            float range=e.city!=null||e.merge!=null?UnitRanges.Interaction(e.actor):e.carrier.BoardingRadius;
            if(Vector2.Distance(e.actor.transform.position,e.Position)>range*.98f)return false;
            var command=new BattleCommand{playerId=e.actor.ownerId,unitId=e.actor.unitId,kind=e.merge!=null?BattleCommandKind.MergeDetachment:e.city!=null?BattleCommandKind.EnterPoint:BattleCommandKind.BoardCarrier,pointId=e.city==null?null:e.city.pointId,targetUnitId=e.merge!=null?e.merge.unitId:e.carrier==null?null:e.carrier.Unit.unitId};
            var result=world.GetComponent<BattleCommandService>().Submit(command);hud.SetCommandFeedback(result.accepted?(e.merge!=null?"已合并分兵部队":e.city!=null?"已下达入城指令":"已搭乘运兵车"):result.reason);if(result.pending&&e.merge!=null){mergingActor=e.actor;mergingTarget=e.merge;mergeUntil=Time.realtimeSinceStartup+5;}highlightUntil=Time.time+1.5f;return result.accepted||result.pending;
        }
        bool Approach(Entry e)
        {
            e.destination=e.Position;var result=world.GetComponent<BattleCommandService>().Submit(new BattleCommand{playerId=e.actor.ownerId,unitId=e.actor.unitId,kind=e.straight?BattleCommandKind.StraightDestination:BattleCommandKind.FreeDestination,destination=e.destination});
            e.revision=e.actor.Motor.ArrowOrderRevision;e.next=Time.time+.5f;if(!result.accepted&&!result.pending)hud.SetCommandFeedback(string.IsNullOrEmpty(result.reason)?"无法到达目标":result.reason);return result.accepted||result.pending;
        }
        public void TickEntries()
        { if(!LocalPvpSession.Simulate)return;
            for(int i=entries.Count-1;i>=0;i--){var e=entries[i];string reason;if(!Validate(e,out reason)){entries.RemoveAt(i);continue;}
                if(e.actor.Motor.ArrowOrderRevision!=e.revision&&e.revision!=0){entries.RemoveAt(i);continue;}
                if(Execute(e)){entries.RemoveAt(i);continue;}
                if(Time.time<e.next)continue;e.next=Time.time+.5f;
                if(e.actor.Motor.IsMoving&&Vector2.Distance(e.destination,e.Position)<.3f)continue;
                float range=e.city!=null||e.merge!=null?UnitRanges.Interaction(e.actor):e.carrier.BoardingRadius;
                if(Vector2.Distance(e.actor.transform.position,e.Position)<=range){hud.SetCommandFeedback("当前状态或据点条件不允许进入");entries.RemoveAt(i);continue;}
                if(!e.actor.Motor.IsMoving){hud.SetCommandFeedback("靠近目标的移动已停止");entries.RemoveAt(i);continue;}if(!Approach(e))entries.RemoveAt(i);
            }
        }
        public void CancelEntriesFor(BattleUnit actor,bool stop){for(int i=entries.Count-1;i>=0;i--)if(entries[i].actor==actor){if(stop)actor.Motor?.Stop();entries.RemoveAt(i);}if(entries.Count==0)EntryTarget=null;}
        public void CancelEntries(bool stop){foreach(var e in entries)if(stop&&e.actor!=null&&e.actor.Motor!=null)e.actor.Motor.Stop();entries.Clear();EntryTarget=null;}
        void EnsureCursor()
        {
            if(cursor!=null||hud==null)return;cursorTexture=new Texture2D(40,40,TextureFormat.RGBA32,false){filterMode=FilterMode.Point};var pixels=new Color[1600];
            for(int y=0;y<40;y++)for(int x=0;x<40;x++){float dx=x-19.5f,dy=y-19.5f,d=Mathf.Sqrt(dx*dx+dy*dy);bool mark=Mathf.Abs(d-10)<1.3f||(Mathf.Abs(dx)<1.2f&&Mathf.Abs(dy)>5&&Mathf.Abs(dy)<18)||(Mathf.Abs(dy)<1.2f&&Mathf.Abs(dx)>5&&Mathf.Abs(dx)<18)||d<1.6f;if(mark)pixels[y*40+x]=new Color(.78f,.10f,.07f,1);}
            foreach(var old in hud.GetComponentInParent<Canvas>().GetComponentsInChildren<RawImage>(true))if(old.name=="Red bombardment cursor"){old.gameObject.SetActive(false);Destroy(old.gameObject);}cursorTexture.SetPixels(pixels);cursorTexture.Apply();rejectedCursor=new Texture2D(32,32,TextureFormat.RGBA32,false){filterMode=FilterMode.Point};var cross=new Color[1024];for(int y=0;y<32;y++)for(int x=0;x<32;x++){float dx=x-15.5f,dy=y-15.5f;float d=Mathf.Min(Mathf.Abs(dx-dy),Mathf.Abs(dx+dy));if(Mathf.Abs(dx)<10&&Mathf.Abs(dy)<10&&d<3)cross[y*32+x]=d<1.8f?new Color(.82f,.10f,.06f,1):new Color(1,.95f,.89f,.9f);}rejectedCursor.SetPixels(cross);rejectedCursor.Apply();normalPointer=Resources.Load<Texture2D>("StaffCursorPointer");if(normalPointer!=null)NormalHotspot=PointerTip(normalPointer);var g=new GameObject("Red bombardment cursor",typeof(RectTransform),typeof(RawImage),typeof(StaffKeepColor));g.transform.SetParent(hud.GetComponentInParent<Canvas>().transform,false);cursor=g.GetComponent<RawImage>();cursor.texture=cursorTexture;cursor.raycastTarget=false;cursor.rectTransform.sizeDelta=Vector2.one*40;g.SetActive(false);
        }
        public static Vector2 PointerTip(Texture2D texture)
        {
            // Unity texture Y points up. The top-left outline apex is the click point.
            for(int y=texture.height-1;y>=0;y--){float sum=0;int count=0;for(int x=0;x<texture.width;x++)if(texture.GetPixel(x,y).a>.55f){sum+=x+.5f;count++;}if(count>0)return new Vector2(sum/count/texture.width,(y+.5f)/texture.height);}
            return new Vector2(0,1);
        }
        public void RefreshCursor(Vector2 screen)
        {
            EnsureCursor();if(cursor==null)return;bool aiming=BombardAiming&&!OverUI();bool rejected=ShowingBlockedCursor||(aiming&&!hud.PreviewValid);bool validAim=aiming&&!rejected;bool show=aiming||rejected||normalPointer!=null;
            if(!Application.isFocused&&!Application.isBatchMode)show=false;
            ShowingNormalCursor=show&&!aiming&&!rejected;cursor.gameObject.SetActive(show&&Application.isBatchMode);
            if(show){var canvas=cursor.GetComponentInParent<Canvas>();Vector2 local;RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)canvas.transform,screen,canvas.renderMode==RenderMode.ScreenSpaceOverlay?null:canvas.worldCamera,out local);cursor.rectTransform.anchorMin=cursor.rectTransform.anchorMax=new Vector2(.5f,.5f);cursor.rectTransform.pivot=aiming||rejected?Vector2.one*.5f:NormalHotspot;cursor.rectTransform.anchoredPosition=local;cursor.texture=validAim?cursorTexture:rejected?rejectedCursor:normalPointer;var size=validAim?Vector2.one*40:rejected?Vector2.one*32:new Vector2(normalPointer.width,normalPointer.height);cursor.rectTransform.sizeDelta=size/Mathf.Max(.01f,canvas.scaleFactor);cursor.transform.SetAsLastSibling();}
            // Replace the actual cursor; live play never overlays a second UI pointer.
            if(!Application.isBatchMode){
                if(show){var texture=validAim?cursorTexture:rejected?rejectedCursor:normalPointer;var hotspot=validAim?new Vector2(20,20):rejected?new Vector2(16,16):new Vector2(NormalHotspot.x*normalPointer.width,(1-NormalHotspot.y)*normalPointer.height);
                    if(!nativeCursorOwned){oldCursorVisible=Cursor.visible;nativeCursorOwned=true;}
                    if(nativeCursorTexture!=texture){Cursor.SetCursor(texture,hotspot,CursorMode.Auto);nativeCursorTexture=texture;}Cursor.visible=true;
                }else RestoreNativeCursor();
            }

        }
        void LateUpdate()
        {
            RefreshCursor(Input.mousePosition);if(EntryTarget==null||entries.Count==0&&Time.time>highlightUntil){if(targetBox!=null)targetBox.enabled=false;return;}
            var component=EntryTarget as Component;if(component==null)return;
            if(targetBox==null){var g=new GameObject("Entry target selection frame");g.transform.SetParent(transform,false);targetBox=g.AddComponent<LineRenderer>();material=new Material(Shader.Find("Sprites/Default"));targetBox.sharedMaterial=material;targetBox.loop=true;targetBox.useWorldSpace=true;targetBox.positionCount=4;targetBox.sortingOrder=35;}
            var camera=world.setup.mapCamera;float pixel=2*camera.orthographicSize/Mathf.Max(1,camera.pixelHeight),half=pixel*22;var at=component.transform.position;targetBox.enabled=true;targetBox.startWidth=targetBox.endWidth=pixel*1.5f;targetBox.startColor=targetBox.endColor=StaffMapStyle.Faction(world.currentPlayer==BattleFactions.BluePlayerId);targetBox.SetPositions(new[]{at+new Vector3(-half,-half),at+new Vector3(-half,half),at+new Vector3(half,half),at+new Vector3(half,-half)});
        }
        void RestoreNativeCursor(){if(!nativeCursorOwned)return;Cursor.SetCursor(null,Vector2.zero,CursorMode.Auto);Cursor.visible=oldCursorVisible;nativeCursorTexture=null;nativeCursorOwned=false;}
        void OnApplicationFocus(bool focused){if(!focused)RestoreNativeCursor();}
        void OnDisable(){CancelSplitPreview();RestoreNativeCursor();CancelEntries(false);if(cursor!=null)cursor.gameObject.SetActive(false);if(targetBox!=null)targetBox.enabled=false;if(cursorHidden){Cursor.visible=oldCursorVisible;cursorHidden=false;}}
        void OnDestroy(){if(splitPanel!=null)Destroy(splitPanel);RestoreNativeCursor();if(cursor!=null)Destroy(cursor.gameObject);if(cursorTexture!=null)Destroy(cursorTexture);if(rejectedCursor!=null)Destroy(rejectedCursor);if(material!=null)Destroy(material);if(targetBox!=null)Destroy(targetBox.gameObject);if(current==this)current=null;}
    }
}




