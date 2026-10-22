using UnityEngine;
using UnityEngine.EventSystems;
namespace CampaignDemo {
 [DefaultExecutionOrder(-100),DisallowMultipleComponent]
 public sealed class TabletopCamera:MonoBehaviour {
  public Camera view;public Vector2 focus;public float tilt=32,yaw;public float moveSpeed=12;public float rotationSpeed=.22f;
  Vector3 previousMouse;Rect bounds;MapZoomController zoom;
  void Awake(){if(view==null)view=GetComponent<RoadMarchSceneSetup>().mapCamera;zoom=GetComponent<MapZoomController>();var pan=GetComponent<MapPanController>();if(pan!=null){pan.RefreshMapBounds();bounds=pan.cameraCenterBounds;pan.enabled=false;}var opening=GetComponent<OpeningCameraFocus>();if(opening!=null)opening.enabled=false;view.orthographic=false;view.fieldOfView=42;view.nearClipPlane=.1f;view.farClipPlane=1500;view.transparencySortMode=TransparencySortMode.CustomAxis;view.transparencySortAxis=Vector3.forward;Apply();}
  public void Pan(Vector2 direction,float dt){var right=Quaternion.Euler(0,0,yaw)*Vector3.right;var up=Quaternion.Euler(0,0,yaw)*Vector3.up;var delta=(right*direction.x+up*direction.y);delta=Vector3.ClampMagnitude(delta,1)*moveSpeed*Mathf.Max(.3f,view.orthographicSize/12)*dt;focus+=(Vector2)delta;if(bounds.width>0){focus.x=Mathf.Clamp(focus.x,bounds.xMin-12,bounds.xMax+12);focus.y=Mathf.Clamp(focus.y,bounds.yMin-12,bounds.yMax+12);}Apply();}
  public void Orbit(Vector2 delta){yaw-=delta.x*rotationSpeed;tilt=Mathf.Clamp(tilt+delta.y*rotationSpeed,0,90);Apply();}
  public void Focus(Vector2 at){focus=at;Apply();}
  public void Apply(){if(view==null)return;tilt=Mathf.Clamp(tilt,0,90);var rotation=Quaternion.Euler(0,0,yaw)*Quaternion.Euler(-tilt,0,0);float distance=view.orthographicSize/Mathf.Tan(view.fieldOfView*Mathf.Deg2Rad*.5f);var position=new Vector3(focus.x,focus.y,0)-rotation*Vector3.forward*distance;position.z=Mathf.Min(position.z,-.25f);view.transform.SetPositionAndRotation(position,rotation);}
  void Update(){bool typing=EventSystem.current!=null&&EventSystem.current.currentSelectedGameObject!=null&&EventSystem.current.currentSelectedGameObject.GetComponent<UnityEngine.UI.InputField>()!=null;if(!typing)Pan(new Vector2((Input.GetKey(KeyCode.D)?1:0)-(Input.GetKey(KeyCode.A)?1:0),(Input.GetKey(KeyCode.W)?1:0)-(Input.GetKey(KeyCode.S)?1:0)),Time.unscaledDeltaTime);if(Input.GetMouseButtonDown(2))previousMouse=Input.mousePosition;if(Input.GetMouseButton(2)){Orbit((Vector2)(Input.mousePosition-previousMouse));previousMouse=Input.mousePosition;}}
  void LateUpdate(){Apply();}
 }
}
