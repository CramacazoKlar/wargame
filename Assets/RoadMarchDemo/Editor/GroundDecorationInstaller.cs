using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
namespace CampaignDemo.Editor {
// Pure cartographic decoration: one baked sprite, no colliders or terrain data changes.
public static class GroundDecorationInstaller {
 static Vector2 V(float x,float y){return new Vector2(x,y);}
 static StaffMapCanvas c;static Rect bounds;static FreeMarchMapRuntime terrain;
 static Color gold=new Color(.88f,.64f,.10f),green=new Color(.34f,.43f,.25f);
 static void Wheat(Vector2 centre,Vector2 size,float angle,int seed){
  float cs=Mathf.Cos(angle),sn=Mathf.Sin(angle);System.Func<Vector2,Vector2> p=q=>centre+V(q.x*cs-q.y*sn,q.x*sn+q.y*cs);
  c.Rectangle(centre,size*.5f,angle,gold,.54f);
  for(float x=-size.x*.5f+.15f;x<size.x*.5f;x+=.22f){float shift=(StaffMapCanvas.Hash((int)(x*100),seed)-.5f)*.12f;c.Line(p(V(x,-size.y*.5f+.12f)),p(V(x+shift,size.y*.5f-.12f)),.032f,new Color(.48f,.32f,.06f),.48f);}
  var a=p(V(-size.x/2,-size.y/2));var b=p(V(size.x/2,-size.y/2));var d=p(V(-size.x/2,size.y/2));var e=p(V(size.x/2,size.y/2));c.Polyline(new[]{a,b,e,d},.10f,new Color(.47f,.32f,.08f),.58f,true);
  for(int j=1;j<=3;j++){float y=-size.y/2+size.y*j/4;c.Line(p(V(-size.x/2,y)),p(V(size.x/2,y)),.13f,new Color(.47f,.32f,.08f),.62f);}
  for(int i=0;i<5;i++){var q=p(V((StaffMapCanvas.Hash(i,seed)-.5f)*size.x*.8f,(StaffMapCanvas.Hash(i,seed+10)-.5f)*size.y*.7f));c.Line(q,q+V(0,.28f),.025f,gold,.55f);for(int j=0;j<3;j++){var t=q+V(0,.12f+j*.055f);c.Line(t,t+V(-.06f,.055f),.023f,gold,.5f);c.Line(t,t+V(.06f,.055f),.023f,gold,.5f);}}
 }
 static void Grass(Vector2 centre,Vector2 size,int seed){
  var pix=c.Pixel(centre);int rx=Mathf.CeilToInt(size.x*c.Scale*.65f),ry=Mathf.CeilToInt(size.y*c.Scale*.65f);
  for(int y=Mathf.Max(0,(int)pix.y-ry);y<Mathf.Min(c.height,(int)pix.y+ry);y++)for(int x=Mathf.Max(0,(int)pix.x-rx);x<Mathf.Min(c.width,(int)pix.x+rx);x++){
   float dx=(x-pix.x)/(size.x*c.Scale*.5f),dy=(y-pix.y)/(size.y*c.Scale*.5f);float noise=StaffMapCanvas.Noise(dx*3+seed,dy*3,seed);float edge=Mathf.Clamp01((1-dx*dx-dy*dy+(noise-.5f)*.55f)*4);c.Ink(x,y,green,edge*(.10f+.13f*noise));}
  for(int i=0;i<95;i++){var q=centre+V((StaffMapCanvas.Hash(i,seed)-.5f)*size.x,(StaffMapCanvas.Hash(i,seed+1)-.5f)*size.y);var n=q-centre;if(n.x*n.x/(size.x*size.x*.25f)+n.y*n.y/(size.y*size.y*.25f)>.8f)continue;float h=.07f+.08f*StaffMapCanvas.Hash(i,seed+2);c.Line(q,q+V(-h*.5f,h),.024f,green,.42f);c.Line(q,q+V(.015f,h*1.2f),.024f,green,.42f);c.Line(q,q+V(h*.6f,h*.7f),.024f,green,.35f);}
 }
 public static void Run(){
  var scene=EditorSceneManager.OpenScene("Assets/Scenes/DoubleValley9x.unity");terrain=Object.FindObjectOfType<FreeMarchMapRuntime>();var cart=terrain.GetComponent<StaffMapCartography>();bounds=terrain.definition.bounds;c=new StaffMapCanvas(3072,1920,bounds);
  // Broad continuous washes; thick ink field divisions sit inside golden wheat.
  Grass(V(-30,14),V(34,17),41);Grass(V(-21,-4),V(30,16),42);
  Grass(V(-34,-19),V(25,17),43);Grass(V(14,19),V(29,15),44);
  Grass(V(26,-1),V(32,17),45);Grass(V(29,-23),V(30,14),46);
  Wheat(V(-32,3),V(22,12),.06f,1);
  Wheat(V(-15,-17),V(19,10),-.09f,2);
  Wheat(V(34,10),V(20,13),-.05f,3);
  Wheat(V(6,-21),V(13,10),.08f,4);
  // Coarse safety mask keeps ink away from roads, woods, mountains, water, cities and resources.
  int mw=768,mh=480;var safe=new bool[mw*mh];var roads=cart.roads.definition;var points=Object.FindObjectsOfType<RecoveryPoint>();var sites=Object.FindObjectsOfType<ResourceSite>();
  for(int y=0;y<mh;y++)for(int x=0;x<mw;x++){var p=V(bounds.xMin+(x+.5f)*bounds.width/mw,bounds.yMin+(y+.5f)*bounds.height/mh);var k=terrain.TerrainAt(new RoadPoint(p.x,p.y));bool ok=k==TerrainKind.Ground||k==TerrainKind.Grass;
   if(ok)foreach(var e in roads.connections){var a=roads.nodes[e.a];var b=roads.nodes[e.b];var d=b-a;float t=d.sqrMagnitude==0?0:Mathf.Clamp01(Vector2.Dot(p-a,d)/d.sqrMagnitude);if(Vector2.Distance(p,a+d*t)<.48f){ok=false;break;}}
   if(ok)foreach(var point in points)if(Vector2.Distance(p,point.transform.position)<1.15f){ok=false;break;}
   if(ok)foreach(var site in sites)if(Vector2.Distance(p,site.transform.position)<1.65f){ok=false;break;}safe[y*mw+x]=ok;}
  int painted=0;for(int y=0;y<c.height;y++)for(int x=0;x<c.width;x++){int i=y*c.width+x;var v=c.pixels[i];if(!safe[(y*mh/c.height)*mw+x*mw/c.width])v=Color.clear;else if(v.a>0){v.r/=v.a;v.g/=v.a;v.b/=v.a;painted++;}c.pixels[i]=v;}
  var texture=new Texture2D(c.width,c.height,TextureFormat.RGBA32,false);texture.SetPixels(c.pixels);texture.Apply();const string path="Assets/Game/DoubleValley/GroundDecoration.png";File.WriteAllBytes(path,texture.EncodeToPNG());AssetDatabase.ImportAsset(path);var imp=(TextureImporter)AssetImporter.GetAtPath(path);imp.textureType=TextureImporterType.Sprite;imp.spritePixelsPerUnit=32;imp.alphaIsTransparency=true;imp.mipmapEnabled=false;imp.maxTextureSize=4096;imp.textureCompression=TextureImporterCompression.Uncompressed;imp.SaveAndReimport();
  var old=GameObject.Find("麦田与草地 · 纯贴图装饰");if(old!=null)Object.DestroyImmediate(old);var go=new GameObject("麦田与草地 · 纯贴图装饰");go.transform.SetParent(terrain.transform,false);go.transform.localPosition=bounds.center;var view=go.AddComponent<SpriteRenderer>();view.sprite=AssetDatabase.LoadAssetAtPath<Sprite>(path);view.sortingOrder=4;go.transform.localScale=new Vector3(bounds.width/view.sprite.bounds.size.x,bounds.height/view.sprite.bounds.size.y,1);
  // Export a readable composite of the existing base and the decoration without changing that base.
  var baseTex=new Texture2D(2,2);baseTex.LoadImage(File.ReadAllBytes(AssetDatabase.GetAssetPath(cart.bakedBackground)));var output=new Texture2D(c.width,c.height);var pixels=new Color[c.pixels.Length];for(int y=0;y<c.height;y++)for(int x=0;x<c.width;x++){var fg=c.pixels[y*c.width+x];pixels[y*c.width+x]=Color.Lerp(baseTex.GetPixelBilinear((x+.5f)/c.width,(y+.5f)/c.height),new Color(fg.r,fg.g,fg.b,1),fg.a);}output.SetPixels(pixels);output.Apply();File.WriteAllBytes("C:/Users/ZWQ/Documents/Codex/2026-10-19/c-desktop-wargame-9/outputs/麦田草地_实际地图预览.png",output.EncodeToPNG());
  if(painted<10000)throw new System.Exception("Decoration missing");if(go.GetComponents<Component>().Length!=2)throw new System.Exception("Decoration must remain visual only");EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();Debug.Log("GROUND_DECORATION_PASS pixels="+painted+" renderers=1 gameplayComponents=0");
 }
}}


