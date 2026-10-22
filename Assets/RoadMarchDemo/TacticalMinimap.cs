using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
namespace CampaignDemo {
 [DefaultExecutionOrder(270)]
 public sealed class TacticalMinimap:MonoBehaviour,IPointerClickHandler {
  public BattleWorld world;
  [Min(100)] public float size=260;
  [Min(0)] public float margin=8;
  [Min(8)] public float edgeRevealPixels=28;
  [Min(.05f)] public float refreshSeconds=.25f;
  [Range(64,512)] public int resolution=192;
  public bool expanded=true;
  public bool showControlGrid=true;
  [Range(0,1)] public float controlOpacity=.45f;
  public Color ownControlColor=new Color(.12f,.4f,1),enemyControlColor=new Color(1,.16f,.12f);
  public Color viewportColor=StaffMapStyle.Mountain;
  public Color gridLineColor=new Color(.18f,.2f,.22f);
  [Min(0)] public float slideSeconds=.18f;
  float reveal;LineRenderer selectedRing;Material selectedMaterial;
  RectTransform window;GameObject tab;RawImage image;Texture2D texture;Color[] terrain,pixels;Rect bounds,worldBounds,viewBounds;float next;int mapVersion=-1;
  Vector3 lastCameraPosition;Quaternion lastCameraRotation;float lastCameraSize,lastCameraAspect;bool hasCameraSnapshot;
  public float ReservedTop {get{return 72;}}
  public RectTransform Window {get{return window;}}
  public float Bottom {get{var hud=GetComponent<CampaignHUD>();var overview=GetComponent<BattleOverviewHUD>();return hud!=null&&hud.Inspect&&overview!=null&&overview.enabled?100:12;}}
  public void SetExpanded(bool value){expanded=value;if(slideSeconds<=0)reveal=value?1:0;ApplyLayout();}
  void Start(){
   if(world==null)world=FindObjectOfType<BattleWorld>();
   var g=new GameObject("Tactical minimap",typeof(RectTransform));g.transform.SetParent(transform,false);window=g.GetComponent<RectTransform>();window.anchorMin=window.anchorMax=Vector2.one;window.pivot=Vector2.one;
   image=g.AddComponent<RawImage>();image.color=Color.white;image.raycastTarget=true;g.AddComponent<MinimapClickForwarder>().minimap=this;
   tab=new GameObject("Minimap collapse tab",typeof(RectTransform));tab.transform.SetParent(transform,false);var r=tab.GetComponent<RectTransform>();r.anchorMin=r.anchorMax=Vector2.one;r.pivot=Vector2.one;r.sizeDelta=new Vector2(20,42);tab.AddComponent<Image>().color=new Color(.23f,.23f,.2f,.95f);var button=tab.AddComponent<Button>();button.onClick.AddListener(()=>{});var drag=tab.AddComponent<StaffDrawerHandle>();drag.Toggle=()=>SetExpanded(!expanded);drag.DragDelta=delta=>{reveal=Mathf.Clamp01(reveal+delta/(size+24));ApplyLayout();};drag.Release=()=>SetExpanded(reveal>=.5f);
   var label=new GameObject("Arrow",typeof(RectTransform));label.transform.SetParent(tab.transform,false);var lr=label.GetComponent<RectTransform>();lr.anchorMin=Vector2.zero;lr.anchorMax=Vector2.one;lr.offsetMin=lr.offsetMax=Vector2.zero;var text=label.AddComponent<Text>();text.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");text.fontSize=12;text.alignment=TextAnchor.MiddleCenter;text.color=Color.white;text.raycastTarget=false;
   StaffMapPresentation.AddBorder(g);tab.GetComponent<Image>().color=StaffMapStyle.Panel;tab.GetComponentInChildren<Text>().color=StaffMapStyle.Ink;StaffMapPresentation.AddBorder(tab);
   size=248;resolution=256;ownControlColor=new Color(.18f,.72f,.3f);enemyControlColor=StaffMapStyle.Red;viewportColor=StaffMapStyle.Ink;controlOpacity=.30f;showControlGrid=true;resolution=Mathf.Clamp(resolution,64,512);reveal=expanded?1:0;if(world!=null)world.PlayerChanged+=PlayerChanged;texture=new Texture2D(resolution,resolution,TextureFormat.RGBA32,false);texture.filterMode=FilterMode.Point;image.texture=texture;terrain=new Color[resolution*resolution];pixels=new Color[terrain.Length];ApplyLayout();
  }
  void ApplyLayout(){
   if(window==null)return;
   window.anchorMin=window.anchorMax=window.pivot=Vector2.zero;
   window.sizeDelta=Vector2.one*size;
   window.anchoredPosition=new Vector2(12-(1-reveal)*(size+24),Bottom);
   window.gameObject.SetActive(expanded||reveal>0);
   var r=tab.GetComponent<RectTransform>();r.anchorMin=r.anchorMax=r.pivot=Vector2.zero;
   r.sizeDelta=new Vector2(30,68);r.anchoredPosition=new Vector2(12+size-(1-reveal)*(size+24),Bottom+size*.5f-34);
   if(reveal<=0)r.anchoredPosition=new Vector2(0,Bottom+size*.5f-34);
   tab.GetComponentInChildren<Text>().text="地图\n"+(expanded?"‹":"›");tab.SetActive(true);
  }
  public bool TabVisibleFor(Vector2 screenPoint){if(expanded||reveal>0)return true;var canvas=GetComponentInParent<Canvas>();return screenPoint.x<=edgeRevealPixels||RectTransformUtility.RectangleContainsScreenPoint(tab.GetComponent<RectTransform>(),screenPoint,canvas!=null&&canvas.renderMode!=RenderMode.ScreenSpaceOverlay?canvas.worldCamera:null);}
  void PlayerChanged(){next=0;if(expanded&&world!=null&&world.Initialized&&texture!=null)Render();}
  Color[] terrainAtlas;int atlasWidth,atlasHeight,atlasVersion=-1;Rect atlasBounds,atlasWaterBounds;FreeMarchMapRuntime atlasMap;RasterWaterMap atlasWater;Texture2D atlasSource;Matrix4x4 atlasWaterMatrix;int atlasBridgeHash;
  void EnsureTerrainAtlas(FreeMarchMapRuntime map)
  {
   var water=map.waterMap;var source=water==null?null:water.source;var waterBounds=water==null?new Rect():water.localBounds;var waterMatrix=water==null?Matrix4x4.identity:water.transform.localToWorldMatrix;int bridgeHash=0;
   if(water!=null&&water.bridges!=null)foreach(var bridge in water.bridges)bridgeHash=unchecked(bridgeHash*397^bridge.GetHashCode());
   if(terrainAtlas!=null&&atlasMap==map&&atlasVersion==map.Version&&atlasBounds==worldBounds&&atlasWater==water&&atlasSource==source&&atlasWaterBounds==waterBounds&&atlasWaterMatrix==waterMatrix&&atlasBridgeHash==bridgeHash)return;
   atlasMap=map;atlasVersion=map.Version;atlasBounds=worldBounds;atlasWater=water;atlasSource=source;atlasWaterBounds=waterBounds;atlasWaterMatrix=waterMatrix;atlasBridgeHash=bridgeHash;
   float longest=Mathf.Max(worldBounds.width,worldBounds.height);atlasWidth=Mathf.Max(1,Mathf.RoundToInt(512*worldBounds.width/longest));atlasHeight=Mathf.Max(1,Mathf.RoundToInt(512*worldBounds.height/longest));terrainAtlas=new Color[atlasWidth*atlasHeight];
   var geometry=map.Geometry();var local=map.definition.bounds;var inverse=map.transform.worldToLocalMatrix;
   for(int y=0;y<atlasHeight;y++)for(int x=0;x<atlasWidth;x++)
   {
    var p=new Vector3(worldBounds.xMin+(x+.5f)/atlasWidth*worldBounds.width,worldBounds.yMin+(y+.5f)/atlasHeight*worldBounds.height);var lp=inverse.MultiplyPoint3x4(p);Color colour=StaffMapStyle.Inset;
    if(local.Contains(lp))
    {
     var kind=geometry.TerrainAt(new RoadPoint(lp.x,lp.y));colour=StaffMapStyle.Terrain(kind);
     
     if(water!=null&&!water.CanTraverse(new RoadPoint(p.x,p.y),new RoadPoint(p.x,p.y)))colour=StaffMapStyle.Water;
    }
    terrainAtlas[y*atlasWidth+x]=colour;
   }
  }
  bool ViewChanged(){var camera=world.setup.mapCamera;return camera!=null&&(!hasCameraSnapshot||camera.transform.position!=lastCameraPosition||camera.transform.rotation!=lastCameraRotation||camera.orthographicSize!=lastCameraSize||camera.aspect!=lastCameraAspect);}
  void Update(){if(tab==null||!tab.GetComponent<StaffDrawerHandle>().Dragging)reveal=slideSeconds<=0?(expanded?1:0):Mathf.MoveTowards(reveal,expanded?1:0,Time.unscaledDeltaTime/slideSeconds);ApplyLayout();UpdateSelectedRing();if(world==null||!world.Initialized||!expanded||Time.unscaledTime<next)return;next=Time.unscaledTime+Mathf.Max(.1f,refreshSeconds);Render();}
  public static Rect CameraView(Camera camera){return MapProjection.View(camera);}
  public static float Coverage(Rect view,Rect map){float w=Mathf.Max(0,Mathf.Min(view.xMax,map.xMax)-Mathf.Max(view.xMin,map.xMin)),h=Mathf.Max(0,Mathf.Min(view.yMax,map.yMax)-Mathf.Max(view.yMin,map.yMin));return map.width>0&&map.height>0?w*h/(map.width*map.height):0;}
  public static bool GridLinesVisible(Rect view,Rect map){return Coverage(view,map)<.5f;}
  public Vector2 WorldToMap(Vector3 point){return new Vector2((point.x-bounds.xMin)/bounds.width,(point.y-bounds.yMin)/bounds.height);}
  public Vector3 MapToWorld(Vector2 uv){return new Vector3(bounds.xMin+uv.x*bounds.width,bounds.yMin+uv.y*bounds.height,0);}
  public void Render(){
   UpdateSelectedRing();
   var map=world.setup.freeMarchMap;if(map==null||map.definition==null)return;int n=Mathf.Clamp(resolution,64,512);resolution=n;if(texture.width!=n){Destroy(texture);resolution=n;texture=new Texture2D(n,n,TextureFormat.RGBA32,false);texture.filterMode=FilterMode.Point;image.texture=texture;terrain=new Color[n*n];pixels=new Color[n*n];mapVersion=-1;}
   map.SyncBoundsToBackground();
   Rect currentBounds;if(!MapViewBounds.TryGet(world.setup.roadMap,map,out currentBounds))return;
   var camera=world.setup.mapCamera;if(camera==null)return;
   var nextView=CameraView(camera);float side=Mathf.Max(nextView.width,nextView.height);var nextBounds=new Rect(nextView.center-Vector2.one*side*.5f,Vector2.one*side);
   bool rebuild=atlasMap!=map||atlasWater!=map.waterMap||atlasSource!=(map.waterMap==null?null:map.waterMap.source)||mapVersion!=map.Version||worldBounds!=currentBounds||bounds!=nextBounds||viewBounds!=nextView;
   worldBounds=currentBounds;viewBounds=nextView;bounds=nextBounds;
   bool drawGridLines=false;
   var previousAtlas=terrainAtlas;EnsureTerrainAtlas(map);rebuild|=previousAtlas!=terrainAtlas;
   if(rebuild){mapVersion=map.Version;
    float invWidth=atlasWidth/worldBounds.width,invHeight=atlasHeight/worldBounds.height;
    for(int y=0;y<resolution;y++)for(int x=0;x<resolution;x++)
    {
     float px=bounds.xMin+(x+.5f)/resolution*bounds.width,py=bounds.yMin+(y+.5f)/resolution*bounds.height;
     Color colour=StaffMapStyle.Inset;
     if(px>=worldBounds.xMin&&px<worldBounds.xMax&&py>=worldBounds.yMin&&py<worldBounds.yMax){int ax=Mathf.Clamp((int)((px-worldBounds.xMin)*invWidth),0,atlasWidth-1),ay=Mathf.Clamp((int)((py-worldBounds.yMin)*invHeight),0,atlasHeight-1);colour=terrainAtlas[ay*atlasWidth+ax];}
     terrain[y*resolution+x]=colour;
    }
    var road=world.setup.roadMap;if(road!=null&&road.definition!=null&&road.definition.connections!=null)foreach(var connection in road.definition.connections){var nodes=road.definition.nodes;if(nodes==null||connection.a<0||connection.b<0||connection.a>=nodes.Length||connection.b>=nodes.Length)continue;Line(terrain,road.transform.TransformPoint(nodes[connection.a]),road.transform.TransformPoint(nodes[connection.b]),StaffMapStyle.Ink);}
   }
   System.Array.Copy(terrain,pixels,terrain.Length);
   var vision=world.GetComponent<StaffVisibilityShade>();if(vision!=null&&vision.Active){vision.Gather();for(int y=0;y<resolution;y++)for(int x=0;x<resolution;x++)pixels[y*resolution+x]=vision.TintAt(pixels[y*resolution+x],MapToWorld(new Vector2((x+.5f)/resolution,(y+.5f)/resolution)));}
   if(showControlGrid&&world.territory!=null)foreach(var player in world.players)foreach(var cell in world.territory.OwnedCells(player)){if(!bounds.Overlaps(cell))continue;Color color=player==world.currentPlayer?ownControlColor:enemyControlColor;var from=WorldToMap(new Vector3(cell.xMin,cell.yMin));var to=WorldToMap(new Vector3(cell.xMax,cell.yMax));int x0=Mathf.Clamp(Mathf.FloorToInt(from.x*resolution),0,resolution-1),x1=Mathf.Clamp(Mathf.CeilToInt(to.x*resolution)-1,0,resolution-1),y0=Mathf.Clamp(Mathf.FloorToInt(from.y*resolution),0,resolution-1),y1=Mathf.Clamp(Mathf.CeilToInt(to.y*resolution)-1,0,resolution-1);for(int y=y0;y<=y1;y++)for(int x=x0;x<=x1;x++){bool edge=drawGridLines&&(x==x0||x==x1||y==y0||y==y1);pixels[y*resolution+x]=Color.Lerp(pixels[y*resolution+x],color,edge?Mathf.Min(1,controlOpacity+.2f):controlOpacity);}}
   if(drawGridLines&&world.territory!=null)foreach(var cell in world.territory.AllCells()){if(!bounds.Overlaps(cell))continue;var gridCorners=new[]{new Vector3(cell.xMin,cell.yMin),new Vector3(cell.xMax,cell.yMin),new Vector3(cell.xMax,cell.yMax),new Vector3(cell.xMin,cell.yMax)};for(int i=0;i<4;i++)Line(pixels,gridCorners[i],gridCorners[(i+1)%4],gridLineColor);}
   foreach(var p in world.points)if(p!=null){bool observed=p.ownerId==world.currentPlayer||!world.fogEnabled||world.VisibleAt(world.currentPlayer,p.transform.position);if(p.kind==FacilityKind.RecoveryPoint&&p.minimapMajorCity)CityMarker(p.transform.position,observed?(p.ownerId==BattleFactions.BluePlayerId?StaffMapStyle.Blue:string.IsNullOrEmpty(p.ownerId)?Color.white:StaffMapStyle.Red):Color.white);else if(observed)Dot(p.transform.position,p.ownerId==BattleFactions.BluePlayerId?StaffMapStyle.Blue:string.IsNullOrEmpty(p.ownerId)?Color.white:StaffMapStyle.Red,3);}
   foreach(var site in world.campaign.logistics.sites)if(site!=null)Dot(site.transform.position,new Color(1,.85f,.08f),3);
   foreach(var u in world.units)if(u!=null&&world.IsVisible(world.currentPlayer,u))Dot(u.transform.position,u.ownerId==BattleFactions.BluePlayerId?StaffMapStyle.Blue:StaffMapStyle.Red,2);
   if(world.campaign!=null&&world.campaign.combined!=null)foreach(var fort in world.campaign.combined.fortifications)if(fort!=null&&!fort.Destroyed&&fort.VisibleTo(world,world.currentPlayer)){Color color=fort.ownerId==BattleFactions.BluePlayerId?StaffMapStyle.Blue:StaffMapStyle.Red;if(fort.IsLine){for(int i=1;i<fort.linePoints.Length;i++)Line(pixels,fort.linePoints[i-1],fort.linePoints[i],color);}else Dot(fort.transform.position,color,2);}
   if(world.campaign!=null){foreach(var depot in world.campaign.depots)if(depot!=null&&!depot.Removed&&(depot.ownerId==world.currentPlayer||!world.fogEnabled||world.VisibleAt(world.currentPlayer,depot.transform.position)))Dot(depot.transform.position,StaffMapStyle.Mountain,2);}
   lastCameraPosition=camera.transform.position;lastCameraRotation=camera.transform.rotation;lastCameraSize=camera.orthographicSize;lastCameraAspect=camera.aspect;hasCameraSnapshot=true;
   var selected=HighlightedUnit();if(selected!=null)Ring(selected.transform.position,4,Color.white);var tracker=GetComponent<MinimapAlertTracker>();if(tracker!=null)foreach(var alert in tracker.Alerts){float age=Time.unscaledTime-alert.raised;Ring(alert.position,5+Mathf.Repeat(age,1)*8,tracker.alertColor);}
   // Letterbox to preserve map proportions; the viewport frame stays at fixed UI coordinates.
   for(int y=0;y<resolution;y++)for(int x=0;x<resolution;x++)if(!viewBounds.Contains(MapToWorld(new Vector2((x+.5f)/resolution,(y+.5f)/resolution))))pixels[y*resolution+x]=StaffMapStyle.Inset;
   var frame=new[]{new Vector3(viewBounds.xMin,viewBounds.yMin),new Vector3(viewBounds.xMax,viewBounds.yMin),new Vector3(viewBounds.xMax,viewBounds.yMax),new Vector3(viewBounds.xMin,viewBounds.yMax)};
   for(int i=0;i<4;i++)Line(pixels,frame[i],frame[(i+1)%4],viewportColor);
   texture.SetPixels(pixels);texture.Apply(false);
  }
  public BattleUnit HighlightedUnit(){var hud=GetComponent<CampaignHUD>();BattleUnit unit=null;if(hud!=null&&hud.Inspect)unit=hud.SelectedUnit;else{var selection=world.GetComponent<ArmySelection>();if(selection!=null&&selection.Selected!=null)unit=selection.Selected.GetComponent<BattleUnit>();}return unit!=null&&unit.ownerId==world.currentPlayer&&unit.OnMap&&world.IsVisible(world.currentPlayer,unit)?unit:null;}
  void UpdateSelectedRing(){if(world==null||!world.Initialized)return;if(world.SimulationEnded){if(selectedRing!=null)selectedRing.enabled=false;return;}var unit=HighlightedUnit();if(unit==null||unit.GetComponent<ArtilleryBattery>()!=null){if(selectedRing!=null)selectedRing.enabled=false;return;}if(selectedRing==null){var g=new GameObject("Single selected army highlight");selectedRing=g.AddComponent<LineRenderer>();selectedMaterial=new Material(Shader.Find("Sprites/Default"));selectedRing.sharedMaterial=selectedMaterial;selectedRing.useWorldSpace=true;selectedRing.loop=true;selectedRing.positionCount=128;selectedRing.sortingOrder=25;selectedRing.startColor=selectedRing.endColor=StaffMapStyle.Blue;}var center=unit.transform.position;center.z=0;float radius=UnitRanges.Attack(unit);var camera=world.setup.mapCamera;selectedRing.startWidth=selectedRing.endWidth=Mathf.Max(.015f,2*camera.orthographicSize/Mathf.Max(1,camera.pixelHeight)*1.5f);selectedRing.enabled=true;for(int i=0;i<128;i++){float a=i*Mathf.PI*2/128;selectedRing.SetPosition(i,center+new Vector3(Mathf.Cos(a),Mathf.Sin(a))*radius);}}
  void Ring(Vector3 position,float radius,Color color){for(int i=0;i<64;i++){float a=i*Mathf.PI*2/64;var uv=WorldToMap(position)+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*radius/resolution;int x=Mathf.RoundToInt(uv.x*(resolution-1)),y=Mathf.RoundToInt(uv.y*(resolution-1));if(x>=0&&y>=0&&x<resolution&&y<resolution)pixels[y*resolution+x]=color;}}
  void CityMarker(Vector3 point,Color color){var uv=WorldToMap(point);int cx=Mathf.RoundToInt(uv.x*(resolution-1)),cy=Mathf.RoundToInt(uv.y*(resolution-1));for(int y=-5;y<=5;y++)for(int x=-5;x<=5;x++){int distance=x*x+y*y;int px=cx+x,py=cy+y;if(px<0||py<0||px>=resolution||py>=resolution)continue;if(distance>=12&&distance<=25)pixels[py*resolution+px]=color;else if(distance<=2)pixels[py*resolution+px]=color;}}
  void Dot(Vector3 point,Color color,int radius){var uv=WorldToMap(point);int cx=Mathf.RoundToInt(uv.x*(resolution-1)),cy=Mathf.RoundToInt(uv.y*(resolution-1));for(int y=cy-radius;y<=cy+radius;y++)for(int x=cx-radius;x<=cx+radius;x++)if(x>=0&&y>=0&&x<resolution&&y<resolution)pixels[y*resolution+x]=color;}
  void Line(Color[] target,Vector3 a,Vector3 b,Color color){var from=WorldToMap(a);var to=WorldToMap(b);int steps=Mathf.CeilToInt(Vector2.Distance(from,to)*resolution);steps=Mathf.Clamp(steps,1,resolution*8);for(int i=0;i<=steps;i++){var p=Vector2.Lerp(from,to,(float)i/steps);int x=Mathf.RoundToInt(p.x*(resolution-1)),y=Mathf.RoundToInt(p.y*(resolution-1));if(x>=0&&y>=0&&x<resolution&&y<resolution)target[y*resolution+x]=color;}}
  public void OnPointerClick(PointerEventData data){if(!expanded||data.button!=PointerEventData.InputButton.Left||world==null||!world.Initialized)return;Vector2 local;if(!RectTransformUtility.ScreenPointToLocalPointInRectangle(window,data.position,data.pressEventCamera,out local))return;var rect=window.rect;var uv=new Vector2((local.x-rect.xMin)/rect.width,(local.y-rect.yMin)/rect.height);if(uv.x<0||uv.y<0||uv.x>1||uv.y>1)return;var p=MapToWorld(uv);if(!viewBounds.Contains(p))return;p.x=Mathf.Clamp(p.x,worldBounds.xMin,worldBounds.xMax);p.y=Mathf.Clamp(p.y,worldBounds.yMin,worldBounds.yMax);var cam=world.setup.mapCamera;var pan=world.setup.panController;if(pan!=null&&pan.useBounds){pan.RefreshMapBounds();p.x=Mathf.Clamp(p.x,pan.cameraCenterBounds.xMin,pan.cameraCenterBounds.xMax);p.y=Mathf.Clamp(p.y,pan.cameraCenterBounds.yMin,pan.cameraCenterBounds.yMax);}var table=world.GetComponent<TabletopCamera>();if(table!=null)table.Focus(p);else cam.transform.position=new Vector3(p.x,p.y,cam.transform.position.z);}
  void OnDestroy(){if(selectedRing!=null)Destroy(selectedRing.gameObject);if(selectedMaterial!=null)Destroy(selectedMaterial);if(world!=null)world.PlayerChanged-=PlayerChanged;if(texture!=null)Destroy(texture);if(window!=null)Destroy(window.gameObject);if(tab!=null)Destroy(tab);}
 }
 public sealed class MinimapClickForwarder:MonoBehaviour,IPointerClickHandler {public TacticalMinimap minimap;public void OnPointerClick(PointerEventData data){minimap.OnPointerClick(data);}}
}

















