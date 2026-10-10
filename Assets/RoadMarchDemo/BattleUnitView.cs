using UnityEngine;
namespace CampaignDemo
{
    public sealed class BattleUnitView : MonoBehaviour
    {
        UnitSymbolIdentity identity;BattleUnit unit;Sprite lastSymbol;Camera lastCamera;float lastZoom=-1,lastRequestedPixels=-1;int lastPixels=-1;Vector3 lastLocalScale;Matrix4x4 lastParent;
        SpriteRenderer[] renderers; Sprite[] original; Color[] colors;
        SpriteDrawMode[] originalModes;Vector2[] originalSizes;
        [Tooltip("Fixed width of the complete symbol canvas in screen pixels, including transparent margins.")]
        [Min(0)] public float minimumSymbolPixels=36;
        public void Initialize()
        { identity=GetComponent<UnitSymbolIdentity>();unit=GetComponent<BattleUnit>();renderers=GetComponentsInChildren<SpriteRenderer>();original=new Sprite[renderers.Length];colors=new Color[renderers.Length];originalModes=new SpriteDrawMode[renderers.Length];originalSizes=new Vector2[renderers.Length];for(int i=0;i<renderers.Length;i++){original[i]=renderers[i].sprite;colors[i]=renderers[i].color;originalModes[i]=renderers[i].drawMode;originalSizes[i]=renderers[i].size;} }
        public void Apply(bool visible,bool own,bool known,Sprite unknown)
        {
            if(renderers==null)Initialize();
            for(int i=0;i<renderers.Length;i++)
            { var r=renderers[i];if(r==null)continue;if(i!=0||identity==null){r.drawMode=originalModes[i];r.size=originalSizes[i];}r.enabled=visible&&(known||i==0);r.sprite=known||unknown==null?original[i]:unknown;r.color=own?colors[i]:known?new Color(1,.55f,.55f,colors[i].a):Color.white; var symbols=identity; if(i==0&&symbols!=null&&(known||own)){var icon=symbols.Select(own);if(icon!=null){r.sprite=icon;r.color=Color.white;}}else if(i==0&&!own&&!known&&symbols!=null&&symbols.catalog!=null&&symbols.catalog.enemyUnknown!=null){r.sprite=symbols.catalog.enemyUnknown;r.color=Color.white;
                }
                if(i==0&&symbols!=null&&symbols.catalog!=null)NormalizeSymbol(r,symbols.catalog);
            }
        }
        void LateUpdate()
        {
            if(unit!=null&&unit.Campaign!=null&&unit.Campaign.world.SimulationEnded)return;
            if(renderers==null||identity==null||identity.catalog==null)return;
            if(renderers.Length>0&&renderers[0]!=null)NormalizeSymbol(renderers[0],identity.catalog);
        }
        public static Vector3 SymbolScale(Transform target)
        {
            // Build from local transforms; SpriteRenderer's sliced geometry can affect cached lossyScale.
            Matrix4x4 matrix=Matrix4x4.identity;
            for(var t=target;t!=null;t=t.parent)matrix=Matrix4x4.TRS(t.localPosition,t.localRotation,t.localScale)*matrix;
            return new Vector3(matrix.MultiplyVector(Vector3.right).magnitude,matrix.MultiplyVector(Vector3.up).magnitude,matrix.MultiplyVector(Vector3.forward).magnitude);
        }
        void NormalizeSymbol(SpriteRenderer renderer,UnitSymbolCatalog catalog)
        {
            if(renderer.sprite==null)return;
            var reference=catalog.enemy!=null&&catalog.enemy.Length>0?catalog.enemy[0]:null;if(reference==null)reference=renderer.sprite;
            // Use the full source canvas, not a tight alpha mesh or the particular unit scale.
            float width=reference.rect.width/reference.pixelsPerUnit;
            var world=unit?.Campaign?.world;var camera=world!=null&&world.setup!=null?world.setup.mapCamera:Camera.main;
            var local=renderer.transform.localScale;var parent=renderer.transform.parent==null?Matrix4x4.identity:renderer.transform.parent.localToWorldMatrix;int pixels=camera==null?0:camera.pixelHeight;float zoom=camera==null?0:camera.orthographicSize;if(lastSymbol==renderer.sprite&&lastCamera==camera&&lastPixels==pixels&&lastZoom==zoom&&lastLocalScale==local&&lastParent==parent&&lastRequestedPixels==minimumSymbolPixels)return;lastRequestedPixels=minimumSymbolPixels;lastSymbol=renderer.sprite;lastCamera=camera;lastPixels=pixels;lastZoom=zoom;lastLocalScale=local;lastParent=parent;
            if(camera!=null&&camera.orthographic)width=minimumSymbolPixels*2*camera.orthographicSize/Mathf.Max(1,camera.pixelHeight);
            if(renderer.sprite.name.StartsWith("Enemy_"))width*=4f/3f;
            float height=width*renderer.sprite.rect.height/renderer.sprite.rect.width;
            renderer.drawMode=SpriteDrawMode.Sliced;var scale=SymbolScale(renderer.transform);
            renderer.size=new Vector2(width/Mathf.Max(.0001f,Mathf.Abs(scale.x)),height/Mathf.Max(.0001f,Mathf.Abs(scale.y)));
        }
    }
}



