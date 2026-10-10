using System;
using UnityEngine;

namespace CampaignDemo
{
    // Converts map data to a runtime graph and displays the map, without spawning armies.
    public sealed class RoadMapRuntime : MonoBehaviour
    {
        public RoadMapDefinition definition;
        public RoadNetwork Network { get; private set; }
        public void Initialize()
        {
            if(definition==null||definition.nodes==null||definition.connections==null)
                throw new ArgumentException("Assign a road map asset with nodes and connections.");
            var points=new RoadPoint[definition.nodes.Length];
            for(int i=0;i<points.Length;i++)
            {
                var p=transform.TransformPoint(definition.nodes[i]); points[i]=new RoadPoint(p.x,p.y);
            }
            var edges=new RoadEdge[definition.connections.Length];
            for(int i=0;i<edges.Length;i++) edges[i]=new RoadEdge(definition.connections[i].a,definition.connections[i].b);
            Network=new RoadNetwork(points,edges);
            if(definition.background!=null)
            {
                var go=new GameObject("Map background"); go.transform.SetParent(transform,false);
                go.transform.localPosition=definition.backgroundCenter;
                var r=go.AddComponent<SpriteRenderer>(); r.sprite=definition.background; r.sortingOrder=-10;
                var size=r.sprite.bounds.size;
                go.transform.localScale=new Vector3(definition.backgroundSize.x/size.x,definition.backgroundSize.y/size.y,1);
            }
            if(definition.showRoads)
            foreach(var edge in Network.Edges)
            {
                var pts=new[]{Network.Nodes[edge.A],Network.Nodes[edge.B]};
                RoadRouteView.Create(transform,"Road outline",definition.roadWidth+0.08f,0,new Color(0.24f,0.22f,0.18f)).Show(pts);
                RoadRouteView.Create(transform,"Road",definition.roadWidth,1,definition.roadColor).Show(pts);
            }
        }
    }
}
