using System.Linq;
using System.Collections.Generic;
using UnityEngine;
namespace CampaignDemo {
 [DefaultExecutionOrder(1100)]
 public sealed class TabletopPresentation:MonoBehaviour {
  public Texture2D referenceAtlas;public Material whiteAtlasMaterial;
  static Font mapFont;public static Font MapFont {get{if(mapFont==null)mapFont=Font.CreateDynamicFontFromOSFont(new[]{"SimHei","Microsoft YaHei","Arial"},32);return mapFont;}}
  public static readonly string[] VillageNames={"Westhaven","Eastmere","Northwick","Southbrook"};
  public static readonly string[] ResourceNames={"Alder Quarry","Willow Supply Fields","Greyford Works","Briarwood Reserve"};
  Material spriteMaterial;
  sealed class Place {public Transform anchor;public GameObject root;public TextMesh body;public Vector3 offset;public string caption;}
  readonly Dictionary<Transform,Place> places=new Dictionary<Transform,Place>();readonly List<Transform> removedPlaces=new List<Transform>();readonly List<Rect> placedRects=new List<Rect>();
  public static void NamePlaces(){var cities=FindObjectsOfType<RecoveryPoint>().Where(p=>!p.isRepairSite).OrderBy(p=>p.pointId).ToArray();for(int i=0;i<cities.Length;i++){cities[i].displayName=VillageNames[i%VillageNames.Length];var marker=cities[i].GetComponent<StaffObjectiveMarker>();if(marker!=null)marker.captionOverride="";}var sites=FindObjectsOfType<ResourceSite>().OrderBy(s=>s.transform.position.x).ThenBy(s=>s.transform.position.y).ToArray();for(int i=0;i<sites.Length;i++)sites[i].displayName=ResourceNames[i%ResourceNames.Length];}
  void Start(){NamePlaces();foreach(var site in FindObjectsOfType<ResourceSite>())RegisterPlace(site.transform,site.displayName,new Vector3(0,-.7f,-.03f));foreach(var city in FindObjectsOfType<RecoveryPoint>().Where(p=>!p.isRepairSite))RegisterPlace(city.transform,city.displayName,new Vector3(0,-.65f,-.03f));BuildTable();gameObject.AddComponent<PaperMapBorder>().Build(GetComponent<BattleWorld>().setup.freeMarchMap);}
  SpriteRenderer Sheet(string title,Vector3 position,Vector2 size,int order,Color color){var g=new GameObject(title);g.transform.SetParent(transform,false);g.transform.position=position;var r=g.AddComponent<SpriteRenderer>();r.sprite=Sprite.Create(Texture2D.whiteTexture,new Rect(0,0,Texture2D.whiteTexture.width,Texture2D.whiteTexture.height),Vector2.one*.5f,1,0,SpriteMeshType.FullRect);r.drawMode=SpriteDrawMode.Sliced;r.size=size;r.sortingOrder=order;r.color=color;return r;}
  void BuildTable(){var world=GetComponent<BattleWorld>();var map=world.setup.freeMarchMap;var b=map.definition.bounds;var centre=map.transform.TransformPoint(b.center);var size=Vector2.Scale(b.size,map.transform.lossyScale);
   if(referenceAtlas!=null){
    // Keep only the contour-map interior: no title, marginal labels or paper margins.
    var crop=new Rect(referenceAtlas.width*.066f,referenceAtlas.height*.122f,referenceAtlas.width*.866f,referenceAtlas.height*.714f);
    var atlasSprite=Sprite.Create(referenceAtlas,crop,Vector2.one*.5f,100,0,SpriteMeshType.FullRect);
    for(int i=0;i<2;i++){var offset=i==0?new Vector2(-size.x*.12f,size.y*.22f):new Vector2(size.x*.14f,-size.y*.22f);var atlas=Sheet(i==0?"White atlas · lower sheet":"Atlas · overlapping contour sheet",new Vector3(centre.x+offset.x,centre.y+offset.y,2-i*.1f),size*3,-120+i,Color.white);atlas.sprite=atlasSprite;atlas.size=new Vector2(size.x*2.6f,size.x*2.6f*crop.height/crop.width);atlas.transform.rotation=Quaternion.Euler(0,0,i==0?-4:3);atlas.sharedMaterial=whiteAtlasMaterial;}
   }
   // Layered soft shadow, offset on the lower sheet; the active map stays on z=0.
   for(int i=18;i>=0;i--){float spread=.14f+i*.095f;Sheet("Paper shadow "+i,new Vector3(centre.x+.48f,centre.y-.52f,.7f),size+new Vector2(12,14)+Vector2.one*spread,-100+(18-i),new Color(0,0,0,.026f));}
   Sheet("Raised paper edge",new Vector3(centre.x,centre.y,.25f),size+Vector2.one*.10f,-70,new Color(.94f,.94f,.93f));
  }
  static Font placeFont;
  public GameObject RegisterPlace(Transform anchor,string caption,Vector3 offset){if(anchor==null)return null;if(!places.TryGetValue(anchor,out var place)){var root=Label(caption,anchor.position+offset);place=new Place{anchor=anchor,root=root,body=root.GetComponentsInChildren<TextMesh>().First(t=>t.name=="Black place name"),offset=offset,caption=caption};places.Add(anchor,place);}if(place.caption!=caption){place.caption=caption;place.root.name="Map place · "+caption;foreach(var text in place.root.GetComponentsInChildren<TextMesh>())text.text=string.Join("\u2009",caption.ToCharArray());}return place.root;}
  public static Rect ScreenRect(Renderer renderer,Camera camera){var bounds=renderer.bounds;var min=new Vector2(float.MaxValue,float.MaxValue);var max=new Vector2(float.MinValue,float.MinValue);for(int i=0;i<8;i++){var at=new Vector3((i&1)==0?bounds.min.x:bounds.max.x,(i&2)==0?bounds.min.y:bounds.max.y,(i&4)==0?bounds.min.z:bounds.max.z);var point=camera.WorldToScreenPoint(at);min=Vector2.Min(min,point);max=Vector2.Max(max,point);}return Rect.MinMaxRect(min.x,min.y,max.x,max.y);}
  void LateUpdate(){var world=GetComponent<BattleWorld>();var camera=world==null||world.setup==null?null:world.setup.mapCamera;if(camera==null)return;placedRects.Clear();removedPlaces.Clear();foreach(var pair in places){var place=pair.Value;if(place.anchor==null){if(place.root!=null)Destroy(place.root);removedPlaces.Add(pair.Key);continue;}var icon=place.anchor.GetComponent<SpriteRenderer>();bool visible=place.anchor.gameObject.activeInHierarchy&&(icon==null||icon.enabled);place.root.SetActive(visible);if(!visible)continue;place.root.transform.position=place.anchor.position+place.offset;var body=place.body.GetComponent<MeshRenderer>();var rect=ScreenRect(body,camera);float height=Mathf.Clamp(camera.pixelHeight/75f,11,16);if(rect.height>.01f)place.root.transform.localScale*=Mathf.Clamp(height/rect.height,.1f,10);rect=ScreenRect(body,camera);float shift=icon==null?0:Mathf.Min(0,ScreenRect(icon,camera).yMin-4-rect.yMax);for(int attempt=0;attempt<6;attempt++){var moved=new Rect(rect.x,rect.y+shift,rect.width,rect.height);bool overlap=false;foreach(var prior in placedRects)if(prior.Overlaps(moved)){overlap=true;break;}if(!overlap)break;shift-=height+4;}if(shift<-.01f){var screen=camera.WorldToScreenPoint(place.root.transform.position);screen.y+=shift;var ray=camera.ScreenPointToRay(screen);var plane=new Plane(Vector3.forward,place.root.transform.position);if(plane.Raycast(ray,out float distance))place.root.transform.position=ray.GetPoint(distance);}placedRects.Add(ScreenRect(body,camera));}foreach(var key in removedPlaces)places.Remove(key);}
  GameObject Label(string text,Vector3 position){
   if(placeFont==null)placeFont=Font.CreateDynamicFontFromOSFont(new[]{"SimHei","Microsoft YaHei","Arial"},64);
   var root=new GameObject("Map place · "+text);root.transform.SetParent(transform,false);root.transform.position=position;
   for(int i=0;i<9;i++){bool outline=i<8;var g=new GameObject(outline?"White outline "+i:"Black place name");g.transform.SetParent(root.transform,false);if(outline){float angle=i*Mathf.PI/4;g.transform.localPosition=new Vector3(Mathf.Cos(angle)*.021f,Mathf.Sin(angle)*.021f,.001f);}var mesh=g.AddComponent<TextMesh>();mesh.font=placeFont;mesh.fontSize=64;mesh.fontStyle=FontStyle.Bold;mesh.characterSize=.165f;mesh.anchor=TextAnchor.UpperCenter;mesh.alignment=TextAlignment.Center;mesh.color=outline?Color.white:new Color(.22f,.22f,.20f);mesh.text=string.Join("\u2009",text.ToCharArray());var renderer=g.GetComponent<MeshRenderer>();renderer.sharedMaterial=placeFont.material;renderer.sortingOrder=outline?25:26;}root.AddComponent<MapLabelOutline>().Configure(GetComponent<StaffVisibilityShade>());return root;
  }

 }
}
