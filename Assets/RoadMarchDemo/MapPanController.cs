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
        public bool keepViewportInsideMap;
        public void ConstrainView()
        {
            if(!useBounds||!keepViewportInsideMap||mapCamera==null||!mapCamera.orthographic)return;
            RefreshMapBounds();var b=cameraCenterBounds;float aspect=Mathf.Max(.01f,mapCamera.aspect);
            float limit=Mathf.Max(.01f,Mathf.Min(b.height*.5f,b.width*.5f/aspect));mapCamera.orthographicSize=Mathf.Min(mapCamera.orthographicSize,limit);
            float h=mapCamera.orthographicSize,w=h*aspect;var p=mapCamera.transform.position;
            p.x=Mathf.Clamp(p.x,b.xMin+w,b.xMax-w);p.y=Mathf.Clamp(p.y,b.yMin+h,b.yMax-h);mapCamera.transform.position=p;
        }
        void LateUpdate(){ConstrainView();}



        RoadMarchSceneSetup linkedSetup;
        public void RefreshMapBounds()
        {
            if(!useBounds)return;
            if(linkedSetup==null)linkedSetup=GetComponent<RoadMarchSceneSetup>();
            Rect current;
            if(linkedSetup!=null&&MapViewBounds.TryGet(linkedSetup.roadMap,linkedSetup.freeMarchMap,out current))cameraCenterBounds=current;
        }

        void Update()
        {
            RefreshMapBounds();
            if(mapCamera==null||!mapCamera.orthographic) { return; }
            if(blockWhileDrawing&&Input.GetMouseButton(0)) { return; }

            float x=(Input.GetKey(KeyCode.D)?1:0)-(Input.GetKey(KeyCode.A)?1:0);
            float y=(Input.GetKey(KeyCode.W)?1:0)-(Input.GetKey(KeyCode.S)?1:0);
            MoveKeyboard(new Vector2(x,y),Time.unscaledDeltaTime);
        }
        public void MoveKeyboard(Vector2 axis,float dt)
        {
            RefreshMapBounds();
            if(mapCamera==null)return;Vector3 displacement=Vector3.ClampMagnitude(new Vector3(axis.x,axis.y,0),1)*Mathf.Max(0,keyboardSpeed)*Mathf.Max(0,dt);
            var at=mapCamera.transform.position+displacement;
            if(useBounds)
            { at.x=Mathf.Clamp(at.x,cameraCenterBounds.xMin,cameraCenterBounds.xMax); at.y=Mathf.Clamp(at.y,cameraCenterBounds.yMin,cameraCenterBounds.yMax); }
            mapCamera.transform.position=at;
            ConstrainView();
        }
        public void SetMouseSensitivity(float value) { if(!float.IsNaN(value)&&!float.IsInfinity(value)) mouseSensitivity=Mathf.Max(0,value); }
        public void SetKeyboardSpeed(float value) { if(!float.IsNaN(value)&&!float.IsInfinity(value)) keyboardSpeed=Mathf.Max(0,value); }


    }
}
