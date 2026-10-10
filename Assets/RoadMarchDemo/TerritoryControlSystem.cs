using System.Collections.Generic;
using UnityEngine;
namespace CampaignDemo
{
    public sealed class TerritoryControlSystem : MonoBehaviour
    {
        public BattleWorld world;
        [System.NonSerialized] public bool suppressPresentation;
        public bool OwnsRect(Rect area,string player){if(owners==null||area.xMin<bounds.xMin||area.xMax>bounds.xMax||area.yMin<bounds.yMin||area.yMax>bounds.yMax)return false;for(int i=0;i<owners.Length;i++)if(CellRect(i).Overlaps(area)&&owners[i]!=player)return false;return true;}
        public IEnumerable<Rect> AllCells(){if(owners!=null)for(int i=0;i<owners.Length;i++)yield return CellRect(i);}
        public IEnumerable<Rect> OwnedCells(string player){if(owners!=null)for(int i=0;i<owners.Length;i++)if(owners[i]==player)yield return CellRect(i);}
        public Rect bounds=new Rect(-18,-12.162406f,36,24.324812f);
        [Min(.1f)] public float cellSize=2;
        public bool usePixelCellSize=true;
        public Vector2Int cellPixels=new Vector2Int(128,128),mapPixels=new Vector2Int(1995,1348);
        public bool onlyInInspectionMode=true;
        public Color ownColor=new Color(.15f,.85f,.25f,.18f),enemyColor=new Color(.9f,.15f,.1f,.18f);
        string[] owners; Dictionary<string,float>[] counts; int columns,rows,revision,drawnRevision=-1;
        Mesh mesh;Material material;MeshRenderer overlayRenderer;Color[] vertexColors;string lastPlayer;bool lastFog;float cellWidth,cellHeight;
        public int CellCount { get { return owners==null?0:owners.Length; } }
        public void Initialize()
        {
            if(owners!=null)return;
            cellSize=Mathf.Max(.1f,cellSize);cellWidth=usePixelCellSize?bounds.width*Mathf.Max(1,cellPixels.x)/Mathf.Max(1,mapPixels.x):cellSize;cellHeight=usePixelCellSize?bounds.height*Mathf.Max(1,cellPixels.y)/Mathf.Max(1,mapPixels.y):cellSize;columns=Mathf.Clamp(Mathf.CeilToInt(bounds.width/cellWidth),1,128);rows=Mathf.Clamp(Mathf.CeilToInt(bounds.height/cellHeight),1,128);
            owners=new string[columns*rows];counts=new Dictionary<string,float>[owners.Length];
            var vertices=new Vector3[owners.Length*4];var indices=new int[owners.Length*6];vertexColors=new Color[vertices.Length];
            for(int i=0;i<owners.Length;i++)
            {
                owners[i]="";counts[i]=new Dictionary<string,float>();var r=CellRect(i);int v=i*4,t=i*6;
                vertices[v]=new Vector3(r.xMin,r.yMin);vertices[v+1]=new Vector3(r.xMax,r.yMin);vertices[v+2]=new Vector3(r.xMax,r.yMax);vertices[v+3]=new Vector3(r.xMin,r.yMax);
                indices[t]=v;indices[t+1]=v+1;indices[t+2]=v+2;indices[t+3]=v;indices[t+4]=v+2;indices[t+5]=v+3;
            }
            var go=new GameObject("Control territory overlay");go.transform.SetParent(transform,false);mesh=new Mesh{name="Fixed world control grid"};if(vertices.Length>65535)mesh.indexFormat=UnityEngine.Rendering.IndexFormat.UInt32;mesh.vertices=vertices;mesh.triangles=indices;mesh.colors=vertexColors;mesh.RecalculateBounds();go.AddComponent<MeshFilter>().sharedMesh=mesh;overlayRenderer=go.AddComponent<MeshRenderer>();overlayRenderer.sortingOrder=-5;material=new Material(Shader.Find("Sprites/Default"));overlayRenderer.sharedMaterial=material;
        }
        public int CellAt(Vector3 p)
        { if(owners==null||!bounds.Contains(p))return -1;return Mathf.Clamp(Mathf.FloorToInt((p.y-bounds.yMin)/cellHeight),0,rows-1)*columns+Mathf.Clamp(Mathf.FloorToInt((p.x-bounds.xMin)/cellWidth),0,columns-1); }
        Rect CellRect(int i) { float x=bounds.xMin+(i%columns)*cellWidth,y=bounds.yMin+(i/columns)*cellHeight;return Rect.MinMaxRect(x,y,Mathf.Min(bounds.xMax,x+cellWidth),Mathf.Min(bounds.yMax,y+cellHeight)); }
        public string OwnerAt(Vector3 p) { int i=CellAt(p);return i<0?"":owners[i]; }
        public void Resolve()
        {
            if(world==null||owners==null)return;foreach(var d in counts)d.Clear();
            foreach(var u in world.units)if(u!=null&&u.OnMap){int i=CellAt(u.transform.position);if(i<0)continue;var d=counts[i];if(!d.ContainsKey(u.ownerId))d.Add(u.ownerId,0);d[u.ownerId]+=world.campaign!=null&&world.campaign.Vehicle(u)?world.campaign.rules.At(world.campaign.rules.transportEquivalents,u.combatDefinition.echelon)*u.Fraction:u.Personnel;}
            for(int i=0;i<owners.Length;i++)
            {
                string cityOwner=null;bool mixed=false;
                foreach(var p in world.points)if(p!=null&&p.kind==FacilityKind.RecoveryPoint&&CellAt(p.transform.position)==i&&!string.IsNullOrEmpty(p.ownerId))
                { if(cityOwner==null)cityOwner=p.ownerId;else if(cityOwner!=p.ownerId)mixed=true; }
                string next=owners[i];
                if(cityOwner!=null)next=mixed?"":cityOwner;
                else
                {
                    string winner=null;float max=0;bool tie=false;
                    foreach(var pair in counts[i]){if(pair.Value>max){max=pair.Value;winner=pair.Key;tie=false;}else if(pair.Value==max)tie=true;}
                    if(winner!=null&&!tie)next=winner;
                    // No troops or equal forces do not erase existing control. No neighbour propagation.
                }
                if(next!=owners[i]){owners[i]=next;revision++;}
            }
        }
        public float Percent(string player,bool enemies=false)
        {
            if(owners==null||bounds.width<=0||bounds.height<=0)return 0;float area=0;
            for(int i=0;i<owners.Length;i++)if(enemies?owners[i]!=""&&owners[i]!=player:owners[i]==player){var r=CellRect(i);area+=r.width*r.height;}
            return 100*area/(bounds.width*bounds.height);
        }
        void LateUpdate()
        {
            if(world==null||overlayRenderer==null)return;
            bool inspect=world.setup.modes.CurrentMode==MouseCommandMode.Interaction;overlayRenderer.enabled=!suppressPresentation&&(!onlyInInspectionMode||inspect);
            if(drawnRevision==revision&&lastPlayer==world.currentPlayer&&lastFog==world.fogEnabled)return;
            for(int i=0;i<owners.Length;i++){Color c=owners[i]==world.currentPlayer?ownColor:owners[i]!=""&&!world.fogEnabled?enemyColor:Color.clear;for(int v=0;v<4;v++)vertexColors[i*4+v]=c;}
            mesh.colors=vertexColors;drawnRevision=revision;lastPlayer=world.currentPlayer;lastFog=world.fogEnabled;
        }
        void OnDestroy() { if(mesh!=null){if(Application.isPlaying)Destroy(mesh);else DestroyImmediate(mesh);}if(material!=null){if(Application.isPlaying)Destroy(material);else DestroyImmediate(material);} }
    }
}




