using System.Collections.Generic;
using UnityEngine;
namespace CampaignDemo {
 public sealed class BattleReplayPresentation:MonoBehaviour {
  public BattleReplayRecorder recorder;public Transform Root {get;private set;}

  public bool ShowTactical {get;private set;}=true;
  Transform tacticalRoot;
  sealed class TacticalTrail {public RoadRouteView view;public List<RoadPoint> points;public bool drawHead;}
  readonly List<TacticalTrail> tacticalTrails=new List<TacticalTrail>();
  public void SetTacticalVisibility(bool tactical){ShowTactical=tactical;if(tacticalRoot!=null)tacticalRoot.gameObject.SetActive(tactical);}
  readonly List<Mesh> meshes=new List<Mesh>();readonly List<Material> materials=new List<Material>();readonly Dictionary<Color,Material> lineMaterials=new Dictionary<Color,Material>();
  sealed class Death {public ReplayArmy army;public SpriteRenderer renderer;}readonly List<Death> deaths=new List<Death>();Texture2D crossTexture;Sprite crossSprite;
  readonly List<SpriteRenderer> markers=new List<SpriteRenderer>();Texture2D mask;float lastZoom=-1;int lastHeight=-1;
  public void Build(){Root=new GameObject("Static battle report").transform;Root.SetParent(transform,false);
   tacticalRoot=new GameObject("战术箭头").transform;tacticalRoot.SetParent(Root,false);BuildObservation();var snap=recorder.Snapshot;ReplayDrawing.LayoutEnds(snap);
   foreach(var army in snap.armies){
    foreach(var route in army.tacticalRoutes){if(route.Count<2)continue;var points=new List<RoadPoint>();foreach(var p in route)points.Add(new RoadPoint(p.x,p.y));var view=RoadRouteView.Create(tacticalRoot,"战术箭头 "+army.id,.05f,14,army.owner==snap.blueOwner?new Color(.08f,.35f,.70f,.95f):new Color(.82f,.08f,.22f,.95f));view.ConfigureArrow(recorder.world.setup.settings,recorder.world.setup.mapCamera,false);bool drawHead=(route[route.Count-1]-army.finalPosition).sqrMagnitude<.00000001f;view.ShowArrow(points,drawHead);tacticalTrails.Add(new TacticalTrail{view=view,points=points,drawHead=drawHead});}
    if((army.displayPosition-army.finalPosition).sqrMagnitude>.00001f)Draw(new[]{army.finalPosition,army.displayPosition},.02f,new Color(.1f,.1f,.1f,.65f),false,"End position locator "+army.id,18);
    if(army.symbol!=null){var g=new GameObject("End symbol "+army.id);g.transform.SetParent(Root,false);g.transform.position=army.displayPosition;var r=g.AddComponent<SpriteRenderer>();r.sprite=army.symbol;r.color=Color.white;r.sortingOrder=40;r.drawMode=SpriteDrawMode.Sliced;markers.Add(r);}
    if(army.dead){if(crossSprite==null){crossTexture=new Texture2D(14,14,TextureFormat.RGBA32,false);crossTexture.filterMode=FilterMode.Point;var pixels=new Color32[196];for(int y=0;y<14;y++)for(int x=0;x<14;x++)if(Mathf.Abs(x-y)<=1||Mathf.Abs(x+y-13)<=1)pixels[y*14+x]=new Color32(0,0,0,255);crossTexture.SetPixels32(pixels);crossTexture.Apply(false,false);crossSprite=Sprite.Create(crossTexture,new Rect(0,0,14,14),new Vector2(.5f,.5f),14,0,SpriteMeshType.FullRect);}var g=new GameObject("Destroyed cross "+army.id);g.transform.SetParent(Root,false);var r=g.AddComponent<SpriteRenderer>();r.sprite=crossSprite;r.drawMode=SpriteDrawMode.Sliced;r.sortingOrder=45;deaths.Add(new Death{army=army,renderer=r});}
   }
   foreach(var defense in snap.defenses){if(defense.points.Length<2)continue;var color=ReplayDrawing.Side(defense.owner,snap.blueOwner);if(defense.destroyed)color.a=.45f;Draw(ReplayDrawing.Round(defense.points),.065f,color,false,"Recorded defense",15);for(int i=1;i<defense.points.Length;i++){var a=defense.points[i-1];var b=defense.points[i];var dir=(b-a).normalized;var n=new Vector3(-dir.y,dir.x);float length=Vector3.Distance(a,b);for(float t=.25f;t<length;t+=.5f){var p=a+dir*t;Draw(new[]{p,p+n*.12f},.03f,color,false,"Defense tick",15);}}}
   UpdateMarkerSizes();
  }
  void LateUpdate(){UpdateMarkerSizes();}
  public void RefreshMarkerSizes(){lastZoom=-1;UpdateMarkerSizes();}
  void UpdateMarkerSizes(){if(recorder==null||recorder.world.setup.mapCamera==null)return;var camera=recorder.world.setup.mapCamera;if(camera.orthographicSize==lastZoom&&camera.pixelHeight==lastHeight)return;lastZoom=camera.orthographicSize;lastHeight=camera.pixelHeight;foreach(var trail in tacticalTrails)trail.view.ShowArrow(trail.points,trail.drawHead);float unit=2*lastZoom/Mathf.Max(1,lastHeight);foreach(var r in markers){float width=(r.sprite.name.StartsWith("Enemy_")?48:36)*unit;r.size=new Vector2(width,width*r.sprite.rect.height/r.sprite.rect.width);}foreach(var death in deaths){float width=death.army.owner==recorder.Snapshot.blueOwner?36:48;death.renderer.transform.position=death.army.displayPosition-Vector3.right*(width*.5f+11)*unit;death.renderer.size=Vector2.one*(14*unit);}}

  Mesh Draw(IList<Vector3> points,float width,Color color,bool arrow,string name,int order){var mesh=ReplayDrawing.Strip(points,width,arrow);meshes.Add(mesh);var g=new GameObject(name);g.transform.SetParent(Root,false);g.AddComponent<MeshFilter>().sharedMesh=mesh;var renderer=g.AddComponent<MeshRenderer>();if(!lineMaterials.TryGetValue(color,out var mat)){mat=new Material(Shader.Find("Sprites/Default"));mat.color=color;materials.Add(mat);lineMaterials.Add(color,mat);}renderer.sharedMaterial=mat;renderer.sortingOrder=order;return mesh;}
  void BuildObservation(){const int resolution=1024;var bounds=recorder.Snapshot.bounds;var pixels=new Color32[resolution*resolution];float dx=bounds.width/resolution,dy=bounds.height/resolution,edge=Mathf.Max(dx,dy);
   foreach(var army in recorder.Snapshot.armies)foreach(var trail in army.observation){if(trail.points.Count==0)continue;bool blue=army.owner==recorder.Snapshot.blueOwner;for(int j=0;j<trail.points.Count;j++){var a=trail.points[j];var b=j==0?a:trail.points[j-1];float radius=trail.radius;
    int x0=Mathf.Clamp(Mathf.FloorToInt((Mathf.Min(a.x,b.x)-radius-edge-bounds.xMin)/dx),0,resolution-1),x1=Mathf.Clamp(Mathf.CeilToInt((Mathf.Max(a.x,b.x)+radius+edge-bounds.xMin)/dx),0,resolution-1);
    int y0=Mathf.Clamp(Mathf.FloorToInt((Mathf.Min(a.y,b.y)-radius-edge-bounds.yMin)/dy),0,resolution-1),y1=Mathf.Clamp(Mathf.CeilToInt((Mathf.Max(a.y,b.y)+radius+edge-bounds.yMin)/dy),0,resolution-1);
    Vector2 delta=b-a;float sq=delta.sqrMagnitude;
    for(int y=y0;y<=y1;y++)for(int x=x0;x<=x1;x++){var p=new Vector2(bounds.xMin+(x+.5f)*dx,bounds.yMin+(y+.5f)*dy);float t=sq>0?Mathf.Clamp01(Vector2.Dot(p-(Vector2)a,delta)/sq):0;float distance=Vector2.Distance(p,(Vector2)a+delta*t);if(distance>radius+edge)continue;byte alpha=(byte)Mathf.RoundToInt(Mathf.Clamp01((radius+edge-distance)/(2*edge))*255);int i=y*resolution+x;var c=pixels[i];if(blue)c.r=System.Math.Max(c.r,alpha);else c.g=System.Math.Max(c.g,alpha);c.a=255;pixels[i]=c;}
   }}
   mask=new Texture2D(resolution,resolution,TextureFormat.RGBA32,false,true);mask.filterMode=FilterMode.Bilinear;mask.wrapMode=TextureWrapMode.Clamp;mask.SetPixels32(pixels);mask.Apply(false,true);
   var g=new GameObject("Frozen precontact observation");g.transform.SetParent(Root,false);var mesh=new Mesh();mesh.vertices=new[]{new Vector3(bounds.xMin,bounds.yMin),new Vector3(bounds.xMax,bounds.yMin),new Vector3(bounds.xMax,bounds.yMax),new Vector3(bounds.xMin,bounds.yMax)};mesh.uv=new[]{Vector2.zero,Vector2.right,Vector2.one,Vector2.up};mesh.triangles=new[]{0,2,1,0,3,2};meshes.Add(mesh);g.AddComponent<MeshFilter>().sharedMesh=mesh;var renderer=g.AddComponent<MeshRenderer>();var material=new Material(Shader.Find("Campaign/ReplayObservation"));material.SetTexture("_MainTex",mask);material.SetColor("_Blue",ReplayDrawing.Blue);material.SetColor("_Red",ReplayDrawing.Red);materials.Add(material);renderer.sharedMaterial=material;renderer.sortingOrder=6;
  }
  void OnDestroy(){foreach(var mesh in meshes)if(mesh!=null)Destroy(mesh);foreach(var material in materials)if(material!=null)Destroy(material);if(mask!=null)Destroy(mask);if(crossSprite!=null)Destroy(crossSprite);if(crossTexture!=null)Destroy(crossTexture);if(Root!=null)Destroy(Root.gameObject);}
 }
}
