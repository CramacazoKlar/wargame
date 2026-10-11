using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
namespace CampaignDemo {
 [DefaultExecutionOrder(270)]
 public sealed class TacticalMinimap:MonoBehaviour,IPointerClickHandler {
  public BattleWorld world;
  public enum LayerMode { All,Military,Supply }
  public LayerMode layerMode;
  readonly System.Collections.Generic.List<Button> layerButtons=new System.Collections.Generic.List<Button>();
  public void SetLayer(LayerMode mode){layerMode=mode;next=0;UpdateLayerButtons();if(texture!=null&&world!=null&&world.Initialized)Render();}
  public bool IncludesOwnUnit(BattleUnit unit){bool supply=unit!=null&&unit.GetComponent<TransportTruck>()!=null;return layerMode==LayerMode.All||(layerMode==LayerMode.Supply?supply:!supply);}
  [Min(100)] public float size=260;
  [Min(0)] public float margin=8;
  [Min(8)] public float edgeRevealPixels=28;
  [Min(.05f)] public float refreshSeconds=.25f;
  [Range(64,512)] public int resolution=192;
  public bool expanded=true;
  public bool showControlGrid=true;
  [Range(0,1)] public float controlOpacity=.45f;
  public Color ownControlColor=new Color(.12f,.4f,1),enemyControlColor=new Color(1,.16f,.12f);
  public Color viewportColor=Color.yellow;
  public Color gridLineColor=new Color(.18f,.2f,.22f);
  [Min(0)] public float slideSeconds=.18f;
  float reveal;LineRenderer selectedRing;Material selectedMaterial;
  RectTransform window;GameObject tab;RawImage image;Texture2D texture;Color[] terrain,pixels;Rect bounds,worldBounds;float next;int mapVersion=-1;
  public float ReservedTop {get{return expanded||reveal>0?size+2*margin:margin;}}
  public RectTransform Window {get{return window;}}
  public void SetExpanded(bool value){expanded=value;if(slideSeconds<=0)reveal=value?1:0;ApplyLayout();}
  void Start(){
   if(world==null)world=FindObjectOfType<BattleWorld>();
   var g=new GameObject("Tactical minimap",typeof(RectTransform));g.transform.SetParent(transform,false);window=g.GetComponent<RectTransform>();window.anchorMin=window.anchorMax=Vector2.one;window.pivot=Vector2.one;
   image=g.AddComponent<RawImage>();image.color=Color.white;image.raycastTarget=true;g.AddComponent<MinimapClickForwarder>().minimap=this;
   tab=new GameObject("Minimap collapse tab",typeof(RectTransform));tab.transform.SetParent(transform,false);var r=tab.GetComponent<RectTransform>();r.anchorMin=r.anchorMax=Vector2.one;r.pivot=Vector2.one;r.sizeDelta=new Vector2(20,42);tab.AddComponent<Image>().color=new Color(.23f,.23f,.2f,.95f);var button=tab.AddComponent<Button>();button.onClick.AddListener(()=>SetExpanded(!expanded));
   var label=new GameObject("Arrow",typeof(RectTransform));label.transform.SetParent(tab.transform,false);var lr=label.GetComponent<RectTransform>();lr.anchorMin=Vector2.zero;lr.anchorMax=Vector2.one;lr.offsetMin=lr.offsetMax=Vector2.zero;var text=label.AddComponent<Text>();text.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");text.fontSize=18;text.alignment=TextAnchor.MiddleCenter;text.color=Color.white;text.raycastTarget=false;
   CreateLayerButtons();
   resolution=Mathf.Clamp(resolution,64,512);reveal=expanded?1:0;if(world!=null)world.PlayerChanged+=PlayerChanged;texture=new Texture2D(resolution,resolution,TextureFormat.RGBA32,false);texture.filterMode=FilterMode.Point;image.texture=texture;terrain=new Color[resolution*resolution];pixels=new Color[terrain.Length];ApplyLayout();
  }
  void CreateLayerButtons(){string[] labels={"全部","军事","补给"};var font=GetComponent<CampaignHUD>()!=null?GetComponent<CampaignHUD>().font:Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");for(int i=0;i<3;i++){int index=i;var g=new GameObject("Minimap layer "+labels[i],typeof(RectTransform));g.transform.SetParent(window,false);var rect=g.GetComponent<RectTransform>();rect.anchorMin=new Vector2(i/3f,0);rect.anchorMax=new Vector2((i+1)/3f,0);rect.pivot=new Vector2(.5f,0);rect.offsetMin=new Vector2(2,2);rect.offsetMax=new Vector2(-2,22);g.AddComponent<Image>().color=new Color(.13f,.16f,.17f,.95f);var button=g.AddComponent<Button>();button.onClick.AddListener(()=>SetLayer((LayerMode)index));layerButtons.Add(button);var label=new GameObject("Text",typeof(RectTransform));label.transform.SetParent(g.transform,false);var lr=label.GetComponent<RectTransform>();lr.anchorMin=Vector2.zero;lr.anchorMax=Vector2.one;lr.offsetMin=lr.offsetMax=Vector2.zero;var text=label.AddComponent<Text>();text.font=font;text.fontSize=11;text.text=labels[i];text.color=Color.white;text.alignment=TextAnchor.MiddleCenter;text.raycastTarget=false;}UpdateLayerButtons();}
  void UpdateLayerButtons(){for(int i=0;i<layerButtons.Count;i++)if(layerButtons[i]!=null)layerButtons[i].GetComponent<Image>().color=i==(int)layerMode?new Color(.35f,.4f,.42f,.98f):new Color(.13f,.16f,.17f,.95f);}
  void ApplyLayout(){if(window==null)return;window.sizeDelta=Vector2.one*size;window.anchoredPosition=new Vector2(-margin+(1-reveal)*(size+margin),-margin);window.gameObject.SetActive(expanded||reveal>0);var r=tab.GetComponent<RectTransform>();r.anchoredPosition=new Vector2(-margin-size+(1-reveal)*(size+margin),-margin-size*.5f+21);tab.GetComponentInChildren<Text>().text=expanded?">":"<";tab.SetActive(TabVisibleFor(Input.mousePosition));}
  public bool TabVisibleFor(Vector2 screenPoint){if(expanded||reveal>0)return true;var canvas=GetComponentInParent<Canvas>();return screenPoint.x>=Screen.width-edgeRevealPixels||RectTransformUtility.RectangleContainsScreenPoint(tab.GetComponent<RectTransform>(),screenPoint,canvas!=null&&canvas.renderMode!=RenderMode.ScreenSpaceOverlay?canvas.worldCamera:null);}
  void PlayerChanged(){next=0;if(expanded&&world!=null&&world.Initialized&&texture!=null)Render();}
  void Update(){reveal=slideSeconds<=0?(expanded?1:0):Mathf.MoveTowards(reveal,expanded?1:0,Time.unscaledDeltaTime/slideSeconds);ApplyLayout();UpdateSelectedRing();if(world==null||!world.Initialized||!expanded||Time.unscaledTime<next)return;next=Time.unscaledTime+refreshSeconds;Render();}
  public Vector2 WorldToMap(Vector3 point){return new Vector2((point.x-bounds.xMin)/bounds.width,(point.y-bounds.yMin)/bounds.height);}
  public Vector3 MapToWorld(Vector2 uv){return new Vector3(bounds.xMin+uv.x*bounds.width,bounds.yMin+uv.y*bounds.height,0);}
  public void Render(){
   UpdateSelectedRing();
   var map=world.setup.freeMarchMap;if(map==null||map.definition==null)return;int n=Mathf.Clamp(resolution,64,512);resolution=n;if(texture.width!=n){Destroy(texture);resolution=n;texture=new Texture2D(n,n,TextureFormat.RGBA32,false);texture.filterMode=FilterMode.Point;image.texture=texture;terrain=new Color[n*n];pixels=new Color[n*n];mapVersion=-1;}
   if(mapVersion!=map.Version){mapVersion=map.Version;var local=map.definition.bounds;var a=map.transform.TransformPoint(new Vector3(local.xMin,local.yMin));var b=map.transform.TransformPoint(new Vector3(local.xMax,local.yMax));var c=map.transform.TransformPoint(new Vector3(local.xMin,local.yMax));var d=map.transform.TransformPoint(new Vector3(local.xMax,local.yMin));float xmin=Mathf.Min(a.x,b.x,c.x,d.x),xmax=Mathf.Max(a.x,b.x,c.x,d.x),ymin=Mathf.Min(a.y,b.y,c.y,d.y),ymax=Mathf.Max(a.y,b.y,c.y,d.y);worldBounds=Rect.MinMaxRect(xmin,ymin,xmax,ymax);float side=Mathf.Max(xmax-xmin,ymax-ymin);bounds=new Rect((xmin+xmax-side)/2,(ymin+ymax-side)/2,side,side);
    for(int y=0;y<resolution;y++)for(int x=0;x<resolution;x++){var p=MapToWorld(new Vector2((x+.5f)/resolution,(y+.5f)/resolution));var lp=map.transform.InverseTransformPoint(p);Color color=new Color(.12f,.13f,.13f);if(local.Contains(lp)){var kind=map.TerrainAt(new RoadPoint(p.x,p.y));color=kind==TerrainKind.Water?new Color(.22f,.39f,.5f):kind==TerrainKind.Forest?new Color(.22f,.32f,.2f):kind==TerrainKind.Mountain?new Color(.43f,.39f,.31f):kind==TerrainKind.HighMountain||kind==TerrainKind.Blocked?new Color(.28f,.27f,.25f):kind==TerrainKind.Road?new Color(.73f,.69f,.55f):new Color(.52f,.57f,.4f);if(map.waterMap!=null&&!map.waterMap.CanTraverse(new RoadPoint(p.x,p.y),new RoadPoint(p.x,p.y)))color=new Color(.22f,.39f,.5f);}terrain[y*resolution+x]=color;}
    var road=world.setup.roadMap;if(road!=null&&road.definition!=null&&road.definition.connections!=null)foreach(var connection in road.definition.connections){var nodes=road.definition.nodes;if(nodes==null||connection.a<0||connection.b<0||connection.a>=nodes.Length||connection.b>=nodes.Length)continue;Line(terrain,road.transform.TransformPoint(nodes[connection.a]),road.transform.TransformPoint(nodes[connection.b]),new Color(.8f,.76f,.6f));}
   }
   System.Array.Copy(terrain,pixels,terrain.Length);
   if(showControlGrid&&world.territory!=null)foreach(var player in world.players)foreach(var cell in world.territory.OwnedCells(player)){if(!bounds.Overlaps(cell))continue;Color color=player==world.currentPlayer?ownControlColor:enemyControlColor;var from=WorldToMap(new Vector3(cell.xMin,cell.yMin));var to=WorldToMap(new Vector3(cell.xMax,cell.yMax));int x0=Mathf.Clamp(Mathf.FloorToInt(from.x*resolution),0,resolution-1),x1=Mathf.Clamp(Mathf.CeilToInt(to.x*resolution)-1,0,resolution-1),y0=Mathf.Clamp(Mathf.FloorToInt(from.y*resolution),0,resolution-1),y1=Mathf.Clamp(Mathf.CeilToInt(to.y*resolution)-1,0,resolution-1);for(int y=y0;y<=y1;y++)for(int x=x0;x<=x1;x++){bool edge=x==x0||x==x1||y==y0||y==y1;pixels[y*resolution+x]=Color.Lerp(pixels[y*resolution+x],color,edge?Mathf.Min(1,controlOpacity+.2f):controlOpacity);}}
   if(showControlGrid&&world.territory!=null)foreach(var cell in world.territory.AllCells()){if(!bounds.Overlaps(cell))continue;var gridCorners=new[]{new Vector3(cell.xMin,cell.yMin),new Vector3(cell.xMax,cell.yMin),new Vector3(cell.xMax,cell.yMax),new Vector3(cell.xMin,cell.yMax)};for(int i=0;i<4;i++)Line(pixels,gridCorners[i],gridCorners[(i+1)%4],gridLineColor);}
   foreach(var p in world.points)if(p!=null){bool observed=p.ownerId==world.currentPlayer||!world.fogEnabled||world.VisibleAt(world.currentPlayer,p.transform.position);if(p.kind==FacilityKind.RecoveryPoint&&p.minimapMajorCity)CityMarker(p.transform.position,observed?(p.ownerId==world.currentPlayer?Color.cyan:string.IsNullOrEmpty(p.ownerId)?Color.white:Color.red):Color.white);else if(observed&&layerMode!=LayerMode.Military)Dot(p.transform.position,p.ownerId==world.currentPlayer?Color.cyan:string.IsNullOrEmpty(p.ownerId)?Color.gray:Color.red,3);}
   foreach(var u in world.units)if(u!=null&&world.IsVisible(world.currentPlayer,u)&&(u.ownerId!=world.currentPlayer||IncludesOwnUnit(u)))Dot(u.transform.position,u.ownerId==world.currentPlayer?Color.cyan:Color.red,2);
   if(layerMode!=LayerMode.Supply&&world.campaign!=null&&world.campaign.combined!=null)foreach(var fort in world.campaign.combined.fortifications)if(fort!=null&&!fort.Destroyed&&fort.VisibleTo(world,world.currentPlayer)){Color color=fort.ownerId==world.currentPlayer?Color.cyan:Color.red;if(fort.IsLine){for(int i=1;i<fort.linePoints.Length;i++)Line(pixels,fort.linePoints[i-1],fort.linePoints[i],color);}else Dot(fort.transform.position,color,2);}
   if(layerMode!=LayerMode.Military&&world.campaign!=null){foreach(var depot in world.campaign.depots)if(depot!=null&&!depot.Removed&&(depot.ownerId==world.currentPlayer||!world.fogEnabled||world.VisibleAt(world.currentPlayer,depot.transform.position)))Dot(depot.transform.position,Color.yellow,2);var logistics=world.campaign.logistics;if(logistics!=null)foreach(var collector in logistics.collectors)if(collector!=null&&collector.Alive&&(collector.ownerId==world.currentPlayer||!world.fogEnabled||world.VisibleAt(world.currentPlayer,collector.transform.position)))Dot(collector.transform.position,Color.yellow,2);}
   var camera=world.setup.mapCamera;float h=camera.orthographicSize,w=h*camera.aspect;var center=camera.transform.position;float left=Mathf.Max(center.x-w,worldBounds.xMin),right=Mathf.Min(center.x+w,worldBounds.xMax),bottom=Mathf.Max(center.y-h,worldBounds.yMin),top=Mathf.Min(center.y+h,worldBounds.yMax);if(right>left&&top>bottom){var corners=new[]{new Vector3(left,bottom),new Vector3(right,bottom),new Vector3(right,top),new Vector3(left,top)};for(int i=0;i<4;i++)Line(pixels,corners[i],corners[(i+1)%4],viewportColor);}
   var selected=HighlightedUnit();if(selected!=null&&IncludesOwnUnit(selected))Ring(selected.transform.position,4,Color.white);var tracker=GetComponent<MinimapAlertTracker>();if(tracker!=null)foreach(var alert in tracker.Alerts){float age=Time.unscaledTime-alert.raised;Ring(alert.position,5+Mathf.Repeat(age,1)*8,tracker.alertColor);}
   texture.SetPixels(pixels);texture.Apply(false);
  }
  public BattleUnit HighlightedUnit(){var hud=GetComponent<CampaignHUD>();BattleUnit unit=null;if(hud!=null&&hud.Inspect)unit=hud.SelectedUnit;else{var selection=world.GetComponent<ArmySelection>();if(selection!=null&&selection.Selected!=null)unit=selection.Selected.GetComponent<BattleUnit>();}return unit!=null&&unit.ownerId==world.currentPlayer&&unit.OnMap&&world.IsVisible(world.currentPlayer,unit)?unit:null;}
  void UpdateSelectedRing(){if(world==null||!world.Initialized)return;if(world.SimulationEnded){if(selectedRing!=null)selectedRing.enabled=false;return;}var unit=HighlightedUnit();if(unit==null){if(selectedRing!=null)selectedRing.enabled=false;return;}if(selectedRing==null){var g=new GameObject("Single selected army highlight");selectedRing=g.AddComponent<LineRenderer>();selectedMaterial=new Material(Shader.Find("Sprites/Default"));selectedRing.sharedMaterial=selectedMaterial;selectedRing.useWorldSpace=true;selectedRing.loop=true;selectedRing.positionCount=128;selectedRing.sortingOrder=25;selectedRing.startColor=selectedRing.endColor=Color.white;}var center=unit.transform.position;center.z=0;float radius=UnitRanges.Attack(unit);var camera=world.setup.mapCamera;selectedRing.startWidth=selectedRing.endWidth=Mathf.Max(.015f,2*camera.orthographicSize/Mathf.Max(1,camera.pixelHeight)*1.5f);selectedRing.enabled=true;for(int i=0;i<128;i++){float a=i*Mathf.PI*2/128;selectedRing.SetPosition(i,center+new Vector3(Mathf.Cos(a),Mathf.Sin(a))*radius);}}
  void Ring(Vector3 position,float radius,Color color){for(int i=0;i<64;i++){float a=i*Mathf.PI*2/64;var uv=WorldToMap(position)+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*radius/resolution;int x=Mathf.RoundToInt(uv.x*(resolution-1)),y=Mathf.RoundToInt(uv.y*(resolution-1));if(x>=0&&y>=0&&x<resolution&&y<resolution)pixels[y*resolution+x]=color;}}
  void CityMarker(Vector3 point,Color color){var uv=WorldToMap(point);int cx=Mathf.RoundToInt(uv.x*(resolution-1)),cy=Mathf.RoundToInt(uv.y*(resolution-1));for(int y=-5;y<=5;y++)for(int x=-5;x<=5;x++){int distance=x*x+y*y;int px=cx+x,py=cy+y;if(px<0||py<0||px>=resolution||py>=resolution)continue;if(distance>=12&&distance<=25)pixels[py*resolution+px]=color;else if(distance<=2)pixels[py*resolution+px]=color;}}
  void Dot(Vector3 point,Color color,int radius){var uv=WorldToMap(point);int cx=Mathf.RoundToInt(uv.x*(resolution-1)),cy=Mathf.RoundToInt(uv.y*(resolution-1));for(int y=cy-radius;y<=cy+radius;y++)for(int x=cx-radius;x<=cx+radius;x++)if(x>=0&&y>=0&&x<resolution&&y<resolution)pixels[y*resolution+x]=color;}
  void Line(Color[] target,Vector3 a,Vector3 b,Color color){var from=WorldToMap(a);var to=WorldToMap(b);int steps=Mathf.CeilToInt(Vector2.Distance(from,to)*resolution);steps=Mathf.Clamp(steps,1,resolution*8);for(int i=0;i<=steps;i++){var p=Vector2.Lerp(from,to,(float)i/steps);int x=Mathf.RoundToInt(p.x*(resolution-1)),y=Mathf.RoundToInt(p.y*(resolution-1));if(x>=0&&y>=0&&x<resolution&&y<resolution)target[y*resolution+x]=color;}}
  public void OnPointerClick(PointerEventData data){if(!expanded||data.button!=PointerEventData.InputButton.Left||world==null||!world.Initialized)return;Vector2 local;if(!RectTransformUtility.ScreenPointToLocalPointInRectangle(window,data.position,data.pressEventCamera,out local))return;var rect=window.rect;var uv=new Vector2((local.x-rect.xMin)/rect.width,(local.y-rect.yMin)/rect.height);if(uv.x<0||uv.y<0||uv.x>1||uv.y>1)return;var p=MapToWorld(uv);var map=world.setup.freeMarchMap;if(map!=null&&map.definition!=null){var localPoint=map.transform.InverseTransformPoint(p);var mapBounds=map.definition.bounds;localPoint.x=Mathf.Clamp(localPoint.x,mapBounds.xMin,mapBounds.xMax);localPoint.y=Mathf.Clamp(localPoint.y,mapBounds.yMin,mapBounds.yMax);p=map.transform.TransformPoint(localPoint);}var cam=world.setup.mapCamera;var pan=world.setup.panController;if(pan!=null&&pan.useBounds){p.x=Mathf.Clamp(p.x,pan.cameraCenterBounds.xMin,pan.cameraCenterBounds.xMax);p.y=Mathf.Clamp(p.y,pan.cameraCenterBounds.yMin,pan.cameraCenterBounds.yMax);}cam.transform.position=new Vector3(p.x,p.y,cam.transform.position.z);}
  void OnDestroy(){if(selectedRing!=null)Destroy(selectedRing.gameObject);if(selectedMaterial!=null)Destroy(selectedMaterial);if(world!=null)world.PlayerChanged-=PlayerChanged;if(texture!=null)Destroy(texture);if(window!=null)Destroy(window.gameObject);if(tab!=null)Destroy(tab);}
 }
 public sealed class MinimapClickForwarder:MonoBehaviour,IPointerClickHandler {public TacticalMinimap minimap;public void OnPointerClick(PointerEventData data){minimap.OnPointerClick(data);}}
}











