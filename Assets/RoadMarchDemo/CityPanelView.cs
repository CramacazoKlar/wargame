using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
namespace CampaignDemo
{
    public sealed class CityPanelView : MonoBehaviour
    {
        CampaignHUD hud;CityManagement service;RecoveryPoint city;string signature,pendingExit;
        UnitRole selectedRole=UnitRole.Infantry;bool truck {get{return selectedRole==UnitRole.Logistics;}}bool showGarrison=true,expandedOrders,productionOpen;ArmyEchelon level;int count=1;
        Text summary,productionInfo,buildingInfo,grade,quantity;Button upgrade,build,gradeMinus,gradePlus,quantityMinus,quantityPlus,cancelAll,ordersToggle;GameObject productionPopup;
        readonly System.Collections.Generic.List<Action> updates=new System.Collections.Generic.List<Action>();
        public static CityPanelView Attach(CampaignHUD hud,Transform parent,CityManagement service,RecoveryPoint city)
        {
            var obj=new GameObject("City management",typeof(RectTransform));obj.transform.SetParent(parent,false);
            var layout=obj.AddComponent<VerticalLayoutGroup>();layout.childControlHeight=layout.childControlWidth=true;layout.childForceExpandHeight=false;layout.spacing=8;
            var view=obj.AddComponent<CityPanelView>();view.hud=hud;view.service=service;view.city=city;view.Refresh();return view;
        }
        Transform Section(string title)
        {
            var obj=new GameObject(title,typeof(RectTransform));obj.transform.SetParent(transform,false);obj.AddComponent<Image>().color=StaffMapStyle.Inset;
            var layout=obj.AddComponent<VerticalLayoutGroup>();layout.padding=new RectOffset(5,5,4,4);layout.spacing=4;layout.childControlHeight=layout.childControlWidth=true;layout.childForceExpandHeight=false;
            Label(obj.transform,title,18,true);return obj.transform;
        }
        Text Label(Transform parent,string value,float height=32,bool header=false)
        {
            var obj=new GameObject(value,typeof(RectTransform),typeof(Text),typeof(LayoutElement));obj.transform.SetParent(parent,false);
            obj.GetComponent<LayoutElement>().preferredHeight=height;var text=obj.GetComponent<Text>();text.font=hud.font;text.fontSize=header?13:12;text.text=value;text.color=StaffMapStyle.Ink;text.raycastTarget=false;text.alignment=TextAnchor.UpperLeft;return text;
        }
        Button Button(Transform parent,string title,Action action)
        {
            var obj=new GameObject(title,typeof(RectTransform),typeof(Image),typeof(Button),typeof(LayoutElement));obj.transform.SetParent(parent,false);obj.GetComponent<LayoutElement>().preferredHeight=26;obj.GetComponent<Image>().color=StaffMapStyle.Inset;
            var label=Label(obj.transform,title);Destroy(label.GetComponent<LayoutElement>());label.rectTransform.anchorMin=Vector2.zero;label.rectTransform.anchorMax=Vector2.one;label.rectTransform.offsetMin=new Vector2(3,0);label.rectTransform.offsetMax=new Vector2(-3,0);label.alignment=TextAnchor.MiddleCenter;label.resizeTextForBestFit=true;label.resizeTextMinSize=9;label.resizeTextMaxSize=12;
            var button=obj.GetComponent<Button>();button.onClick.AddListener(()=>{hud.CancelTool();action();});return button;
        }
        void Send(BattleCommand command){var result=hud.Send(command);if(result.pending&&command.kind==BattleCommandKind.ExitPoint)pendingExit=command.unitId;if(result.accepted&&!result.pending&&command.kind==BattleCommandKind.ExitPoint){var unit=service.world.units.FirstOrDefault(u=>u!=null&&u.unitId==command.unitId);if(unit!=null&&unit.OnMap){hud.SelectUnit(unit);return;}}Refresh();}
        void Update(){if(string.IsNullOrEmpty(pendingExit)||service==null)return;var unit=service.world.units.FirstOrDefault(u=>u!=null&&u.unitId==pendingExit);if(unit!=null&&unit.OnMap){pendingExit=null;hud.SelectUnit(unit);}}
        // CampaignHUD owns periodic refresh; callbacks also refresh immediately.
        public void Refresh()
        {
            if(hud==null||service==null||city==null)return;
            var snapshot=service.Snapshot(city,service.world.currentPlayer);if(snapshot==null){gameObject.SetActive(false);return;}
            if(!gameObject.activeSelf)gameObject.SetActive(true);
            string key=showGarrison+"|"+expandedOrders+"|"+selectedRole+"|"+productionOpen+"|"+string.Join(",",snapshot.garrison.Select(u=>u.unitId))+"|"+string.Join(",",snapshot.nearby.Select(u=>u.unitId))+"|"+string.Join(",",snapshot.orders.Select(j=>j.id));
            if(signature!=key){signature=key;Rebuild(snapshot);}
            summary.text=snapshot.name+" · "+BattleFactions.Name(snapshot.owner)+(city.isRepairSite?"\n恢复补给："+FacilityRecoveryQueue.ResourcePool(service.world,city).resourceStock.ToString("0"):"\n库存："+snapshot.stock.ToString("0"))+"  驻军："+snapshot.garrison.Length+" / "+snapshot.capacity;
            buildingInfo.text="兵工厂："+(snapshot.factory?"已建成":snapshot.building.Contains("升级")?"建造中":"未建")+"\n"+snapshot.building;
            var quote=service.Upgrade(city,snapshot.owner);upgrade.interactable=quote.allowed;upgrade.GetComponentInChildren<Text>().text=snapshot.factory?"兵工厂已建成":"升级兵工厂（"+(service.Logistics==null?0:service.Logistics.factoryBuildCost).ToString("0")+"资源）";
            upgrade.gameObject.SetActive(!snapshot.factory);
            var production=service.Production;
            if(!LevelUnitAvailability.Allowed(service.world,selectedRole)){var roles=System.Enum.GetValues(typeof(UnitRole)).Cast<UnitRole>().Where(r=>LevelUnitAvailability.Allowed(service.world,r)).ToArray();if(roles.Length>0)selectedRole=roles[0];}
            int maximum=(int)CityProductionRules.MaximumEchelon(selectedRole);level=(ArmyEchelon)Mathf.Clamp((int)level,0,maximum);if(truck)count=1;
            var q=service.ProductionQuote(city,snapshot.owner,truck,level,count,selectedRole);
            build.interactable=q.allowed;productionInfo.text=float.IsInfinity(q.cost)||float.IsNaN(q.cost)?q.reason:"费用："+ResourceEconomy.Money(q.cost)+"资源 · "+q.seconds.ToString("0")+"秒"+(q.allowed?"":"\n"+q.reason);
            grade.text=BattleTextFormatter.Echelon(level);gradeMinus.interactable=(int)level>0;gradePlus.interactable=(int)level<maximum;
            quantity.text="数量 "+count;quantityMinus.interactable=!truck&&count>1;quantityPlus.interactable=!truck&&count<MaximumCount;
            productionPopup.SetActive(productionOpen&&!city.isRepairSite);
            cancelAll.interactable=service.Logistics!=null&&service.Logistics.IsBuilding(city);
            ordersToggle.GetComponentInChildren<Text>().text=(snapshot.orders.Length+(service.Logistics!=null&&service.Logistics.HasNonInfantryJob(city)?1:0))+"项订单（"+(expandedOrders?"收起":"展开")+"）";
            foreach(var update in updates)update();
        }
        void Rebuild(CitySnapshot snapshot)
        {
            foreach(Transform child in transform){child.gameObject.SetActive(false);Destroy(child.gameObject);}updates.Clear();
            summary=Label(Section("城市概况"),"",36);
            var nav=new GameObject("City tabs",typeof(RectTransform),typeof(HorizontalLayoutGroup),typeof(LayoutElement));nav.transform.SetParent(transform,false);nav.GetComponent<LayoutElement>().preferredHeight=26;var navigation=nav.GetComponent<HorizontalLayoutGroup>();navigation.spacing=4;navigation.childControlWidth=navigation.childControlHeight=true;navigation.childForceExpandHeight=false;
            var troopTab=Button(nav.transform,"驻军与入城",()=>{showGarrison=true;Refresh();});troopTab.image.color=showGarrison?new Color(.76f,.70f,.51f):StaffMapStyle.Inset;
            var productionTab=Button(nav.transform,"建筑与生产",()=>{showGarrison=false;Refresh();});productionTab.gameObject.SetActive(!city.isRepairSite);if(city.isRepairSite)showGarrison=true;productionTab.image.color=!showGarrison?new Color(.76f,.70f,.51f):StaffMapStyle.Inset;
            var garrison=Section("驻军与修复");if(snapshot.garrison.Length==0)Label(garrison,"目前没有驻军",22);
            foreach(var member in snapshot.garrison)
            {
                var unit=member;
                var card=new GameObject("驻军 "+unit.unitId,typeof(RectTransform),typeof(VerticalLayoutGroup));card.transform.SetParent(garrison,false);
                var cardLayout=card.GetComponent<VerticalLayoutGroup>();cardLayout.padding=new RectOffset(3,3,5,7);cardLayout.spacing=3;cardLayout.childControlHeight=cardLayout.childControlWidth=true;cardLayout.childForceExpandHeight=false;
                var title=Label(card.transform,GarrisonTitle(unit),40);title.name="驻军名称";title.fontSize=14;title.fontStyle=FontStyle.Bold;title.horizontalOverflow=HorizontalWrapMode.Wrap;title.verticalOverflow=VerticalWrapMode.Overflow;
                var info=Label(card.transform,"",36);
                var repair=Button(card.transform,"恢复单位",()=>Send(new BattleCommand{kind=BattleCommandKind.ToggleRepair,unitId=unit.unitId}));
                var leave=Button(card.transform,"离开",()=>Send(new BattleCommand{kind=BattleCommandKind.ExitPoint,unitId=unit.unitId}));
                var inspect=Button(card.transform,"查看单位："+GarrisonTitle(unit),()=>hud.SelectUnit(unit));
                updates.Add(()=>{title.text=GarrisonTitle(unit);inspect.GetComponentInChildren<Text>().text="查看单位："+title.text;info.text="兵力 "+unit.Personnel.ToString("0")+" / "+unit.Capacity.ToString("0")+"\n"+BattleTextFormatter.State(unit.State)+(FacilityRecoveryQueue.For(service.world)?.Contains(unit)==true?" · "+FacilityRecoveryQueue.For(service.world).Status(unit):"");var q=service.Repair(unit,city,snapshot.owner);repair.interactable=q.allowed;repair.GetComponentInChildren<Text>().text=FacilityRecoveryQueue.For(service.world)?.Contains(unit)==true?"取消恢复":unit.State==ArmyBattleState.Repairing?"取消恢复":q.allowed?"恢复单位（满修约"+q.cost.ToString("0")+"资源）":q.reason;leave.interactable=service.Exit(unit,city,snapshot.owner).allowed;});
            }
            var entry=Section("地图交互");Label(entry,city.isRepairSite?"部队进入后依次恢复；使用己方城市的恢复补给。":"选中部队后，直接点击城市进入。",32);
            var factory=Section("建筑与生产");factory.gameObject.SetActive(!city.isRepairSite);buildingInfo=Label(factory,"",32);upgrade=Button(factory,"升级兵工厂",()=>Send(new BattleCommand{kind=BattleCommandKind.BuildFactory,pointId=city.pointId}));
            Label(factory,"选择兵种，再设置编制",20);
            productionPopup=new GameObject("生产设置小面板",typeof(RectTransform),typeof(Image),typeof(VerticalLayoutGroup));productionPopup.transform.SetParent(factory,false);productionPopup.GetComponent<Image>().color=StaffMapStyle.Inset;var popupLayout=productionPopup.GetComponent<VerticalLayoutGroup>();popupLayout.padding=new RectOffset(5,5,5,5);popupLayout.spacing=4;popupLayout.childControlWidth=popupLayout.childControlHeight=true;popupLayout.childForceExpandHeight=false;
            Label(productionPopup.transform,LevelUnitAvailability.Name(selectedRole)+" · 生产设置",20,true);
            var grades=Row(productionPopup.transform,"编制选择",28);gradeMinus=Button(grades,"−",()=>{level=(ArmyEchelon)Mathf.Max(0,(int)level-1);Refresh();});gradeMinus.name="编制减少";grade=Label(grades,"班",26);grade.alignment=TextAnchor.MiddleCenter;grade.gameObject.GetComponent<LayoutElement>().flexibleWidth=2;gradePlus=Button(grades,"＋",()=>{level=(ArmyEchelon)Mathf.Min((int)CityProductionRules.MaximumEchelon(selectedRole),(int)level+1);Refresh();});gradePlus.name="编制增加";
            var quantities=Row(productionPopup.transform,"数量选择",28);quantityMinus=Button(quantities,"−",()=>{count=Mathf.Max(1,count-1);Refresh();});quantity=Label(quantities,"数量 1",26);quantity.alignment=TextAnchor.MiddleCenter;quantity.gameObject.GetComponent<LayoutElement>().flexibleWidth=2;quantityPlus=Button(quantities,"＋",()=>{count=Mathf.Min(MaximumCount,count+1);Refresh();});
            productionInfo=Label(productionPopup.transform,"",42);
            build=Button(productionPopup.transform,"加入生产",()=>Send(new BattleCommand{kind=truck?BattleCommandKind.BuildTruck:BattleCommandKind.BuildInfantry,pointId=city.pointId,echelon=level,count=truck?1:count,productionRole=selectedRole}));
            Button(productionPopup.transform,"返回兵种列表",()=>{productionOpen=false;Refresh();});
            var roles=Enum.GetValues(typeof(UnitRole)).Cast<UnitRole>().Where(r=>LevelUnitAvailability.Allowed(service.world,r)).ToArray();Transform row=null;
            for(int i=0;i<roles.Length;i++){
                var role=roles[i];if(i%2==0){row=Row(factory,"兵种卡片",50);row.gameObject.SetActive(!productionOpen);}
                float price=role==UnitRole.Logistics?service.Logistics.truckPrefab.GetComponent<TransportTruck>().definition.BuildCost(ArmyEchelon.Squad):service.Production.Cost(ArmyEchelon.Squad,role);
                var card=Button(row,LevelUnitAvailability.Name(role)+"\n"+ResourceEconomy.Money(price),()=>{selectedRole=role;level=ArmyEchelon.Squad;count=1;productionOpen=true;Refresh();});card.name="生产卡片 "+role;card.GetComponent<LayoutElement>().preferredHeight=50;card.GetComponent<LayoutElement>().flexibleWidth=1;
                var catalog=Resources.Load<UnitSymbolCatalog>("UnitSymbols");if(catalog!=null){var icon=new GameObject("兵种图标",typeof(RectTransform),typeof(Image));icon.transform.SetParent(card.transform,false);var image=icon.GetComponent<Image>();image.sprite=catalog.Unit(role,ArmyEchelon.Squad,service.world.currentPlayer==BattleFactions.BluePlayerId);image.preserveAspect=true;image.raycastTarget=false;image.rectTransform.anchorMin=image.rectTransform.anchorMax=new Vector2(0,.5f);image.rectTransform.pivot=new Vector2(0,.5f);image.rectTransform.anchoredPosition=new Vector2(4,0);image.rectTransform.sizeDelta=new Vector2(27,32);card.GetComponentInChildren<Text>().rectTransform.offsetMin=new Vector2(32,0);}
                updates.Add(()=>card.image.color=productionOpen&&selectedRole==role?new Color(.76f,.70f,.51f):StaffMapStyle.Inset);
            }
            var orders=Section("生产订单");
            ordersToggle=Button(orders,"展开订单",()=>{expandedOrders=!expandedOrders;Refresh();});
            if(expandedOrders&&snapshot.orders.Length==0){var label=Label(orders,"",34);updates.Add(()=>label.text=service.Logistics==null?"当前没有生产订单":service.Logistics.BuildStatus(city));}
            if(expandedOrders)foreach(var item in snapshot.orders){var job=item;var label=Label(orders,"",34);updates.Add(()=>label.text=LevelUnitAvailability.Name(job.role)+"·"+BattleTextFormatter.Echelon(job.echelon)+"  "+(job.automated&&service.world.RedIsPlayerControlled?"AI暂停":service.Production.ActiveJob(city)!=job?"排队中":job.remaining<=0?"等待合法出生位置":"剩余 "+job.remaining.ToString("0.0")+"秒"));Button(orders,"取消该订单",()=>Send(new BattleCommand{kind=BattleCommandKind.CancelCityProduction,pointId=city.pointId,productionJobId=job.id}));}
            cancelAll=Button(orders,"取消全部生产并退款",()=>Send(new BattleCommand{kind=BattleCommandKind.CancelCityProduction,pointId=city.pointId}));
            cancelAll.gameObject.SetActive(expandedOrders);
            garrison.gameObject.SetActive(showGarrison);entry.gameObject.SetActive(showGarrison&&!LocalPrototypeOrders.EnabledFor(service.world));factory.gameObject.SetActive(!showGarrison);orders.gameObject.SetActive(!showGarrison);
        }
        public static string GarrisonTitle(BattleUnit unit){if(unit==null)return "";string designation=unit.DisplayDesignation;if(string.IsNullOrWhiteSpace(designation))designation=unit.name;var definition=unit.combatDefinition;return definition==null?designation:designation+" · "+LevelUnitAvailability.Name(definition.role)+" · "+BattleTextFormatter.Echelon(definition.echelon);}
        int MaximumCount {get{return service.Production!=null&&service.Production.rules!=null?Mathf.Clamp(service.Production.rules.maximumQueuedPerCity,1,10):1;}}
        Transform Row(Transform parent,string title,float height){var obj=new GameObject(title,typeof(RectTransform),typeof(HorizontalLayoutGroup),typeof(LayoutElement));obj.transform.SetParent(parent,false);obj.GetComponent<LayoutElement>().preferredHeight=height;var layout=obj.GetComponent<HorizontalLayoutGroup>();layout.spacing=4;layout.childControlWidth=layout.childControlHeight=true;layout.childForceExpandWidth=true;layout.childForceExpandHeight=false;return obj.transform;}
    }
}


