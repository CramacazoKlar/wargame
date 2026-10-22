using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
namespace CampaignDemo
{
    // Screen-space frame with world-coordinate ticks. No terrain texture or input ownership.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class StaffViewportGrid:MaskableGraphic
    {
        public Camera mapCamera;public Font labelFont;public float metersPerWorldUnit=1000;
        public float Step {get;private set;}public Rect Frame {get;private set;}
        readonly List<Text> labels=new List<Text>();int used;
        Vector3 lastPosition;Quaternion lastRotation;float lastZoom=-1;Vector2 lastSize;bool force=true;
        public static float NiceStep(float desired){float power=Mathf.Pow(10,Mathf.Floor(Mathf.Log10(Mathf.Max(.00001f,desired))));float value=desired/power;return (value<=1?1:value<=2?2:value<=5?5:10)*power;}
        protected override void Awake(){base.Awake();raycastTarget=false;}
        void LateUpdate(){RefreshGrid();}
        public void RefreshGrid()
        {
            if(mapCamera==null)return;var size=rectTransform.rect.size;
            if(!force&&lastPosition==mapCamera.transform.position&&lastRotation==mapCamera.transform.rotation&&lastZoom==mapCamera.orthographicSize&&lastSize==size)return;
            force=false;lastPosition=mapCamera.transform.position;lastRotation=mapCamera.transform.rotation;lastZoom=mapCamera.orthographicSize;lastSize=size;
            float pixelScale=size.y/Mathf.Max(1,mapCamera.pixelHeight);float padding=30*pixelScale;
            Frame=new Rect(-size.x*.5f+padding,-size.y*.5f+60*pixelScale,size.x-2*padding,size.y-100*pixelScale);
            Step=NiceStep(2*mapCamera.orthographicSize*115/Mathf.Max(1,mapCamera.pixelHeight));
            used=0;Walk(null);Label("网格 "+(Step*metersPerWorldUnit>=1000?(Step*metersPerWorldUnit/1000).ToString("0.##")+" km":(Step*metersPerWorldUnit).ToString("0.##")+" m"),new Vector2(Frame.xMax-65*pixelScale,Frame.yMin-20*pixelScale),pixelScale);
            for(int i=used;i<labels.Count;i++)labels[i].gameObject.SetActive(false);SetVerticesDirty();
        }
        Vector2 Screen(Vector3 world){var p=mapCamera.WorldToViewportPoint(world);var r=rectTransform.rect;return new Vector2((p.x-.5f)*r.width,(p.y-.5f)*r.height);}
        Vector3 World(Vector2 p){var r=rectTransform.rect;return MapProjection.ViewportGround(mapCamera,new Vector2(p.x/r.width+.5f,p.y/r.height+.5f));}
        void Label(string value,Vector2 at,float scale)
        {
            Text text;if(used<labels.Count)text=labels[used];else{var g=new GameObject("Viewport coordinate",typeof(RectTransform),typeof(Text),typeof(StaffKeepColor));g.transform.SetParent(transform,false);text=g.GetComponent<Text>();text.raycastTarget=false;text.alignment=TextAnchor.MiddleCenter;text.fontStyle=FontStyle.Bold;text.horizontalOverflow=HorizontalWrapMode.Overflow;labels.Add(text);}used++;
            text.gameObject.SetActive(true);text.font=labelFont;text.fontSize=Mathf.Max(9,Mathf.RoundToInt(12*scale));text.color=StaffMapStyle.Ink;text.text=value;text.rectTransform.anchorMin=text.rectTransform.anchorMax=new Vector2(.5f,.5f);text.rectTransform.sizeDelta=new Vector2(100*scale,22*scale);text.rectTransform.anchoredPosition=at;
        }
        void Walk(VertexHelper vh)
        {
            if(mapCamera==null||Step<=0)return;float scale=rectTransform.rect.height/Mathf.Max(1,mapCamera.pixelHeight);
            var corners=new[]{World(new Vector2(Frame.xMin,Frame.yMin)),World(new Vector2(Frame.xMax,Frame.yMin)),World(new Vector2(Frame.xMax,Frame.yMax)),World(new Vector2(Frame.xMin,Frame.yMax))};float xmin=float.MaxValue,xmax=float.MinValue,ymin=float.MaxValue,ymax=float.MinValue;
            foreach(var p in corners){xmin=Mathf.Min(xmin,p.x);xmax=Mathf.Max(xmax,p.x);ymin=Mathf.Min(ymin,p.y);ymax=Mathf.Max(ymax,p.y);}
            for(float x=Mathf.Ceil(xmin/Step)*Step;x<=xmax+Step*.001f;x+=Step){Vector2 a=Screen(new Vector3(x,ymin-Step)),b=Screen(new Vector3(x,ymax+Step));if(!Clip(ref a,ref b,Frame))continue;if(vh==null){Label((x*metersPerWorldUnit/1000).ToString("0.##"),new Vector2(a.x,a.y-15*scale),scale);Label((x*metersPerWorldUnit/1000).ToString("0.##"),new Vector2(b.x,b.y+15*scale),scale);}else Ink(vh,a,b,scale,x+31);}
            for(float y=Mathf.Ceil(ymin/Step)*Step;y<=ymax+Step*.001f;y+=Step){Vector2 a=Screen(new Vector3(xmin-Step,y)),b=Screen(new Vector3(xmax+Step,y));if(!Clip(ref a,ref b,Frame))continue;if(vh==null){Label((y*metersPerWorldUnit/1000).ToString("0.##"),new Vector2(a.x-16*scale,a.y),scale);Label((y*metersPerWorldUnit/1000).ToString("0.##"),new Vector2(b.x+16*scale,b.y),scale);}else Ink(vh,a,b,scale,y+79);}
        }
        static bool Clip(ref Vector2 a,ref Vector2 b,Rect r){var d=b-a;float lo=0,hi=1;float[] p={-d.x,d.x,-d.y,d.y},q={a.x-r.xMin,r.xMax-a.x,a.y-r.yMin,r.yMax-a.y};for(int i=0;i<4;i++){if(Mathf.Abs(p[i])<.00001f){if(q[i]<0)return false;continue;}float t=q[i]/p[i];if(p[i]<0)lo=Mathf.Max(lo,t);else hi=Mathf.Min(hi,t);if(lo>hi)return false;}b=a+d*hi;a+=d*lo;return true;}
        void Ink(VertexHelper vh,Vector2 a,Vector2 b,float scale,float salt){int count=Mathf.Clamp(Mathf.CeilToInt(Vector2.Distance(a,b)/(40*scale)),2,16);for(int i=0;i<count;i++){var start=Vector2.Lerp(a,b,i/(float)count);var end=Vector2.Lerp(a,b,(i+1f)/count);var world=World((start+end)*.5f);float pressure=StaffMapCanvas.Noise(world.x*2.1f+world.y*1.9f,salt,217);if(pressure<.29f)continue;var ink=StaffMapStyle.Ink;ink.a=Mathf.Lerp(.32f,.58f,pressure);Line(vh,start,end,scale*.8f,ink);}}
        public int BorderGaps {get;private set;}
        public int BorderTexturedSamples {get;private set;}
        void BorderStroke(VertexHelper vh,Vector2 origin,Vector2 direction,float from,float to,float scale,int side)
        {
            var normal=new Vector2(-direction.y,direction.x);int count=Mathf.Max(1,Mathf.CeilToInt((to-from)/(24f*scale)));
            for(int i=0;i<count;i++){
                float start=Mathf.Lerp(from,to,i/(float)count),end=Mathf.Lerp(from,to,(i+1f)/count),mid=(start+end)*.5f;
                float pressure=StaffMapCanvas.Noise(mid/(scale*18),side+5,254),grain=StaffMapCanvas.Hash(Mathf.FloorToInt(mid/(scale*2.5f)),side,731);
                float w0=(StaffMapCanvas.Noise(start/(scale*14),side+8,217)-.5f)*scale*.7f,w1=(StaffMapCanvas.Noise(end/(scale*14),side+8,217)-.5f)*scale*.7f;
                var a=origin+direction*start+normal*w0;var b=origin+direction*end+normal*w1;
                float width=scale*Mathf.Lerp(.85f,1.6f,pressure);var ink=StaffMapStyle.Hex(0x171815);ink.a=Mathf.Lerp(.64f,.94f,pressure)*Mathf.Lerp(.76f,1,grain);
                // Faint feathering and occasional retraced graphite match the terrain road ink.
                var feather=ink;feather.a=.075f;Line(vh,a,b,width+scale*1.5f,feather);Line(vh,a,b,width,ink);
                if(grain>.76f){var echo=ink;echo.a=.14f;Line(vh,a+normal*scale*.95f,b+normal*scale*.95f,scale*.5f,echo);}
                if(grain<.11f){var dark=StaffMapStyle.Hex(0x11120F);dark.a=.23f;Line(vh,Vector2.Lerp(a,b,.16f),Vector2.Lerp(a,b,.79f),width*.85f,dark);}
                BorderTexturedSamples++;
            }
        }
        void BorderInk(VertexHelper vh,Vector2 a,Vector2 b,float scale,int side)
        {
            float length=Vector2.Distance(a,b);var direction=(b-a)/length;float arc=0;int slot=0;
            while(arc<length){
                float span=(12+StaffMapCanvas.Hash(slot,side,731)*25)*scale;float end=Mathf.Min(length,arc+span);
                bool gap=arc>16*scale&&end<length-16*scale&&StaffMapCanvas.Hash(slot,side,947)<.13f;
                if(gap){float cut=Mathf.Min((3+StaffMapCanvas.Hash(slot,side,1121)*8)*scale,span*.55f);float mid=(arc+end)*.5f;BorderStroke(vh,a,direction,arc,mid-cut*.5f,scale,side);BorderStroke(vh,a,direction,mid+cut*.5f,end,scale,side);BorderGaps++;}
                else BorderStroke(vh,a,direction,arc,end,scale,side);
                arc=end;slot++;
            }
        }
        static void Line(VertexHelper vh,Vector2 a,Vector2 b,float width,Color color){var n=new Vector2(-(b-a).y,(b-a).x).normalized*width*.5f;int i=vh.currentVertCount;vh.AddVert(a-n,color,Vector2.zero);vh.AddVert(a+n,color,Vector2.zero);vh.AddVert(b+n,color,Vector2.zero);vh.AddVert(b-n,color,Vector2.zero);vh.AddTriangle(i,i+1,i+2);vh.AddTriangle(i,i+2,i+3);}
        protected override void OnPopulateMesh(VertexHelper vh){vh.Clear();if(mapCamera==null||Step<=0)return;Walk(vh);float scale=rectTransform.rect.height/Mathf.Max(1,mapCamera.pixelHeight);var ink=StaffMapStyle.Ink;ink.a=.95f;var a=new Vector2(Frame.xMin,Frame.yMin);var b=new Vector2(Frame.xMax,Frame.yMin);var c=new Vector2(Frame.xMax,Frame.yMax);var d=new Vector2(Frame.xMin,Frame.yMax);BorderGaps=BorderTexturedSamples=0;BorderInk(vh,a,b,scale,0);BorderInk(vh,b,c,scale,1);BorderInk(vh,c,d,scale,2);BorderInk(vh,d,a,scale,3);}
    }
}




