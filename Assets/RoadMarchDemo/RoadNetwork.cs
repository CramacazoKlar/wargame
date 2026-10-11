using System;
using System.Collections.Generic;

namespace CampaignDemo
{
    // Pure data and geometry. The visible map is never used for navigation.
    public struct RoadPoint
    {
        public double X, Y;
        public RoadPoint(double x, double y) { X = x; Y = y; }
        public static double Distance(RoadPoint a, RoadPoint b)
        { double x = a.X - b.X, y = a.Y - b.Y; return Math.Sqrt(x*x + y*y); }
        public static RoadPoint Lerp(RoadPoint a, RoadPoint b, double t)
        { return new RoadPoint(a.X+(b.X-a.X)*t, a.Y+(b.Y-a.Y)*t); }
    }

    public struct RoadEdge
    {
        public int A, B;
        public RoadEdge(int a, int b) { A=a; B=b; }
    }

    public struct RoadHit
    {
        public int Edge;
        public RoadPoint Position;
        public double Distance;
        public RoadHit(int edge, RoadPoint position, double distance)
        { Edge=edge; Position=position; Distance=distance; }
    }

    public sealed class RoadNetwork
    {
        public readonly RoadPoint[] Nodes;
        public readonly RoadEdge[] Edges;
        readonly List<int>[] neighbours;

        public RoadNetwork(RoadPoint[] nodes, RoadEdge[] edges)
        {
            Nodes=(RoadPoint[])nodes.Clone(); Edges=(RoadEdge[])edges.Clone();
            neighbours=new List<int>[Nodes.Length];
            for(int i=0;i<Nodes.Length;i++)
            {
                if(double.IsNaN(Nodes[i].X)||double.IsInfinity(Nodes[i].X)||
                   double.IsNaN(Nodes[i].Y)||double.IsInfinity(Nodes[i].Y))
                    throw new ArgumentException("Road coordinates must be finite.");
                neighbours[i]=new List<int>();
            }
            foreach(var e in Edges)
            {
                if(e.A<0||e.B<0||e.A>=Nodes.Length||e.B>=Nodes.Length||e.A==e.B||
                   RoadPoint.Distance(Nodes[e.A],Nodes[e.B])<0.000001)
                    throw new ArgumentException("Invalid or zero-length road edge.");
                neighbours[e.A].Add(e.B); neighbours[e.B].Add(e.A);
            }
        }

        public List<RoadHit> Candidates(RoadPoint cursor, double tolerance)
        {
            var hits=new List<RoadHit>();
            for(int i=0;i<Edges.Length;i++)
            {
                var a=Nodes[Edges[i].A]; var b=Nodes[Edges[i].B];
                double dx=b.X-a.X, dy=b.Y-a.Y;
                double t=((cursor.X-a.X)*dx+(cursor.Y-a.Y)*dy)/(dx*dx+dy*dy);
                var p=RoadPoint.Lerp(a,b,Math.Max(0,Math.Min(1,t)));
                double distance=RoadPoint.Distance(cursor,p);
                if(distance<=tolerance) hits.Add(new RoadHit(i,p,distance));
            }
            hits.Sort((a,b)=>a.Distance.CompareTo(b.Distance));
            return hits;
        }

        // Find a connector between two points on roads, never a straight off-road chord.
        public bool Connect(RoadHit from, RoadHit to, double maxLength,
                            out List<RoadPoint> path)
        {
            path=null; double best=maxLength+0.000001;
            if(from.Edge==to.Edge)
            {
                double d=RoadPoint.Distance(from.Position,to.Position);
                if(d<=maxLength) { best=d; path=new List<RoadPoint>{from.Position,to.Position}; }
            }
            var a=Edges[from.Edge]; var b=Edges[to.Edge];
            foreach(int start in new[]{a.A,a.B})
            foreach(int finish in new[]{b.A,b.B})
            {
                List<int> nodes; double length;
                if(!Shortest(start,finish,out nodes,out length)) continue;
                length+=RoadPoint.Distance(from.Position,Nodes[start])+
                        RoadPoint.Distance(Nodes[finish],to.Position);
                if(length>maxLength||length>=best) continue;
                best=length; path=new List<RoadPoint>{from.Position};
                foreach(int n in nodes) Append(path,Nodes[n]);
                Append(path,to.Position);
            }
            return path!=null;
        }

        // Project any cursor position onto the closest reachable road, with no distance cutoff.
        public bool SnapConnected(RoadHit previous,RoadPoint cursor,out RoadHit next,out List<RoadPoint> connector)
        {
            next=default(RoadHit);connector=null;
            foreach(var hit in Candidates(cursor,double.PositiveInfinity))if(Connect(previous,hit,double.PositiveInfinity,out connector)){next=hit;return true;}
            return false;
        }
        // Continuity rejects nearby roads that are disconnected or reachable only by a long detour.
        public bool Trace(RoadHit previous, RoadPoint cursor, double tolerance,
                          double maxConnector, out RoadHit next, out List<RoadPoint> connector)
        {
            next=default(RoadHit); connector=null;
            double best=double.PositiveInfinity;
            foreach(var hit in Candidates(cursor,tolerance))
            {
                List<RoadPoint> route;
                if(!Connect(previous,hit,maxConnector,out route)) continue;
                // Small hysteresis prevents jitter between roads near an intersection.
                double score=hit.Distance+(hit.Edge==previous.Edge ? 0 : tolerance*0.08);
                if(score>=best) continue;
                best=score; next=hit; connector=route;
            }
            return connector!=null;
        }

        // Discard only short retraced excursions at a shared branching node.
        // Long deliberate reversals and disconnected roads remain unchanged.
        public void AppendTurn(List<RoadPoint> draft,IList<RoadPoint> connector,
                               int previousEdge,int nextEdge,double tolerance)
        {
            if(draft==null||connector==null)throw new ArgumentNullException();
            if(tolerance>0&&previousEdge!=nextEdge&&previousEdge>=0&&nextEdge>=0&&
               previousEdge<Edges.Length&&nextEdge<Edges.Length)
            {
                var from=Edges[previousEdge];var to=Edges[nextEdge];
                foreach(int node in new[]{from.A,from.B})
                {
                    if((to.A!=node&&to.B!=node)||neighbours[node].Count<3)continue;
                    var junction=Nodes[node];
                    var a=Nodes[from.A==node?from.B:from.A];var b=Nodes[to.A==node?to.B:to.A];
                    double dot=(a.X-junction.X)*(b.X-junction.X)+(a.Y-junction.Y)*(b.Y-junction.Y);
                    double lengths=RoadPoint.Distance(a,junction)*RoadPoint.Distance(b,junction);
                    if(Math.Abs(dot/lengths)>.95)continue; // A genuine turn, not collinear tracing.
                    bool connected=false;foreach(var p in connector)if(RoadPoint.Distance(p,junction)<.00001){connected=true;break;}
                    if(!connected)continue;
                    int anchor=-1;
                    for(int i=draft.Count-1;i>=0;i--)
                    {
                        if(RoadPoint.Distance(draft[i],junction)>tolerance+.00001)break;
                        if(RoadPoint.Distance(draft[i],junction)<.00001){anchor=i;break;}
                    }
                    if(anchor>=0&&anchor<draft.Count-1)
                    {
                        bool onEdge=true;
                        for(int i=anchor+1;i<draft.Count;i++)
                        {
                            var p=draft[i];double cross=(p.X-junction.X)*(a.Y-junction.Y)-(p.Y-junction.Y)*(a.X-junction.X);
                            double along=(p.X-junction.X)*(a.X-junction.X)+(p.Y-junction.Y)*(a.Y-junction.Y);
                            if(Math.Abs(cross)>RoadPoint.Distance(a,junction)*.00001||along<-.00001){onEdge=false;break;}
                        }
                        if(onEdge)
                        {
                            draft.RemoveRange(anchor+1,draft.Count-anchor-1);
                            // Connector starts at the discarded tail; retain from junction onwards only.
                            bool reached=false;foreach(var p in connector){if(RoadPoint.Distance(p,junction)<.00001)reached=true;if(reached)Append(draft,p);}
                            return;
                        }
                    }
                }
            }
            foreach(var p in connector)Append(draft,p);
        }

        bool Shortest(int start,int finish,out List<int> result,out double length)
        {
            var dist=new double[Nodes.Length]; var prev=new int[Nodes.Length];
            var visited=new bool[Nodes.Length];
            for(int i=0;i<Nodes.Length;i++) { dist[i]=double.PositiveInfinity; prev[i]=-1; }
            dist[start]=0;
            for(int iteration=0;iteration<Nodes.Length;iteration++)
            {
                int u=-1;
                for(int i=0;i<Nodes.Length;i++)
                    if(!visited[i]&&(u<0||dist[i]<dist[u])) u=i;
                if(u<0||double.IsPositiveInfinity(dist[u])) break;
                if(u==finish) break;
                visited[u]=true;
                foreach(int v in neighbours[u])
                {
                    double d=dist[u]+RoadPoint.Distance(Nodes[u],Nodes[v]);
                    if(d<dist[v]) { dist[v]=d; prev[v]=u; }
                }
            }
            length=dist[finish]; result=null;
            if(double.IsPositiveInfinity(length)) return false;
            result=new List<int>();
            for(int n=finish;n!=-1;n=prev[n]) result.Add(n);
            result.Reverse(); return true;
        }

        public static void Append(List<RoadPoint> path,RoadPoint p)
        { if(path.Count==0||RoadPoint.Distance(path[path.Count-1],p)>0.00001) path.Add(p); }
    }
}
