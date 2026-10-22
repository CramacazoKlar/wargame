using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace CampaignDemo.Editor
{
    // Editor-only rendering: no scene objects, saved data, or duplicate game backgrounds.
    [InitializeOnLoad]
    public static class SceneMapPreview
    {
        sealed class Preview
        {
            public Mesh mesh;
            public Material material;
        }
        static readonly Dictionary<RoadMapRuntime, Preview> previews = new Dictionary<RoadMapRuntime, Preview>();
        static SceneMapPreview()
        {
            SceneView.duringSceneGui += Draw;
            AssemblyReloadEvents.beforeAssemblyReload += Clear;
            EditorApplication.playModeStateChanged += state => Clear();
        }
        static void Draw(SceneView view)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || Event.current.type != EventType.Repaint) return;
            var alive = new HashSet<RoadMapRuntime>();
            foreach (var map in Resources.FindObjectsOfTypeAll<RoadMapRuntime>())
            {
                if (map == null || !map.gameObject.scene.IsValid() || !map.gameObject.scene.isLoaded ||
                    !map.gameObject.activeInHierarchy || map.definition == null || map.definition.background == null ||
                    SceneVisibilityManager.instance.IsHidden(map.gameObject)) continue;
                var terrain=map.GetComponent<FreeMarchMapRuntime>();
                if(terrain!=null&&terrain.SyncBoundsToBackground())EditorUtility.SetDirty(terrain.definition);
                alive.Add(map);
                Preview preview;
                if (!previews.TryGetValue(map, out preview))
                {
                    var shader = Shader.Find("Sprites/Default");
                    if (shader == null) continue;
                    preview = new Preview {
                        mesh = new Mesh { name = "Scene map preview", hideFlags = HideFlags.HideAndDontSave },
                        material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave }
                    };
                    previews.Add(map, preview);
                }
                UpdateMesh(preview.mesh, map.definition);
                preview.material.mainTexture = map.definition.background.texture;
                preview.material.color = Color.white;
                Graphics.DrawMesh(preview.mesh, map.transform.localToWorldMatrix, preview.material,
                    map.gameObject.layer, view.camera, 0, null, ShadowCastingMode.Off, false);
            }
            var stale = new List<RoadMapRuntime>();
            foreach (var pair in previews) if (!alive.Contains(pair.Key)) { Release(pair.Value); stale.Add(pair.Key); }
            foreach (var map in stale) previews.Remove(map);
        }
        public static void UpdateMesh(Mesh mesh, RoadMapDefinition data)
        {
            var half = data.backgroundSize * .5f; var centre = data.backgroundCenter;
            // Behind editable objects on the XY map plane; actual camera projection handles rotation/zoom.
            mesh.vertices = new[] {
                new Vector3(centre.x-half.x, centre.y-half.y, .01f),
                new Vector3(centre.x-half.x, centre.y+half.y, .01f),
                new Vector3(centre.x+half.x, centre.y+half.y, .01f),
                new Vector3(centre.x+half.x, centre.y-half.y, .01f)
            };
            var rect = data.background.textureRect; var texture = data.background.texture;
            float l = rect.xMin/texture.width, r = rect.xMax/texture.width;
            float b = rect.yMin/texture.height, t = rect.yMax/texture.height;
            mesh.uv = new[] { new Vector2(l,b), new Vector2(l,t), new Vector2(r,t), new Vector2(r,b) };
            mesh.colors = new[] { Color.white, Color.white, Color.white, Color.white };
            mesh.triangles = new[] { 0,1,2,0,2,3 }; mesh.RecalculateBounds();
        }
        public static Bounds WorldBounds(RoadMapRuntime map)
        {
            var half = map.definition.backgroundSize*.5f; var centre = map.definition.backgroundCenter;
            var bounds = new Bounds(map.transform.TransformPoint(centre-half), Vector3.zero);
            bounds.Encapsulate(map.transform.TransformPoint(centre+half));
            bounds.Encapsulate(map.transform.TransformPoint(centre+new Vector2(-half.x,half.y)));
            bounds.Encapsulate(map.transform.TransformPoint(centre+new Vector2(half.x,-half.y)));
            return bounds;
        }
        public static void Frame(RoadMapRuntime map)
        {
            if(map==null||map.definition==null||map.definition.background==null)
            { Debug.LogWarning("Assign the map Definition and its Background sprite first.",map); return; }
            var view = SceneView.lastActiveSceneView ?? EditorWindow.GetWindow<SceneView>();
            view.in2DMode = true; view.Frame(WorldBounds(map),false); view.Repaint();
        }
        static void Release(Preview preview)
        { Object.DestroyImmediate(preview.mesh); Object.DestroyImmediate(preview.material); }
        static void Clear()
        { foreach(var preview in previews.Values) Release(preview); previews.Clear(); SceneView.RepaintAll(); }
    }
}
