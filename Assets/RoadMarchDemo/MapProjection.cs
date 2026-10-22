using UnityEngine;
namespace CampaignDemo {
 public static class MapProjection {
  public static Vector3 Ground(Camera camera,Vector2 screen){if(camera==null)return Vector3.zero;var ray=camera.ScreenPointToRay(screen);var plane=new Plane(Vector3.forward,Vector3.zero);Vector3 point;if(plane.Raycast(ray,out var distance)&&distance<=camera.farClipPlane)point=ray.GetPoint(distance);else point=ray.GetPoint(camera.farClipPlane);point.z=0;return point;}
  public static Vector3 ViewportGround(Camera camera,Vector2 uv){return Ground(camera,new Vector2(camera.pixelRect.x+uv.x*camera.pixelWidth,camera.pixelRect.y+uv.y*camera.pixelHeight));}
  public static float PixelSize(Camera camera,Vector3 at){if(camera==null)return .01f;if(camera.orthographic)return 2*camera.orthographicSize/Mathf.Max(1,camera.pixelHeight);float depth=Vector3.Dot(at-camera.transform.position,camera.transform.forward);return 2*Mathf.Max(.01f,depth)*Mathf.Tan(camera.fieldOfView*Mathf.Deg2Rad*.5f)/Mathf.Max(1,camera.pixelHeight);}
  public static Rect View(Camera camera){var a=ViewportGround(camera,Vector2.zero);var b=ViewportGround(camera,Vector2.right);var c=ViewportGround(camera,Vector2.one);var d=ViewportGround(camera,Vector2.up);return Rect.MinMaxRect(Mathf.Min(a.x,b.x,c.x,d.x),Mathf.Min(a.y,b.y,c.y,d.y),Mathf.Max(a.x,b.x,c.x,d.x),Mathf.Max(a.y,b.y,c.y,d.y));}
 }
}
