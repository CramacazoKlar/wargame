using System.Collections.Generic;
using UnityEngine;
namespace CampaignDemo
{
    // Presentation only: the existing BattleWorld visibility rules remain authoritative.
    [DefaultExecutionOrder(950)]
    public sealed class StaffVisibilityShade:MonoBehaviour
    {
        public BattleWorld world;
        [Range(0, .8f)] public float darkness=.52f;
        [Range(.04f,.4f)] public float featherRatio=.14f;
        [Range(0,.3f)] public float rimBrightness=.16f;
        public Color shadow=new Color(.095f,.115f,.12f,1);
        public Color rim=new Color(1,.97f,.86f,1);
        struct Observer {public Vector2 at;public float radius,feather;}
        readonly List<Observer> observers=new List<Observer>();
        SpriteRenderer display;Texture2D texture;Sprite sprite;Color32[] pixels;float[] margins;float next;int lastHash;bool hasMask;
        public int RasterBuilds {get;private set;}
        public Rect RenderBounds {get;private set;}
        public bool Active {get{return world!=null&&world.Initialized&&world.fogEnabled&&!world.SimulationEnded;}}
        public void Gather()
        {
            observers.Clear();if(!Active)return;
            foreach(var u in world.units)if(u!=null&&u.gameObject.activeInHierarchy&&u.OnMap&&u.ownerId==world.currentPlayer&&u.combatDefinition!=null){float radius=Mathf.Max(0,u.combatDefinition.visionRadius);observers.Add(new Observer{at=u.transform.position,radius=radius,feather=Mathf.Clamp(radius*featherRatio,.08f,.4f)});}
        }
        public bool IsShadowedAt(Vector2 at){if(!Active)return false;foreach(var observer in observers)if((at-observer.at).sqrMagnitude<=observer.radius*observer.radius)return false;return true;}
        public Color ShadeAt(Vector2 at)
        {
            if(!Active)return Color.clear;float margin=-1;
            foreach(var o in observers){var delta=at-o.at;float outer=o.radius+o.feather;if(delta.sqrMagnitude>=outer*outer)continue;margin=Mathf.Max(margin,(o.radius-delta.magnitude)/o.feather);if(margin>=1)return Color.clear;}
            return ShadeForMargin(margin);
        }
        Color ShadeForMargin(float margin){
            if(margin>=1)return Color.clear;
            if(margin>=0){var glow=rim;glow.a=rimBrightness*(1-margin)*(1-margin);return glow;}
            float t=Mathf.SmoothStep(0,1,-margin);var color=Color.Lerp(rim,shadow,t);color.a=Mathf.Lerp(rimBrightness,darkness,t);return color;
        }
        public Color TintAt(Color background,Vector2 at){var shade=ShadeAt(at);return Color.Lerp(background,new Color(shade.r,shade.g,shade.b,background.a),shade.a);}
        public void RefreshShade()
        {
            if(world==null)world=GetComponent<BattleWorld>();Gather();if(!Active){if(display!=null)display.enabled=false;return;}
            var camera=world.setup.mapCamera;if(camera==null)return;
            if(!MapViewBounds.TryGet(world.setup.roadMap,world.setup.freeMarchMap,out var bounds))return;
            if(bounds.width<=0||bounds.height<=0){if(display!=null)display.enabled=false;return;}RenderBounds=bounds;
            int hash=bounds.GetHashCode()^darkness.GetHashCode()^rimBrightness.GetHashCode()^shadow.GetHashCode()^rim.GetHashCode();foreach(var o in observers)unchecked{hash=hash*397^o.at.GetHashCode()^o.radius.GetHashCode()^o.feather.GetHashCode();}
            if(hasMask&&hash==lastHash&&display!=null){display.enabled=true;return;}
            int width=Mathf.Clamp(Mathf.RoundToInt(bounds.width*12),256,1024),height=Mathf.Clamp(Mathf.RoundToInt(width*bounds.height/bounds.width),128,1024);
            if(texture==null||texture.width!=width||texture.height!=height){ReleaseTexture();texture=new Texture2D(width,height,TextureFormat.RGBA32,false){name="Map anchored soft vision mask",filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp};pixels=new Color32[width*height];margins=new float[pixels.Length];sprite=Sprite.Create(texture,new Rect(0,0,width,height),Vector2.one*.5f,100,0,SpriteMeshType.FullRect);}
            if(display==null){var obj=new GameObject("视野盲区 · 柔光过渡");obj.transform.SetParent(transform,false);display=obj.AddComponent<SpriteRenderer>();display.sortingOrder=4;}
            Color32 hidden=ShadeForMargin(-1);for(int i=0;i<pixels.Length;i++){pixels[i]=hidden;margins[i]=-1;}
            float dx=bounds.width/width,dy=bounds.height/height;
            foreach(var o in observers){float reach=o.radius+o.feather;int x0=Mathf.Clamp(Mathf.FloorToInt((o.at.x-reach-bounds.xMin)/dx),0,width-1),x1=Mathf.Clamp(Mathf.CeilToInt((o.at.x+reach-bounds.xMin)/dx),0,width-1),y0=Mathf.Clamp(Mathf.FloorToInt((o.at.y-reach-bounds.yMin)/dy),0,height-1),y1=Mathf.Clamp(Mathf.CeilToInt((o.at.y+reach-bounds.yMin)/dy),0,height-1);
                for(int y=y0;y<=y1;y++)for(int x=x0;x<=x1;x++){int at=y*width+x;float px=bounds.xMin+(x+.5f)*dx-o.at.x,py=bounds.yMin+(y+.5f)*dy-o.at.y;float distance=px*px+py*py;if(distance>=reach*reach)continue;float margin=(o.radius-Mathf.Sqrt(distance))/o.feather;if(margin<=margins[at])continue;margins[at]=margin;pixels[at]=ShadeForMargin(margin);}
            }
            texture.SetPixels32(pixels);texture.Apply(false);display.sprite=sprite;display.enabled=true;display.transform.position=new Vector3(bounds.center.x,bounds.center.y,0);display.transform.rotation=Quaternion.identity;var scale=BattleUnitView.SymbolScale(display.transform.parent);display.transform.localScale=new Vector3(bounds.width*100/width/Mathf.Max(.0001f,scale.x),bounds.height*100/height/Mathf.Max(.0001f,scale.y),1);lastHash=hash;hasMask=true;RasterBuilds++;
        }
        void LateUpdate(){if(Time.unscaledTime<next)return;next=Time.unscaledTime+.15f;RefreshShade();}
        void OnDisable(){if(display!=null)display.enabled=false;}
        void ReleaseTexture(){if(sprite!=null)Destroy(sprite);if(texture!=null)Destroy(texture);sprite=null;texture=null;}
        void OnDestroy(){ReleaseTexture();if(display!=null)Destroy(display.gameObject);}
    }
}

