using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
namespace CampaignDemo.Editor
{
    [CustomEditor(typeof(RoadMapDefinition))]
    public sealed class RoadMapDefinitionEditor:UnityEditor.Editor
    {
        ReorderableList nodes;
        void OnEnable()
        {
            nodes=new ReorderableList(serializedObject,serializedObject.FindProperty("nodes"),false,true,true,true);
            nodes.drawHeaderCallback=r=>EditorGUI.LabelField(r,"Road nodes — removal updates connections");
            nodes.drawElementCallback=(r,i,a,f)=>{r.height=EditorGUIUtility.singleLineHeight;EditorGUI.PropertyField(r,nodes.serializedProperty.GetArrayElementAtIndex(i),new GUIContent("Node "+i));};
            nodes.onRemoveCallback=list=>Remove(list.index);
        }
        void Remove(int index)
        {
            serializedObject.ApplyModifiedProperties();var map=(RoadMapDefinition)target;
            Undo.RecordObject(map,"Delete road node and remap connections");map.RemoveNode(index);EditorUtility.SetDirty(map);serializedObject.Update();nodes.index=Mathf.Min(index,map.nodes.Length-1);SceneView.RepaintAll();
        }
        public override void OnInspectorGUI()
        {
            serializedObject.Update();DrawPropertiesExcluding(serializedObject,"m_Script","nodes");nodes.DoLayoutList();
            EditorGUILayout.HelpBox("Select a node and press minus to remove it safely. Connected roads are removed; later indices are updated. Ctrl+Z undoes the operation.",MessageType.Info);
            if(serializedObject.ApplyModifiedProperties())((RoadMapDefinition)target).MarkDirty();
        }
    }
}
