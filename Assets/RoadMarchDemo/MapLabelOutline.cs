using UnityEngine;
namespace CampaignDemo {
 [DefaultExecutionOrder(1150)]
 public sealed class MapLabelOutline:MonoBehaviour {
  StaffVisibilityShade shade;MeshRenderer[] outlines;MeshRenderer body;
  public bool OutlineVisible {get;private set;}
  public void Configure(StaffVisibilityShade visibility){shade=visibility;var all=GetComponentsInChildren<MeshRenderer>();var list=new System.Collections.Generic.List<MeshRenderer>();foreach(var renderer in all){if(renderer.name.StartsWith("White outline"))list.Add(renderer);else if(renderer.name=="Black place name")body=renderer;}outlines=list.ToArray();Refresh();}
  public void Refresh(){if(outlines==null)return;if(shade==null)shade=GetComponentInParent<StaffVisibilityShade>();Vector2 at=body!=null?(Vector2)body.bounds.center:(Vector2)transform.position;OutlineVisible=shade==null||!shade.IsShadowedAt(at);foreach(var renderer in outlines)if(renderer!=null)renderer.enabled=OutlineVisible;}
  void LateUpdate(){Refresh();}
 }
}
