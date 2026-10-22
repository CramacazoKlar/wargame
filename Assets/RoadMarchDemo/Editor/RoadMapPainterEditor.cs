using System;
using UnityEditor;
using UnityEngine;

namespace CampaignDemo.Editor
{
    [CustomEditor(typeof(RoadMapRuntime))]
    public sealed class RoadMapPainterEditor : UnityEditor.Editor
    {
        bool painting;
        int previous=-1;
        int deleteNodeIndex;
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var root=(RoadMapRuntime)target;
            if(GUILayout.Button("打开态势地图工具（双线道路 / 桥梁 / 森林 / 弯曲山地）"))StaffMapAuthoringWindow.Open();
            if(root.definition==null)
            {
                if(GUILayout.Button("Create map asset"))
                {
                    string path=EditorUtility.SaveFilePanelInProject("Save map","RoadMap","asset","");
                    if(!string.IsNullOrEmpty(path))
                    {
                        var data=ScriptableObject.CreateInstance<RoadMapDefinition>();
                        data.nodes=new Vector2[0]; data.connections=new RoadMapDefinition.Connection[0];
                        data.spawnNodes=new int[0]; AssetDatabase.CreateAsset(data,path);
                        Undo.RecordObject(root,"Assign map"); root.definition=data; EditorUtility.SetDirty(root);
                    }
                }
                return;
            }
            if(Application.isPlaying) return;
            bool next=GUILayout.Toggle(painting,"Paint roads in Scene view","Button");
            if(next!=painting) { painting=next; previous=-1; SceneView.RepaintAll(); }
            if(GUILayout.Button("Start another road")) { previous=-1; SceneView.RepaintAll(); }
            if(GUILayout.Button("Frame map in Scene view")) SceneMapPreview.Frame(root);
            deleteNodeIndex=EditorGUILayout.IntField("Node to delete",deleteNodeIndex);
            using(new EditorGUI.DisabledScope(root.definition.nodes==null||deleteNodeIndex<0||deleteNodeIndex>=root.definition.nodes.Length))
            if(GUILayout.Button("Delete node and connected roads")){Undo.RecordObject(root.definition,"Delete road node");root.definition.RemoveNode(deleteNodeIndex);EditorUtility.SetDirty(root.definition);previous=-1;SceneView.RepaintAll();}
            if(GUILayout.Button("Edit map data / background")) Selection.activeObject=root.definition;
            EditorGUILayout.HelpBox("Painting: left-click adds nodes. Click an existing node to connect a junction. Esc ends the stroke. Alt navigation remains available. Disable painting to drag nodes.",MessageType.Info);
        }
        void OnSceneGUI()
        {
            if(StaffMapAuthoringWindow.Controls((RoadMapRuntime)target))return;
            if(Application.isPlaying) return;
            var root=(RoadMapRuntime)target; var map=root.definition;
            if(map==null) return;
            var nodes=map.nodes??new Vector2[0];
            Handles.color=new Color(0.1f,0.6f,0.9f);
            foreach(var link in map.connections??new RoadMapDefinition.Connection[0])
                if(link.a>=0&&link.b>=0&&link.a<nodes.Length&&link.b<nodes.Length)
                    Handles.DrawLine(root.transform.TransformPoint(nodes[link.a]),root.transform.TransformPoint(nodes[link.b]));
            for(int i=0;i<nodes.Length;i++)
            {
                var p=root.transform.TransformPoint(nodes[i]); Handles.Label(p,"Node "+i);
                if(!painting)
                {
                    EditorGUI.BeginChangeCheck(); var moved=Handles.PositionHandle(p,Quaternion.identity);
                    if(EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(map,"Move road node"); var local=root.transform.InverseTransformPoint(moved);
                        map.nodes[i]=new Vector2(local.x,local.y); map.MarkDirty(); EditorUtility.SetDirty(map);
                    }
                }
            }
            if(!painting) return;
            var e=Event.current;
            if(e.type==EventType.KeyDown&&e.keyCode==KeyCode.Escape)
            { previous=-1; e.Use(); return; }
            if(e.alt) return;
            if(e.type==EventType.Layout) HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));
            var ray=HandleUtility.GUIPointToWorldRay(e.mousePosition);
            var plane=new Plane(root.transform.forward,root.transform.position);
            float distance; if(!plane.Raycast(ray,out distance)) return;
            var world=ray.GetPoint(distance);
            if(previous>=0&&previous<nodes.Length)
            { Handles.color=Color.yellow; Handles.DrawLine(root.transform.TransformPoint(nodes[previous]),world); }
            if(e.type!=EventType.MouseDown||e.button!=0) { if(e.type==EventType.MouseMove) SceneView.RepaintAll(); return; }
            int picked=-1; float closest=14;
            for(int i=0;i<nodes.Length;i++)
            {
                float d=Vector2.Distance(HandleUtility.WorldToGUIPoint(root.transform.TransformPoint(nodes[i])),e.mousePosition);
                if(d<closest) { closest=d; picked=i; }
            }
            Undo.RecordObject(map,"Paint road");
            if(picked<0)
            {
                picked=nodes.Length; Array.Resize(ref nodes,nodes.Length+1);
                var local=root.transform.InverseTransformPoint(world); nodes[picked]=new Vector2(local.x,local.y);
                map.nodes=nodes;
            }
            if(previous>=0&&previous<nodes.Length&&previous!=picked&&Vector2.Distance(nodes[previous],nodes[picked])>0.00001f)
            {
                var links=map.connections??new RoadMapDefinition.Connection[0]; bool exists=false;
                foreach(var l in links) if((l.a==previous&&l.b==picked)||(l.a==picked&&l.b==previous)) exists=true;
                if(!exists) { Array.Resize(ref links,links.Length+1); links[links.Length-1]=new RoadMapDefinition.Connection(previous,picked); map.connections=links; }
            }
            previous=picked; map.MarkDirty(); EditorUtility.SetDirty(map); e.Use(); SceneView.RepaintAll();
        }
    }
}
