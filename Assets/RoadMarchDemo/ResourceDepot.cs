using UnityEngine;
using UnityEngine.Serialization;
using System.Collections.Generic;
namespace CampaignDemo {
 public sealed class ResourceDepot:MonoBehaviour {
  public string depotId,displayName="修复点";
  [FormerlySerializedAs("ownerId"),SerializeField]string storedOwner;
  [FormerlySerializedAs("stock"),SerializeField]float storedStock;
  public RecoveryPoint recovery;
  public string ownerId {get{return recovery==null?storedOwner:recovery.ownerId;}set{storedOwner=value;if(recovery!=null)recovery.ownerId=value;RefreshAppearance();}}
  public float stock {get{return recovery==null?storedStock:recovery.resourceStock;}set{storedStock=Mathf.Max(0,value);if(recovery!=null)recovery.resourceStock=storedStock;}}
  public bool resourcePile;public float captureProgress;public string capturingPlayer;public bool Removed {get;private set;}
  BattleWorld viewWorld;
  public void RefreshAppearance(){var renderer=GetComponent<SpriteRenderer>();if(renderer==null||resourcePile)return;renderer.sprite=StaffMapSymbols.Depot(ownerId);renderer.color=Color.white;if(viewWorld==null)viewWorld=FindObjectOfType<BattleWorld>();var camera=viewWorld==null||viewWorld.setup==null?null:viewWorld.setup.mapCamera;if(camera!=null){float size=Mathf.Clamp(camera.pixelHeight/45f,16,40)*MapProjection.PixelSize(camera,transform.position);var scale=BattleUnitView.SymbolScale(transform);renderer.drawMode=SpriteDrawMode.Sliced;renderer.size=new Vector2(size/Mathf.Max(.0001f,scale.x),size/Mathf.Max(.0001f,scale.y));}var labels=viewWorld==null?null:viewWorld.GetComponent<TabletopPresentation>();if(labels!=null)labels.RegisterPlace(transform,displayName,new Vector3(0,-.45f,-.03f));}
  public void EnsureRecovery(CampaignRuntime campaign){if(resourcePile||Removed||campaign==null||!LocalPrototypeOrders.EnabledFor(this))return;if(recovery==null){recovery=GetComponent<RecoveryPoint>();if(recovery==null)recovery=gameObject.AddComponent<RecoveryPoint>();recovery.pointId="repair:"+depotId;recovery.ownerId=storedOwner;recovery.resourceStock=storedStock;recovery.isRepairSite=true;recovery.displayName="修复点";recovery.kind=FacilityKind.RecoveryPoint;recovery.capacity=6;recovery.transitionSeconds=0;recovery.resourcePerPerson=100;recovery.minimapMajorCity=false;recovery.captureRadius=campaign.rules.warehouseBuildRadius;displayName="修复点";gameObject.name=displayName;}var points=new List<RecoveryPoint>(campaign.world.points);if(!points.Contains(recovery)){points.Add(recovery);campaign.world.points=points.ToArray();}}
  public void Remove(){var world=FindObjectOfType<BattleWorld>();if(recovery!=null&&world!=null){foreach(var u in world.units)if(u!=null&&u.Point==recovery){FacilityRecoveryQueue.For(world)?.Cancel(u);u.BeginExit();}var points=new List<RecoveryPoint>(world.points);points.Remove(recovery);world.points=points.ToArray();}Removed=true;gameObject.SetActive(false);}
 }
}
