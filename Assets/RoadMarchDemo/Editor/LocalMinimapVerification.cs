using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;
namespace CampaignDemo.Editor
{
    public static class LocalMinimapVerification
    {
        static int count;
        static void Check(bool ok,string message){if(!ok)throw new Exception(message);count++;}
        static BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
        public static void Run()
        {
            var root=new GameObject("Local minimap verification");var ui=new GameObject("Minimap UI");var camObj=new GameObject("Camera");var data=ScriptableObject.CreateInstance<FreeMarchMapDefinition>();
            try
            {
                var camera=camObj.AddComponent<Camera>();camera.orthographic=true;camera.aspect=2;camera.orthographicSize=5;camera.transform.position=new Vector3(0,0,-10);
                var setup=root.AddComponent<RoadMarchSceneSetup>();setup.mapCamera=camera;var map=root.AddComponent<FreeMarchMapRuntime>();map.definition=data;data.bounds=new Rect(-20,-10,40,20);setup.freeMarchMap=map;
                var world=root.AddComponent<BattleWorld>();world.setup=setup;world.units=new BattleUnit[0];world.points=new RecoveryPoint[0];
                var minimap=ui.AddComponent<TacticalMinimap>();minimap.world=world;
                typeof(TacticalMinimap).GetMethod("Start",Flags).Invoke(minimap,null);minimap.Render();
                var first=minimap.WorldToMap(Vector3.zero);Check(Vector2.Distance(first,new Vector2(.5f,.5f))<.001f,"camera centre in fixed frame centre");
                var firstEdge=minimap.WorldToMap(new Vector3(10,5));
                camera.transform.position=new Vector3(12,3,-10);minimap.Render();Check(Vector2.Distance(minimap.WorldToMap(new Vector3(12,3)),first)<.001f,"view follows camera pan");Check(Vector2.Distance(minimap.WorldToMap(new Vector3(22,8)),firstEdge)<.001f,"frame fixed when panning");
                camera.orthographicSize=2.5f;minimap.Render();Check(Vector2.Distance(minimap.WorldToMap(new Vector3(17,5.5f)),firstEdge)<.001f,"frame fixed when zooming");Check(Vector3.Distance(minimap.MapToWorld(first),new Vector3(12,3))<.001f,"click centre maps to current local viewport");
                var bounds=new Rect(-20,-10,40,20);Check(TacticalMinimap.GridLinesVisible(new Rect(-10,-5,20,10),bounds),"quarter coverage retains grid");Check(!TacticalMinimap.GridLinesVisible(new Rect(-20,-5,40,10),bounds),"exact half coverage hides grid");Check(!TacticalMinimap.GridLinesVisible(bounds,bounds),"whole map coverage hides grid");Check(TacticalMinimap.GridLinesVisible(new Rect(10,0,40,20),bounds),"outside map does not count toward coverage");Check(minimap.showControlGrid,"grid threshold preserves control colour switch");
                var buttons=(System.Collections.Generic.List<UnityEngine.UI.Button>)typeof(TacticalMinimap).GetField("layerButtons",Flags).GetValue(minimap);Check(buttons[0].GetComponentInChildren<UnityEngine.UI.Text>().text=="全部","Chinese labels preserved");
                var territory=root.AddComponent<TerritoryControlSystem>();territory.world=world;territory.usePixelCellSize=false;territory.cellSize=2;territory.Initialize();world.territory=territory;world.currentPlayer="A";
                var owners=new string[territory.CellCount];for(int i=0;i<owners.Length;i++)owners[i]="A";territory.ApplyNetworkOwners(owners);
                camera.transform.position=new Vector3(0,0,-10);camera.orthographicSize=10;minimap.Render();
                var texture=(Texture2D)typeof(TacticalMinimap).GetField("texture",Flags).GetValue(minimap);var tinted=texture.GetPixel(96,96);
                minimap.showControlGrid=false;minimap.Render();var plain=texture.GetPixel(96,96);Check(tinted.b>plain.b+.05f,"occupancy blue remains at whole-map zoom");
                minimap.showControlGrid=true;minimap.Render();int wideLines=0;foreach(var colour in texture.GetPixels())if(Mathf.Abs(colour.r-minimap.gridLineColor.r)<.005f&&Mathf.Abs(colour.g-minimap.gridLineColor.g)<.005f&&Mathf.Abs(colour.b-minimap.gridLineColor.b)<.005f)wideLines++;Check(wideLines==0,"wide view has no grid-line pixels");
                camera.orthographicSize=5;minimap.Render();int closeLines=0;foreach(var colour in texture.GetPixels())if(Mathf.Abs(colour.r-minimap.gridLineColor.r)<.005f&&Mathf.Abs(colour.g-minimap.gridLineColor.g)<.005f&&Mathf.Abs(colour.b-minimap.gridLineColor.b)<.005f)closeLines++;Check(closeLines==0,"grid stays hidden when zooming in");
                Debug.Log("LOCAL_MINIMAP_VERIFY_PASS "+count);EditorApplication.Exit(0);
            }
            catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
            finally{UnityEngine.Object.DestroyImmediate(ui);UnityEngine.Object.DestroyImmediate(root);UnityEngine.Object.DestroyImmediate(camObj);UnityEngine.Object.DestroyImmediate(data);}
        }
    }
}

