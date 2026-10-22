using UnityEngine;
using System.Collections.Generic;
namespace CampaignDemo
{
    public sealed class FreeMarchMapRuntime : MonoBehaviour
    {
        public FreeMarchMapDefinition definition;
        public RasterWaterMap waterMap;
        public bool automaticRoadTerrain=true;
        [Min(.01f)] public float roadTerrainWidth=.5f;
        RoadMapDefinition cachedRoad;int cachedRoadRevision=-1;float cachedRoadWidth;bool cachedAutomaticRoad;
        TerrainGeometry cachedGeometry;
        FreeMarchMapDefinition cachedDefinition;
        int cachedRevision;
        int runtimeVersion;
        Matrix4x4 cachedMatrix;
        bool hasMatrix;
        public int Version
        {
            get
            {
                Geometry();
                var matrix=transform.localToWorldMatrix;
                if(!hasMatrix||matrix!=cachedMatrix) { cachedMatrix=matrix; hasMatrix=true; unchecked { runtimeVersion++; } }
                return runtimeVersion;
            }
        }
        public void InvalidateGeometry() { cachedGeometry=null; }
        public TerrainKind TerrainAt(RoadPoint world)
        {
            var geo=Geometry(); if(geo==null) return TerrainKind.Blocked;
            var p=transform.InverseTransformPoint(new Vector3((float)world.X,(float)world.Y,0));
            return geo.TerrainAt(new RoadPoint(p.x,p.y));
        }
        public bool IsForest(RoadPoint world)
        {
            var geo=Geometry();if(geo==null)return false;
            var p=transform.InverseTransformPoint(new Vector3((float)world.X,(float)world.Y,0));var at=new RoadPoint(p.x,p.y);
            var terrain=geo.TerrainAt(at);if(terrain==TerrainKind.Water||terrain==TerrainKind.Blocked)return false;
            if(waterMap!=null&&!waterMap.CanTraverse(world,world))return false;
            if(geo.DefaultTerrain==TerrainKind.Forest)return true;
            foreach(var region in geo.Regions)if(region!=null&&region.Kind==TerrainKind.Forest&&region.Polygon!=null&&region.Polygon.Length>=3&&region.NearPoint(at)&&TerrainGeometry.Contains(region.Polygon,at))return true;
            return false;
        }
        public List<RoadPoint> SplitRoute(IList<RoadPoint> world)
        {
            var geo=Geometry(); if(geo==null||world==null) return null;
            var local=new List<RoadPoint>(world.Count);
            foreach(var point in world)
            { var p=transform.InverseTransformPoint(new Vector3((float)point.X,(float)point.Y,0)); local.Add(new RoadPoint(p.x,p.y)); }
            var split=geo.SplitRoute(local);
            for(int i=0;i<split.Count;i++)
            { var p=transform.TransformPoint(new Vector3((float)split[i].X,(float)split[i].Y,0)); split[i]=new RoadPoint(p.x,p.y); }
            return split;
        }
        public bool CanTraverse(RoadPoint from,RoadPoint to,TerrainAccess access)
        {
            if(definition==null) return false;
            var a=transform.InverseTransformPoint(new Vector3((float)from.X,(float)from.Y,0));
            var b=transform.InverseTransformPoint(new Vector3((float)to.X,(float)to.Y,0));
            var geometry=Geometry();
            return geometry!=null&&geometry.CanTraverse(new RoadPoint(a.x,a.y),new RoadPoint(b.x,b.y),access)&&((access&TerrainAccess.Water)!=0||waterMap==null||waterMap.CanTraverse(from,to));
        }
        public bool SyncBoundsToBackground()
        {
            var road=GetComponent<RoadMapRuntime>();
            if(definition==null||road==null||road.definition==null||road.definition.background==null)return false;
            var data=road.definition;if(data.backgroundSize.x<=0||data.backgroundSize.y<=0)return false;
            var target=new Rect(data.backgroundCenter-data.backgroundSize*.5f,data.backgroundSize);
            if(definition.bounds==target)return false;
            definition.bounds=target;definition.MarkDirty();InvalidateGeometry();return true;
        }
        public TerrainGeometry Geometry()
        {
            SyncBoundsToBackground();
            if(definition==null)
            {
                if(cachedDefinition!=null||cachedGeometry!=null) { cachedDefinition=null; cachedGeometry=null; unchecked { runtimeVersion++; } }
                return null;
            }
            var roadRuntime=GetComponent<RoadMapRuntime>();var road=roadRuntime==null?null:roadRuntime.definition;
            int roadRevision=road==null?0:road.Revision;
            float effectiveWidth=Mathf.Max(roadTerrainWidth,road==null?0:road.roadWidth);
            if(cachedGeometry!=null&&cachedDefinition==definition&&cachedRevision==definition.Revision&&cachedRoad==road&&cachedRoadRevision==roadRevision&&cachedRoadWidth==effectiveWidth&&cachedAutomaticRoad==automaticRoadTerrain) return cachedGeometry;
            var regions=definition.regions??new FreeMarchMapDefinition.Region[0];
            var geo=new TerrainGeometry{Min=new RoadPoint(definition.bounds.xMin,definition.bounds.yMin),Max=new RoadPoint(definition.bounds.xMax,definition.bounds.yMax),DefaultTerrain=definition.defaultTerrain,Regions=new TerrainGeometry.Region[regions.Length]};
            for(int i=0;i<regions.Length;i++)
            {
                if(regions[i]==null) continue;
                var p=regions[i].polygon??new Vector2[0]; var points=new RoadPoint[p.Length];
                for(int n=0;n<p.Length;n++) points[n]=new RoadPoint(p[n].x,p[n].y);
                geo.Regions[i]=new TerrainGeometry.Region{Kind=regions[i].terrain,Polygon=points};
            }
            geo.roadsOverrideWater=LocalTacticalBattle.EnabledFor(this)||GetComponent<StaffMapCartography>()!=null;
            geo.SetAutomaticRoads(automaticRoadTerrain?AutomaticRoadTerrain.Build(road,effectiveWidth):new TerrainGeometry.Region[0]);
            cachedRoad=road;cachedRoadRevision=roadRevision;cachedRoadWidth=effectiveWidth;cachedAutomaticRoad=automaticRoadTerrain;
            cachedDefinition=definition; cachedRevision=definition.Revision; cachedGeometry=geo;
            unchecked { runtimeVersion++; } return geo;
        }
        void OnValidate() { InvalidateGeometry(); }
        void OnDrawGizmosSelected()
        {
            if(definition==null) return;
            foreach(var r in definition.regions??new FreeMarchMapDefinition.Region[0])
            {
                if(r==null||r.polygon==null||r.polygon.Length<3) continue;
                Gizmos.color=r.terrain==TerrainKind.Blocked?Color.red:Color.green;
                for(int i=0;i<r.polygon.Length;i++) Gizmos.DrawLine(transform.TransformPoint(r.polygon[i]),transform.TransformPoint(r.polygon[(i+1)%r.polygon.Length]));
            }
        }
    }
}



