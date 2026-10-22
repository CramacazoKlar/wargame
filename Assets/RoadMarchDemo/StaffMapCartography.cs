using System;
using System.Collections.Generic;
using UnityEngine;
namespace CampaignDemo
{
    [ExecuteAlways,DisallowMultipleComponent,DefaultExecutionOrder(350)]
    public sealed class StaffMapCartography:MonoBehaviour
    {
        [Serializable] public struct Bridge {public Vector2 from,to;public int edge;}
        public RoadMapRuntime roads;public FreeMarchMapRuntime terrain;
        [Tooltip("预先绘制的地图底图；运行时直接显示，避免重新逐像素绘图。")]
        public Sprite bakedBackground;
        public bool drawRoadsInBackground=true;
        [Range(512,4096)] public int resolution=2048;
        [Min(.025f)] public float visualRoadWidth=.085f;
        [Range(0,1)] public float wear=.55f;
        public int seed=217;
        [Range(0,1)] public float foldStrength=.75f;
        public int GridMissingSamples {get;private set;}
        [HideInInspector] public bool interactivePreview;
        public readonly List<Bridge> Bridges=new List<Bridge>();
        public Texture2D Texture {get{return texture;}}
        public int BuildCount {get;private set;}
        public TerrainContourInk ContourInk {get;private set;}
        public SettlementClusterInk SettlementInk {get;private set;}
        SpriteRenderer view;Sprite sprite;Texture2D texture;TerrainGeometry raw;
        int lastHash=int.MinValue;float next;Rect[] manualBridges;RasterWaterMap bridgeWater;
        readonly Dictionary<long,List<TerrainGeometry.Region>> tiles=new Dictionary<long,List<TerrainGeometry.Region>>();float tileSize=1;bool indexed;
        void OnEnable(){Invalidate();}void OnValidate(){Invalidate();}
        public void Invalidate(){lastHash=int.MinValue;next=0;Resolve();if(terrain!=null)terrain.InvalidateGeometry();}
        void Resolve(){if(roads==null)roads=GetComponent<RoadMapRuntime>();if(terrain==null)terrain=GetComponent<FreeMarchMapRuntime>();}
        void Update(){Resolve();if(terrain==null||terrain.definition==null||roads==null||roads.definition==null)return;if(bakedBackground!=null){ShowBakedBackground();return;}if(Time.realtimeSinceStartup<next)return;next=Time.realtimeSinceStartup+.25f;int hash=GeometryHash();if(hash!=lastHash){Rebuild();lastHash=GeometryHash();}}
        public void ShowBakedBackground(){Resolve();if(bakedBackground==null||terrain==null||terrain.definition==null)return;if(view==null){var g=new GameObject("态势地图 · 已烘焙底图");g.hideFlags=HideFlags.DontSave;g.transform.SetParent(terrain.transform,false);view=g.AddComponent<SpriteRenderer>();view.sortingOrder=3;}var bounds=terrain.definition.bounds;view.sprite=bakedBackground;view.color=Color.white;view.transform.localPosition=new Vector3(bounds.center.x,bounds.center.y,0);view.transform.localScale=new Vector3(bounds.width/bakedBackground.bounds.size.x,bounds.height/bakedBackground.bounds.size.y,1);}
        int GeometryHash(){unchecked{int h=seed*397^terrain.definition.bounds.GetHashCode()^foldStrength.GetHashCode()^resolution^wear.GetHashCode()^visualRoadWidth.GetHashCode()^interactivePreview.GetHashCode();var d=roads.definition;h=h*397^d.Revision;h=h*397^terrain.definition.Revision;h=h*397^terrain.transform.localToWorldMatrix.GetHashCode();h=h*397^roads.transform.localToWorldMatrix.GetHashCode();foreach(var p in d.nodes??new Vector2[0])h=h*397^p.GetHashCode();foreach(var e in d.connections??new RoadMapDefinition.Connection[0])h=h*397^e.a*31^e.b;return h;}}
        void RawGeometry(){var d=terrain.definition;raw=new TerrainGeometry{Min=new RoadPoint(d.bounds.xMin,d.bounds.yMin),Max=new RoadPoint(d.bounds.xMax,d.bounds.yMax),DefaultTerrain=d.defaultTerrain};var list=new List<TerrainGeometry.Region>();foreach(var r in d.regions??new FreeMarchMapDefinition.Region[0]){if(r==null||r.polygon==null)continue;var p=new RoadPoint[r.polygon.Length];for(int i=0;i<p.Length;i++)p[i]=new RoadPoint(r.polygon[i].x,r.polygon[i].y);list.Add(new TerrainGeometry.Region{Kind=r.terrain,Polygon=p});}raw.Regions=list.ToArray();indexed=false;}
        static long Tile(int x,int y){return ((long)x<<32)^(uint)y;}
        void IndexRegions(){indexed=true;tiles.Clear();tileSize=Mathf.Max(.1f,terrain.definition.bounds.width/32);foreach(var r in raw.Regions){if(r.Polygon.Length<3)continue;float x0=float.MaxValue,y0=float.MaxValue,x1=float.MinValue,y1=float.MinValue;foreach(var p in r.Polygon){x0=Mathf.Min(x0,(float)p.X);y0=Mathf.Min(y0,(float)p.Y);x1=Mathf.Max(x1,(float)p.X);y1=Mathf.Max(y1,(float)p.Y);}for(int y=Mathf.FloorToInt(y0/tileSize);y<=Mathf.FloorToInt(y1/tileSize);y++)for(int x=Mathf.FloorToInt(x0/tileSize);x<=Mathf.FloorToInt(x1/tileSize);x++){long key=Tile(x,y);if(!tiles.TryGetValue(key,out var list))tiles[key]=list=new List<TerrainGeometry.Region>();list.Add(r);}}}
        TerrainKind KindAt(Vector2 p){if(raw==null)RawGeometry();if(!indexed)IndexRegions();var at=new RoadPoint(p.x,p.y);var kind=raw.DefaultTerrain;if(tiles.TryGetValue(Tile(Mathf.FloorToInt(p.x/tileSize),Mathf.FloorToInt(p.y/tileSize)),out var list))foreach(var r in list)if(r.NearPoint(at)&&TerrainGeometry.Contains(r.Polygon,at))kind=r.Kind;return kind;}
        float ForestEdge(Vector2 p){float distance=float.MaxValue;if(tiles.TryGetValue(Tile(Mathf.FloorToInt(p.x/tileSize),Mathf.FloorToInt(p.y/tileSize)),out var list))foreach(var r in list){var at=new RoadPoint(p.x,p.y);if(r.Kind!=TerrainKind.Forest||!r.NearPoint(at)||!TerrainGeometry.Contains(r.Polygon,at))continue;float boundary=float.MaxValue;for(int i=0;i<r.Polygon.Length;i++){var a=r.Polygon[i];var b=r.Polygon[(i+1)%r.Polygon.Length];var va=new Vector2((float)a.X,(float)a.Y);var vb=new Vector2((float)b.X,(float)b.Y);var d=vb-va;float t=d.sqrMagnitude>0?Mathf.Clamp01(Vector2.Dot(p-va,d)/d.sqrMagnitude):0;boundary=Mathf.Min(boundary,Vector2.Distance(p,va+d*t));}if(distance==float.MaxValue)distance=boundary;else distance=Mathf.Max(distance,boundary);}return distance;}
        Vector2 RoadPointLocal(Vector2 p){return terrain.transform.InverseTransformPoint(roads.transform.TransformPoint(p));}
        public bool RawWater(Vector2 p){if(raw==null)RawGeometry();if(KindAt(p)==TerrainKind.Water)return true;var water=terrain.waterMap;if(water==null)return false;var world=terrain.transform.TransformPoint(p);return water.IsRawWater(new RoadPoint(world.x,world.y));}
        public void RefreshBridges()
        {
            Resolve();if(roads==null||roads.definition==null||terrain==null||terrain.definition==null)return;RawGeometry();IndexRegions();Bridges.Clear();var nodes=roads.definition.nodes??new Vector2[0];var edges=roads.definition.connections??new RoadMapDefinition.Connection[0];
            for(int i=0;i<edges.Length;i++)
            {
                var e=edges[i];if(e.a<0||e.b<0||e.a>=nodes.Length||e.b>=nodes.Length)continue;Vector2 a=RoadPointLocal(nodes[e.a]),b=RoadPointLocal(nodes[e.b]),direction=(b-a).normalized;float length=Vector2.Distance(a,b);if(length<.0001f)continue;
                int samples=Mathf.Clamp(Mathf.CeilToInt(length/.025f),2,20000);bool previous=RawWater(a),dryBank=!previous;float enter=0;
                for(int k=1;k<=samples;k++){float t=k/(float)samples;bool water=RawWater(Vector2.Lerp(a,b,t));if(water&&!previous)enter=(k-1f)/samples;if(!water&&previous){var from=Vector2.Lerp(a,b,enter)-direction*.1f;var to=Vector2.Lerp(a,b,t)+direction*.1f;if(Vector2.Distance(from,to)>.12f)Bridges.Add(new Bridge{from=from,to=to,edge=i});}if(!water)dryBank=true;previous=water;}
                // A connection node may itself be in water: adjacent edge decks meet at that node.
                if(previous){var from=Vector2.Lerp(a,b,enter)-direction*.1f;var to=b+direction*.04f;if(Vector2.Distance(from,to)>.12f)Bridges.Add(new Bridge{from=from,to=to,edge=i});}
            }
            var raster=terrain.waterMap;
            if(raster!=null&&Application.isPlaying)
            {
                if(bridgeWater!=raster){RestoreBridges();bridgeWater=raster;manualBridges=(Rect[])raster.bridges.Clone();}
                var rects=new List<Rect>(manualBridges??new Rect[0]);foreach(var b in Bridges){Vector2 a=raster.transform.InverseTransformPoint(terrain.transform.TransformPoint(b.from)),c=raster.transform.InverseTransformPoint(terrain.transform.TransformPoint(b.to));float padding=Mathf.Max(.04f,roads.definition.roadWidth*.6f);rects.Add(Rect.MinMaxRect(Mathf.Min(a.x,c.x)-padding,Mathf.Min(a.y,c.y)-padding,Mathf.Max(a.x,c.x)+padding,Mathf.Max(a.y,c.y)+padding));}raster.bridges=rects.ToArray();
            }
        }
        public void Rebuild()
        {
            if(Application.isPlaying&&bakedBackground!=null){ShowBakedBackground();return;}
            Resolve();if(terrain==null||terrain.definition==null||roads==null||roads.definition==null)return;RawGeometry();RefreshBridges();BuildCount++;
            Rect bounds=terrain.definition.bounds;int width=Mathf.Clamp(interactivePreview?Mathf.Min(resolution,1024):resolution,512,4096),height=Mathf.Max(128,Mathf.RoundToInt(width*bounds.height/bounds.width));var c=new StaffMapCanvas(width,height,bounds);
            for(int y=0;y<height;y++)for(int x=0;x<width;x++)
            {
                float u=(x+.5f)/width,v=(y+.5f)/height;var p=new Vector2(bounds.xMin+u*bounds.width,bounds.yMin+v*bounds.height);var kind=KindAt(p);
                Color color=kind==TerrainKind.Water?StaffMapStyle.Water:kind==TerrainKind.HighMountain?StaffMapStyle.HighMountain:kind==TerrainKind.Mountain?StaffMapStyle.Mountain:StaffMapStyle.Paper;
                if(kind==TerrainKind.Forest){float opacity=Mathf.Clamp01(ForestEdge(p)/(bounds.width/190)+StaffMapCanvas.Noise(p.x*19,p.y*19,seed)*.4f-.1f);color=Color.Lerp(color,StaffMapStyle.Forest,opacity*.87f);}
                if(terrain.waterMap!=null&&RawWater(p))color=StaffMapStyle.Water;
                float coarse=StaffMapCanvas.Noise(u*13,v*9,seed),grain=StaffMapCanvas.Hash(x,y,seed)-.5f;
                color*=1+grain*.035f;float stains=Mathf.Clamp01((StaffMapCanvas.Noise(u*27,v*20,seed+31)-.57f)*3);
                color=Color.Lerp(color,StaffMapStyle.Hex(0xA49A80),wear*(.19f*stains+.07f*coarse));
                float edge=Mathf.Min(Mathf.Min(u,1-u),Mathf.Min(v,1-v));color=Color.Lerp(color,StaffMapStyle.Hex(0xA99B7F),wear*Mathf.Exp(-edge*75)*(.2f+.35f*coarse));
                float fold=Mathf.Exp(-Mathf.Abs(v-.5f)*height/1.6f)+.65f*Mathf.Exp(-Mathf.Abs(u-.5f)*width/1.4f);color=Color.Lerp(color,StaffMapStyle.Hex(0x958B76),wear*.14f*fold);color.a=1;c.pixels[y*width+x]=color;
            }
            DrawForests(c);DrawMountains(c);DrawRiverInk(c);DrawSettlements(c);if(drawRoadsInBackground)DrawRoads(c);
            for(int i=0;i<11000;i++){float u=StaffMapCanvas.Hash(i,0,seed+90),v=StaffMapCanvas.Hash(i,1,seed+90);float edge=Mathf.Min(Mathf.Min(u,1-u),Mathf.Min(v,1-v));float alpha=wear*(.09f+Mathf.Exp(-edge*30)*.16f);c.Ellipse(new Vector2(bounds.xMin+u*bounds.width,bounds.yMin+v*bounds.height),Vector2.one*bounds.width/2600*(.4f+StaffMapCanvas.Hash(i,2,seed)*1.2f),StaffMapStyle.Hex(0x8D816B),alpha);}
            // Final paper wear crosses all printed terrain and linework, anchored to the map.
            for(int y=0;y<height;y++)for(int x=0;x<width;x++){
                float u=(x+.5f)/width,v=(y+.5f)/height;
                float stain=Mathf.Clamp01((StaffMapCanvas.Noise(u*9,v*7,seed+811)-.43f)*2.6f);
                float fibre=StaffMapCanvas.Hash(x,y,seed+812);
                float rub=Mathf.Clamp01((StaffMapCanvas.Noise(u*45,v*34,seed+814)-.66f)*5);
                var col=c.pixels[y*width+x];col=Color.Lerp(col,StaffMapStyle.Hex(0x82765D),wear*(stain*.38f+rub*.14f));
                col=Color.Lerp(col,StaffMapStyle.Paper,wear*(fibre>.975f?.15f:0));float edgeDistance=Mathf.Min(Mathf.Min(u,1-u),Mathf.Min(v,1-v));
                col=Color.Lerp(col,StaffMapStyle.Hex(0x756449),wear*.40f*Mathf.Exp(-edgeDistance*26)*( .45f+.55f*StaffMapCanvas.Noise(u*32,v*25,seed+821)));
                float folds=PaperCrease(new Vector2(u,v),seed)*foldStrength;
                col=folds<0?Color.Lerp(col,StaffMapStyle.Hex(0x615540),Mathf.Clamp01(-folds)*.37f):Color.Lerp(col,StaffMapStyle.Hex(0xF8F0DD),Mathf.Clamp01(folds)*.42f);
                col*=.955f;col.a=1;c.pixels[y*width+x]=col;
            }
            ReleaseTexture();texture=new Texture2D(width,height,TextureFormat.RGB24,false){name="Code generated staff map",filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp};texture.SetPixels(c.pixels);texture.Apply();sprite=Sprite.Create(texture,new Rect(0,0,width,height),new Vector2(.5f,.5f),width/bounds.width);
            if(view==null){var g=new GameObject("态势地图 · 代码生成");g.hideFlags=HideFlags.DontSave;g.transform.SetParent(terrain.transform,false);view=g.AddComponent<SpriteRenderer>();view.sortingOrder=3;}
            view.transform.localPosition=new Vector3(bounds.center.x,bounds.center.y,0);view.sprite=sprite;view.color=Color.white;
            lastHash=GeometryHash();
        }
        public static float PaperCrease(Vector2 uv,int seed)
        {
            float result=0;
            // Uneven fold valleys with an adjacent lit lip, broad compression and fine fibres.
            for(int i=0;i<5;i++){
                bool vertical=i<3;float along=vertical?uv.y:uv.x,across=vertical?uv.x:uv.y;
                float centre=vertical?(i==0?.315f:i==1?.67f:.91f):(i==3?.49f:.82f);
                float bend=(StaffMapCanvas.Noise(along*5,i,seed+935)-.5f)*.008f;
                float d=across-centre-bend;float strength=i==2||i==4?.46f:1;
                float uneven=.65f+.35f*StaffMapCanvas.Noise(along*22,i,seed+936);
                result+=strength*uneven*(-.84f*Mathf.Exp(-d*d/.0000035f)+.72f*Mathf.Exp(-(d-.0026f)*(d-.0026f)/.0000024f)-.16f*Mathf.Exp(-d*d/.00012f));
            }
            float edge=Mathf.Min(Mathf.Min(uv.x,1-uv.x),Mathf.Min(uv.y,1-uv.y));
            float crumple=StaffMapCanvas.Noise(uv.x*57+uv.y*21,uv.y*17,seed+940)-.5f;
            result+=crumple*.8f*Mathf.Exp(-edge*12);return result;
        }
        void GridStroke(StaffMapCanvas c,Vector2 a,Vector2 b,float width,Color ink,int salt)
        {
            float length=Vector2.Distance(a,b);int count=Mathf.Max(2,Mathf.CeilToInt(length/.025f));var direction=(b-a).normalized;var normal=new Vector2(-direction.y,direction.x);
            for(int i=0;i<count;i++){
                float arc=(i+.5f)*length/count;float pressure=RiverPressure(arc,salt,seed+1523);
                if(pressure<.34f){GridMissingSamples++;continue;}
                float wobble=(StaffMapCanvas.Noise(arc*8,salt,seed+32)-.5f)*width*.45f;
                c.Line(Vector2.Lerp(a,b,i/(float)count)+normal*wobble,Vector2.Lerp(a,b,(i+1f)/count)+normal*wobble,width*Mathf.Lerp(.76f,1.2f,pressure),ink,Mathf.Lerp(.10f,.23f,pressure));
            }
        }
        public int RiverBankSegments {get;private set;}
        public int RiverInkSamples {get;private set;}
        public int RiverMissingSamples {get;private set;}
        static float RiverPressure(float arc,int salt,int seed){return StaffMapCanvas.Noise(arc*2.1f,salt,seed)*.72f+StaffMapCanvas.Noise(arc*6.7f,salt+7,seed+11)*.28f;}
        void RiverStroke(StaffMapCanvas c,Vector2 a,Vector2 b,ref float arc,int salt,bool bank)
        {
            float length=Vector2.Distance(a,b);if(length<.00001f)return;
            int count=Mathf.Max(1,Mathf.CeilToInt(length/(c.bounds.width/1900)));var n=new Vector2(-(b-a).y,(b-a).x).normalized;
            for(int i=0;i<count;i++){
                float at=arc+length*(i+.5f)/count;float pressure=RiverPressure(at,salt,seed+451);
                if(!bank){RiverInkSamples++;if(pressure<.5f)RiverMissingSamples++;}
                if(pressure<(bank?.29f:.5f))continue;
                float width=c.bounds.width/(bank?2350:2550)*Mathf.Lerp(.7f,1.35f,pressure);
                float wobble=(StaffMapCanvas.Noise(at*10,salt,seed+13)-.5f)*width*.65f;
                c.Line(Vector2.Lerp(a,b,i/(float)count)+n*wobble,Vector2.Lerp(a,b,(i+1f)/count)+n*wobble,width,StaffMapStyle.Hex(bank?0x131612:0x1A1C17),bank?Mathf.Lerp(.48f,.84f,pressure):Mathf.Lerp(.18f,.47f,pressure));
            }
            arc+=length;
        }
        void DrawRiverInk(StaffMapCanvas c)
        {
            RiverBankSegments=RiverInkSamples=RiverMissingSamples=0;
            // Marching squares follows every shoreline, including islands and horizontal bends.
            int nx=Mathf.Min(c.width,1000),ny=Mathf.Max(32,Mathf.RoundToInt(nx*c.bounds.height/c.bounds.width));
            float dx=c.bounds.width/nx,dy=c.bounds.height/ny;var wet=new bool[(nx+1)*(ny+1)];
            for(int y=0;y<=ny;y++)for(int x=0;x<=nx;x++)wet[y*(nx+1)+x]=RawWater(new Vector2(c.bounds.xMin+x*dx,c.bounds.yMin+y*dy));
            var links=new Dictionary<long,List<long>>();var positions=new Dictionary<long,Vector2>();
            Func<int,int,long> key=(x,y)=>((long)x<<32)|(uint)y;
            Action<long,long> join=(a,b)=>{if(!links.TryGetValue(a,out var list))links[a]=list=new List<long>();list.Add(b);if(!links.TryGetValue(b,out list))links[b]=list=new List<long>();list.Add(a);};
            for(int y=0;y<ny;y++)for(int x=0;x<nx;x++){
                var corners=new[]{new Vector2(x,y),new Vector2(x+1,y),new Vector2(x+1,y+1),new Vector2(x,y+1)};var hits=new List<long>();
                for(int edge=0;edge<4;edge++){
                    var a=corners[edge];var b=corners[(edge+1)%4];bool wa=wet[(int)a.y*(nx+1)+(int)a.x],wb=wet[(int)b.y*(nx+1)+(int)b.x];if(wa==wb)continue;
                    long id=key((int)(a.x+b.x),(int)(a.y+b.y));hits.Add(id);
                    if(!positions.ContainsKey(id)){var lo=new Vector2(c.bounds.xMin+a.x*dx,c.bounds.yMin+a.y*dy);var hi=new Vector2(c.bounds.xMin+b.x*dx,c.bounds.yMin+b.y*dy);for(int k=0;k<5;k++){var mid=(lo+hi)*.5f;if(RawWater(mid)==wa)lo=mid;else hi=mid;}positions[id]=(lo+hi)*.5f;}
                }
                if(hits.Count==2)join(hits[0],hits[1]);else if(hits.Count==4){join(hits[0],hits[1]);join(hits[2],hits[3]);}
            }
            var visited=new HashSet<long>();int path=0;
            foreach(var entry in links){if(visited.Contains(entry.Key))continue;long start=entry.Key;long current=start,previous=long.MinValue;float arc=0;int guard=0;
                // Start open coastlines at their endpoints so no half is skipped.
                long probe=start,last=long.MinValue;while(links[probe].Count==2&&guard++<links.Count){long next=links[probe][0]==last?links[probe][1]:links[probe][0];if(next==start)break;last=probe;probe=next;}if(links[probe].Count==1)current=probe;
                guard=0;while(guard++<=links.Count){visited.Add(current);long next=long.MinValue;foreach(long n in links[current])if(n!=previous&&(!visited.Contains(n)||n==start)){next=n;break;}if(next==long.MinValue)break;RiverStroke(c,positions[current],positions[next],ref arc,path+31,true);RiverBankSegments++;previous=current;current=next;if(visited.Contains(current))break;}path++;
            }
            Vector2 previousCentre=Vector2.zero;bool have=false;float centreArc=0;float step=c.bounds.height/c.height*2;
            for(float y=c.bounds.yMin+step;y<c.bounds.yMax;y+=step){bool found=false;float best=float.MaxValue;Vector2 chosen=Vector2.zero;
                for(float x=c.bounds.xMin;x<c.bounds.xMax;x+=step){if(!RawWater(new Vector2(x,y)))continue;float start=x;while(x<c.bounds.xMax&&RawWater(new Vector2(x,y)))x+=step;float span=x-start;if(span<step*4||span>c.bounds.width*.42f)continue;var mid=new Vector2((start+x)*.5f,y);float score=have?Mathf.Abs(mid.x-previousCentre.x):span;if(score<best){best=score;chosen=mid;found=true;}}
                if(found){if(have&&Vector2.Distance(previousCentre,chosen)<step*12)RiverStroke(c,previousCentre,chosen,ref centreArc,79,false);previousCentre=chosen;have=true;}else have=false;
            }
        }
        void DrawForests(StaffMapCanvas c)
        {
            var bounds=c.bounds;float step=bounds.width/118;var pale=StaffMapStyle.Hex(0x8F9D79);var middle=StaffMapStyle.Hex(0x778864);var dark=StaffMapStyle.Hex(0x3E5435);
            for(int j=0;j<Mathf.CeilToInt(bounds.height/step);j++)for(int i=0;i<119;i++)
            {
                float noise=StaffMapCanvas.Hash(i,j,seed);var at=new Vector2(bounds.xMin+(i+.2f+.6f*noise)*step,bounds.yMin+(j+.15f+.7f*StaffMapCanvas.Hash(i,j,seed+1))*step);
                if(KindAt(at)!=TerrainKind.Forest)continue;if(noise<.12f)continue;
                float r=step*(.57f+noise*.48f);c.Ellipse(at,new Vector2(r,r*.83f),pale,.22f);c.Ellipse(at+Vector2.right*r*.28f,new Vector2(r*.72f,r*.75f),middle,.14f);
            }
            float treeStep=step*.81f;
            for(int j=0;j<Mathf.CeilToInt(bounds.height/treeStep);j++)for(int i=0;i<Mathf.CeilToInt(bounds.width/treeStep);i++)
            {
                float n=StaffMapCanvas.Hash(i,j,seed+19);if(n<.33f)continue;
                var at=new Vector2(bounds.xMin+(i-.2f+1.4f*StaffMapCanvas.Hash(i,j,seed+22))*treeStep,bounds.yMin+(j-.2f+1.4f*StaffMapCanvas.Hash(i,j,seed+20))*treeStep);
                if(KindAt(at)!=TerrainKind.Forest)continue;
                float r=treeStep*(.09f+.055f*n);c.Ellipse(at,new Vector2(r*1.9f,r*2.35f),middle,.6f);
                c.Ellipse(at,new Vector2(r,r*1.65f),dark,.88f);c.Ellipse(at+Vector2.up*r*1.3f,new Vector2(r*.56f,r*1.1f),dark,.9f);
                c.Line(at-Vector2.up*r*2.5f,at+Vector2.up*r*1.9f,r*.45f,StaffMapStyle.Ink,.7f,false);
            }
        }
        void DrawMountains(StaffMapCanvas c){ContourInk=new TerrainContourInk(terrain,roads,seed);ContourInk.Draw(c);}
        void DrawRoads(StaffMapCanvas c)
        {
            var nodes=roads.definition.nodes??new Vector2[0];var color=StaffMapStyle.Hex(0x171815);float line=c.bounds.width/2100;
            var connections=roads.definition.connections??new RoadMapDefinition.Connection[0];
            for(int edgeIndex=0;edgeIndex<connections.Length;edgeIndex++)
            {
                var edge=connections[edgeIndex];if(edge.a<0||edge.b<0||edge.a>=nodes.Length||edge.b>=nodes.Length)continue;
                int first=Mathf.Min(edge.a,edge.b),last=Mathf.Max(edge.a,edge.b);Vector2 a=RoadPointLocal(nodes[first]),b=RoadPointLocal(nodes[last]);
                c.Line(a,b,visualRoadWidth*.80f,StaffMapStyle.Paper,.70f,false);
                int current=edgeIndex;
                var ink=StaffRoadInk.Build(a,b,visualRoadWidth,line,c.bounds.width/36f,seed+first*7919+last*104729,point=>{
                    foreach(var bridge in Bridges)if(bridge.edge==current){var d=bridge.to-bridge.from;float t=Vector2.Dot(point-bridge.from,d)/Mathf.Max(.00001f,d.sqrMagnitude);if(t>-.15f&&t<1.15f)return true;}return false;
                });
                foreach(var stroke in ink.strokes)c.Polyline(stroke.points,stroke.width,stroke.dark?StaffMapStyle.Hex(0x11120F):color,stroke.alpha);
            }
            foreach(var bridge in Bridges)
            {
                Vector2 a=bridge.from,b=bridge.to,d=(b-a).normalized,n=new Vector2(-d.y,d.x);float half=visualRoadWidth*.9f;
                c.Line(a,b,half*2,StaffMapStyle.Panel,1,false);c.Line(a+n*half,b+n*half,line*1.3f,color,.95f);c.Line(a-n*half,b-n*half,line*1.3f,color,.95f);
                int count=Mathf.Max(2,Mathf.CeilToInt(Vector2.Distance(a,b)/(.08f)));for(int i=0;i<=count;i++){var p=Vector2.Lerp(a,b,i/(float)count);c.Line(p-n*half*.85f,p+n*half*.85f,line*.75f,color,.55f);}
                c.Line(a-n*half*1.35f,a+n*half*1.35f,line*1.7f,color,.92f);c.Line(b-n*half*1.35f,b+n*half*1.35f,line*1.7f,color,.92f);
            }
        }
        void DrawSettlements(StaffMapCanvas c){SettlementInk=new SettlementClusterInk(terrain,roads,seed);SettlementInk.Draw(c);}
        void DrawFrame(StaffMapCanvas c)
        {
            GridMissingSamples=0;float margin=c.bounds.width*.018f;Rect b=new Rect(c.bounds.xMin+margin,c.bounds.yMin+margin,c.bounds.width-2*margin,c.bounds.height-2*margin);float line=c.bounds.width/2600;
            var ink=StaffMapStyle.Hex(0x171815);var corners=new[]{new Vector2(b.xMin,b.yMin),new Vector2(b.xMax,b.yMin),new Vector2(b.xMax,b.yMax),new Vector2(b.xMin,b.yMax)};c.Polyline(corners,line*1.6f,ink,.98f,true);
            float spacing=c.bounds.width/16;for(int i=1;i<16;i++){float x=b.xMin+i*b.width/16;GridStroke(c,new Vector2(x,b.yMin),new Vector2(x,b.yMax),line*.85f,ink,i+13);}
            int rows=Mathf.Max(2,Mathf.RoundToInt(b.height/spacing));for(int i=1;i<rows;i++){float y=b.yMin+i*b.height/rows;GridStroke(c,new Vector2(b.xMin,y),new Vector2(b.xMax,y),line*.85f,ink,i+71);}
            for(int i=0;i<=16;i++){float x=b.xMin+i*b.width/16;c.Line(new Vector2(x,b.yMin),new Vector2(x,b.yMin-margin*.16f),line,ink,.6f);c.Line(new Vector2(x,b.yMax),new Vector2(x,b.yMax+margin*.16f),line,ink,.6f);DrawNumber(c,i,new Vector2(x-margin*.20f,b.yMax+margin*.3f),margin*.17f,ink);}
            for(int i=0;i<=rows;i++)DrawNumber(c,i,new Vector2(b.xMin-margin*.7f,b.yMin+i*b.height/rows),margin*.17f,ink);
        }
        static void DrawNumber(StaffMapCanvas c,int value,Vector2 at,float size,Color ink)
        {
            float[][] glyphs={new float[]{.2f,0,.8f,0,1,.25f,1,1.75f,.8f,2,.2f,2,0,1.75f,0,.25f,.2f,0},new float[]{.15f,1.65f,.55f,2,.55f,0},new float[]{0,1.7f,.2f,2,.8f,2,1,1.7f,1,1.35f,0,0,1,0},new float[]{0,2,.8f,2,1,1.7f,.8f,1.1f,.4f,1,.8f,.9f,1,.6f,.85f,.15f,.65f,0,0,0},new float[]{.8f,0,.8f,2,0,.6f,1,.6f},new float[]{1,2,0,2,0,1.1f,.75f,1.1f,1,.85f,1,.25f,.75f,0,0,0},new float[]{1,2,.3f,2,0,1.6f,0,.3f,.25f,0,.8f,0,1,.3f,1,.8f,.75f,1.1f,0,1.1f},new float[]{0,2,1,2,.3f,0},new float[]{.2f,1,.05f,1.35f,.05f,1.75f,.25f,2,.75f,2,.95f,1.75f,.95f,1.35f,.8f,1,.2f,1,0,.7f,0,.25f,.25f,0,.75f,0,1,.25f,1,.7f,.8f,1},new float[]{1,.9f,.25f,.9f,0,1.2f,0,1.75f,.25f,2,.75f,2,1,1.75f,1,.4f,.7f,0,0,0}};
            string text=value.ToString("00");for(int j=0;j<text.Length;j++){var g=glyphs[text[j]-'0'];for(int i=2;i<g.Length;i+=2)c.Line(at+new Vector2(j*1.6f+g[i-2],g[i-1])*size,at+new Vector2(j*1.6f+g[i],g[i+1])*size,size*.21f,ink,.98f,false);}
        }
        void RestoreBridges(){if(bridgeWater!=null&&manualBridges!=null)bridgeWater.bridges=manualBridges;bridgeWater=null;manualBridges=null;}
        void ReleaseTexture(){if(sprite!=null){if(Application.isPlaying)Destroy(sprite);else DestroyImmediate(sprite);}if(texture!=null){if(Application.isPlaying)Destroy(texture);else DestroyImmediate(texture);}}
        void OnDestroy(){RestoreBridges();if(view!=null){if(Application.isPlaying)Destroy(view.gameObject);else DestroyImmediate(view.gameObject);}ReleaseTexture();}
    }
}





