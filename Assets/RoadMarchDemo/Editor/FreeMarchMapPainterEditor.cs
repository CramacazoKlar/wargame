using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
namespace CampaignDemo.Editor
{
    [CustomEditor(typeof(FreeMarchMapRuntime))]
    public sealed class FreeMarchMapPainterEditor : UnityEditor.Editor
    {
        bool painting;
        TerrainKind brush=TerrainKind.Blocked;
        readonly List<Vector2> draft=new List<Vector2>();
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var root=(FreeMarchMapRuntime)target;
            if(root.definition==null)
            {
                if(GUILayout.Button("Create free-march map"))
                {
                    string path=EditorUtility.SaveFilePanelInProject("Save map","FreeMarchMap","asset","");
                    if(!string.IsNullOrEmpty(path))
                    {
                        var asset=ScriptableObject.CreateInstance<FreeMarchMapDefinition>(); AssetDatabase.CreateAsset(asset,path);
                        Undo.RecordObject(root,"Assign terrain map"); root.definition=asset; EditorUtility.SetDirty(root);
                    }
                }
                return;
            }
            if(Application.isPlaying) return;
            brush=(TerrainKind)EditorGUILayout.EnumPopup("Terrain brush",brush);
            if(GUILayout.Button(painting?"Cancel polygon":"Paint polygon"))
            { painting=!painting; draft.Clear(); SceneView.RepaintAll(); }
            if(painting&&draft.Count>=3&&GUILayout.Button("Finish polygon")) Commit(root.definition);
            if(GUILayout.Button("Edit regions / map bounds")) Selection.activeObject=root.definition;
        }
        static Color TerrainColor(TerrainKind kind){return kind==TerrainKind.Blocked?Color.red:kind==TerrainKind.HighMountain?new Color(.75f,.35f,1):kind==TerrainKind.Mountain?new Color(.8f,.55f,.25f):Color.green;}
        void Commit(FreeMarchMapDefinition map)
        {
            if(draft.Count<3) return;
            Undo.RecordObject(map,"Paint terrain polygon");
            var regions=map.regions??new FreeMarchMapDefinition.Region[0]; Array.Resize(ref regions,regions.Length+1);
            regions[regions.Length-1]=new FreeMarchMapDefinition.Region{name=brush+" "+regions.Length,terrain=brush,polygon=draft.ToArray()};
            map.regions=regions; map.MarkDirty(); EditorUtility.SetDirty(map); painting=false; draft.Clear(); SceneView.RepaintAll();
        }
        void OnSceneGUI()
        {
            if(Application.isPlaying) return;
            var root=(FreeMarchMapRuntime)target; var map=root.definition;
            if(map==null) return;
            var roadMap=root.GetComponent<RoadMapRuntime>();
            var background=roadMap==null?null:roadMap.definition;
            if(background!=null&&background.background!=null)
            {
                var half=background.backgroundSize*0.5f;
                var a=HandleUtility.WorldToGUIPoint(root.transform.TransformPoint(background.backgroundCenter+new Vector2(-half.x,half.y)));
                var b=HandleUtility.WorldToGUIPoint(root.transform.TransformPoint(background.backgroundCenter+new Vector2(half.x,-half.y)));
                var imageRect=Rect.MinMaxRect(Mathf.Min(a.x,b.x),Mathf.Min(a.y,b.y),Mathf.Max(a.x,b.x),Mathf.Max(a.y,b.y));
                var spriteRect=background.background.textureRect; var texture=background.background.texture;
                var uv=new Rect(spriteRect.x/texture.width,spriteRect.y/texture.height,spriteRect.width/texture.width,spriteRect.height/texture.height);
                Handles.BeginGUI(); GUI.DrawTextureWithTexCoords(imageRect,texture,uv); Handles.EndGUI();
            }
            var rect=map.bounds;
            Handles.color=Color.white;
            var corners=new[]{new Vector2(rect.xMin,rect.yMin),new Vector2(rect.xMax,rect.yMin),new Vector2(rect.xMax,rect.yMax),new Vector2(rect.xMin,rect.yMax)};
            for(int i=0;i<4;i++) Handles.DrawLine(root.transform.TransformPoint(corners[i]),root.transform.TransformPoint(corners[(i+1)%4]));
            foreach(var region in map.regions??new FreeMarchMapDefinition.Region[0])
            {
                if(region==null||region.polygon==null) continue;
                Handles.color=TerrainColor(region.terrain);
                for(int i=0;i<region.polygon.Length;i++)
                {
                    var p=root.transform.TransformPoint(region.polygon[i]);
                    Handles.DrawLine(p,root.transform.TransformPoint(region.polygon[(i+1)%region.polygon.Length]));
                    if(!painting)
                    {
                        EditorGUI.BeginChangeCheck(); var moved=Handles.PositionHandle(p,Quaternion.identity);
                        if(EditorGUI.EndChangeCheck())
                        {
                            Undo.RecordObject(map,"Move terrain vertex"); var local=root.transform.InverseTransformPoint(moved);
                            region.polygon[i]=new Vector2(local.x,local.y); map.MarkDirty(); EditorUtility.SetDirty(map);
                        }
                    }
                }
                if(region.polygon.Length>0) Handles.Label(root.transform.TransformPoint(region.polygon[0]),region.name+" ("+region.terrain+")");
            }
            if(!painting) return;
            var e=Event.current;
            if(e.type==EventType.KeyDown&&e.keyCode==KeyCode.Escape) { draft.Clear(); painting=false; e.Use(); Repaint(); return; }
            if(e.type==EventType.KeyDown&&e.keyCode==KeyCode.Return&&draft.Count>=3) { Commit(map); e.Use(); Repaint(); return; }
            if(e.alt) return;
            if(e.type==EventType.Layout) HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));
            var ray=HandleUtility.GUIPointToWorldRay(e.mousePosition); float distance;
            if(!new Plane(root.transform.forward,root.transform.position).Raycast(ray,out distance)) return;
            var world=ray.GetPoint(distance); var localPoint=root.transform.InverseTransformPoint(world);
            Handles.color=TerrainColor(brush);
            for(int i=1;i<draft.Count;i++) Handles.DrawLine(root.transform.TransformPoint(draft[i-1]),root.transform.TransformPoint(draft[i]));
            if(draft.Count>0)
            { Handles.DrawLine(root.transform.TransformPoint(draft[draft.Count-1]),world); Handles.DrawDottedLine(world,root.transform.TransformPoint(draft[0]),4); }
            if(e.type==EventType.MouseDown&&e.button==0)
            {
                var point=new Vector2(localPoint.x,localPoint.y);
                if(draft.Count==0||Vector2.Distance(draft[draft.Count-1],point)>0.001f) draft.Add(point);
                e.Use(); Repaint(); SceneView.RepaintAll();
            }
            if(e.type==EventType.MouseMove) SceneView.RepaintAll();
        }
    }
}

