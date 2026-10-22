using System;
using UnityEngine;
namespace CampaignDemo
{
    // Navigation-only bitmap. Never renders any geometry over the map.
    public sealed class RasterWaterMap : MonoBehaviour
    {
        public Texture2D source;
        public Rect localBounds;
        public Rect[] bridges=new Rect[0];
        Color32[] pixels; Texture2D cached; int width,height;
        public bool IsWater(RoadPoint world)
        {
            var p=transform.InverseTransformPoint(new Vector3((float)world.X,(float)world.Y));
            foreach(var bridge in bridges) if(bridge.Contains(p))return false;
            return IsRawWater(world);
        }
        public bool IsRawWater(RoadPoint world)
        {
            var p=transform.InverseTransformPoint(new Vector3((float)world.X,(float)world.Y));
            if(source==null)return false;
            if(cached!=source||pixels==null) { cached=source; pixels=source.GetPixels32(); width=source.width;height=source.height; }
            if(!localBounds.Contains(p))return false;
            int x=Mathf.Clamp(Mathf.FloorToInt((p.x-localBounds.xMin)/localBounds.width*width),0,width-1);
            int y=Mathf.Clamp(Mathf.FloorToInt((p.y-localBounds.yMin)/localBounds.height*height),0,height-1);
            var c=pixels[y*width+x]; return c.b>c.r+30&&c.g>c.r+12&&c.b>=c.g;
        }
        public bool CanTraverse(RoadPoint a,RoadPoint b)
        {
            if(source==null)return true;
            var pa=transform.InverseTransformPoint(new Vector3((float)a.X,(float)a.Y));
            var pb=transform.InverseTransformPoint(new Vector3((float)b.X,(float)b.Y));
            int samples=Mathf.CeilToInt(Mathf.Max(Mathf.Abs(pa.x-pb.x)/localBounds.width*source.width,Mathf.Abs(pa.y-pb.y)/localBounds.height*source.height)*2)+1;
            for(int i=0;i<=samples;i++)if(IsWater(RoadPoint.Lerp(a,b,(double)i/samples)))return false;
            return true;
        }
    }
}
