using UnityEngine;
using UnityEngine.EventSystems;

namespace CampaignDemo
{
    [DefaultExecutionOrder(-80), DisallowMultipleComponent]
    public sealed class MapPanController : MonoBehaviour
    {
        public Camera mapCamera;
        [Min(0)] public float keyboardSpeed=8;
        [Min(0)] public float mouseSensitivity=1;
        public bool blockWhileDrawing=true;
        public bool useBounds;
        public Rect cameraCenterBounds=new Rect(-12,-8,24,16);
        bool dragging;
        Vector3 previousMouse;

        void Update()
        {
            if(mapCamera==null||!mapCamera.orthographic) { dragging=false; return; }
            if(blockWhileDrawing&&Input.GetMouseButton(0)) { dragging=false; return; }
            bool overUI=EventSystem.current!=null&&EventSystem.current.IsPointerOverGameObject();
            if(Input.GetMouseButtonDown(1))
            { dragging=!overUI; previousMouse=Input.mousePosition; }
            if(Input.GetMouseButtonUp(1)||!Input.GetMouseButton(1)) dragging=false;
            Vector3 displacement=Vector3.zero;
            if(dragging)
            {
                var delta=Input.mousePosition-previousMouse; previousMouse=Input.mousePosition;
                float unitsPerPixel=2*mapCamera.orthographicSize/Mathf.Max(1,mapCamera.pixelHeight);
                displacement-=new Vector3(delta.x,delta.y,0)*unitsPerPixel*Mathf.Max(0,mouseSensitivity);
            }
            float x=(Input.GetKey(KeyCode.D)?1:0)-(Input.GetKey(KeyCode.A)?1:0);
            float y=(Input.GetKey(KeyCode.W)?1:0)-(Input.GetKey(KeyCode.S)?1:0);
            displacement+=Vector3.ClampMagnitude(new Vector3(x,y,0),1)*Mathf.Max(0,keyboardSpeed)*Time.unscaledDeltaTime;
            var at=mapCamera.transform.position+displacement;
            if(useBounds)
            { at.x=Mathf.Clamp(at.x,cameraCenterBounds.xMin,cameraCenterBounds.xMax); at.y=Mathf.Clamp(at.y,cameraCenterBounds.yMin,cameraCenterBounds.yMax); }
            mapCamera.transform.position=at;
        }
        public void SetMouseSensitivity(float value) { if(!float.IsNaN(value)&&!float.IsInfinity(value)) mouseSensitivity=Mathf.Max(0,value); }
        public void SetKeyboardSpeed(float value) { if(!float.IsNaN(value)&&!float.IsInfinity(value)) keyboardSpeed=Mathf.Max(0,value); }
        void OnDisable() { dragging=false; }
        void OnApplicationFocus(bool focused) { if(!focused) dragging=false; }
    }
}
