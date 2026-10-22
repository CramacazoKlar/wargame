using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
namespace CampaignDemo
{
    [DisallowMultipleComponent,DefaultExecutionOrder(950)]
    public sealed class StaffObjectiveMarker:MonoBehaviour
    {
        [Min(16)] public float diameterPixels=24;
        [Tooltip("留空时根据实际据点列表显示 OBJ 01、OBJ 02 等。")]
        public string captionOverride="";
        public bool neutralGray;
        [Min(0)] public int objectiveNumber;
        public string Caption {get{if(!string.IsNullOrWhiteSpace(captionOverride))return captionOverride;int number=objectiveNumber;if(number<=0&&world!=null&&world.points!=null)number=Array.IndexOf(world.points,point)+1;return point!=null?point.displayName:"OBJ "+Mathf.Max(1,number).ToString("00");}}
        public Text Label {get{return label;}}
        public float ActualPixels {get;private set;}
        static readonly Dictionary<int,Sprite> sprites=new Dictionary<int,Sprite>();
        RecoveryPoint point;SpriteRenderer ring;BattleWorld world;Text label;Canvas canvas;Material material;
        public static Sprite Circle(int width){return Circle(width,3);}
        public static Sprite Circle(int width,int faction)
        {
            int cacheKey=width*10+faction;var edge=faction==0?StaffMapStyle.Blue:faction==1?StaffMapStyle.Red:faction==3?Color.white:faction==4?new Color(.5f,.5f,.5f):StaffMapStyle.Ink;var interior=faction==0?StaffMapStyle.BlueFill:faction==1?StaffMapStyle.RedFill:faction==3?new Color(1,1,1,.34f):faction==4?new Color(.65f,.65f,.65f,.85f):StaffMapStyle.Panel;
            if(sprites.TryGetValue(cacheKey,out var s)&&s!=null)return s;
            var texture=new Texture2D(width,width,TextureFormat.RGBA32,false){name="Staff objective ring "+width,filterMode=FilterMode.Point,wrapMode=TextureWrapMode.Clamp};var pixels=new Color[width*width];float radius=width*.41f,stroke=Mathf.Max(1.6f,width*.075f);
            for(int y=0;y<width;y++)for(int x=0;x<width;x++){float distance=Vector2.Distance(new Vector2(x+.5f,y+.5f),Vector2.one*width*.5f);float ink=Mathf.Clamp01(stroke*.5f+.55f-Mathf.Abs(distance-radius));float angle=Mathf.Atan2(y+.5f-width*.5f,x+.5f-width*.5f);if(width>=24&&(Mathf.Abs(angle-.7f)<.055f||Mathf.Abs(angle+2.1f)<.045f))ink*=.12f;float fill=distance<radius-stroke*.5f?1:0;pixels[y*width+x]=fill>0?interior:new Color(edge.r,edge.g,edge.b,ink);}
            texture.SetPixels(pixels);texture.Apply();s=Sprite.Create(texture,new Rect(0,0,width,width),Vector2.one*.5f,100,0,SpriteMeshType.FullRect);s.name="Staff objective ring "+width;sprites[cacheKey]=s;return s;
        }
        void Start(){point=GetComponent<RecoveryPoint>();ring=GetComponent<SpriteRenderer>();world=FindObjectOfType<BattleWorld>();}
        public void RefreshPresentation()
        {
            if(point==null)point=GetComponent<RecoveryPoint>();if(ring==null)ring=GetComponent<SpriteRenderer>();if(world==null)world=FindObjectOfType<BattleWorld>();if(point==null||ring==null)return;
            var camera=world!=null&&world.setup!=null?world.setup.mapCamera:Camera.main;if(camera==null)return;
            int pixels=Mathf.Clamp(Mathf.RoundToInt(diameterPixels*camera.pixelHeight/900f),20,80);ActualPixels=pixels;ring.sprite=Circle(pixels,string.IsNullOrEmpty(point.ownerId)?(neutralGray?4:2):point.ownerId==BattleFactions.BluePlayerId?0:1);ring.enabled=true;
            if(material==null){material=new Material(Shader.Find("Sprites/Default"));ring.sharedMaterial=material;}
            var ink=string.IsNullOrEmpty(point.ownerId)?StaffMapStyle.Ink:StaffMapStyle.Faction(point.ownerId==BattleFactions.BluePlayerId);ring.color=Color.white;ring.sortingOrder=22;
            float size=pixels*MapProjection.PixelSize(camera,transform.position);var scale=BattleUnitView.SymbolScale(transform);ring.drawMode=SpriteDrawMode.Sliced;ring.size=new Vector2(size/Mathf.Max(.0001f,scale.x),size/Mathf.Max(.0001f,scale.y));
            if(label==null)
            {
                var parent=GameObject.Find("Staff unit designations");if(parent==null)return;canvas=parent.GetComponentInParent<Canvas>();var g=new GameObject("Objective caption "+point.pointId,typeof(RectTransform),typeof(Text));g.transform.SetParent(parent.transform,false);label=g.GetComponent<Text>();var hud=FindObjectOfType<CampaignHUD>();label.font=TabletopPresentation.MapFont;label.fontStyle=FontStyle.Normal;label.alignment=TextAnchor.UpperCenter;label.horizontalOverflow=HorizontalWrapMode.Overflow;label.verticalOverflow=VerticalWrapMode.Overflow;label.raycastTarget=false;label.rectTransform.pivot=new Vector2(.5f,1);label.gameObject.AddComponent<Outline>().effectDistance=new Vector2(1,-1);label.GetComponent<Outline>().effectColor=StaffMapStyle.Paper;
            }
            var root=canvas.transform as RectTransform;var vp=camera.WorldToViewportPoint(transform.position);bool visible=vp.z>0&&vp.x>=0&&vp.x<=1&&vp.y>=0&&vp.y<=1;if(world.GetComponent<TabletopPresentation>()!=null)visible=false;label.gameObject.SetActive(visible);if(!visible)return;
            label.text=Caption;label.color=ink;float unitsPerPixel=root.rect.height/Mathf.Max(1,camera.pixelHeight);label.fontSize=Mathf.Max(10,Mathf.RoundToInt(pixels*.58f*unitsPerPixel));label.rectTransform.sizeDelta=new Vector2(240,32);label.rectTransform.anchoredPosition=new Vector2((vp.x-.5f)*root.rect.width,(vp.y-.5f)*root.rect.height-(pixels*.5f+3)*unitsPerPixel);
        }
        void LateUpdate(){RefreshPresentation();}
        void OnDisable(){if(label!=null)label.gameObject.SetActive(false);}
        void OnDestroy(){if(label!=null)Destroy(label.gameObject);if(material!=null)Destroy(material);}
    }
}


