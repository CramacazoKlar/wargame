using UnityEngine;
namespace CampaignDemo
{
    public static class MapViewBounds
    {
        public static Vector3[] ClipViewport(Vector3[] points, Rect clip)
        {
            var polygon=new System.Collections.Generic.List<Vector3>(points);
            for(int side=0;side<4;side++)
            {
                var result=new System.Collections.Generic.List<Vector3>();
                if(polygon.Count==0)break;
                var previous=polygon[polygon.Count-1];float previousDistance=ClipDistance(previous,clip,side);
                foreach(var current in polygon)
                {
                    float distance=ClipDistance(current,clip,side);
                    if((distance>=0)!=(previousDistance>=0))result.Add(Vector3.Lerp(previous,current,previousDistance/(previousDistance-distance)));
                    if(distance>=0)result.Add(current);
                    previous=current;previousDistance=distance;
                }
                polygon=result;
            }
            return polygon.ToArray();
        }
        static float ClipDistance(Vector3 point,Rect clip,int side)
        {return side==0?point.x-clip.xMin:side==1?clip.xMax-point.x:side==2?point.y-clip.yMin:clip.yMax-point.y;}
        public static Rect TransformRect(Transform root, Rect local)
        {
            var a=root.TransformPoint(new Vector3(local.xMin,local.yMin));
            var b=root.TransformPoint(new Vector3(local.xMax,local.yMin));
            var c=root.TransformPoint(new Vector3(local.xMax,local.yMax));
            var d=root.TransformPoint(new Vector3(local.xMin,local.yMax));
            return Rect.MinMaxRect(Mathf.Min(a.x,b.x,c.x,d.x),Mathf.Min(a.y,b.y,c.y,d.y),Mathf.Max(a.x,b.x,c.x,d.x),Mathf.Max(a.y,b.y,c.y,d.y));
        }
        public static bool TryGet(RoadMapRuntime road, FreeMarchMapRuntime terrain, out Rect bounds)
        {
            bounds=new Rect();bool found=false;
            if(terrain!=null&&terrain.definition!=null&&terrain.definition.bounds.width>0&&terrain.definition.bounds.height>0)
            {bounds=TransformRect(terrain.transform,terrain.definition.bounds);found=true;}
            if(road!=null&&road.definition!=null&&road.definition.background!=null)
            {
                var data=road.definition;
                if(data.backgroundSize.x>0&&data.backgroundSize.y>0)
                {
                    var art=TransformRect(road.transform,new Rect(data.backgroundCenter-data.backgroundSize*.5f,data.backgroundSize));
                    bounds=found?Rect.MinMaxRect(Mathf.Min(bounds.xMin,art.xMin),Mathf.Min(bounds.yMin,art.yMin),Mathf.Max(bounds.xMax,art.xMax),Mathf.Max(bounds.yMax,art.yMax)):art;found=true;
                }
            }
            return found;
        }
    }
}
