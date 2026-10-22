using System.Collections.Generic;
using UnityEngine;
namespace CampaignDemo {
 [DefaultExecutionOrder(280)] public sealed class CampaignRangeDisplay:MonoBehaviour {
  public BattleWorld world;public int GeometryRebuilds {get;private set;}
  sealed class Circle {public LineRenderer line;public Vector3[] points=new Vector3[128];public Vector3 center;public float radius=-1,width=-1;}
  sealed class Entry {public ArtilleryBattery battery;public Circle[] circles=new Circle[2];}
  readonly Dictionary<BattleUnit,Entry> entries=new Dictionary<BattleUnit,Entry>();Material material;
  static readonly Vector3[] unitCircle=MakeCircle();
  static Vector3[] MakeCircle(){var p=new Vector3[128];for(int i=0;i<p.Length;i++){float angle=i*Mathf.PI*2/p.Length;p[i]=new Vector3(Mathf.Cos(angle),Mathf.Sin(angle));}return p;}
  void LateUpdate(){if(world==null||!world.Initialized||world.setup==null||world.setup.modes==null)return;if(world.SimulationEnded){Hide();return;}
   bool inspect=world.setup.modes.CurrentMode==MouseCommandMode.Interaction;var selection=world.GetComponent<ArmySelection>();var selected=selection!=null&&selection.Selected!=null?selection.Selected.GetComponent<BattleUnit>():null;var camera=world.setup.mapCamera;float width=camera!=null&&camera.orthographic?2f*camera.orthographicSize/Mathf.Max(1,camera.pixelHeight)*2:.025f;
   foreach(var unit in world.units){if(unit==null)continue;bool show=(inspect||((world.setup.modes.CurrentMode==MouseCommandMode.RoadMarch||world.setup.modes.CurrentMode==MouseCommandMode.FreeMarch)&&unit==selected))&&unit.ownerId==world.currentPlayer&&unit.OnMap&&unit.combatDefinition!=null&&world.IsVisible(world.currentPlayer,unit);
    if(!entries.TryGetValue(unit,out var entry)){if(!show)continue;if(material==null)material=new Material(Shader.Find("Sprites/Default"));entry=new Entry{battery=unit.GetComponent<ArtilleryBattery>()};for(int i=0;i<2;i++){var g=new GameObject(i==0?"Campaign vision range":"Campaign attack range");g.transform.SetParent(transform,false);var line=g.AddComponent<LineRenderer>();line.useWorldSpace=true;line.loop=true;line.sharedMaterial=material;line.sortingOrder=24;line.positionCount=128;line.startColor=line.endColor=i==0?StaffMapStyle.Blue:StaffMapStyle.Red;entry.circles[i]=new Circle{line=line};}entries.Add(unit,entry);}
    var center=unit.transform.position;center.z=0;for(int i=0;i<2;i++){var circle=entry.circles[i];var line=circle.line;if(line==null)continue;bool visible=show&&(i==0||(inspect||unit==selected)&&(unit.combatDefinition.fullStrengthAttack>0||entry.battery!=null));if(line.enabled!=visible)line.enabled=visible;if(!visible)continue;
     float radius=i==0?unit.combatDefinition.visionRadius:UnitRanges.Attack(unit);
     if(circle.width!=width){circle.width=width;line.startWidth=line.endWidth=width;}
     if(circle.radius==radius&&circle.center==center)continue;circle.radius=radius;circle.center=center;for(int n=0;n<128;n++)circle.points[n]=center+unitCircle[n]*radius;line.SetPositions(circle.points);GeometryRebuilds++;
    }
   }
   foreach(var pair in entries)if(pair.Key==null)foreach(var circle in pair.Value.circles)if(circle.line!=null)circle.line.enabled=false;
  }
  void Hide(){foreach(var entry in entries.Values)foreach(var circle in entry.circles)if(circle.line!=null&&circle.line.enabled)circle.line.enabled=false;}
  void OnDisable(){Hide();}
  GUIStyle rangeLabel;
  void OnGUI(){if(world==null||!world.Initialized||world.SimulationEnded)return;var selection=world.GetComponent<ArmySelection>();var unit=selection!=null&&selection.Selected!=null?selection.Selected.GetComponent<BattleUnit>():null;if(unit==null||unit.ownerId!=world.currentPlayer||!unit.OnMap||unit.GetComponent<ArtilleryBattery>()==null)return;var camera=world.setup.mapCamera;if(camera==null)return;if(rangeLabel==null){rangeLabel=new GUIStyle(GUI.skin.label){font=Font.CreateDynamicFontFromOSFont(new[]{"Microsoft YaHei","Arial"},12),fontSize=12,alignment=TextAnchor.MiddleCenter};}for(int i=0;i<2;i++){float radius=i==0?unit.combatDefinition.visionRadius:UnitRanges.Attack(unit);var p=camera.WorldToScreenPoint(unit.transform.position+Vector3.up*radius);if(p.z<=0||p.y<0||p.y>Screen.height)continue;rangeLabel.normal.textColor=i==0?StaffMapStyle.Blue:StaffMapStyle.Red;GUI.Label(new Rect(p.x-100,Screen.height-p.y-24,200,22),(i==0?"视野 ":"炮击射程 ")+radius.ToString("0.##")+" km",rangeLabel);}}
  void OnDestroy(){foreach(var entry in entries.Values)foreach(var circle in entry.circles)if(circle.line!=null)Destroy(circle.line.gameObject);if(material!=null)Destroy(material);}
 }
}


