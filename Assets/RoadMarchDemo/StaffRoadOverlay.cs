using System.Collections.Generic;
using UnityEngine;
namespace CampaignDemo {
 // RoadMapDefinition is the only source for drawing and traversable road corridors.
 [ExecuteAlways,DisallowMultipleComponent,DefaultExecutionOrder(360)]
 public sealed class StaffRoadOverlay:MonoBehaviour {
  public RoadMapRuntime roads;public FreeMarchMapRuntime terrain;
  GameObject outline,fill,bridges;Mesh outerMesh,innerMesh,bridgeMesh;Material outerMaterial,innerMaterial,bridgeMaterial;Texture2D inkTexture;int lastHash;bool built;
  public int BridgeCount {get;private set;}
  public Texture2D InkTexture {get{return inkTexture;}}
  int Signature(){unchecked{var d=roads.definition;int h=d.roadWidth.GetHashCode();h=h*397^(terrain!=null&&terrain.definition!=null?terrain.definition.Revision:0);foreach(var p in d.nodes)h=h*397^p.GetHashCode();foreach(var e in d.connections)h=h*397^e.a*31^e.b;return h;}}
  void Update(){if(roads==null)roads=GetComponent<RoadMapRuntime>();if(terrain==null)terrain=GetComponent<FreeMarchMapRuntime>();if(roads==null||roads.definition==null)return;int h=Signature();if(built&&h==lastHash)return;Rebuild();}
  public void Rebuild(){if(roads==null||roads.definition==null)return;Release();var d=roads.definition;lastHash=Signature();built=true;
   outerMaterial=new Material(Shader.Find("Sprites/Default")){color=StaffMapStyle.Ink};innerMaterial=new Material(Shader.Find("Sprites/Default")){color=new Color(.88f,.85f,.75f)};
   inkTexture=new Texture2D(64,64,TextureFormat.RGBA32,false){name="道路墨迹 · 深浅与断墨",wrapMode=TextureWrapMode.Repeat,filterMode=FilterMode.Bilinear};for(int y=0;y<64;y++)for(int x=0;x<64;x++){float grain=StaffMapCanvas.Hash(x,y,217),pressure=Mathf.PerlinNoise(x*.12f+3,y*.12f+9);float alpha=Mathf.Lerp(.58f,1,pressure)*Mathf.Lerp(.82f,1,grain);if(grain<.07f)alpha*=.2f;inkTexture.SetPixel(x,y,new Color(1,1,1,alpha));}inkTexture.Apply();outerMaterial.mainTexture=inkTexture;
   outerMesh=Geometry(Mathf.Max(.02f,d.roadWidth));innerMesh=Geometry(Mathf.Max(.01f,d.roadWidth*.70f));
   outline=View("道路边线 · 与通行数据一致",outerMesh,outerMaterial,-.04f);fill=View("道路路面 · 与通行数据一致",innerMesh,innerMaterial,-.05f);
   if(terrain!=null){terrain.roadTerrainWidth=d.roadWidth;terrain.InvalidateGeometry();}
   BuildBridges();
  }
  GameObject View(string label,Mesh mesh,Material material,float z){var g=new GameObject(label){hideFlags=HideFlags.DontSave};g.transform.SetParent(roads.transform,false);g.transform.localPosition=new Vector3(0,0,z);g.AddComponent<MeshFilter>().sharedMesh=mesh;var r=g.AddComponent<MeshRenderer>();r.sharedMaterial=material;r.sortingOrder=3;return g;}
  Mesh Geometry(float width){var d=roads.definition;var v=new List<Vector3>();var t=new List<int>();float half=width*.5f;
   foreach(var e in d.connections){if(e.a<0||e.b<0||e.a>=d.nodes.Length||e.b>=d.nodes.Length)continue;var a=d.nodes[e.a];var b=d.nodes[e.b];var delta=b-a;if(delta.sqrMagnitude<.000001f)continue;var n=new Vector2(-delta.y,delta.x).normalized*half;int at=v.Count;v.Add(a-n);v.Add(a+n);v.Add(b+n);v.Add(b-n);t.AddRange(new[]{at,at+1,at+2,at,at+2,at+3});}
   foreach(var p in d.nodes){int at=v.Count;v.Add(p);for(int i=0;i<=12;i++){float a=i*Mathf.PI*2/12;v.Add(p+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*half);}for(int i=0;i<12;i++)t.AddRange(new[]{at,at+i+1,at+i+2});}
   var mesh=new Mesh{name="Shared road geometry"};mesh.SetVertices(v);mesh.SetTriangles(t,0);var colors=new Color[v.Count];var uv=new Vector2[v.Count];for(int i=0;i<colors.Length;i++){colors[i]=Color.white;uv[i]=new Vector2(v[i].x*.8f,v[i].y*.8f);}mesh.colors=colors;mesh.uv=uv;mesh.RecalculateBounds();return mesh;
  }
  void BuildBridges(){BridgeCount=0;if(terrain==null)return;var cart=terrain.GetComponent<StaffMapCartography>();if(cart==null)return;cart.roads=roads;cart.terrain=terrain;cart.RefreshBridges();
   var vertices=new List<Vector3>();var colors=new List<Color>();var triangles=new List<int>();float half=roads.definition.roadWidth*.5f;var ink=new Color(.07f,.075f,.065f,.98f);var plank=new Color(.09f,.095f,.08f,.65f);
   foreach(var bridge in cart.Bridges){Vector2 a=roads.transform.InverseTransformPoint(terrain.transform.TransformPoint(bridge.from)),b=roads.transform.InverseTransformPoint(terrain.transform.TransformPoint(bridge.to));var delta=b-a;if(delta.sqrMagnitude<.00001f)continue;var normal=new Vector2(-delta.y,delta.x).normalized;
    BridgeStroke(a,b,half*2,StaffMapStyle.Panel,vertices,colors,triangles);
    int count=Mathf.Clamp(Mathf.CeilToInt(delta.magnitude/.12f),2,200);for(int i=1;i<count;i++){var at=Vector2.Lerp(a,b,i/(float)count);BridgeStroke(at-normal*half*.9f,at+normal*half*.9f,.018f,plank,vertices,colors,triangles);}
    BridgeStroke(a-normal*half,b-normal*half,.035f,ink,vertices,colors,triangles);BridgeStroke(a+normal*half,b+normal*half,.035f,ink,vertices,colors,triangles);
    BridgeStroke(a-normal*half*1.25f,a+normal*half*1.25f,.045f,ink,vertices,colors,triangles);BridgeStroke(b-normal*half*1.25f,b+normal*half*1.25f,.045f,ink,vertices,colors,triangles);BridgeCount++;
   }
   if(vertices.Count==0)return;bridgeMesh=new Mesh{name="Bridges at shared road river crossings"};if(vertices.Count>65535)bridgeMesh.indexFormat=UnityEngine.Rendering.IndexFormat.UInt32;bridgeMesh.SetVertices(vertices);bridgeMesh.SetColors(colors);bridgeMesh.SetTriangles(triangles,0);bridgeMesh.RecalculateBounds();bridgeMaterial=new Material(Shader.Find("Sprites/Default"));bridges=View("桥梁 · 桥面护栏与桥枕",bridgeMesh,bridgeMaterial,-.06f);bridges.GetComponent<MeshRenderer>().sortingOrder=4;
  }
  static void BridgeStroke(Vector2 a,Vector2 b,float width,Color color,List<Vector3> vertices,List<Color> colors,List<int> triangles){var delta=(b-a).normalized;var n=new Vector2(-delta.y,delta.x)*width*.5f;int offset=vertices.Count;vertices.Add(a-n);vertices.Add(a+n);vertices.Add(b+n);vertices.Add(b-n);for(int i=0;i<4;i++)colors.Add(color);triangles.AddRange(new[]{offset,offset+1,offset+2,offset,offset+2,offset+3});}
  void Dispose(Object value){if(value==null)return;if(Application.isPlaying)Destroy(value);else DestroyImmediate(value);}
  void Release(){Dispose(outline);Dispose(fill);Dispose(outerMesh);Dispose(innerMesh);Dispose(outerMaterial);Dispose(innerMaterial);Dispose(inkTexture);Dispose(bridges);Dispose(bridgeMesh);Dispose(bridgeMaterial);outline=fill=bridges=null;BridgeCount=0;}
  void OnDisable(){Release();built=false;}
  void OnDestroy(){Release();}
 }
}

