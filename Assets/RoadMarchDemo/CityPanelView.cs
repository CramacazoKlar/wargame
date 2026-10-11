using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
namespace CampaignDemo
{
    public sealed class CityPanelView : MonoBehaviour
    {
        CampaignHUD hud;CityManagement service;RecoveryPoint city;string signature;
        UnitRole selectedRole=UnitRole.Infantry;bool truck {get{return selectedRole==UnitRole.Logistics;}}bool showGarrison,expandedOrders;ArmyEchelon level;int count=1;
        Text summary,productionInfo,buildingInfo;Button upgrade,build,typeButton,grade,quantity,cancelAll,ordersToggle;
        readonly System.Collections.Generic.List<Action> updates=new System.Collections.Generic.List<Action>();
        public static CityPanelView Attach(CampaignHUD hud,Transform parent,CityManagement service,RecoveryPoint city)
        {
            var obj=new GameObject("City management",typeof(RectTransform));obj.transform.SetParent(parent,false);
            var layout=obj.AddComponent<VerticalLayoutGroup>();layout.childControlHeight=layout.childControlWidth=true;layout.childForceExpandHeight=false;layout.spacing=8;
            var view=obj.AddComponent<CityPanelView>();view.hud=hud;view.service=service;view.city=city;view.Refresh();return view;
        }
        Transform Section(string title)
        {
            var obj=new GameObject(title,typeof(RectTransform));obj.transform.SetParent(transform,false);obj.AddComponent<Image>().color=new Color(.17f,.14f,.1f,.7f);
            var layout=obj.AddComponent<VerticalLayoutGroup>();layout.padding=new RectOffset(5,5,4,4);layout.spacing=4;layout.childControlHeight=layout.childControlWidth=true;layout.childForceExpandHeight=false;
            Label(obj.transform,title,18,true);return obj.transform;
        }
        Text Label(Transform parent,string value,float height=32,bool header=false)
        {
            var obj=new GameObject(value,typeof(RectTransform),typeof(Text),typeof(LayoutElement));obj.transform.SetParent(parent,false);
            obj.GetComponent<LayoutElement>().preferredHeight=height;var text=obj.GetComponent<Text>();text.font=hud.font;text.fontSize=header?13:12;text.text=value;text.color=new Color(.95f,.9f,.76f);text.raycastTarget=false;text.alignment=TextAnchor.UpperLeft;return text;
        }
        Button Button(Transform parent,string title,Action action)
        {
            var obj=new GameObject(title,typeof(RectTransform),typeof(Image),typeof(Button),typeof(LayoutElement));obj.transform.SetParent(parent,false);obj.GetComponent<LayoutElement>().preferredHeight=26;obj.GetComponent<Image>().color=new Color(.38f,.29f,.17f);
            var label=Label(obj.transform,title);Destroy(label.GetComponent<LayoutElement>());label.rectTransform.anchorMin=Vector2.zero;label.rectTransform.anchorMax=Vector2.one;label.rectTransform.offsetMin=new Vector2(3,0);label.rectTransform.offsetMax=new Vector2(-3,0);label.alignment=TextAnchor.MiddleCenter;label.resizeTextForBestFit=true;label.resizeTextMinSize=9;label.resizeTextMaxSize=12;
            var button=obj.GetComponent<Button>();button.onClick.AddListener(()=>{hud.CancelTool();action();});return button;
        }
        void Send(BattleCommand command){hud.Send(command);Refresh();}
        // CampaignHUD owns periodic refresh; callbacks also refresh immediately.
        public void Refresh()
        {
            if(hud==null||service==null||city==null)return;
            var snapshot=service.Snapshot(city,service.world.currentPlayer);if(snapshot==null){gameObject.SetActive(false);return;}
            if(!gameObject.activeSelf)gameObject.SetActive(true);
            string key=showGarrison+"|"+expandedOrders+"|"+string.Join(",",snapshot.garrison.Select(u=>u.unitId))+"|"+string.Join(",",snapshot.nearby.Select(u=>u.unitId))+"|"+string.Join(",",snapshot.orders.Select(j=>j.id));
            if(signature!=key){signature=key;Rebuild(snapshot);}
            summary.text=snapshot.name+" · "+BattleFactions.Name(snapshot.owner)+"\n库存："+snapshot.stock.ToString("0")+"  驻军："+snapshot.garrison.Length+" / "+snapshot.capacity;
            buildingInfo.text="兵工厂："+(snapshot.factory?"已建成":snapshot.building.Contains("升级")?"建造中":"未建")+"\n"+snapshot.building;
            var quote=service.Upgrade(city,snapshot.owner);upgrade.interactable=quote.allowed;upgrade.GetComponentInChildren<Text>().text=snapshot.factory?"兵工厂已建成":"升级兵工厂（"+(service.Logistics==null?0:service.Logistics.factoryBuildCost).ToString("0")+"资源）";
            upgrade.gameObject.SetActive(!snapshot.factory);
            var production=service.Production;
            if(!LevelUnitAvailability.Allowed(service.world,selectedRole)){var roles=System.Enum.GetValues(typeof(UnitRole)).Cast<UnitRole>().Where(r=>LevelUnitAvailability.Allowed(service.world,r)).ToArray();if(roles.Length>0)selectedRole=roles[0];}
            var q=service.ProductionQuote(city,snapshot.owner,truck,level,count,selectedRole);
            build.interactable=q.allowed;productionInfo.text=float.IsInfinity(q.cost)||float.IsNaN(q.cost)?q.reason:"费用："+q.cost.ToString("0")+"资源  合计："+q.seconds.ToString("0")+"秒"+(q.allowed?"":"\n"+q.reason);
            typeButton.GetComponentInChildren<Text>().text="兵种："+LevelUnitAvailability.Name(selectedRole);grade.GetComponentInChildren<Text>().text="编制："+BattleTextFormatter.Echelon(level);
            quantity.GetComponentInChildren<Text>().text="数量："+(truck?1:count);quantity.interactable=!truck;
            cancelAll.interactable=service.Logistics!=null&&service.Logistics.IsBuilding(city);
            ordersToggle.GetComponentInChildren<Text>().text=(snapshot.orders.Length+(service.Logistics!=null&&service.Logistics.HasNonInfantryJob(city)?1:0))+"项订单（"+(expandedOrders?"收起":"展开")+"）";
            foreach(var update in updates)update();
        }
        void Rebuild(CitySnapshot snapshot)
        {
            foreach(Transform child in transform){child.gameObject.SetActive(false);Destroy(child.gameObject);}updates.Clear();
            summary=Label(Section("城市概况"),"",36);
            var nav=new GameObject("City tabs",typeof(RectTransform),typeof(HorizontalLayoutGroup),typeof(LayoutElement));nav.transform.SetParent(transform,false);nav.GetComponent<LayoutElement>().preferredHeight=26;var navigation=nav.GetComponent<HorizontalLayoutGroup>();navigation.spacing=4;navigation.childControlWidth=navigation.childControlHeight=true;navigation.childForceExpandHeight=false;
            var troopTab=Button(nav.transform,"驻军与入城",()=>{showGarrison=true;Refresh();});troopTab.image.color=showGarrison?new Color(.6f,.45f,.22f):new Color(.28f,.22f,.15f);
            var productionTab=Button(nav.transform,"建筑与生产",()=>{showGarrison=false;Refresh();});productionTab.image.color=!showGarrison?new Color(.6f,.45f,.22f):new Color(.28f,.22f,.15f);
            var garrison=Section("驻军与修复");if(snapshot.garrison.Length==0)Label(garrison,"目前没有驻军",22);
            foreach(var member in snapshot.garrison)
            {
                var unit=member;var info=Label(garrison,"",40);
                var repair=Button(garrison,"开始修复",()=>Send(new BattleCommand{kind=BattleCommandKind.ToggleRepair,unitId=unit.unitId}));
                var leave=Button(garrison,"出城",()=>Send(new BattleCommand{kind=BattleCommandKind.ExitPoint,unitId=unit.unitId}));
                Button(garrison,"查看单位："+unit.DisplayDesignation,()=>hud.SelectUnit(unit));
                updates.Add(()=>{info.text=unit.DisplayDesignation+"  "+unit.Personnel.ToString("0")+" / "+unit.Capacity.ToString("0")+"\n"+BattleTextFormatter.State(unit.State);var q=service.Repair(unit,city,snapshot.owner);repair.interactable=q.allowed;repair.GetComponentInChildren<Text>().text=unit.State==ArmyBattleState.Repairing?"停止修复":q.allowed?"开始修复（满修约"+q.cost.ToString("0")+"资源）":q.reason;leave.interactable=service.Exit(unit,city,snapshot.owner).allowed;});
            }
            var entry=Section("附近部队入城");if(snapshot.nearby.Length==0)Label(entry,"附近没有可操作的己方部队",32);
            foreach(var nearby in snapshot.nearby){var unit=nearby;var button=Button(entry,"入城："+unit.DisplayDesignation,()=>Send(new BattleCommand{kind=BattleCommandKind.EnterPoint,unitId=unit.unitId,pointId=city.pointId}));updates.Add(()=>{var q=service.Enter(unit,city,snapshot.owner);button.interactable=q.allowed;button.GetComponentInChildren<Text>().text=q.allowed?"入城："+unit.DisplayDesignation:unit.DisplayDesignation+"："+q.reason;});}
            var factory=Section("建筑与生产");buildingInfo=Label(factory,"",32);upgrade=Button(factory,"升级兵工厂",()=>Send(new BattleCommand{kind=BattleCommandKind.BuildFactory,pointId=city.pointId}));
            var choices=new GameObject("Production selectors",typeof(RectTransform),typeof(HorizontalLayoutGroup),typeof(LayoutElement));choices.transform.SetParent(factory,false);choices.GetComponent<LayoutElement>().preferredHeight=26;var choiceLayout=choices.GetComponent<HorizontalLayoutGroup>();choiceLayout.childControlWidth=choiceLayout.childControlHeight=true;choiceLayout.childForceExpandHeight=false;choiceLayout.spacing=3;
            typeButton=Button(choices.transform,"兵种：普通步兵",()=>{var roles=System.Enum.GetValues(typeof(UnitRole)).Cast<UnitRole>().Where(r=>LevelUnitAvailability.Allowed(service.world,r)).ToArray();if(roles.Length>0)selectedRole=roles[(System.Array.IndexOf(roles,selectedRole)+1)%roles.Length];Refresh();});
            grade=Button(choices.transform,"编制：班",()=>{level=(ArmyEchelon)(((int)level+1)%8);Refresh();});
            quantity=Button(choices.transform,"数量：1",()=>{int maximum=service.Production!=null&&service.Production.rules!=null?Mathf.Clamp(service.Production.rules.maximumQueuedPerCity,1,10):1;count=count%maximum+1;Refresh();});
            productionInfo=Label(factory,"",32);
            build=Button(factory,"加入生产",()=>Send(new BattleCommand{kind=truck?BattleCommandKind.BuildTruck:BattleCommandKind.BuildInfantry,pointId=city.pointId,echelon=level,count=truck?1:count,productionRole=selectedRole}));
            var orders=Section("生产订单");
            ordersToggle=Button(orders,"展开订单",()=>{expandedOrders=!expandedOrders;Refresh();});
            if(expandedOrders&&snapshot.orders.Length==0){var label=Label(orders,"",34);updates.Add(()=>label.text=service.Logistics==null?"当前没有生产订单":service.Logistics.BuildStatus(city));}
            if(expandedOrders)foreach(var item in snapshot.orders){var job=item;var label=Label(orders,"",34);updates.Add(()=>label.text=LevelUnitAvailability.Name(job.role)+"·"+BattleTextFormatter.Echelon(job.echelon)+"  "+(job.automated&&service.world.RedIsPlayerControlled?"AI暂停":service.Production.ActiveJob(city)!=job?"排队中":job.remaining<=0?"等待合法出生位置":"剩余 "+job.remaining.ToString("0.0")+"秒"));Button(orders,"取消该订单",()=>Send(new BattleCommand{kind=BattleCommandKind.CancelCityProduction,pointId=city.pointId,productionJobId=job.id}));}
            cancelAll=Button(orders,"取消全部生产并退款",()=>Send(new BattleCommand{kind=BattleCommandKind.CancelCityProduction,pointId=city.pointId}));
            cancelAll.gameObject.SetActive(expandedOrders);
            garrison.gameObject.SetActive(showGarrison);entry.gameObject.SetActive(showGarrison);factory.gameObject.SetActive(!showGarrison);orders.gameObject.SetActive(!showGarrison);
        }
    }
}
