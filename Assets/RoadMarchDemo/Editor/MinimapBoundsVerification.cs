using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;
namespace CampaignDemo.Editor
{
    public static class MinimapBoundsVerification
    {
        static int checks;
        static void Check(bool value,string name){if(!value)throw new Exception(name);checks++;}
        public static void Run()
        {
            var root=new GameObject("Map navigation verification");var cameraObject=new GameObject("Map camera");var ui=new GameObject("Minimap test");
            var roadData=ScriptableObject.CreateInstance<RoadMapDefinition>();var terrainData=ScriptableObject.CreateInstance<FreeMarchMapDefinition>();var texture=new Texture2D(2,2);var sprite=Sprite.Create(texture,new Rect(0,0,2,2),Vector2.one*.5f);
            try
            {
                var setup=root.AddComponent<RoadMarchSceneSetup>();var road=root.AddComponent<RoadMapRuntime>();var terrain=root.AddComponent<FreeMarchMapRuntime>();var pan=root.AddComponent<MapPanController>();
                var camera=cameraObject.AddComponent<Camera>();camera.orthographic=true;camera.transform.position=new Vector3(0,0,-10);
                setup.roadMap=road;setup.freeMarchMap=terrain;setup.mapCamera=camera;setup.panController=pan;
                road.definition=roadData;terrain.definition=terrainData;roadData.background=sprite;roadData.backgroundSize=new Vector2(36,26.725f);terrainData.bounds=new Rect(-18,-13.3625f,36,26.725f);
                pan.mapCamera=camera;pan.useBounds=true;pan.cameraCenterBounds=new Rect(-1,-1,2,2);
                root.transform.position=new Vector3(10,20);root.transform.localScale=new Vector3(2,2,1);
                pan.RefreshMapBounds();Check(Mathf.Abs(pan.cameraCenterBounds.width-72)<.001f,"scaled map updates camera width");Check(Mathf.Abs(pan.cameraCenterBounds.height-53.45f)<.001f,"scaled map updates camera height");Check(pan.cameraCenterBounds.center==(Vector2)root.transform.position,"moved map updates camera centre");
                camera.transform.position=new Vector3(1000,1000,-10);pan.MoveKeyboard(Vector2.zero,0);Check(Mathf.Abs(camera.transform.position.x-46)<.001f&&Mathf.Abs(camera.transform.position.y-46.725f)<.001f,"camera reaches new far corner instead of old clamp");
                roadData.backgroundSize=new Vector2(100,60);pan.RefreshMapBounds();Check(pan.cameraCenterBounds.width==200&&pan.cameraCenterBounds.height==120,"larger artwork included without stale terrain crop");
                var world=root.AddComponent<BattleWorld>();world.setup=setup;var minimap=ui.AddComponent<TacticalMinimap>();minimap.world=world;
                Rect bounds;MapViewBounds.TryGet(road,terrain,out bounds);float side=Mathf.Max(bounds.width,bounds.height);var square=new Rect(bounds.center-Vector2.one*side*.5f,Vector2.one*side);
                typeof(TacticalMinimap).GetField("bounds",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(minimap,square);
                var point=new Vector3(bounds.xMax,bounds.yMax,0);var uv=minimap.WorldToMap(point);Check(uv.x<=1&&uv.y<=1,"far map corner remains in minimap");Check(Vector3.Distance(minimap.MapToWorld(uv),point)<.001f,"click navigation round trip reaches far corner");
                var changed=typeof(TacticalMinimap).GetMethod("ViewChanged",BindingFlags.Instance|BindingFlags.NonPublic);
                Check((bool)changed.Invoke(minimap,null),"first camera view forces refresh");
                foreach(var field in new[]{"lastCameraPosition","lastCameraRotation","lastCameraSize","lastCameraAspect","hasCameraSnapshot"})
                {object value=field=="lastCameraPosition"?(object)camera.transform.position:field=="lastCameraRotation"?camera.transform.rotation:field=="lastCameraSize"?camera.orthographicSize:field=="lastCameraAspect"?camera.aspect:(object)true;typeof(TacticalMinimap).GetField(field,BindingFlags.Instance|BindingFlags.NonPublic).SetValue(minimap,value);}
                Check(!(bool)changed.Invoke(minimap,null),"unchanged camera retains timed refresh");camera.transform.position+=Vector3.right;Check((bool)changed.Invoke(minimap,null),"camera movement refreshes immediately");camera.transform.position-=Vector3.right;camera.orthographicSize+=1;Check((bool)changed.Invoke(minimap,null),"zoom refreshes immediately");
                var clipped=MapViewBounds.ClipViewport(new[]{new Vector3(-500,-500),new Vector3(500,-500),new Vector3(500,500),new Vector3(-500,500)},bounds); Check(clipped.Length==4,"zoomed-out viewport remains visible on minimap");foreach(var corner in clipped)Check(corner.x>=bounds.xMin-.001f&&corner.x<=bounds.xMax+.001f&&corner.y>=bounds.yMin-.001f&&corner.y<=bounds.yMax+.001f,"viewport clipped to map edges");
                Debug.Log("MINIMAP_BOUNDS_VERIFY_PASS "+checks);EditorApplication.Exit(0);
            }
            catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
            finally{UnityEngine.Object.DestroyImmediate(ui);UnityEngine.Object.DestroyImmediate(root);UnityEngine.Object.DestroyImmediate(cameraObject);UnityEngine.Object.DestroyImmediate(roadData);UnityEngine.Object.DestroyImmediate(terrainData);UnityEngine.Object.DestroyImmediate(sprite);UnityEngine.Object.DestroyImmediate(texture);}
        }
    }
}

