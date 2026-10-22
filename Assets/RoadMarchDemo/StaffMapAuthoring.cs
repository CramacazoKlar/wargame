using System;
using System.Collections.Generic;
using UnityEngine;
namespace CampaignDemo
{
    [ExecuteAlways,DisallowMultipleComponent]
    public sealed class StaffMapAuthoring:MonoBehaviour
    {
        [Serializable] public sealed class MountainStroke
        {
            public string id,name="山地";public bool highMountain;
            [Min(.1f)] public float halfWidth=1;
            public List<Vector2> points=new List<Vector2>();
        }
        public RoadMapRuntime roads;public FreeMarchMapRuntime terrain;
        public List<MountainStroke> mountains=new List<MountainStroke>();
        public int seed=217;
        public void Resolve(){if(roads==null)roads=GetComponent<RoadMapRuntime>();if(terrain==null)terrain=GetComponent<FreeMarchMapRuntime>();}
        public static List<Vector2> Spline(IList<Vector2> points,int samples=20)
        {
            var result=new List<Vector2>();if(points==null||points.Count<2)return result;
            for(int i=0;i<points.Count-1;i++)for(int j=0;j<samples;j++)
            {var a=points[Mathf.Max(0,i-1)];var b=points[i];var c=points[i+1];var d=points[Mathf.Min(points.Count-1,i+2)];float t=j/(float)samples,t2=t*t,t3=t2*t;result.Add(.5f*((2*b)+(-a+c)*t+(2*a-5*b+4*c-d)*t2+(-a+3*b-3*c+d)*t3));}
            result.Add(points[points.Count-1]);return result;
        }
        public static Vector2[] MountainPolygon(MountainStroke stroke)
        {
            var curve=Spline(stroke.points);if(curve.Count<2)return new Vector2[0];var left=new List<Vector2>();var right=new List<Vector2>();
            for(int i=0;i<curve.Count;i++){Vector2 tangent=curve[Mathf.Min(curve.Count-1,i+1)]-curve[Mathf.Max(0,i-1)];var normal=new Vector2(-tangent.y,tangent.x).normalized;float taper=Mathf.Pow(Mathf.Sin(Mathf.PI*(i+.15f)/(curve.Count-.7f)),.4f);float width=stroke.halfWidth*Mathf.Lerp(.08f,1,Mathf.Clamp01(taper));left.Add(curve[i]+normal*width);right.Add(curve[i]-normal*width);}
            right.Reverse();left.AddRange(right);return left.ToArray();
        }
        public void SyncMountains()
        {
            Resolve();if(terrain==null||terrain.definition==null)return;var regions=new List<FreeMarchMapDefinition.Region>(terrain.definition.regions??new FreeMarchMapDefinition.Region[0]);
            var local=GetComponent<LocalBattleMap>();if(local!=null)regions=new List<FreeMarchMapDefinition.Region>(local.baseRegions??new FreeMarchMapDefinition.Region[0]);
            regions.RemoveAll(r=>r!=null&&r.name.StartsWith("制图山地_"));
            foreach(var stroke in mountains){if(string.IsNullOrEmpty(stroke.id))stroke.id=Guid.NewGuid().ToString("N");var polygon=MountainPolygon(stroke);if(polygon.Length>=3)regions.Add(new FreeMarchMapDefinition.Region{name="制图山地_"+stroke.id,terrain=stroke.highMountain?TerrainKind.HighMountain:TerrainKind.Mountain,polygon=polygon});}
            if(local!=null){local.baseRegions=regions.ToArray();local.Rebuild();}else{terrain.definition.regions=regions.ToArray();terrain.definition.MarkDirty();terrain.InvalidateGeometry();}
            Refresh();
        }
        public void PaintForest(Vector2 at,float radius)
        {
            Resolve();if(terrain==null||terrain.definition==null)return;var random=new System.Random(seed++);var polygon=new Vector2[20];for(int i=0;i<polygon.Length;i++){float angle=i*Mathf.PI*2/polygon.Length;polygon[i]=at+new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*radius*(.85f+(float)random.NextDouble()*.25f);}
            var local=GetComponent<LocalBattleMap>();var list=new List<FreeMarchMapDefinition.Region>(local!=null?local.baseRegions:terrain.definition.regions);list.Add(new FreeMarchMapDefinition.Region{name="点绘森林 "+seed,terrain=TerrainKind.Forest,polygon=polygon});
            if(local!=null){local.baseRegions=list.ToArray();local.Rebuild();}else{terrain.definition.regions=list.ToArray();terrain.definition.MarkDirty();terrain.InvalidateGeometry();}Refresh();
        }
        public void Refresh(){Resolve();var view=GetComponent<StaffMapCartography>();if(view==null)view=gameObject.AddComponent<StaffMapCartography>();view.roads=roads;view.terrain=terrain;view.Invalidate();}
    }
}
