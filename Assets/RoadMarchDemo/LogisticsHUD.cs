using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
namespace CampaignDemo
{
    [DefaultExecutionOrder(250)]
    public sealed class LogisticsHUD : MonoBehaviour
    {
        public LogisticsWorld logistics;
        public BattleHUD battleHUD;
        public Font font;
        public BattleInformationSource information;
        public BattleUIActions actions;
        GameObject panel;Text info;Dropdown source,destination,city,echelon;Button factory,truck,deploy,start,stop,save;
        ResourceSite selectedSite;ResourceCollector selectedCollector;bool placing;string status="";
        readonly List<ResourceCollector> sources=new List<ResourceCollector>();readonly List<RecoveryPoint> sourceCities=new List<RecoveryPoint>();readonly List<RecoveryPoint> cities=new List<RecoveryPoint>();
        string owner;int sourceRevision=-1,cityRevision=-1;
        readonly Dictionary<ResourceSite,Button> siteLabels=new Dictionary<ResourceSite,Button>();
        readonly Dictionary<ResourceCollector,Button> collectorLabels=new Dictionary<ResourceCollector,Button>();
        BattleWorld World {get{return logistics==null?null:logistics.world;}}
        void Start()
        {
            if(logistics==null||battleHUD==null){enabled=false;return;}font=battleHUD.font;if(information==null)information=battleHUD.information;if(actions==null)actions=battleHUD.actions;
            panel=UI("Logistics management",transform,new Vector2(0,.5f),new Vector2(225,0),new Vector2(270,430));panel.AddComponent<Image>().color=new Color(.18f,.13f,.07f,.95f);
            info=Label(panel.transform,"资源与运输",new Vector2(0,153),new Vector2(250,112));
            source=Drop(panel.transform,new Vector2(0,75));destination=Drop(panel.transform,new Vector2(0,38));city=Drop(panel.transform,new Vector2(0,1));echelon=Drop(panel.transform,new Vector2(0,38));echelon.AddOptions(new List<string>{"班","排","连","营","团","旅","师","军"});echelon.value=2;
            start=Button(panel.transform,"开始自动往返",new Vector2(-62,-40),()=>{var t=SelectedTruck();if(t==null)return;if(ConfigureTransport(t,true))status="已启动，沿道路自动寻路";});
            stop=Button(panel.transform,"应用手动运输",new Vector2(62,-40),()=>{var t=SelectedTruck();if(t!=null){ConfigureTransport(t,false);}});
            save=Button(panel.transform,"保存当前去程路线",new Vector2(0,-77),()=>{var t=SelectedTruck();if(t==null||t.Unit.Motor==null)return;if(ConfigureTransport(t,false,true))status="已保存当前路线；请在装货来源附近规划去程后保存";},new Vector2(245,30));
            factory=Button(panel.transform,"升级兵工厂",new Vector2(-62,-117),()=>actions.Execute(new BattleCommand{kind=BattleCommandKind.BuildFactory,pointId=battleHUD.SelectedPoint==null?null:battleHUD.SelectedPoint.pointId}));
            truck=Button(panel.transform,"制造运输车",new Vector2(62,-117),()=>actions.Execute(new BattleCommand{kind=BattleCommandKind.BuildTruck,pointId=battleHUD.SelectedPoint==null?null:battleHUD.SelectedPoint.pointId,echelon=(ArmyEchelon)echelon.value}));
            deploy=Button(panel.transform,"部署收集器（再点击资源区）",new Vector2(0,-157),()=>{placing=selectedSite!=null;status=placing?"在所选资源区左键部署；右键取消":"先点击地图资源区名称";},new Vector2(245,30));
            foreach(var s in logistics.sites)if(s!=null){var b=Button(transform,s.displayName,Vector2.zero,()=>{selectedSite=s;selectedCollector=null;},new Vector2(150,25));b.GetComponent<Image>().color=Color.clear;siteLabels.Add(s,b);}
            panel.transform.SetAsLastSibling();
        }
        TransportTruck SelectedTruck(){return battleHUD.SelectedUnit==null?null:battleHUD.SelectedUnit.GetComponent<TransportTruck>();}
        bool ConfigureTransport(TransportTruck t,bool automatic,bool saveRoute=false)
        {
            int i=source.value;var collector=i<sources.Count?sources[i]:null;var origin=i>=sources.Count&&i-sources.Count<sourceCities.Count?sourceCities[i-sources.Count]:null;var target=destination.value<cities.Count?cities[destination.value]:null;
            var result=actions.Execute(new BattleCommand{kind=saveRoute?BattleCommandKind.SaveTransportRoute:BattleCommandKind.SetTransport,unitId=t.Unit.unitId,collectorId=collector==null?null:collector.collectorId,sourceCityId=origin==null?null:origin.pointId,destinationCityId=target==null?null:target.pointId,enabled=automatic});status=result.accepted?"操作已接受":result.reason;return result.accepted;
        }
        void Update()
        {
            if(World==null||!World.Initialized||panel==null)return;bool inspect=World.setup.modes.CurrentMode==MouseCommandMode.Interaction;panel.SetActive(inspect);if(!inspect){placing=false;foreach(var b in siteLabels.Values)b.gameObject.SetActive(false);foreach(var b in collectorLabels.Values)b.gameObject.SetActive(false);return;}
            RefreshOptions();
            if(placing&&Input.GetMouseButtonDown(1)){placing=false;status="取消部署";}
            if(placing&&Input.GetMouseButtonDown(0)&&(EventSystem.current==null||!EventSystem.current.IsPointerOverGameObject())){var p=World.setup.mapCamera.ScreenToWorldPoint(new Vector3(Input.mousePosition.x,Input.mousePosition.y,-World.setup.mapCamera.transform.position.z));var payer=city.value<cities.Count?cities[city.value]:null;var result=actions.Execute(new BattleCommand{kind=BattleCommandKind.BuildCollector,siteId=selectedSite==null?null:selectedSite.siteId,pointId=payer==null?null:payer.pointId,destination=p});if(result.accepted)placing=false;status=result.accepted?"部署成功":result.reason;}
            foreach(var pair in siteLabels){pair.Value.gameObject.SetActive(true);Place(pair.Value,pair.Key.transform.position,new Vector2(0,28));}
            foreach(var c in logistics.collectors)if(c!=null&&!collectorLabels.ContainsKey(c)){var capture=c;var b=Button(transform,"",Vector2.zero,()=>{selectedCollector=capture;selectedSite=capture.site;},new Vector2(185,25));b.GetComponent<Image>().color=Color.clear;collectorLabels.Add(c,b);}
            foreach(var pair in collectorLabels){var c=pair.Key;var data=information.GetCollector(World.currentPlayer,c.collectorId);bool visible=data!=null&&data.visible;pair.Value.gameObject.SetActive(visible);if(visible){pair.Value.GetComponentInChildren<Text>().text=data.own?"收集器 "+data.stock.Value.ToString("0")+" / "+data.storageLimit.Value:"敌方收集器";Place(pair.Value,c.transform.position,new Vector2(0,-24));}}
            var overview=information.GetOverview(World.currentPlayer);var t=SelectedTruck();var cityInfo=battleHUD.SelectedPoint==null?null:information.GetFacility(World.currentPlayer,battleHUD.SelectedPoint.pointId);var transportInfo=t==null?null:information.GetTransport(World.currentPlayer,t.Unit.unitId);bool selectedCity=cityInfo!=null&&cityInfo.own&&cityInfo.kind==FacilityKind.RecoveryPoint;
            source.gameObject.SetActive(transportInfo!=null);destination.gameObject.SetActive(source.gameObject.activeSelf);start.gameObject.SetActive(source.gameObject.activeSelf);stop.gameObject.SetActive(source.gameObject.activeSelf);save.gameObject.SetActive(source.gameObject.activeSelf);
            echelon.gameObject.SetActive(selectedCity);factory.gameObject.SetActive(selectedCity);truck.gameObject.SetActive(selectedCity);city.gameObject.SetActive(selectedSite!=null);deploy.gameObject.SetActive(selectedSite!=null);
            info.text="资源与运输\n";
            if(transportInfo!=null)info.text+="货物："+transportInfo.cargo.ToString("0")+" / "+transportInfo.capacity.ToString("0")+"\n"+transportInfo.status+"\n"+status;
            else if(selectedCity)info.text+=cityInfo.name+"\n工厂："+(cityInfo.hasFactory==true?"已建":"未建")+"（全局 "+(overview.factoryCount+overview.factoryPendingCount)+" / "+overview.factoryLimit+"）\n"+cityInfo.buildStatus+"\n"+logistics.Message;
            else if(selectedCollector!=null){var data=information.GetCollector(World.currentPlayer,selectedCollector.collectorId);info.text+=data==null?"无可查看信息":"收集器状态："+(data.alive?"运行":"已摧毁")+"\n耐久："+data.health.ToString("0")+"\n"+status;}
            else info.text+="选择城市、运输车或资源区\n"+status;
            panel.transform.SetAsLastSibling();
        }
        void RefreshOptions()
        {
            int revision=logistics.collectors.Count;int cityCount=0;foreach(var p in World.points)if(p!=null&&information.GetFacility(World.currentPlayer,p.pointId)?.own==true&&p.kind==FacilityKind.RecoveryPoint)cityCount++;
            int hash=17;foreach(var p in World.points)if(p!=null)hash=hash*31+(information.GetFacility(World.currentPlayer,p.pointId)?.ownerId??"").GetHashCode();foreach(var c in logistics.collectors)if(c!=null)hash=hash*31+(c.Alive?1:0);
            if(owner==World.currentPlayer&&sourceRevision==revision+hash&&cityRevision==cityCount)return;owner=World.currentPlayer;sourceRevision=revision+hash;cityRevision=cityCount;sources.Clear();sourceCities.Clear();cities.Clear();
            var sourceNames=new List<string>();var cityNames=new List<string>();foreach(var c in logistics.collectors)if(c!=null&&information.GetCollector(owner,c.collectorId)?.own==true&&information.GetCollector(owner,c.collectorId).alive){sources.Add(c);sourceNames.Add("来源：收集器 "+sources.Count);}foreach(var p in World.points)if(p!=null&&information.GetFacility(owner,p.pointId)?.own==true&&p.kind==FacilityKind.RecoveryPoint){cities.Add(p);sourceCities.Add(p);cityNames.Add(p.displayName);sourceNames.Add("来源："+p.displayName);}source.ClearOptions();source.AddOptions(sourceNames);destination.ClearOptions();destination.AddOptions(cityNames);city.ClearOptions();city.AddOptions(cityNames);
        }
        void Place(Button b,Vector3 position,Vector2 offset){var canvas=GetComponent<Canvas>();var s=World.setup.mapCamera.WorldToScreenPoint(position);RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)transform,s,canvas.renderMode==RenderMode.ScreenSpaceOverlay?null:canvas.worldCamera,out var p);var r=b.GetComponent<RectTransform>();r.anchorMin=r.anchorMax=new Vector2(.5f,.5f);r.anchoredPosition=p+offset;}
        GameObject UI(string name,Transform parent,Vector2 anchor,Vector2 at,Vector2 size){var g=new GameObject(name,typeof(RectTransform));g.transform.SetParent(parent,false);var r=(RectTransform)g.transform;r.anchorMin=r.anchorMax=anchor;r.anchoredPosition=at;r.sizeDelta=size;return g;}
        Text Label(Transform parent,string text,Vector2 at,Vector2 size){var g=UI(text,parent,new Vector2(.5f,.5f),at,size);var t=g.AddComponent<Text>();t.font=font;t.fontSize=14;t.color=new Color(.95f,.87f,.68f);t.text=text;t.raycastTarget=false;return t;}
        Button Button(Transform parent,string title,Vector2 at,UnityEngine.Events.UnityAction action,Vector2? size=null){var dimensions=size??new Vector2(120,30);var g=UI(title,parent,new Vector2(.5f,.5f),at,dimensions);g.AddComponent<Image>().color=new Color(.35f,.26f,.14f,.95f);var b=g.AddComponent<Button>();var l=Label(g.transform,title,Vector2.zero,dimensions);l.alignment=TextAnchor.MiddleCenter;b.onClick.AddListener(action);return b;}
        Dropdown Drop(Transform parent,Vector2 at){var g=UI("Selection",parent,new Vector2(.5f,.5f),at,new Vector2(245,30));g.AddComponent<Image>().color=new Color(.35f,.26f,.14f);var d=g.AddComponent<Dropdown>();d.captionText=Label(g.transform,"",Vector2.zero,new Vector2(235,30));var template=UI("Template",g.transform,new Vector2(.5f,0),new Vector2(0,-80),new Vector2(245,160));template.AddComponent<Image>().color=new Color(.2f,.15f,.08f);var scroll=template.AddComponent<ScrollRect>();var viewport=UI("Viewport",template.transform,new Vector2(.5f,.5f),Vector2.zero,new Vector2(245,160));viewport.AddComponent<Image>();viewport.AddComponent<Mask>().showMaskGraphic=false;var content=UI("Content",viewport.transform,new Vector2(.5f,1),Vector2.zero,new Vector2(245,30));content.GetComponent<RectTransform>().pivot=new Vector2(.5f,1);var item=UI("Item",content.transform,new Vector2(.5f,.5f),Vector2.zero,new Vector2(245,30));item.AddComponent<Image>().color=new Color(.35f,.26f,.14f);var toggle=item.AddComponent<Toggle>();toggle.targetGraphic=item.GetComponent<Image>();d.itemText=Label(item.transform,"",Vector2.zero,new Vector2(235,30));scroll.viewport=viewport.GetComponent<RectTransform>();scroll.content=content.GetComponent<RectTransform>();scroll.horizontal=false;d.template=template.GetComponent<RectTransform>();template.SetActive(false);return d;}
    }
}






