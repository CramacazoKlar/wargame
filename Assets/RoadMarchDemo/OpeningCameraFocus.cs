using UnityEngine;
namespace CampaignDemo {
 [DefaultExecutionOrder(200),DisallowMultipleComponent]
 public sealed class OpeningCameraFocus:MonoBehaviour {
  bool finished;
  void LateUpdate(){if(finished||LocalTacticalBattle.EnabledFor(this))return;var world=GetComponent<BattleWorld>();if(world==null||!world.Initialized||world.setup==null||world.setup.mapCamera==null)return;var session=LocalPvpSession.Active;if(session!=null&&world.currentPlayer!=session.LocalPlayer)return;Focus(world.setup.mapCamera,world.points,world.currentPlayer);finished=true;}
  public static bool Center(RecoveryPoint[] points,string owner,out Vector2 center){center=Vector2.zero;int count=0;if(points!=null)foreach(var city in points)if(city!=null&&city.kind==FacilityKind.RecoveryPoint&&city.ownerId==owner){center+=(Vector2)city.transform.position;count++;}if(count==0)return false;center/=count;return true;}
  public static bool Focus(Camera camera,RecoveryPoint[] points,string owner){if(camera==null||!Center(points,owner,out var center))return false;var position=camera.transform.position;camera.transform.position=new Vector3(center.x,center.y,position.z);return true;}
 }
}
