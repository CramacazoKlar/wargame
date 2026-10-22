using System.Collections.Generic;
using UnityEngine;
namespace CampaignDemo
{
    // One palette and one pixel-aligned symbol source for map, UI, thumbnails and exports.
    public static class StaffMapStyle
    {
        public static readonly Color Paper=Hex(0xE7E0CF),Panel=Hex(0xEDE7D9),Inset=Hex(0xDDD6C5),Ink=Hex(0x171815),Rule=Hex(0xA6A294);
        public static readonly Color Blue=Hex(0x3D5969),Red=Hex(0x905342),Forest=Hex(0x82916D),Mountain=Hex(0xD3C6AA),HighMountain=Hex(0xC2B18E),Water=Hex(0x829FAC);
        public static readonly Color BlueFill=Hex(0xB8CBD2),RedFill=Hex(0xD5B4A4);
        public static Color Hex(int rgb){return new Color(((rgb>>16)&255)/255f,((rgb>>8)&255)/255f,(rgb&255)/255f);}
        public static Color Faction(bool blue){return blue?Blue:Red;}
        public static Color Terrain(TerrainKind kind){return kind==TerrainKind.Water?Water:kind==TerrainKind.Forest?Forest:kind==TerrainKind.Mountain?Mountain:kind==TerrainKind.HighMountain||kind==TerrainKind.Blocked?HighMountain:kind==TerrainKind.Road?Ink:Paper;}
        public static string RoleName(UnitRole role){switch(role){case UnitRole.Infantry:return "步兵";case UnitRole.Scout:return "侦察";case UnitRole.Artillery:return "炮兵";case UnitRole.RocketArtillery:return "火箭兵";case UnitRole.Radar:return "雷达";case UnitRole.Engineer:return "工兵";case UnitRole.Tank:return "装甲";case UnitRole.PersonnelCarrier:return "运兵车";case UnitRole.Logistics:return "运输";default:return "山地步兵";}}
    }

    public enum StaffSymbolDetail { Full,Compact,Block }
    public static class StaffMapSymbols
    {
        static readonly Dictionary<string,Sprite> cache=new Dictionary<string,Sprite>();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset(){foreach(var s in cache.Values)if(s!=null){Object.Destroy(s.texture);Object.Destroy(s);}cache.Clear();}
        // Hysteresis is expressed in map coverage, independent of desktop resolution.
        public static StaffSymbolDetail Detail(float ratio,StaffSymbolDetail previous)
        {
            if(previous==StaffSymbolDetail.Block&&ratio>1.45f)return StaffSymbolDetail.Block;
            if(ratio>1.65f)return StaffSymbolDetail.Block;
            if(previous==StaffSymbolDetail.Full&&ratio<.48f)return StaffSymbolDetail.Full;
            return ratio<.38f?StaffSymbolDetail.Full:StaffSymbolDetail.Compact;
        }
        public static int Width(StaffSymbolDetail detail){return detail==StaffSymbolDetail.Full?44:detail==StaffSymbolDetail.Compact?32:8;}
        public static Sprite Unit(UnitRole role,ArmyEchelon echelon,bool blue,int width=44,bool unknown=false,bool block=false)
        {
            string key=$"{role}-{echelon}-{blue}-{width}-{unknown}-{block}";
            if(cache.TryGetValue(key,out var sprite)&&sprite!=null)return sprite;
            if(!unknown&&(role==UnitRole.Radar||role==UnitRole.RocketArtillery)){var smooth=Resources.Load<SmoothUnitSymbols>("Radar/"+(role==UnitRole.Radar?"Radar":"Rocket")+"Symbols");if(smooth!=null){var icon=smooth.Select(Mathf.Max(16,width),blue?BattleFactions.BluePlayerId:BattleFactions.RedPlayerId);cache[key]=icon;return icon;}}
            var canvas=new Pen(width,block?width:Mathf.RoundToInt(width*.85f),StaffMapStyle.Faction(blue));
            if(block)canvas.Fill(new Rect(.1f,.1f,.8f,.8f),canvas.ink);
            else
            {
                // Pale opaque interiors keep linework legible over dark forest and roads.
                if(blue){canvas.Fill(new Rect(.10f,.18f,.8f,.56f),StaffMapStyle.BlueFill);canvas.Box(.10f,.18f,.90f,.74f);}
                else {canvas.Diamond(.50f,.43f,.34f,.34f*canvas.Aspect,true);}
                if(unknown){canvas.Line(.42f,.55f,.49f,.60f);canvas.Line(.49f,.60f,.58f,.55f);canvas.Line(.58f,.55f,.50f,.43f);canvas.Dot(.50f,.32f,.025f);}
                else
                {
                    float l=blue?.15f:.35f,r=blue?.85f:.65f,b=blue?.25f:.29f,t=blue?.66f:.58f;
                    switch(role)
                    {
                        case UnitRole.Infantry:canvas.Line(l,b,r,t);canvas.Line(l,t,r,b);break;
                        case UnitRole.Scout:canvas.Line(l,b,r,t);break;
                        case UnitRole.Tank:canvas.Ellipse(.5f,.46f,blue?.23f:.15f,.13f);break;
                        case UnitRole.Artillery:canvas.Dot(.5f,.46f,.065f);break;
                        case UnitRole.Engineer:canvas.Line(.32f,.55f,.68f,.55f);for(int i=0;i<3;i++)canvas.Line(.32f+i*.18f,.55f,.32f+i*.18f,.34f);break;
                        case UnitRole.PersonnelCarrier:canvas.Ellipse(.5f,.46f,.17f,.11f);canvas.Line(l,b,r,t);break;
                        case UnitRole.Logistics:canvas.Line(.32f,.55f,.68f,.55f);canvas.Line(.32f,.55f,.32f,.36f);canvas.Line(.32f,.36f,.68f,.36f);canvas.Line(.68f,.36f,.68f,.55f);canvas.Dot(.38f,.30f,.025f);canvas.Dot(.62f,.30f,.025f);break;
                        case UnitRole.MountainInfantry:canvas.Line(.28f,.33f,.5f,.60f);canvas.Line(.5f,.60f,.72f,.33f);canvas.Line(.28f,.33f,.72f,.33f);canvas.Line(.35f,.60f,.65f,.33f);break;
                    }
                }
                if(!unknown)
                {
                    int level=Mathf.Clamp((int)echelon,0,7);
                    if(level<=2){int dots=level+1;for(int i=0;i<dots;i++)canvas.Dot(.5f+(i-(dots-1)*.5f)*.13f,.88f,.025f);}
                    else if(level<=4){int bars=level-2;for(int i=0;i<bars;i++)canvas.Line(.5f+(i-(bars-1)*.5f)*.12f,.80f,.5f+(i-(bars-1)*.5f)*.12f,.96f);}
                    else {int crosses=level-4;for(int i=0;i<crosses;i++){float x=.5f+(i-(crosses-1)*.5f)*.18f;canvas.Line(x-.055f,.82f,x+.055f,.95f);canvas.Line(x-.055f,.95f,x+.055f,.82f);}}
                }
            }
            if(!block&&width>=24)canvas.Wear(blue);
            sprite=canvas.Sprite("Staff_"+key);cache[key]=sprite;return sprite;
        }
        public static Sprite Depot(string owner,int width=40){string key="depot-"+owner+"-"+width;if(cache.TryGetValue(key,out var sprite)&&sprite!=null)return sprite;var pen=new Pen(width,width,StaffMapStyle.Ink);Color fill=owner==BattleFactions.BluePlayerId?StaffMapStyle.BlueFill:owner==BattleFactions.RedPlayerId?StaffMapStyle.RedFill:StaffMapStyle.Panel;pen.Fill(new Rect(.12f,.12f,.76f,.76f),fill);pen.Box(.12f,.12f,.88f,.88f);pen.Box(.28f,.25f,.72f,.58f);pen.Line(.28f,.58f,.5f,.76f);pen.Line(.5f,.76f,.72f,.58f);pen.Line(.5f,.25f,.5f,.45f);sprite=pen.Sprite("Staff_"+key);cache[key]=sprite;return sprite;}
        public static Sprite Facility(bool blue,bool city,int width=40)
        {
            string key=$"facility-{blue}-{city}-{width}";if(cache.TryGetValue(key,out var s)&&s!=null)return s;
            var p=new Pen(width,width,StaffMapStyle.Faction(blue));p.Fill(new Rect(.12f,.12f,.76f,.76f),StaffMapStyle.Panel);p.Box(.12f,.12f,.88f,.88f);
            if(city){p.Box(.28f,.27f,.72f,.60f);p.Line(.28f,.60f,.5f,.77f);p.Line(.5f,.77f,.72f,.60f);p.Line(.5f,.27f,.5f,.48f);}
            else{p.Line(.30f,.5f,.70f,.5f);p.Line(.5f,.30f,.5f,.70f);p.Ellipse(.5f,.5f,.24f,.24f);}
            s=p.Sprite("Staff_"+key);cache[key]=s;return s;
        }
        public static Sprite Compass(int width=64)
        {
            string key="compass"+width;if(cache.TryGetValue(key,out var s)&&s!=null)return s;var p=new Pen(width,width,StaffMapStyle.Ink);
            p.Triangle(new Vector2(.5f,.95f),new Vector2(.77f,.13f),new Vector2(.5f,.31f),StaffMapStyle.Hex(0x171B1B));
            p.Line(.5f,.95f,.23f,.13f);p.Line(.23f,.13f,.5f,.31f);p.Line(.5f,.31f,.77f,.13f);p.Line(.77f,.13f,.5f,.95f);p.Line(.5f,.31f,.5f,.95f);
            s=p.Sprite("Staff compass");cache[key]=s;return s;
        }
        sealed class Pen
        {
            readonly int w,h;readonly Color[] pixels;public readonly Color ink;float stroke;
            public float Aspect {get{return (float)w/h;}}
            public Pen(int width,int height,Color color){w=width;h=height;pixels=new Color[w*h];ink=color;stroke=width>=100?4.2f:width>=64?2.6f:width>=40?2f:1.55f;}
            public void Fill(Rect r,Color c){for(int y=0;y<h;y++)for(int x=0;x<w;x++)if(r.Contains(new Vector2((x+.5f)/w,(y+.5f)/h)))pixels[y*w+x]=c;}
            public void Line(float x0,float y0,float x1,float y1)
            {
                Vector2 a=new Vector2(x0*w,y0*h),b=new Vector2(x1*w,y1*h),d=b-a;float sq=d.sqrMagnitude;
                for(int y=Mathf.Max(0,Mathf.FloorToInt(Mathf.Min(a.y,b.y)-stroke));y<Mathf.Min(h,Mathf.CeilToInt(Mathf.Max(a.y,b.y)+stroke));y++)
                for(int x=Mathf.Max(0,Mathf.FloorToInt(Mathf.Min(a.x,b.x)-stroke));x<Mathf.Min(w,Mathf.CeilToInt(Mathf.Max(a.x,b.x)+stroke));x++)
                {var p=new Vector2(x+.5f,y+.5f);float t=sq==0?0:Mathf.Clamp01(Vector2.Dot(p-a,d)/sq);Blend(x,y,Mathf.Clamp01(stroke*.5f+.5f-Vector2.Distance(p,a+d*t)));}
            }
            void Blend(int x,int y,float alpha){if(alpha<=0)return;var old=pixels[y*w+x];float outAlpha=alpha+old.a*(1-alpha);pixels[y*w+x]=new Color((ink.r*alpha+old.r*old.a*(1-alpha))/outAlpha,(ink.g*alpha+old.g*old.a*(1-alpha))/outAlpha,(ink.b*alpha+old.b*old.a*(1-alpha))/outAlpha,outAlpha);}
            public void Box(float l,float b,float r,float t){Line(l,b,l,t);Line(l,t,r,t);Line(r,t,r,b);Line(r,b,l,b);}
            public void Diamond(float x,float y,float rx,float ry,bool fill){if(fill)for(int j=0;j<h;j++)for(int i=0;i<w;i++)if(Mathf.Abs(((i+.5f)/w-x)/rx)+Mathf.Abs(((j+.5f)/h-y)/ry)<=1)pixels[j*w+i]=StaffMapStyle.RedFill;Line(x-rx,y,x,y+ry);Line(x,y+ry,x+rx,y);Line(x+rx,y,x,y-ry);Line(x,y-ry,x-rx,y);}
            public void Triangle(Vector2 a,Vector2 b,Vector2 c,Color color){
                for(int y=0;y<h;y++)for(int x=0;x<w;x++){var p=new Vector2((x+.5f)/w,(y+.5f)/h);float d0=(b.x-a.x)*(p.y-a.y)-(b.y-a.y)*(p.x-a.x),d1=(c.x-b.x)*(p.y-b.y)-(c.y-b.y)*(p.x-b.x),d2=(a.x-c.x)*(p.y-c.y)-(a.y-c.y)*(p.x-c.x);if(d0>=0&&d1>=0&&d2>=0||d0<=0&&d1<=0&&d2<=0)pixels[y*w+x]=color;}
            }
            public void Dot(float x,float y,float radius){float r=radius*w;for(int j=0;j<h;j++)for(int i=0;i<w;i++)Blend(i,j,Mathf.Clamp01(r+.5f-Vector2.Distance(new Vector2(i+.5f,j+.5f),new Vector2(x*w,y*h))));}
            public void Ellipse(float x,float y,float rx,float ry){for(int i=0;i<48;i++){float a=i*Mathf.PI*2/48,b=(i+1)*Mathf.PI*2/48;Line(x+Mathf.Cos(a)*rx,y+Mathf.Sin(a)*ry,x+Mathf.Cos(b)*rx,y+Mathf.Sin(b)*ry);}}
            // Two tiny missing-ink spots on the frame, retaining the role and echelon marks.
            public void Wear(bool blue)
            {
                var spots=blue?new[]{new Vector2(.32f,.74f),new Vector2(.90f,.36f)}:new[]{new Vector2(.33f,.60f),new Vector2(.67f,.26f)};
                float radius=Mathf.Max(.55f,w*.018f);
                foreach(var spot in spots)for(int y=0;y<h;y++)for(int x=0;x<w;x++)
                {
                    int i=y*w+x;var c=pixels[i];if(c.a<.1f||Mathf.Abs(c.r-ink.r)+Mathf.Abs(c.g-ink.g)+Mathf.Abs(c.b-ink.b)>.12f)continue;
                    float d=Vector2.Distance(new Vector2(x+.5f,y+.5f),new Vector2(spot.x*w,spot.y*h));if(d<radius){c.a*=Mathf.Lerp(.12f,1,d/radius);pixels[i]=c;}
                }
            }
            public Sprite Sprite(string name){var t=new Texture2D(w,h,TextureFormat.RGBA32,false){name=name,filterMode=FilterMode.Point,wrapMode=TextureWrapMode.Clamp};t.SetPixels(pixels);t.Apply();var sprite=UnityEngine.Sprite.Create(t,new Rect(0,0,w,h),new Vector2(.5f,.5f),100,0,SpriteMeshType.FullRect);sprite.name=name;return sprite;}
        }
    }
}
