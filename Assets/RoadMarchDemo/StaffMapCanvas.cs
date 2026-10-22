using UnityEngine;
namespace CampaignDemo
{
    // Deterministic CPU ink rasterisation. No image assets or frame-dependent random strokes.
    public sealed class StaffMapCanvas
    {
        public readonly int width,height;public readonly Rect bounds;public readonly Color[] pixels;
        public float Scale {get{return width/bounds.width;}}
        public StaffMapCanvas(int w,int h,Rect area){width=w;height=h;bounds=area;pixels=new Color[w*h];}
        public static float Noise(float x,float y,int seed=0){int ix=Mathf.FloorToInt(x),iy=Mathf.FloorToInt(y);float tx=x-ix,ty=y-iy;tx=tx*tx*(3-2*tx);ty=ty*ty*(3-2*ty);return Mathf.Lerp(Mathf.Lerp(Hash(ix,iy,seed),Hash(ix+1,iy,seed),tx),Mathf.Lerp(Hash(ix,iy+1,seed),Hash(ix+1,iy+1,seed),tx),ty);}
        public static float Hash(int x,int y,int seed=0){uint n=unchecked((uint)(x*374761393+y*668265263+seed*144269));n=(n^(n>>13))*1274126177u;return (n^(n>>16))/(float)uint.MaxValue;}
        public Vector2 Pixel(Vector2 p){return new Vector2((p.x-bounds.xMin)*Scale,(p.y-bounds.yMin)*height/bounds.height);}
        public void Ink(int x,int y,Color color,float opacity){if(x<0||y<0||x>=width||y>=height||opacity<=0)return;int i=y*width+x;pixels[i]=Color.Lerp(pixels[i],color,Mathf.Clamp01(opacity*color.a));}
        public void Line(Vector2 a,Vector2 b,float thickness,Color color,float alpha=1,bool rough=true)
        {
            a=Pixel(a);b=Pixel(b);float radius=Mathf.Max(.35f,thickness*Scale*.5f);Vector2 d=b-a;float sq=d.sqrMagnitude;
            int x0=Mathf.Max(0,Mathf.FloorToInt(Mathf.Min(a.x,b.x)-radius-1)),x1=Mathf.Min(width-1,Mathf.CeilToInt(Mathf.Max(a.x,b.x)+radius+1));
            int y0=Mathf.Max(0,Mathf.FloorToInt(Mathf.Min(a.y,b.y)-radius-1)),y1=Mathf.Min(height-1,Mathf.CeilToInt(Mathf.Max(a.y,b.y)+radius+1));
            for(int y=y0;y<=y1;y++)for(int x=x0;x<=x1;x++){var p=new Vector2(x+.5f,y+.5f);float t=sq<.0001f?0:Mathf.Clamp01(Vector2.Dot(p-a,d)/sq);float coverage=Mathf.Clamp01(radius+.6f-Vector2.Distance(p,a+t*d));if(coverage>0)Ink(x,y,color,coverage*alpha*(rough?.65f+.35f*Hash(x/2,y/2,31):1));}
        }
        public void Ellipse(Vector2 centre,Vector2 radius,Color color,float alpha=1)
        {
            var p=Pixel(centre);var r=new Vector2(Mathf.Max(.7f,radius.x*Scale),Mathf.Max(.7f,radius.y*Scale));
            for(int y=Mathf.Max(0,(int)(p.y-r.y-1));y<=Mathf.Min(height-1,(int)(p.y+r.y+1));y++)for(int x=Mathf.Max(0,(int)(p.x-r.x-1));x<=Mathf.Min(width-1,(int)(p.x+r.x+1));x++){float dist=new Vector2((x+.5f-p.x)/r.x,(y+.5f-p.y)/r.y).magnitude;Ink(x,y,color,Mathf.Clamp01((1-dist)*Mathf.Min(r.x,r.y)+.5f)*alpha);}
        }
        public void Polyline(System.Collections.Generic.IList<Vector2> p,float thickness,Color color,float alpha=1,bool closed=false){for(int i=1;i<p.Count;i++)Line(p[i-1],p[i],thickness,color,alpha);if(closed&&p.Count>2)Line(p[p.Count-1],p[0],thickness,color,alpha);}
        public void Rectangle(Vector2 centre,Vector2 halfSize,float angle,Color color,float alpha=1)
        {
            var at=Pixel(centre);var half=halfSize*Scale;float cs=Mathf.Cos(angle),sn=Mathf.Sin(angle),extent=half.magnitude+1;
            for(int y=Mathf.Max(0,(int)(at.y-extent));y<=Mathf.Min(height-1,(int)(at.y+extent));y++)for(int x=Mathf.Max(0,(int)(at.x-extent));x<=Mathf.Min(width-1,(int)(at.x+extent));x++){float dx=x+.5f-at.x,dy=y+.5f-at.y;float px=Mathf.Abs(dx*cs+dy*sn),py=Mathf.Abs(-dx*sn+dy*cs);Ink(x,y,color,Mathf.Clamp01(Mathf.Min(half.x-px,half.y-py)+.5f)*alpha);}
        }
    }
}
