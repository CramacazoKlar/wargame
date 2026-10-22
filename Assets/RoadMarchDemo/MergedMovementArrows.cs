using System.Collections.Generic;
using UnityEngine;
namespace CampaignDemo {
 [DefaultExecutionOrder(600)] public sealed class MergedMovementArrows:MonoBehaviour {
  public BattleWorld world;
  [Header("实际路径箭头 · 本地显示")]
  public Color color=new Color(.08f,.35f,.70f,1);
  [Min(.01f)] public float squadRadius=.48f;
  [Min(.001f)] public float strokeWidth=.035f;
  [Min(0)] public float minimumStrokePixels=1.5f;
  [Min(.01f)] public float headLength=.24f;
  [Range(.2f,3)] public float taper=1.15f;
  [Range(16,96)] public int samples=40;
  public bool showDetectionCircles;
  public int MovingCount {get;private set;}public int GroupCount {get;private set;}public int MergedGroupCount {get;private set;}
  public IReadOnlyList<MovementArrowGeometry.Input> Inputs {get{return inputs;}}
  readonly List<MovementArrowGeometry.Input> inputs=new List<MovementArrowGeometry.Input>();
  public int GeometryBuildCount {get;private set;}
  readonly List<LineRenderer> pool=new List<LineRenderer>();int used;Material material;
  public static bool Owns(ArmyRouteMotor motor){var u=motor==null?null:motor.GetComponent<BattleUnit>();var world=u==null||u.Campaign==null?null:u.Campaign.world;if(world==null)return false;var view=world.GetComponent<MergedMovementArrows>();return view!=null&&view.isActiveAndEnabled;}
  void Awake(){if(world==null)world=GetComponent<BattleWorld>();}
  void LateUpdate(){Refresh();}
  public void Refresh(){used=0;inputs.Clear();MovingCount=GroupCount=MergedGroupCount=0;if(world==null||!world.Initialized||world.SimulationEnded){HideUnused();return;}
   foreach(var u in world.units){if(u==null||!u.OnMap||u.ownerId!=world.currentPlayer||!world.IsVisible(world.currentPlayer,u))continue;var motor=u.Motor;if(motor==null||!motor.IsMoving)continue;
    var remaining=motor.RemainingRoute();if(remaining.Count<2)continue;var route=new List<Vector2>();foreach(var point in remaining)route.Add(new Vector2((float)point.X,(float)point.Y));var start=route[0];var end=route[route.Count-1];
    inputs.Add(new MovementArrowGeometry.Input{id=u.unitId,owner=u.ownerId,orderRevision=motor.ArrowOrderRevision,start=start,direction=(end-start).normalized,route=route});
    var camera=world.setup.mapCamera;float pixel=camera==null?0:2*camera.orthographicSize/Mathf.Max(1,camera.pixelHeight);
    var shape=RoadRouteView.ArrowContour(remaining,Mathf.Max(.14f,pixel*6),Mathf.Max(.30f,pixel*12),Mathf.Max(headLength,pixel*10));
    var contour=new List<Vector2>();foreach(var point in shape)contour.Add(point);Draw(contour,StaffMapStyle.Faction(u.ownerId==BattleFactions.BluePlayerId),strokeWidth);
   }
   MovingCount=GroupCount=inputs.Count;GeometryBuildCount++;HideUnused();
  }
  void Draw(IList<Vector2> points,Color tint,float width){if(points.Count<2)return;if(material==null)material=new Material(Shader.Find("Sprites/Default"));LineRenderer line;if(used<pool.Count)line=pool[used];else{var g=new GameObject("实际路径箭头");g.transform.SetParent(transform,false);line=g.AddComponent<LineRenderer>();line.sharedMaterial=material;line.useWorldSpace=true;line.loop=false;line.numCornerVertices=0;line.numCapVertices=0;line.sortingOrder=23;pool.Add(line);}used++;line.enabled=true;var c=world.setup.mapCamera;float units=c==null?0:2*c.orthographicSize/Mathf.Max(1,c.pixelHeight);line.startWidth=line.endWidth=Mathf.Max(width,units*minimumStrokePixels);line.startColor=line.endColor=tint;line.positionCount=points.Count;for(int i=0;i<points.Count;i++)line.SetPosition(i,new Vector3(points[i].x,points[i].y,0));}
  void HideUnused(){for(int i=used;i<pool.Count;i++)if(pool[i]!=null)pool[i].enabled=false;}
  void OnDisable(){used=0;HideUnused();}
  void OnDestroy(){foreach(var line in pool)if(line!=null)Destroy(line.gameObject);if(material!=null)Destroy(material);}
 }
}
