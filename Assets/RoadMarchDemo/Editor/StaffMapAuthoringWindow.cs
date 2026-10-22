using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
namespace CampaignDemo.Editor
{
    public sealed class StaffMapAuthoringWindow:EditorWindow
    {
        enum Mode { 编辑,连线道路,点绘森林,弯曲山地 }
        Mode mode;StaffMapAuthoring author;RoadMapRuntime road;int previous=-1,selectedMountain=-1;float forestRadius=.8f,mountainWidth=1.1f;bool high;Vector2 lastPaint;bool hasLast;
        static StaffMapAuthoringWindow active;
        Vector2 scroll;
        public static bool Controls(RoadMapRuntime map){return active!=null&&active.author!=null&&active.road==map;}
        public static bool Controls(FreeMarchMapRuntime map){return active!=null&&active.author!=null&&active.author.terrain==map;}
        [MenuItem("Tools/态势地图/绘制工具")]
        public static void Open(){GetWindow<StaffMapAuthoringWindow>("态势地图工具");}
        void OnEnable(){active=this;SceneView.duringSceneGui+=SceneGUI;Undo.undoRedoPerformed+=UndoRedo;}
        void OnDisable(){if(active==this)active=null;SceneView.duringSceneGui-=SceneGUI;Undo.undoRedoPerformed-=UndoRedo;}
        void UndoRedo(){if(author!=null){author.Resolve();var local=author.GetComponent<LocalBattleMap>();if(local!=null)local.Rebuild();if(author.terrain!=null)author.terrain.InvalidateGeometry();author.Refresh();}SceneView.RepaintAll();Repaint();}
        void OnGUI(){using(var scope=new EditorGUILayout.ScrollViewScope(scroll)){scroll=scope.scrollPosition;DrawToolGUI();}}
        void DrawToolGUI()
        {
            EditorGUILayout.LabelField("参谋态势图 · 数据与外观一起编辑",EditorStyles.boldLabel);
            road=(RoadMapRuntime)EditorGUILayout.ObjectField("道路对象",road,typeof(RoadMapRuntime),true);
            if(GUILayout.Button("绑定当前选择 / 场景地图")){if(road==null&&Selection.activeGameObject!=null)road=Selection.activeGameObject.GetComponent<RoadMapRuntime>();if(road==null)road=FindObjectOfType<RoadMapRuntime>();Bind();}
            if(author==null&&road!=null)author=road.GetComponent<StaffMapAuthoring>();
            if(author==null){EditorGUILayout.HelpBox("选择带 RoadMapRuntime 的地图对象，点击绑定。工具沿用现有节点、连接与地形数据。",MessageType.Info);return;}
            EditorGUI.BeginChangeCheck();var next=(Mode)GUILayout.Toolbar((int)mode,new[]{"编辑","道路","森林","山地"});if(EditorGUI.EndChangeCheck()){mode=next;previous=-1;hasLast=false;SceneView.RepaintAll();}
            if(mode==Mode.连线道路){EditorGUILayout.HelpBox("场景中左键连续点选；靠近已有节点会连接路口。Esc 或“另起一条”断开笔画。双线道路、跨河桥梁自动生成，原节点寻路功能保留。编辑模式可拖动节点。",MessageType.Info);if(GUILayout.Button("另起一条道路"))previous=-1;}
            if(mode==Mode.点绘森林){forestRadius=EditorGUILayout.Slider("森林笔刷半径",forestRadius,.15f,4);EditorGUILayout.HelpBox("在场景中按住左键点涂/拖涂。生成不规则浅绿树冠和深绿树点，同时写入可藏匿的森林地形。Ctrl+Z 撤销。",MessageType.Info);}
            if(mode==Mode.弯曲山地)
            {
                mountainWidth=EditorGUILayout.Slider("山体半宽",mountainWidth,.2f,5);high=EditorGUILayout.Toggle("高山（颜色略深）",high);
                if(GUILayout.Button("新建山脉控制线")){Record("新建山脉");var stroke=new StaffMapAuthoring.MountainStroke{id=Guid.NewGuid().ToString("N"),name=high?"高山":"山地",highMountain=high,halfWidth=mountainWidth};author.mountains.Add(stroke);selectedMountain=author.mountains.Count-1;Dirty();}
                for(int i=0;i<author.mountains.Count;i++)if(GUILayout.Toggle(selectedMountain==i,author.mountains[i].name+" · "+author.mountains[i].points.Count+" 个控制点","Button"))selectedMountain=i;
                if(selectedMountain>=0&&selectedMountain<author.mountains.Count)
                {
                    var stroke=author.mountains[selectedMountain];EditorGUI.BeginChangeCheck();float width=EditorGUILayout.Slider("当前山体半宽",stroke.halfWidth,.2f,5);bool isHigh=EditorGUILayout.Toggle("当前山体为高山",stroke.highMountain);
                    if(EditorGUI.EndChangeCheck()){Record("调整山体");stroke.halfWidth=width;stroke.highMountain=isHigh;author.SyncMountains();Dirty();}
                    if(GUILayout.Button("删除当前山脉")){Record("删除山脉");author.mountains.RemoveAt(selectedMountain);selectedMountain=-1;author.SyncMountains();Dirty();}
                }
                EditorGUILayout.HelpBox("新建后在场景中点击添加控制点。拖动圆点弯曲整条山脉；拖动侧边方块调整宽度。等高线、色块和实际山地范围同步变化，Ctrl+Z 可撤销。",MessageType.Info);
            }
            var view=author.GetComponent<StaffMapCartography>();if(view!=null){EditorGUI.BeginChangeCheck();float wear=EditorGUILayout.Slider("纸面脏污",view.wear,0,1);float width=EditorGUILayout.Slider("双线道路间距",view.visualRoadWidth,.04f,.20f);if(EditorGUI.EndChangeCheck()){Undo.RecordObject(view,"调整制图风格");view.wear=wear;view.visualRoadWidth=width;view.Invalidate();EditorUtility.SetDirty(view);}}
            if(GUILayout.Button("刷新地图外观")){author.Refresh();author.GetComponent<StaffMapCartography>().Rebuild();SceneView.RepaintAll();}
            if(GUILayout.Button("保存场景和地图数据")){AssetDatabase.SaveAssets();UnityEditor.SceneManagement.EditorSceneManager.SaveScene(author.gameObject.scene);}
        }
        void Bind(){if(road==null)return;author=road.GetComponent<StaffMapAuthoring>();if(author==null)author=Undo.AddComponent<StaffMapAuthoring>(road.gameObject);author.roads=road;author.terrain=road.GetComponent<FreeMarchMapRuntime>();if(author.terrain==null)author.terrain=FindObjectOfType<FreeMarchMapRuntime>();author.Resolve();author.Refresh();Selection.activeGameObject=road.gameObject;Dirty();}
        void Record(string name){Undo.RecordObject(author,name);if(author.terrain!=null&&author.terrain.definition!=null)Undo.RecordObject(author.terrain.definition,name);var local=author.GetComponent<LocalBattleMap>();if(local!=null)Undo.RecordObject(local,name);if(road!=null&&road.definition!=null)Undo.RecordObject(road.definition,name);}
        void Dirty(){EditorUtility.SetDirty(author);if(author.terrain!=null&&author.terrain.definition!=null)EditorUtility.SetDirty(author.terrain.definition);var local=author.GetComponent<LocalBattleMap>();if(local!=null)EditorUtility.SetDirty(local);if(road!=null&&road.definition!=null)EditorUtility.SetDirty(road.definition);UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(author.gameObject.scene);SceneView.RepaintAll();Repaint();}
        void SceneGUI(SceneView scene)
        {
            if(author==null||Application.isPlaying||author.terrain==null||road==null||road.definition==null)return;var e=Event.current;var terrain=author.terrain;var nodes=road.definition.nodes??new Vector2[0];
            var preview=author.GetComponent<StaffMapCartography>();if(preview!=null){if(e.type==EventType.MouseDrag)preview.interactivePreview=true;if(e.type==EventType.MouseUp&&preview.interactivePreview){preview.interactivePreview=false;preview.Invalidate();}}
            if(mode==Mode.编辑)
            {
                Handles.color=StaffMapStyle.Blue;for(int i=0;i<nodes.Length;i++){var p=road.transform.TransformPoint(nodes[i]);EditorGUI.BeginChangeCheck();var moved=Handles.FreeMoveHandle(p,HandleUtility.GetHandleSize(p)*.045f,Vector3.zero,Handles.DotHandleCap);if(EditorGUI.EndChangeCheck()){Record("移动道路节点");road.definition.nodes[i]=road.transform.InverseTransformPoint(moved);road.definition.MarkDirty();author.Refresh();Dirty();}}
                return;
            }
            if(mode==Mode.弯曲山地&&selectedMountain>=0&&selectedMountain<author.mountains.Count)
            {
                var stroke=author.mountains[selectedMountain];var curve=StaffMapAuthoring.Spline(stroke.points);Handles.color=StaffMapStyle.Hex(0x9A805F);for(int i=1;i<curve.Count;i++)Handles.DrawLine(terrain.transform.TransformPoint(curve[i-1]),terrain.transform.TransformPoint(curve[i]));
                for(int i=0;i<stroke.points.Count;i++)
                {
                    var p=terrain.transform.TransformPoint(stroke.points[i]);float size=HandleUtility.GetHandleSize(p)*.06f;EditorGUI.BeginChangeCheck();var moved=Handles.FreeMoveHandle(p,size,Vector3.zero,Handles.DotHandleCap);if(EditorGUI.EndChangeCheck()){Record("弯曲山脉");stroke.points[i]=terrain.transform.InverseTransformPoint(moved);author.SyncMountains();Dirty();}
                    if(i==stroke.points.Count/2&&stroke.points.Count>=2){var tangent=(stroke.points[Mathf.Min(stroke.points.Count-1,i+1)]-stroke.points[Mathf.Max(0,i-1)]).normalized;var n=new Vector2(-tangent.y,tangent.x);var side=terrain.transform.TransformPoint(stroke.points[i]+n*stroke.halfWidth);Handles.DrawDottedLine(p,side,4);EditorGUI.BeginChangeCheck();var movedSide=Handles.FreeMoveHandle(side,size*.8f,Vector3.zero,Handles.RectangleHandleCap);if(EditorGUI.EndChangeCheck()){Record("拖动山体宽度");stroke.halfWidth=Mathf.Max(.1f,Vector2.Distance(terrain.transform.InverseTransformPoint(movedSide),stroke.points[i]));author.SyncMountains();Dirty();}}
                }
            }
            if(e.alt)return;if(e.type==EventType.KeyDown&&e.keyCode==KeyCode.Escape){previous=-1;hasLast=false;e.Use();return;}
            if(e.type==EventType.Layout)HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));
            var ray=HandleUtility.GUIPointToWorldRay(e.mousePosition);if(!new Plane(terrain.transform.forward,terrain.transform.position).Raycast(ray,out float distance))return;var world=ray.GetPoint(distance);var localPoint=(Vector2)terrain.transform.InverseTransformPoint(world);
            if(mode==Mode.点绘森林){Handles.color=StaffMapStyle.Forest;Handles.DrawWireDisc(world,terrain.transform.forward,forestRadius);}
            if(mode==Mode.连线道路&&previous>=0&&previous<nodes.Length){Handles.color=StaffMapStyle.Ink;Handles.DrawDottedLine(road.transform.TransformPoint(nodes[previous]),world,3);}
            if(e.type==EventType.MouseUp){hasLast=false;}
            bool click=e.type==EventType.MouseDown&&e.button==0;bool paint=mode==Mode.点绘森林&&e.type==EventType.MouseDrag&&e.button==0&&(!hasLast||Vector2.Distance(lastPaint,localPoint)>forestRadius*.40f);
            if(!click&&!paint){if(e.type==EventType.MouseMove)scene.Repaint();return;}
            // Leave drags on a mountain handle to Unity's existing hot control.
            if(mode==Mode.弯曲山地&&GUIUtility.hotControl!=0)return;
            if(mode==Mode.连线道路)
            {
                int picked=-1;float closest=14;for(int i=0;i<nodes.Length;i++){float d=Vector2.Distance(HandleUtility.WorldToGUIPoint(road.transform.TransformPoint(nodes[i])),e.mousePosition);if(d<closest){closest=d;picked=i;}}
                Record("连线生成双线道路");if(picked<0){picked=nodes.Length;Array.Resize(ref nodes,nodes.Length+1);nodes[picked]=road.transform.InverseTransformPoint(world);road.definition.nodes=nodes;}
                if(previous>=0&&previous!=picked){var connections=new List<RoadMapDefinition.Connection>(road.definition.connections??new RoadMapDefinition.Connection[0]);if(!connections.Exists(c=>(c.a==previous&&c.b==picked)||(c.a==picked&&c.b==previous)))connections.Add(new RoadMapDefinition.Connection(previous,picked));road.definition.connections=connections.ToArray();}
                previous=picked;road.definition.MarkDirty();author.Refresh();Dirty();e.Use();
            }
            else if(mode==Mode.点绘森林){Record("点绘森林");author.PaintForest(localPoint,forestRadius);lastPaint=localPoint;hasLast=true;Dirty();e.Use();}
            else if(mode==Mode.弯曲山地&&selectedMountain>=0&&selectedMountain<author.mountains.Count){Record("添加山脉控制点");author.mountains[selectedMountain].points.Add(localPoint);author.SyncMountains();Dirty();e.Use();}
        }
    }
    [CustomEditor(typeof(StaffMapAuthoring))]public sealed class StaffMapAuthoringInspector:UnityEditor.Editor
    {
        public override void OnInspectorGUI(){DrawDefaultInspector();if(GUILayout.Button("打开态势地图绘制工具"))StaffMapAuthoringWindow.Open();if(GUILayout.Button("同步山地和地图外观")){var a=(StaffMapAuthoring)target;a.Resolve();if(a.terrain!=null&&a.terrain.definition!=null){Undo.RecordObject(a.terrain.definition,"同步山地");a.SyncMountains();EditorUtility.SetDirty(a.terrain.definition);}}}
    }
}
