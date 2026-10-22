using System.Collections.Generic;
using UnityEngine;
namespace CampaignDemo
{
    public static class AutomaticRoadTerrain
    {
        public static TerrainGeometry.Region[] Build(RoadMapDefinition data,float width)
        {
            var regions=new List<TerrainGeometry.Region>();
            if(data==null||data.nodes==null||data.connections==null)return regions.ToArray();
            float half=Mathf.Max(.01f,width)*.5f;
            foreach(var edge in data.connections)
            {
                if(edge.a<0||edge.b<0||edge.a>=data.nodes.Length||edge.b>=data.nodes.Length)continue;
                var a=data.nodes[edge.a];var b=data.nodes[edge.b];var direction=b-a;if(direction.sqrMagnitude<.00000001f)continue;
                direction.Normalize();var side=new Vector2(-direction.y,direction.x)*half;a-=direction*half;b+=direction*half;
                var vertices=new[]{a-side,b-side,b+side,a+side};var points=new RoadPoint[4];for(int i=0;i<4;i++)points[i]=new RoadPoint(vertices[i].x,vertices[i].y);
                regions.Add(new TerrainGeometry.Region{Kind=TerrainKind.Road,Polygon=points});
            }
            return regions.ToArray();
        }
    }
}
