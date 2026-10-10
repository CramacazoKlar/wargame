using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
namespace CampaignDemo
{
    [DefaultExecutionOrder(240)]
    public sealed class CombinedArmsHUD : MonoBehaviour
    {
        public BattleHUD hud;
        CombinedArmsWorld system;
        Button fire,board,build,dismantle,cancel,support,vehicleEscape,vehicleEnter;
        GameObject passengersPanel;
        Text message;
        readonly List<Button> passengerButtons=new List<Button>();
        BattleCommandKind pending;BattleUnit pendingUnit;string pendingPlayer;bool targeting;
        LineRenderer preview;
        readonly Dictionary<BattleUnit,LineRenderer> boardingRanges=new Dictionary<BattleUnit,LineRenderer>();
        Material material;
        public bool CapturingInput {get{return targeting;}}
        BattleWorld World {get{return hud.world;}}
        void Start()
        {
            if(hud==null)hud=GetComponent<BattleHUD>();if(hud==null){enabled=false;return;}
            system=World.GetComponent<CombinedArmsWorld>();if(system==null){enabled=false;return;}
            var panel=transform.Find("Unit and facility details");if(panel==null){enabled=false;return;}
            fire=Button(panel,"发射（点击目标）",new Vector2(-61,-230),new Vector2(116,30),()=>Target(BattleCommandKind.ArtilleryFire));
            board=Button(panel,"上车",new Vector2(61,-230),new Vector2(116,30),Board);
            build=Button(panel,"修建工事",new Vector2(-61,-230),new Vector2(116,30),()=>Target(BattleCommandKind.BuildFortification));
            dismantle=Button(panel,"拆除工事",new Vector2(-61,-268),new Vector2(116,30),()=>Target(BattleCommandKind.DismantleFortification));
            cancel=Button(panel,"取消施工",new Vector2(61,-268),new Vector2(116,30),()=>Send(new BattleCommand{kind=BattleCommandKind.CancelEngineering,unitId=hud.SelectedUnit.unitId}));
            support=Button(transform,"炮击支援",Vector2.zero,new Vector2(150,32),()=>Target(BattleCommandKind.ArtillerySupport));var rect=support.GetComponent<RectTransform>();rect.anchorMin=rect.anchorMax=Vector2.one;rect.anchoredPosition=new Vector2(-370,-25);
            passengersPanel=UI("车内乘员",panel,new Vector2(0,-175),new Vector2(245,235));
            vehicleEscape=Button(panel,"逃跑",new Vector2(-61,-38),new Vector2(112,30),()=>Send(new BattleCommand{kind=BattleCommandKind.Escape,unitId=hud.SelectedUnit.unitId}));
            vehicleEnter=Button(panel,"进入恢复点",new Vector2(61,-38),new Vector2(112,30),()=>{foreach(var p in World.points)if(World.CanEnter(hud.SelectedUnit,p)){Send(new BattleCommand{kind=BattleCommandKind.EnterPoint,unitId=hud.SelectedUnit.unitId,pointId=p.pointId});break;}});
            passengersPanel.AddComponent<Image>().color=new Color(.2f,.15f,.09f,.98f);
            var viewport=UI("Viewport",passengersPanel.transform,Vector2.zero,new Vector2(245,235));viewport.AddComponent<Image>().color=Color.white;viewport.AddComponent<Mask>().showMaskGraphic=false;
            var content=UI("Content",viewport.transform,Vector2.zero,new Vector2(240,235)).GetComponent<RectTransform>();content.anchorMin=new Vector2(.5f,1);content.anchorMax=new Vector2(.5f,1);content.pivot=new Vector2(.5f,1);content.anchoredPosition=Vector2.zero;
            var scroll=passengersPanel.AddComponent<ScrollRect>();scroll.viewport=viewport.GetComponent<RectTransform>();scroll.content=content;scroll.horizontal=false;scroll.movementType=ScrollRect.MovementType.Clamped;
            message=Label(transform,"",new Vector2(0,105),new Vector2(700,30));message.rectTransform.anchorMin=message.rectTransform.anchorMax=new Vector2(.5f,0);
            material=new Material(Shader.Find("Sprites/Default"));preview=Circle("炮击或施工目标");
        }
        public string Description(BattleUnit u)
        {
            if(u==null||u.ownerId!=World.currentPlayer)return "";var s="";var battery=u.GetComponent<ArtilleryBattery>();var carrier=u.GetComponent<PersonnelCarrier>();
            if(battery!=null)s+="\n炮击："+(battery.Ready?"已就绪":battery.Remaining>0?"冷却 "+battery.Remaining.ToString("0.0")+"秒":"当前状态不可发射")+"  射程 "+(battery.definition==null?0:battery.definition.range).ToString("0.0");
            if(carrier!=null)s+="\n血量："+carrier.Health.ToString("0")+" / "+carrier.maxHealth+"\n载员："+carrier.Used+" / "+carrier.capacity+"  速度 "+(carrier.SpeedMultiplier*100).ToString("0")+"%";
            if(u.Carrier!=null)s+="\n乘坐："+u.Carrier.Unit.DisplayDesignation;
            if(u.Working)s+="\n施工剩余："+system.WorkRemaining(u).ToString("0.0")+"秒";
            return s;
        }
        void Target(BattleCommandKind kind){pending=kind;pendingUnit=hud.SelectedUnit;pendingPlayer=World.currentPlayer;targeting=true;message.text=kind==BattleCommandKind.DismantleFortification?"点击要拆除的工事；右键或 Esc 取消":"点击地图选择目标；右键或 Esc 取消";}
        void Send(BattleCommand c){var result=hud.actions.Execute(c);message.text=result.accepted?"操作已完成":result.reason;}
        PersonnelCarrier Nearby(BattleUnit unit)
        {
            if(unit==null||!unit.OnMap||unit.Working||unit.Point!=null)return null;var role=unit.combatDefinition.role;if(role!=UnitRole.Infantry&&role!=UnitRole.MountainInfantry&&role!=UnitRole.Scout&&role!=UnitRole.Artillery&&role!=UnitRole.Engineer)return null;
            PersonnelCarrier best=null;float nearest=float.MaxValue;
            foreach(var u in World.units)if(u!=null&&u.ownerId==unit.ownerId&&u.OnMap){var c=u.GetComponent<PersonnelCarrier>();if(c==null||c.Unit.Working||c.Unit.Point!=null||c.Used+Mathf.CeilToInt(unit.Personnel)>c.capacity)continue;float d=Vector2.Distance(unit.transform.position,c.transform.position);if(d<=c.boardingRadius&&d<nearest){nearest=d;best=c;}}return best;
        }
        void Board(){var c=Nearby(hud.SelectedUnit);if(c!=null)Send(new BattleCommand{kind=BattleCommandKind.BoardCarrier,unitId=hud.SelectedUnit.unitId,targetUnitId=c.Unit.unitId});}
        void Update()
        {
            if(system==null||!World.Initialized||message==null)return;
            if(!targeting)return;
            if(World.currentPlayer!=pendingPlayer||World.setup.modes.CurrentMode!=MouseCommandMode.Interaction||Input.GetMouseButtonDown(1)||Input.GetKeyDown(KeyCode.Escape)||(pending!=BattleCommandKind.ArtillerySupport&&(pendingUnit==null||!pendingUnit.OnMap))){targeting=false;preview.enabled=false;message.text="已取消选点";return;}
            var p=World.setup.mapCamera.ScreenToWorldPoint(new Vector3(Input.mousePosition.x,Input.mousePosition.y,-World.setup.mapCamera.transform.position.z));p.z=0;
            float radius=pending==BattleCommandKind.ArtillerySupport?system.supportDefinition.blastRadius:pending==BattleCommandKind.ArtilleryFire?pendingUnit.GetComponent<ArtilleryBattery>().definition.blastRadius:system.engineering.fortificationRadius;
            Draw(preview,p,radius,new Color(1,.65f,.2f));
            if(Input.GetMouseButtonDown(0)&&(EventSystem.current==null||!EventSystem.current.IsPointerOverGameObject())){var result=hud.actions.Execute(new BattleCommand{kind=pending,unitId=pendingUnit==null?null:pendingUnit.unitId,destination=p});message.text=result.accepted?"命令已接受":result.reason;if(result.accepted){targeting=false;preview.enabled=false;}}
        }
        void LateUpdate()
        {
            if(system==null||message==null)return;bool inspect=World.setup.modes.CurrentMode==MouseCommandMode.Interaction;
            var u=hud.SelectedUnit;bool own=inspect&&u!=null&&u.Alive&&u.ownerId==World.currentPlayer;var c=own?u.GetComponent<PersonnelCarrier>():null;var battery=own?u.GetComponent<ArtilleryBattery>():null;
            fire.gameObject.SetActive(own&&battery!=null);fire.interactable=battery!=null&&battery.Ready;
            board.gameObject.SetActive(own&&Nearby(u)!=null);build.gameObject.SetActive(own&&u.OnMap&&u.combatDefinition.role==UnitRole.Engineer&&!u.Working);
            dismantle.gameObject.SetActive(own&&u.OnMap&&c==null&&!u.Working&&u.State==ArmyBattleState.Deployed&&u.GetComponent<TransportTruck>()==null);
            cancel.gameObject.SetActive(own&&u.Working);support.gameObject.SetActive(inspect&&system.supportEnabled&&system.supportDefinition!=null);support.interactable=system.SupportRemaining(World.currentPlayer)<=0;
            support.GetComponentInChildren<Text>().text=system.SupportRemaining(World.currentPlayer)>0?"炮击支援 "+Mathf.CeilToInt(system.SupportRemaining(World.currentPlayer))+"秒":"炮击支援 · 就绪";
            message.gameObject.SetActive(inspect);passengersPanel.SetActive(c!=null&&u.OnMap);
            vehicleEscape.gameObject.SetActive(c!=null&&u.OnMap&&u.State==ArmyBattleState.Engaged);
            bool canEnter=false;if(c!=null)foreach(var point in World.points)if(World.CanEnter(u,point)){canEnter=true;break;}vehicleEnter.gameObject.SetActive(c!=null&&canEnter);
            foreach(var unit in World.units)if(unit!=null&&unit.GetComponent<PersonnelCarrier>()!=null){if(!boardingRanges.TryGetValue(unit,out var line)){line=Circle("运兵车上车范围");boardingRanges.Add(unit,line);}line.enabled=inspect&&unit.ownerId==World.currentPlayer&&unit.OnMap;if(line.enabled)Draw(line,unit.transform.position,unit.GetComponent<PersonnelCarrier>().boardingRadius,new Color(.4f,1,.45f));}
            if(c!=null){var content=passengersPanel.GetComponent<ScrollRect>().content;while(passengerButtons.Count<c.Passengers.Count){int index=passengerButtons.Count;var b=Button(content,"",Vector2.zero,new Vector2(234,32),()=>Unload(index));passengerButtons.Add(b);}content.sizeDelta=new Vector2(240,Mathf.Max(235,c.Passengers.Count*38));
                for(int i=0;i<passengerButtons.Count;i++){var b=passengerButtons[i];b.gameObject.SetActive(i<c.Passengers.Count);if(i>=c.Passengers.Count)continue;var rect=b.GetComponent<RectTransform>();rect.anchorMin=rect.anchorMax=new Vector2(.5f,1);rect.anchoredPosition=new Vector2(0,-20-i*38);b.GetComponentInChildren<Text>().text=c.Passengers[i].DisplayDesignation+" · 下车";}
            }
        }
        void Unload(int index){var c=hud.SelectedUnit==null?null:hud.SelectedUnit.GetComponent<PersonnelCarrier>();if(c==null||index>=c.Passengers.Count)return;Send(new BattleCommand{kind=BattleCommandKind.UnloadCarrier,unitId=c.Unit.unitId,targetUnitId=c.Passengers[index].unitId});}
        LineRenderer Circle(string name){var g=new GameObject(name);var line=g.AddComponent<LineRenderer>();line.loop=true;line.useWorldSpace=true;line.sharedMaterial=material;line.startWidth=line.endWidth=.025f;line.sortingOrder=21;line.enabled=false;return line;}
        void Draw(LineRenderer line,Vector3 p,float radius,Color color){line.enabled=true;line.positionCount=64;line.startColor=line.endColor=color;for(int i=0;i<64;i++){float a=i*Mathf.PI*2/64;line.SetPosition(i,p+new Vector3(Mathf.Cos(a)*radius,Mathf.Sin(a)*radius));}}
        GameObject UI(string name,Transform parent,Vector2 p,Vector2 size){var g=new GameObject(name,typeof(RectTransform));g.transform.SetParent(parent,false);var r=g.GetComponent<RectTransform>();r.anchorMin=r.anchorMax=new Vector2(.5f,.5f);r.anchoredPosition=p;r.sizeDelta=size;return g;}
        Text Label(Transform parent,string title,Vector2 p,Vector2 size){var t=UI("Label",parent,p,size).AddComponent<Text>();t.font=hud.font;t.fontSize=14;t.text=title;t.color=new Color(.95f,.87f,.68f);t.alignment=TextAnchor.MiddleCenter;t.raycastTarget=false;return t;}
        Button Button(Transform parent,string title,Vector2 p,Vector2 size,UnityEngine.Events.UnityAction action){var g=UI(title,parent,p,size);g.AddComponent<Image>().color=new Color(.38f,.28f,.16f);var b=g.AddComponent<Button>();Label(g.transform,title,Vector2.zero,size);b.onClick.AddListener(action);return b;}
        void OnDestroy(){if(preview!=null)Destroy(preview.gameObject);foreach(var line in boardingRanges.Values)if(line!=null)Destroy(line.gameObject);if(material!=null)Destroy(material);}
    }
}

