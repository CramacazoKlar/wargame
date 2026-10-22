using System;
using System.Collections.Generic;
namespace CampaignDemo
{
    public enum TerrainKind { Ground=0, Road=1, Grass=2, Mountain=3, Water=4, Blocked=5, Forest=6, HighMountain=7 }
    [Flags] public enum TerrainAccess { None=0, Ground=1, Road=2, Grass=4, Mountain=8, Water=16, Forest=64, HighMountain=128, All=223 }
    public sealed class TerrainGeometry
    {
        public sealed class Region
        {
            public TerrainKind Kind;
            public RoadPoint[] Polygon;
            bool boundsReady;
            double minX,minY,maxX,maxY;
            void PrepareBounds()
            {
                if(boundsReady) return;
                minX=minY=double.PositiveInfinity; maxX=maxY=double.NegativeInfinity;
                if(Polygon!=null) foreach(var p in Polygon)
                { minX=Math.Min(minX,p.X); minY=Math.Min(minY,p.Y); maxX=Math.Max(maxX,p.X); maxY=Math.Max(maxY,p.Y); }
                boundsReady=true;
            }
            public bool NearPoint(RoadPoint p)
            { PrepareBounds(); return p.X>=minX-Epsilon&&p.X<=maxX+Epsilon&&p.Y>=minY-Epsilon&&p.Y<=maxY+Epsilon; }
            public bool NearSegment(RoadPoint a,RoadPoint b)
            { PrepareBounds(); return Math.Max(a.X,b.X)>=minX-Epsilon&&Math.Min(a.X,b.X)<=maxX+Epsilon&&Math.Max(a.Y,b.Y)>=minY-Epsilon&&Math.Min(a.Y,b.Y)<=maxY+Epsilon; }
        }
        public bool roadsOverrideWater;
        public RoadPoint Min,Max;
        public TerrainKind DefaultTerrain;
        public Region[] Regions=new Region[0];
        Region[] indexedRegions;
        readonly Dictionary<long,List<Region>> regionIndex=new Dictionary<long,List<Region>>();
        void EnsureRegionIndex()
        {
            if(ReferenceEquals(indexedRegions,Regions))return;
            indexedRegions=Regions;regionIndex.Clear();
            foreach(var r in Regions){if(r==null||r.Polygon==null||r.Polygon.Length<3)continue;
                double x0=double.PositiveInfinity,y0=double.PositiveInfinity,x1=double.NegativeInfinity,y1=double.NegativeInfinity;
                foreach(var p in r.Polygon){x0=Math.Min(x0,p.X);y0=Math.Min(y0,p.Y);x1=Math.Max(x1,p.X);y1=Math.Max(y1,p.Y);}
                for(int y=(int)Math.Floor((y0-Epsilon)/2);y<=(int)Math.Floor((y1+Epsilon)/2);y++)for(int x=(int)Math.Floor((x0-Epsilon)/2);x<=(int)Math.Floor((x1+Epsilon)/2);x++){
                    long key=RoadKey(x,y);if(!regionIndex.TryGetValue(key,out var list)){list=new List<Region>();regionIndex.Add(key,list);}list.Add(r);
                }
            }
        }
        Region[] automaticRoads=new Region[0];
        readonly Dictionary<long,List<Region>> roadIndex=new Dictionary<long,List<Region>>();
        static long RoadKey(int x,int y){return ((long)x<<32)^(uint)y;}
        public void SetAutomaticRoads(Region[] roads)
        {
            automaticRoads=roads;roadIndex.Clear();
            foreach(var r in roads)
            {
                double x0=double.PositiveInfinity,y0=double.PositiveInfinity,x1=double.NegativeInfinity,y1=double.NegativeInfinity;
                foreach(var p in r.Polygon){x0=Math.Min(x0,p.X);y0=Math.Min(y0,p.Y);x1=Math.Max(x1,p.X);y1=Math.Max(y1,p.Y);}
                for(int y=(int)Math.Floor(y0);y<=(int)Math.Floor(y1);y++)for(int x=(int)Math.Floor(x0);x<=(int)Math.Floor(x1);x++)
                {long key=RoadKey(x,y);List<Region> list;if(!roadIndex.TryGetValue(key,out list)){list=new List<Region>();roadIndex.Add(key,list);}list.Add(r);}
            }
        }
        TerrainKind WithRoad(TerrainKind kind,RoadPoint p)
        {
            if(kind==TerrainKind.Blocked||(!roadsOverrideWater&&kind==TerrainKind.Water))return kind;
            List<Region> list;if(roadIndex.TryGetValue(RoadKey((int)Math.Floor(p.X),(int)Math.Floor(p.Y)),out list))foreach(var r in list)if(r.NearPoint(p)&&Contains(r.Polygon,p))return TerrainKind.Road;
            return kind;
        }
        IEnumerable<Region> BoundaryRegions(){foreach(var r in Regions)yield return r;foreach(var r in automaticRoads)yield return r;}
        readonly HashSet<Region> segmentCandidates=new HashSet<Region>();
        IEnumerable<Region> SegmentBoundaryRegions(RoadPoint a,RoadPoint b){EnsureRegionIndex();int x0=(int)Math.Floor((Math.Min(a.X,b.X)-Epsilon)/2),x1=(int)Math.Floor((Math.Max(a.X,b.X)+Epsilon)/2),y0=(int)Math.Floor((Math.Min(a.Y,b.Y)-Epsilon)/2),y1=(int)Math.Floor((Math.Max(a.Y,b.Y)+Epsilon)/2);if((long)(x1-x0+1)*(y1-y0+1)>128){foreach(var r in BoundaryRegions())yield return r;yield break;}segmentCandidates.Clear();for(int y=y0;y<=y1;y++)for(int x=x0;x<=x1;x++)if(regionIndex.TryGetValue(RoadKey(x,y),out var list))foreach(var r in list)segmentCandidates.Add(r);x0=(int)Math.Floor(Math.Min(a.X,b.X)-Epsilon);x1=(int)Math.Floor(Math.Max(a.X,b.X)+Epsilon);y0=(int)Math.Floor(Math.Min(a.Y,b.Y)-Epsilon);y1=(int)Math.Floor(Math.Max(a.Y,b.Y)+Epsilon);for(int y=y0;y<=y1;y++)for(int x=x0;x<=x1;x++)if(roadIndex.TryGetValue(RoadKey(x,y),out var list))foreach(var r in list)segmentCandidates.Add(r);foreach(var r in segmentCandidates)yield return r;}
        const double Epsilon=0.0000001;
        // Unity queries geometry on its main thread. Reuse scratch memory per snapshot.
        readonly List<double> cuts=new List<double>(64);
        public TerrainKind TerrainAt(RoadPoint p)
        {
            if(p.X<Min.X||p.Y<Min.Y||p.X>Max.X||p.Y>Max.Y) return TerrainKind.Blocked;
            var kind=DefaultTerrain;
            EnsureRegionIndex();
            if(regionIndex.TryGetValue(RoadKey((int)Math.Floor(p.X/2),(int)Math.Floor(p.Y/2)),out var nearby))
                foreach(var r in nearby)if(r.NearPoint(p)&&Contains(r.Polygon,p))kind=r.Kind;
            return WithRoad(kind,p);
        }
        public List<RoadPoint> SplitRoute(IList<RoadPoint> path)
        {
            var result=new List<RoadPoint>();
            if(path==null||path.Count==0) return result;
            RoadNetwork.Append(result,path[0]);
            for(int i=1;i<path.Count;i++)
            {
                var a=path[i-1]; var b=path[i]; cuts.Clear(); cuts.Add(0); cuts.Add(1);
                foreach(var region in SegmentBoundaryRegions(a,b))
                {
                    if(region==null||region.Polygon==null||!region.NearSegment(a,b)) continue;
                    for(int n=0;n<region.Polygon.Length;n++) Intersections(a,b,region.Polygon[n],region.Polygon[(n+1)%region.Polygon.Length],cuts);
                }
                cuts.Sort();
                foreach(double t in cuts) RoadNetwork.Append(result,RoadPoint.Lerp(a,b,t));
            }
            return result;
        }
        public bool CanStand(RoadPoint p,TerrainAccess access)
        {
            if(p.X<Min.X||p.Y<Min.Y||p.X>Max.X||p.Y>Max.Y) return false;
            var kind=TerrainAt(p);
            return kind!=TerrainKind.Blocked&&(((int)access&(1<<(int)kind))!=0);
        }
        // Split a segment at every polygon boundary. Even thin obstacles cannot be skipped.
        public bool CanTraverse(RoadPoint from,RoadPoint to,TerrainAccess access)
        {
            if(!CanStand(from,access)||!CanStand(to,access)) return false;
            cuts.Clear(); cuts.Add(0); cuts.Add(1);
            foreach(var r in SegmentBoundaryRegions(from,to))
            {
                if(r==null||r.Polygon==null||!r.NearSegment(from,to)) continue;
                for(int i=0;i<r.Polygon.Length;i++) Intersections(from,to,r.Polygon[i],r.Polygon[(i+1)%r.Polygon.Length],cuts);
            }
            cuts.Sort();
            for(int i=0;i<cuts.Count;i++)
            {
                if(!CanStand(RoadPoint.Lerp(from,to,cuts[i]),access)) return false;
                if(i>0&&cuts[i]-cuts[i-1]>Epsilon&&!CanStand(RoadPoint.Lerp(from,to,(cuts[i]+cuts[i-1])*0.5),access)) return false;
            }
            return true;
        }
        public static bool Contains(RoadPoint[] polygon,RoadPoint p)
        {
            bool inside=false;
            for(int i=0,j=polygon.Length-1;i<polygon.Length;j=i++)
            {
                var a=polygon[j]; var b=polygon[i];
                if(OnSegment(a,b,p)) return true;
                if((a.Y>p.Y)!=(b.Y>p.Y)&&p.X<(b.X-a.X)*(p.Y-a.Y)/(b.Y-a.Y)+a.X) inside=!inside;
            }
            return inside;
        }
        static double Cross(double ax,double ay,double bx,double by) { return ax*by-ay*bx; }
        static bool OnSegment(RoadPoint a,RoadPoint b,RoadPoint p)
        { return Math.Abs(Cross(b.X-a.X,b.Y-a.Y,p.X-a.X,p.Y-a.Y))<=Epsilon&&p.X>=Math.Min(a.X,b.X)-Epsilon&&p.X<=Math.Max(a.X,b.X)+Epsilon&&p.Y>=Math.Min(a.Y,b.Y)-Epsilon&&p.Y<=Math.Max(a.Y,b.Y)+Epsilon; }
        static void Intersections(RoadPoint a,RoadPoint b,RoadPoint c,RoadPoint d,List<double> cuts)
        {
            double rx=b.X-a.X,ry=b.Y-a.Y,sx=d.X-c.X,sy=d.Y-c.Y;
            double cross=Cross(rx,ry,sx,sy),qx=c.X-a.X,qy=c.Y-a.Y;
            if(Math.Abs(cross)>Epsilon)
            {
                double t=Cross(qx,qy,sx,sy)/cross,u=Cross(qx,qy,rx,ry)/cross;
                if(t>=-Epsilon&&t<=1+Epsilon&&u>=-Epsilon&&u<=1+Epsilon) cuts.Add(Math.Max(0,Math.Min(1,t)));
            }
            else if(Math.Abs(Cross(qx,qy,rx,ry))<=Epsilon)
            {
                double length=rx*rx+ry*ry; if(length<Epsilon) return;
                double t=((c.X-a.X)*rx+(c.Y-a.Y)*ry)/length,u=((d.X-a.X)*rx+(d.Y-a.Y)*ry)/length;
                if(t>=0&&t<=1) cuts.Add(t); if(u>=0&&u<=1) cuts.Add(u);
            }
        }
    }
}


