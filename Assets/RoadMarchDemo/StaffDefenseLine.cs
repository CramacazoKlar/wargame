using System.Collections.Generic;
using UnityEngine;
namespace CampaignDemo
{
    [DefaultExecutionOrder(450)]
    public sealed class StaffDefenseLine:MonoBehaviour
    {
        public Fortification fort;public float spacing=.30f,tickLength=.15f;
        public int DashCount {get;private set;}
        Mesh mesh;MeshRenderer view;Material material;BattleWorld world;int lastHash;float lastWidth=-1;
        void Start(){if(fort==null)fort=GetComponent<Fortification>();world=FindObjectOfType<BattleWorld>();}
        void LateUpdate()
        {
            if(fort==null||!fort.IsLine)return;foreach(var old in GetComponentsInChildren<LineRenderer>())old.enabled=false;
            if(view==null){var g=new GameObject("态势防线 · 长横十字");g.transform.SetParent(transform,false);g.AddComponent<MeshFilter>().sharedMesh=mesh=new Mesh{name="Fixed staff defence marks"};view=g.AddComponent<MeshRenderer>();material=new Material(Shader.Find("Sprites/Default"));view.sharedMaterial=material;view.sortingOrder=22;}
            view.enabled=!fort.Destroyed&&(world==null||fort.VisibleTo(world,world.currentPlayer));if(!view.enabled)return;
            var camera=world!=null&&world.setup!=null?world.setup.mapCamera:Camera.main;float width=Mathf.Max(.022f,camera==null?.022f:2*camera.orthographicSize/Mathf.Max(1,camera.pixelHeight)*1.6f);
            if(LocalPrototypeOrders.EnabledFor(this))width=Mathf.Max(.035f,width*1.5f);int hash=fort.ownerId==null?0:fort.ownerId.GetHashCode();unchecked{foreach(var p in fort.linePoints)hash=hash*397^p.GetHashCode();hash=hash*397^spacing.GetHashCode()^tickLength.GetHashCode();}
            if(hash!=lastHash||Mathf.Abs(width-lastWidth)>.0001f){Rebuild(width);lastHash=hash;lastWidth=width;}
        }
        void Rebuild(float width)
        {
            var vertices=new List<Vector3>();var colors=new List<Color>();var triangles=new List<int>();Color ink=StaffMapStyle.Faction(fort.ownerId==BattleFactions.BluePlayerId);float length=fort.WorldLength;float step=LocalPrototypeOrders.EnabledFor(this)?Mathf.Max(spacing*1.5f,width*8):Mathf.Max(.10f,spacing);float tick=LocalPrototypeOrders.EnabledFor(this)?Mathf.Max(tickLength*1.6f,width*4.5f):tickLength;DashCount=0;
            for(float start=0;start<length;start+=step)
            {
                float end=Mathf.Min(length,start+step*.85f);var slice=fort.Slice(start,end);for(int i=1;i<slice.Length;i++)Quad(slice[i-1],slice[i],width,ink,vertices,colors,triangles);
                float arc=(start+end)*.5f;var at=fort.PointAtArc(arc);var tangent=(fort.PointAtArc(Mathf.Min(length,arc+.025f))-fort.PointAtArc(Mathf.Max(0,arc-.025f))).normalized;var normal=new Vector3(-tangent.y,tangent.x);if(Vector3.Dot(normal,fort.engineerSide-at)<0)normal=-normal;
                Quad(at-normal*tick*.5f,at+normal*tick*.5f,width,ink,vertices,colors,triangles);DashCount++;
            }
            mesh.Clear();if(vertices.Count>65535)mesh.indexFormat=UnityEngine.Rendering.IndexFormat.UInt32;mesh.SetVertices(vertices);mesh.SetColors(colors);mesh.SetTriangles(triangles,0);mesh.RecalculateBounds();
        }
        void Quad(Vector3 a,Vector3 b,float width,Color ink,List<Vector3> vertices,List<Color> colors,List<int> triangles)
        {
            var d=(b-a).normalized;var n=new Vector3(-d.y,d.x)*width*.5f;int offset=vertices.Count;vertices.Add(transform.InverseTransformPoint(a+n));vertices.Add(transform.InverseTransformPoint(b+n));vertices.Add(transform.InverseTransformPoint(b-n));vertices.Add(transform.InverseTransformPoint(a-n));
            float grain=StaffMapCanvas.Hash(Mathf.RoundToInt(a.x*100),Mathf.RoundToInt(a.y*100),217);ink.a=.8f+grain*.2f;for(int i=0;i<4;i++)colors.Add(ink);triangles.AddRange(new[]{offset,offset+1,offset+2,offset,offset+2,offset+3});
        }
        void OnDestroy(){if(mesh!=null)Destroy(mesh);if(material!=null)Destroy(material);if(view!=null)Destroy(view.gameObject);}
    }
}
