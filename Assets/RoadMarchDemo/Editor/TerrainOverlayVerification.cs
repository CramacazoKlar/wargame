using System;
using System.IO;
using UnityEngine;
using UnityEditor;
namespace CampaignDemo.Editor
{
    public static class TerrainOverlayVerification
    {
        static int checks;
        static void Check(bool value, string message) { if (!value) throw new Exception(message); checks++; }
        static FreeMarchMapDefinition.Region Region(TerrainKind kind, params Vector2[] points)
        { return new FreeMarchMapDefinition.Region { terrain = kind, polygon = points }; }
        static FreeMarchMapDefinition.Region Box(TerrainKind kind, float x, float y, float w, float h)
        { return Region(kind, new Vector2(x,y), new Vector2(x+w,y), new Vector2(x+w,y+h), new Vector2(x,y+h)); }
        public static void Run()
        {
            GameObject root = null; FreeMarchMapDefinition data = null;
            try
            {
                root = new GameObject("Overlay verification");
                var map = root.AddComponent<FreeMarchMapRuntime>();
                var modes = root.AddComponent<MouseModeStateMachine>();
                var overlay = root.AddComponent<TerrainMarchOverlay>();
                data = ScriptableObject.CreateInstance<FreeMarchMapDefinition>();
                data.bounds = new Rect(0,0,8,4); map.definition = data;
                data.regions = new[] {
                    Box(TerrainKind.Grass,0,0,8,4), Box(TerrainKind.Mountain,2,0,3,4),
                    Box(TerrainKind.HighMountain,4,0,2,4), Box(TerrainKind.Road,3,1,1,2),
                    Box(TerrainKind.Water,6,0,1,4), Box(TerrainKind.Forest,7,0,1,4), Box(TerrainKind.Blocked,0,0,1,1)
                };
                overlay.map=map; overlay.modes=modes; overlay.resolution=128;
                modes.SetMode(MouseCommandMode.RoadMarch); overlay.Refresh();
                Check(root.GetComponentInChildren<SpriteRenderer>()==null,"terrain hidden by default");overlay.ToggleTerrain();
                var renderer=root.GetComponentInChildren<SpriteRenderer>();
                Check(renderer!=null&&renderer.enabled,"mode 1 shows overlay");
                var texture=renderer.sprite.texture;
                Func<float,float,Color32> pixel=(x,y)=>(Color32)texture.GetPixel((int)(x/8*texture.width),(int)(y/4*texture.height));
                Check(pixel(1,2).r==105&&pixel(1,2).g==165&&pixel(1,2).b==95&&pixel(1,2).a==115,"grass lighter green with clearer fill");
                Check(pixel(2.5f,2).r==242&&pixel(2.5f,2).g==145,"mountain orange");
                Check(pixel(4.5f,2).b==230&&pixel(4.5f,2).r==172,"high mountain purple overrides mountain");
                Check(pixel(3.5f,2).a==0,"later road removes underlying tint");
                Check(pixel(6.5f,2).a==0,"water unchanged");
                Check(pixel(7.5f,2).r==35&&pixel(7.5f,2).g==112&&pixel(7.5f,2).b==48&&pixel(7.5f,2).a==71,"forest dark green with same opacity");
                Check(pixel(.5f,.5f).r==35&&pixel(.5f,.5f).g==35&&pixel(.5f,.5f).b==35&&pixel(.5f,.5f).a==166,"blocked darker gray with stronger fill");
                Check(renderer.sortingOrder==-9,"overlay between background and units");
                Check(Mathf.Abs(renderer.bounds.size.x-8)<.001f&&Mathf.Abs(renderer.bounds.size.y-4)<.001f,"aligned with map bounds");
                modes.SetMode(MouseCommandMode.FreeMarch);overlay.Refresh();Check(renderer.enabled,"mode 2 shows overlay");
                Check(renderer.sprite.texture==texture,"mode switch reuses texture");
                modes.SetMode(MouseCommandMode.Interaction);overlay.Refresh();Check(renderer.enabled,"R visibility persists in mode 3");
                modes.SetMode(MouseCommandMode.None);overlay.Refresh();Check(renderer.enabled,"visibility controlled by R rather than mode");overlay.ToggleTerrain();Check(!renderer.enabled,"second toggle hides terrain");overlay.ToggleTerrain();Check(renderer.enabled,"third toggle shows terrain");
                // Concave polygons, transparent overrides, and out-of-bounds clipping.
                data.regions=new[] {Region(TerrainKind.Mountain,new Vector2(-1,-1),new Vector2(5,-1),new Vector2(5,1),new Vector2(1,1),new Vector2(1,5),new Vector2(-1,5))};
                data.MarkDirty();modes.SetMode(MouseCommandMode.FreeMarch);overlay.Refresh();texture=renderer.sprite.texture;
                Check(pixel(.5f,3).a==71&&pixel(4,.5f).a==71,"concave polygon filled and clipped");
                Check(pixel(4,3).a==0,"concave notch remains clear");
                data.defaultTerrain=TerrainKind.HighMountain;data.regions=new FreeMarchMapDefinition.Region[0];data.MarkDirty();overlay.Refresh();texture=renderer.sprite.texture;
                Check(pixel(4,2).b==230,"default coloured terrain fills bounds after edit");
                // Edit-mode checks invoke the play-mode lifecycle callback explicitly.
                overlay.SendMessage("OnDisable");Check(!renderer.enabled,"disabled component hides overlay");
                Debug.Log("TERRAIN_OVERLAY_VERIFY_PASS "+checks);EditorApplication.Exit(0);
            }
            catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
            finally { if(root!=null)UnityEngine.Object.DestroyImmediate(root);if(data!=null)UnityEngine.Object.DestroyImmediate(data); }
        }
    }
}



