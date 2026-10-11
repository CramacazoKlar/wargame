using System;
using System.Collections.Generic;
using UnityEngine;
namespace CampaignDemo {
 [Serializable] public sealed class ReplayVisionTrail {public float radius;public List<Vector3> points=new List<Vector3>();}
 [Serializable] public sealed class ReplayArmy {
  public string id,owner,label;public UnitRole role;public ArmyEchelon echelon;
  public bool hasInitialPosition;public Vector3 initialPosition;
  public bool hasSummaryStart;public Vector3 summaryStart;public List<ReplayTimedPoint> timeline=new List<ReplayTimedPoint>();
  public bool dead;public Vector3 finalPosition,displayPosition;public float deathTime=-1;
  public List<List<Vector3>> routes=new List<List<Vector3>>();public List<List<Vector3>> fullRoutes=new List<List<Vector3>>();
  public List<List<ReplayTimedPoint>> movementSegments=new List<List<ReplayTimedPoint>>();public List<List<Vector3>> tacticalRoutes=new List<List<Vector3>>();
  public List<ReplayVisionTrail> observation=new List<ReplayVisionTrail>();
  [NonSerialized] public BattleUnit unit;[NonSerialized] public Sprite symbol;
  [NonSerialized] public bool travelling,postTravelling,observing;
 }
 [Serializable] public sealed class ReplayDefense {public string owner;public bool destroyed;public Vector3[] points;[NonSerialized] public Fortification fort;}
 [Serializable] public sealed class ReplaySnapshot {public int version=1;public float observationCutoff=-1;public float elapsed,firstContact=-1,metersPerWorldUnit=1000;public Rect bounds;public string blueOwner;public List<ReplayArmy> armies=new List<ReplayArmy>();public List<ReplayDefense> defenses=new List<ReplayDefense>();}
 [DefaultExecutionOrder(110)]
 public sealed class BattleReplayRecorder:MonoBehaviour {
  public BattleWorld world;public ReplaySnapshot Snapshot {get;private set;}=new ReplaySnapshot();
  public bool Finished {get;private set;}public string LastExport {get;internal set;}="";
  public string exportDirectory;readonly Dictionary<string,ReplayArmy> records=new Dictionary<string,ReplayArmy>();readonly Dictionary<int,ReplayDefense> defenses=new Dictionary<int,ReplayDefense>();
  public int Samples {get;private set;}public int ObservationPointCount {get {int n=0;foreach(var a in Snapshot.armies)foreach(var t in a.observation)n+=t.points.Count;return n;}}
  void OnDestroy(){if(world!=null)world.CombatEffectOccurred-=OnCombat;}
  public void Initialize(BattleWorld target){world=target;Snapshot.blueOwner=world.players[0];Snapshot.bounds=MapBounds();var reference=FindObjectOfType<MapReferenceHUD>();if(reference!=null)Snapshot.metersPerWorldUnit=reference.metersPerWorldUnit;world.CombatEffectOccurred+=OnCombat;Sample();}
  Rect MapBounds(){var map=world.setup.freeMarchMap;if(map!=null&&map.definition!=null){var b=map.definition.bounds;var min=map.transform.TransformPoint(new Vector3(b.xMin,b.yMin));var max=map.transform.TransformPoint(new Vector3(b.xMax,b.yMax));return Rect.MinMaxRect(Mathf.Min(min.x,max.x),Mathf.Min(min.y,max.y),Mathf.Max(min.x,max.x),Mathf.Max(min.y,max.y));}return new Rect(-25,-25,50,50);}
  public void Advance(float dt){if(Finished)return;Snapshot.elapsed+=Mathf.Max(0,dt);Sample();}
  public void Sample(){if(Finished||world==null||!world.Initialized)return;foreach(var unit in world.units)if(unit!=null)SampleUnit(unit);SampleDefenses();Samples++;}
  public void SampleUnit(BattleUnit unit){if(Finished||unit==null)return;
   if(!records.TryGetValue(unit.unitId,out var army)){
    army=new ReplayArmy{id=unit.unitId,owner=unit.ownerId,label=unit.DisplayDesignation,role=unit.combatDefinition.role,echelon=unit.combatDefinition.echelon,unit=unit};
    var identity=unit.GetComponent<UnitSymbolIdentity>();var renderer=unit.GetComponent<SpriteRenderer>();army.symbol=identity==null||identity.catalog==null?(renderer==null?null:renderer.sprite):identity.catalog.Unit(army.role,army.echelon,army.owner==Snapshot.blueOwner);
    records.Add(army.id,army);Snapshot.armies.Add(army);
   }
   if(army.dead)return;
   var p=unit.Carrier!=null?unit.Carrier.Unit.transform.position:unit.Point!=null&&!unit.OnMap?unit.Point.transform.position:unit.transform.position;p.z=0;if(!army.hasInitialPosition){army.initialPosition=p;army.hasInitialPosition=true;}army.finalPosition=p;QuarterReplay.Capture(army,Snapshot.elapsed,p,unit.combatDefinition.visionRadius,unit.OnMap);
   bool travel=unit.Alive&&(unit.OnMap||unit.Carrier!=null);
   if(travel){if(!army.travelling||army.movementSegments.Count==0)army.movementSegments.Add(new List<ReplayTimedPoint>());var segment=army.movementSegments[army.movementSegments.Count-1];var sample=new ReplayTimedPoint{time=Snapshot.elapsed,position=p};if(segment.Count>0&&segment[segment.Count-1].time==sample.time)segment[segment.Count-1]=sample;else segment.Add(sample);if(!army.travelling||army.fullRoutes.Count==0)army.fullRoutes.Add(new List<Vector3>());Append(army.fullRoutes[army.fullRoutes.Count-1],p);}army.travelling=travel;bool post=travel&&Snapshot.firstContact>=0;if(post){if(!army.postTravelling||army.routes.Count==0)army.routes.Add(new List<Vector3>());Append(army.routes[army.routes.Count-1],p);}army.postTravelling=post;

  }
  // Keep real bends; replace only a middle point within two metres of the same forward segment.
  static void Append(List<Vector3> points,Vector3 point){int n=points.Count;if(n==0){points.Add(point);return;}if((points[n-1]-point).sqrMagnitude<.00000001f)return;
   if(n>=2){var a=points[n-2];var b=points[n-1];var d=point-a;float t=d.sqrMagnitude>0?Vector3.Dot(b-a,d)/d.sqrMagnitude:0;if(t>=0&&t<=1&&(a+d*t-b).sqrMagnitude<=.000004f&&Vector3.Dot((b-a).normalized,(point-b).normalized)>.9995f){points[n-1]=point;return;}}
   points.Add(point);
  }
  void OnCombat(CombatEffectEvent effect){if(effect.Stage==CombatEffectStage.DirectAttack&&effect.SourceOwner!=effect.TargetOwner)BeginContact();}
  public void BeginContact(){if(Finished||Snapshot.firstContact>=0)return;Sample();Snapshot.firstContact=Snapshot.elapsed;Sample();}
  public void RecordDeath(BattleUnit unit){if(Finished)return;if(records.TryGetValue(unit.unitId,out var existing)&&existing.dead)return;SampleUnit(unit);if(records.TryGetValue(unit.unitId,out var a)){QuarterReplay.Capture(a,Snapshot.elapsed,a.finalPosition,unit.combatDefinition.visionRadius,unit.OnMap,true);a.dead=true;a.deathTime=Snapshot.elapsed;a.travelling=false;a.observing=false;}}
  void SampleDefenses(){var combined=world.GetComponent<CombinedArmsWorld>();if(combined==null)return;foreach(var fort in combined.fortifications)if(fort!=null&&!defenses.ContainsKey(fort.GetInstanceID())){var record=new ReplayDefense{owner=fort.ownerId,destroyed=fort.Destroyed,fort=fort,points=fort.IsLine?(Vector3[])fort.linePoints.Clone():new[]{fort.transform.position+Vector3.left*fort.radius,fort.transform.position+Vector3.right*fort.radius}};defenses.Add(fort.GetInstanceID(),record);Snapshot.defenses.Add(record);}foreach(var record in Snapshot.defenses)if(record.fort==null||record.fort.Destroyed)record.destroyed=true;}
  public void Finish(){if(Finished)return;Sample();foreach(var army in Snapshot.armies)if(!army.dead)QuarterReplay.Capture(army,Snapshot.elapsed,army.finalPosition,army.unit.combatDefinition.visionRadius,army.unit.OnMap,true);QuarterReplay.Build(Snapshot);foreach(var army in Snapshot.armies)QuarterReplay.BuildTactical(army,0f);ReplayTacticalCleanup.Build(Snapshot,Mathf.Max(.03f,world.setup.settings.arrowShaftWidth));ReplayTacticalCleanup.Polish(Snapshot,Mathf.Max(.03f,world.setup.settings.arrowShaftWidth));Finished=true;var display=gameObject.AddComponent<BattleReplayPresentation>();display.recorder=this;display.Build();}
 }
}
