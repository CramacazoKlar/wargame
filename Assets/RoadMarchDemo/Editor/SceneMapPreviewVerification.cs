using System;
using UnityEditor;
using UnityEngine;
namespace CampaignDemo.Editor
{
    public static class SceneMapPreviewVerification
    {
        public static void Run()
        {
            var root=new GameObject("Preview test");var data=ScriptableObject.CreateInstance<RoadMapDefinition>();
            var texture=new Texture2D(100,80);var sprite=Sprite.Create(texture,new Rect(10,20,60,40),new Vector2(.5f,.5f));
            var mesh=new Mesh();
            try
            {
                data.background=sprite;data.backgroundSize=new Vector2(36,26.724992f);data.backgroundCenter=new Vector2(3,-2);
                var map=root.AddComponent<RoadMapRuntime>();map.definition=data;
                SceneMapPreview.UpdateMesh(mesh,data);
                if(mesh.vertexCount!=4||mesh.triangles.Length!=6)throw new Exception("Quad geometry");
                if(Vector2.Distance(mesh.bounds.size,data.backgroundSize)>.001f)throw new Exception("Native map aspect");
                if(Vector2.Distance(mesh.bounds.center,data.backgroundCenter)>.001f)throw new Exception("Background centre");
                if(Vector2.Distance(mesh.uv[0],new Vector2(.1f,.25f))>.001f||Vector2.Distance(mesh.uv[2],new Vector2(.7f,.75f))>.001f)throw new Exception("Sprite UV orientation");
                root.transform.position=new Vector3(5,2,0);root.transform.localScale=new Vector3(2,3,1);
                var bounds=SceneMapPreview.WorldBounds(map);
                if(Vector2.Distance(bounds.size,new Vector2(72,80.174976f))>.001f)throw new Exception("Frame follows map scale");
                if(Vector2.Distance(bounds.center,new Vector2(11,-4))>.001f)throw new Exception("Frame follows map centre");
                if(root.transform.childCount!=0)throw new Exception("No persistent preview children");
                Debug.Log("SCENE_MAP_PREVIEW_VERIFY_PASS 7");EditorApplication.Exit(0);
            }
            catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
            finally { UnityEngine.Object.DestroyImmediate(root);UnityEngine.Object.DestroyImmediate(data);UnityEngine.Object.DestroyImmediate(mesh);UnityEngine.Object.DestroyImmediate(sprite);UnityEngine.Object.DestroyImmediate(texture); }
        }
    }
}
