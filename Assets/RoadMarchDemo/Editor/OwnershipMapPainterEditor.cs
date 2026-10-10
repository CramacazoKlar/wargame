using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
namespace CampaignDemo.Editor
{
    [CustomEditor(typeof(OwnershipMapRuntime))]
    public sealed class OwnershipMapPainterEditor : UnityEditor.Editor
    {
        readonly List<Vector2> draft=new List<Vector2>();
        bool painting;
        string regionName="Region",owner="A";
        OwnershipState state;
        Color color=new Color(0.2f,0.65f,0.9f);
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector(); var root=(OwnershipMapRuntime)target;
            if(root.definition==null)
            {
                if(GUILayout.Button("Create ownership map"))
                {
                    string path=EditorUtility.SaveFilePanelInProject("Save ownership map","OwnershipMap","asset","");
                    if(!string.IsNullOrEmpty(path))
                    { var asset=ScriptableObject.CreateInstance<OwnershipMapDefinition>(); AssetDatabase.CreateAsset(asset,path); Undo.RecordObject(root,"Assign ownership map"); root.definition=asset; EditorUtility.SetDirty(root); }
                }
                return;
            }
            if(Application.isPlaying) return;
            regionName=EditorGUILayout.TextField("Region name",regionName); state=(OwnershipState)EditorGUILayout.EnumPopup("Initial ownership",state);
            owner=EditorGUILayout.TextField("Player ID",owner); color=EditorGUILayout.ColorField("Region color",color);
            if(GUILayout.Button(painting?"Cancel polygon":"Paint territory")) { painting=!painting; draft.Clear(); SceneView.RepaintAll(); }
            if(painting&&draft.Count>=3&&GUILayout.Button("Finish territory")) Commit(root.definition);
        }
        void Commit(OwnershipMapDefinition map)
        {
            if(draft.Count<3) return;
            Undo.RecordObject(map,"Paint territory"); var regions=map.regions??new OwnershipMapDefinition.Region[0]; Array.Resize(ref regions,regions.Length+1);
            regions[regions.Length-1]=new OwnershipMapDefinition.Region{name=regionName,polygon=draft.ToArray(),state=state,ownerId=owner,color=color};
            map.regions=regions; map.MarkDirty(); EditorUtility.SetDirty(map); draft.Clear(); painting=false; SceneView.RepaintAll();
        }
        void OnSceneGUI()
        {
            if(Application.isPlaying) return;
            var root=(OwnershipMapRuntime)target; var map=root.definition; if(map==null) return;
            foreach(var region in map.regions??new OwnershipMapDefinition.Region[0])
            {
                if(region==null||region.polygon==null) continue;
                Handles.color=region.state==OwnershipState.Unoccupied?Color.gray:region.color;
                for(int i=0;i<region.polygon.Length;i++)
                {
                    var at=root.transform.TransformPoint(region.polygon[i]); Handles.DrawLine(at,root.transform.TransformPoint(region.polygon[(i+1)%region.polygon.Length]));
                    if(!painting)
                    {
                        EditorGUI.BeginChangeCheck(); var moved=Handles.PositionHandle(at,Quaternion.identity);
                        if(EditorGUI.EndChangeCheck())
                        { Undo.RecordObject(map,"Move territory vertex"); var local=root.transform.InverseTransformPoint(moved); region.polygon[i]=new Vector2(local.x,local.y); map.MarkDirty(); EditorUtility.SetDirty(map); }
                    }
                }
                if(region.polygon.Length>0) Handles.Label(root.transform.TransformPoint(region.polygon[0]),region.name);
            }
            if(!painting) return;
            var e=Event.current;
            if(e.type==EventType.KeyDown&&e.keyCode==KeyCode.Escape) { painting=false; draft.Clear(); e.Use(); Repaint(); return; }
            if(e.type==EventType.KeyDown&&e.keyCode==KeyCode.Return&&draft.Count>=3) { Commit(map); e.Use(); Repaint(); return; }
            if(e.alt) return;
            if(e.type==EventType.Layout) HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));
            float distance; var ray=HandleUtility.GUIPointToWorldRay(e.mousePosition);
            if(!new Plane(root.transform.forward,root.transform.position).Raycast(ray,out distance)) return;
            var world=ray.GetPoint(distance); var localPoint=root.transform.InverseTransformPoint(world);
            Handles.color=color;
            for(int i=1;i<draft.Count;i++) Handles.DrawLine(root.transform.TransformPoint(draft[i-1]),root.transform.TransformPoint(draft[i]));
            if(draft.Count>0) Handles.DrawLine(root.transform.TransformPoint(draft[draft.Count-1]),world);
            if(e.type==EventType.MouseDown&&e.button==0)
            { var p=new Vector2(localPoint.x,localPoint.y); if(draft.Count==0||Vector2.Distance(p,draft[draft.Count-1])>0.001f) draft.Add(p); e.Use(); Repaint(); }
            if(e.type==EventType.MouseMove) SceneView.RepaintAll();
        }
    }
}
