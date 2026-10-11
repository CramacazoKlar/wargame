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



        void Update()
        {
            if(mapCamera==null||!mapCamera.orthographic) { return; }
            if(blockWhileDrawing&&Input.GetMouseButton(0)) { return; }

            float x=(Input.GetKey(KeyCode.D)?1:0)-(Input.GetKey(KeyCode.A)?1:0);
            float y=(Input.GetKey(KeyCode.W)?1:0)-(Input.GetKey(KeyCode.S)?1:0);
            MoveKeyboard(new Vector2(x,y),Time.unscaledDeltaTime);
        }
        public void MoveKeyboard(Vector2 axis,float dt)
        {
            if(mapCamera==null)return;Vector3 displacement=Vector3.ClampMagnitude(new Vector3(axis.x,axis.y,0),1)*Mathf.Max(0,keyboardSpeed)*Mathf.Max(0,dt);
            var at=mapCamera.transform.position+displacement;
            if(useBounds)
            { at.x=Mathf.Clamp(at.x,cameraCenterBounds.xMin,cameraCenterBounds.xMax); at.y=Mathf.Clamp(at.y,cameraCenterBounds.yMin,cameraCenterBounds.yMax); }
            mapCamera.transform.position=at;
        }
        public void SetMouseSensitivity(float value) { if(!float.IsNaN(value)&&!float.IsInfinity(value)) mouseSensitivity=Mathf.Max(0,value); }
        public void SetKeyboardSpeed(float value) { if(!float.IsNaN(value)&&!float.IsInfinity(value)) keyboardSpeed=Mathf.Max(0,value); }


    }
}
