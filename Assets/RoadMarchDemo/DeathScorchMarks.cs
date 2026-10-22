using System.Collections.Generic;
using UnityEngine;
namespace CampaignDemo {
 // Marks are local ground decoration; neither damage nor navigation reads this component.
 [DisallowMultipleComponent,DefaultExecutionOrder(800)]
 public sealed class DeathScorchMarks:MonoBehaviour {
  public BattleWorld world;
  [Range(1,256)] public int maxMarks=80;
  [Range(.1f,1)] public float markSize=.84f;
  struct Snapshot {public bool alive,onMap,visible;public string owner;}
  readonly Dictionary<BattleUnit,Snapshot> previous=new Dictionary<BattleUnit,Snapshot>();
  readonly List<BattleUnit> stale=new List<BattleUnit>();
  readonly Queue<SpriteRenderer> marks=new Queue<SpriteRenderer>();
  readonly Dictionary<SpriteRenderer,string> observedBy=new Dictionary<SpriteRenderer,string>();
  Sprite sprite;int serial;
  public int Count {get{return marks.Count;}}
  public SpriteRenderer Oldest {get{return marks.Count==0?null:marks.Peek();}}
  void Awake(){if(world==null)world=GetComponent<BattleWorld>();BuildSprite();}
  void BuildSprite(){
   const int n=64;var texture=new Texture2D(n,n,TextureFormat.RGBA32,false){name="Charcoal death crater",wrapMode=TextureWrapMode.Clamp};
   for(int y=0;y<n;y++)for(int x=0;x<n;x++){
    float px=(x-31.5f)/30,py=(y-31.5f)/30;float a=Mathf.Atan2(py,px);float r=Mathf.Sqrt(px*px+py*py)/(1+.07f*Mathf.Sin(a*5)+.06f*Mathf.Cos(a*9));
    float grain=Mathf.PerlinNoise(x*.67f+13,y*.67f+31);float core=Mathf.Exp(-r*r*7)*.52f;float lip=Mathf.Exp(-Mathf.Pow((r-.55f)/.16f,2))*.22f;float edge=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.7f,1,r));
    texture.SetPixel(x,y,new Color(.075f,.068f,.06f,(core+lip)*edge*(.7f+grain*.3f)));
   }texture.Apply();sprite=Sprite.Create(texture,new Rect(0,0,n,n),Vector2.one*.5f,n);
  }
  public void AddMark(Vector3 position){
   if(sprite==null)BuildSprite();Trim(Mathf.Max(1,maxMarks)-1);
   var g=new GameObject("阵亡弹坑 "+(++serial));g.transform.SetParent(transform,false);g.transform.position=new Vector3(position.x,position.y,0);
   g.transform.rotation=Quaternion.Euler(0,0,serial*137.508f);g.transform.localScale=new Vector3(markSize*(.85f+(serial%5)*.07f),markSize*(.8f+(serial%3)*.08f),1);
   var view=g.AddComponent<SpriteRenderer>();view.sprite=sprite;view.sortingOrder=5;marks.Enqueue(view);observedBy[view]=world==null?null:world.currentPlayer;RefreshVisibility();
  }
  void Trim(int limit){while(marks.Count>limit){var first=marks.Dequeue();if(first!=null){observedBy.Remove(first);first.gameObject.SetActive(false);Destroy(first.gameObject);}}}
  public void Observe(){
   if(world==null||!world.Initialized)return;
   foreach(var unit in world.units){if(unit==null)continue;
    bool alive=unit.Alive;bool visible=alive&&world.IsVisible(world.currentPlayer,unit);
    if(previous.TryGetValue(unit,out var old)&&old.alive&&!alive&&old.onMap&&
       (old.owner==world.currentPlayer||!world.fogEnabled||(old.visible&&world.VisibleAt(world.currentPlayer,unit.transform.position))))AddMark(unit.transform.position);
    previous[unit]=new Snapshot{alive=alive,onMap=unit.OnMap,visible=visible,owner=unit.ownerId};
   }
   stale.Clear();foreach(var entry in previous)if(entry.Key==null||System.Array.IndexOf(world.units,entry.Key)<0)stale.Add(entry.Key);foreach(var unit in stale)previous.Remove(unit);
   Trim(Mathf.Max(1,maxMarks));RefreshVisibility();
  }
  // A witnessed ground mark is remembered even after its observer dies or moves away.
  // Never expose marks witnessed only by the other player's view.
  void RefreshVisibility(){foreach(var mark in marks)if(mark!=null)mark.enabled=world==null||!world.fogEnabled||!observedBy.TryGetValue(mark,out var player)||player==null||player==world.currentPlayer;}
  void LateUpdate(){Observe();}
  public void Clear(){Trim(0);observedBy.Clear();previous.Clear();}
  void OnDisable(){Clear();}
  void OnDestroy(){Clear();if(sprite!=null){Destroy(sprite.texture);Destroy(sprite);}}
 }
}


