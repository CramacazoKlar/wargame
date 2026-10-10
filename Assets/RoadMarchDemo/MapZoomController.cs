using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
namespace CampaignDemo
{
    [DisallowMultipleComponent]
    public sealed class MapZoomController : MonoBehaviour
    {
        [Header("Assign your own objects")]
        public Camera mapCamera;
        public Button resetNode;
        public Slider zoomSlider;
        [Header("Camera size limits - smaller means closer")]
        [Min(0.01f)] public float minimumSize=2;
        [Min(0.01f)] public float maximumSize=14;
        [Min(0)] public float wheelStep=0.5f;
        [Range(.5f,6)] public float wheelSensitivity=2;
        public bool startAtMiddle=true;
        [Header("Input")]
        public bool blockWheelOverOtherUI=true;
        public bool blockWheelWhileDrawing=true;
        public float CurrentSize { get { return mapCamera==null?0:mapCamera.orthographicSize; } }
        public float NormalizedZoom { get { return Range.SliderFromSize(CurrentSize); } }
        MapZoomRange Range { get { return new MapZoomRange(minimumSize,maximumSize); } }
        Slider boundSlider;
        Button boundButton;
        Canvas sliderCanvas;
        Transform sliderParent;
        bool started;

        void OnEnable()
        {
            BindUI();
            if(started) { ResolveCamera(); SetSize(CurrentSize); }
        }
        void Start()
        {
            started=true; ResolveCamera();
            if(mapCamera==null||!mapCamera.orthographic)
            { Debug.LogError("MapZoomController requires the orthographic map camera.",this); enabled=false; return; }
            SetSize(startAtMiddle?Range.MiddleSize:CurrentSize);
        }
        void ResolveCamera()
        {
            if(mapCamera!=null) return;
            var setup=GetComponent<RoadMarchSceneSetup>();
            if(setup!=null) mapCamera=setup.mapCamera;
            if(mapCamera==null) mapCamera=GetComponent<Camera>();
            if(mapCamera==null) mapCamera=Camera.main;
        }
        void BindUI()
        {
            UnbindUI(); boundSlider=zoomSlider; boundButton=resetNode;
            if(boundSlider!=null)
            {
                boundSlider.wholeNumbers=false; boundSlider.minValue=0; boundSlider.maxValue=1;
                boundSlider.onValueChanged.AddListener(SetNormalizedZoom);
                sliderCanvas=boundSlider.GetComponentInParent<Canvas>(); sliderParent=boundSlider.transform.parent;
            }
            if(boundButton!=null) boundButton.onClick.AddListener(ResetToMiddle);
        }
        void UnbindUI()
        {
            if(boundSlider!=null) boundSlider.onValueChanged.RemoveListener(SetNormalizedZoom);
            if(boundButton!=null) boundButton.onClick.RemoveListener(ResetToMiddle);
            boundSlider=null; boundButton=null;
            sliderCanvas=null; sliderParent=null;
        }
        void OnDisable() { UnbindUI(); }
        void Update()
        {
            if(mapCamera==null||!mapCamera.orthographic) return;
            // Supports references and limits edited at runtime as well.
            if(boundSlider!=zoomSlider||boundButton!=resetNode) BindUI();
            float delta=Input.mouseScrollDelta.y;
            bool pointerOnSlider=PointerOnSlider();
            bool blocked=blockWheelWhileDrawing&&Input.GetMouseButton(0)&&!pointerOnSlider;
            if(!blocked&&blockWheelOverOtherUI&&EventSystem.current!=null&&
                EventSystem.current.IsPointerOverGameObject()&&!pointerOnSlider) blocked=true;
            if(!blocked&&delta!=0) ScrollWheel(delta);
            else SetSize(CurrentSize);
        }
        public void ScrollWheel(float delta) { SetSize(Range.Scroll(CurrentSize,delta,wheelStep*Mathf.Clamp(wheelSensitivity,.5f,6))); }
        bool PointerOnSlider()
        {
            if(zoomSlider==null||!zoomSlider.gameObject.activeInHierarchy) return false;
            if(sliderParent!=zoomSlider.transform.parent)
            { sliderCanvas=zoomSlider.GetComponentInParent<Canvas>(); sliderParent=zoomSlider.transform.parent; }
            var canvas=sliderCanvas;
            Camera uiCamera=canvas!=null&&canvas.renderMode!=RenderMode.ScreenSpaceOverlay?canvas.worldCamera:null;
            return RectTransformUtility.RectangleContainsScreenPoint(zoomSlider.transform as RectTransform,Input.mousePosition,uiCamera);
        }
        public void SetNormalizedZoom(float value) { SetSize(Range.SizeFromSlider(value)); }
        public void ResetToMiddle() { SetSize(Range.MiddleSize); }
        public void SetSize(float value)
        {
            if(mapCamera==null||!mapCamera.orthographic) return;
            var range=Range; mapCamera.orthographicSize=range.ClampSize(value);
            if(boundSlider!=null) boundSlider.SetValueWithoutNotify(range.SliderFromSize(mapCamera.orthographicSize));
        }
    }
}
