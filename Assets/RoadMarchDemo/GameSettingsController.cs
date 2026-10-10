using UnityEngine;
using UnityEngine.UI;
namespace CampaignDemo
{
    public sealed class GameSettingsController : MonoBehaviour
    {
        public MapPanController pan;
        public GameObject settingsPanel;
        public void TogglePanel() { if(settingsPanel!=null)settingsPanel.SetActive(!settingsPanel.activeSelf); }
        public Slider panSlider,planningSlider,bgmSlider,effectsSlider;
        public MapZoomController zoom;
        public Slider wheelSensitivitySlider;
        Text wheelSensitivityLabel;
        public AudioSource bgmSource;
        public AudioSource[] effectSources=new AudioSource[0];
        public float planningSensitivity=1;
        float bgmVolume=.6f,effectsVolume=.8f;
        RoadMarchController road; FreeMarchController free;
        void Start()
        {
            if(zoom==null)zoom=GetComponent<MapZoomController>();
            if(zoom!=null)zoom.wheelSensitivity=Mathf.Clamp(PlayerPrefs.GetFloat("Campaign.WheelSensitivity",2),.5f,6);
            CreateWheelSetting();
            Bind(wheelSensitivitySlider,zoom==null?2:zoom.wheelSensitivity,SetWheelSensitivity);
            UpdateWheelLabel();
            if(pan!=null)pan.mouseSensitivity=PlayerPrefs.GetFloat("Campaign.PanSensitivity",1);
            planningSensitivity=PlayerPrefs.GetFloat("Campaign.PlanningSensitivity",1);
            bgmVolume=PlayerPrefs.GetFloat("Campaign.BgmVolume",.6f);effectsVolume=PlayerPrefs.GetFloat("Campaign.EffectsVolume",.8f);
            Bind(panSlider,pan==null?1:pan.mouseSensitivity,SetPan);Bind(planningSlider,planningSensitivity,SetPlanning);
            Bind(bgmSlider,bgmVolume,SetBgm);Bind(effectsSlider,effectsVolume,SetEffects);ApplyAudio();
        }
        void CreateWheelSetting()
        {
            if(wheelSensitivitySlider!=null||settingsPanel==null||panSlider==null)return;
            var panel=settingsPanel.GetComponent<RectTransform>();
            // Preserve the panel top while adding one compact setting row at its bottom.
            foreach(Transform child in settingsPanel.transform){var rect=child as RectTransform;if(rect!=null)rect.anchoredPosition+=Vector2.up*35;}
            panel.sizeDelta+=new Vector2(0,70);panel.anchoredPosition+=Vector2.down*35;
            var sliderObject=Instantiate(panSlider.gameObject,settingsPanel.transform,false);sliderObject.name="Wheel zoom sensitivity";
            var sliderRect=sliderObject.GetComponent<RectTransform>();sliderRect.anchoredPosition=new Vector2(0,-143);sliderRect.sizeDelta=new Vector2(270,14);
            wheelSensitivitySlider=sliderObject.GetComponent<Slider>();wheelSensitivitySlider.onValueChanged.RemoveAllListeners();wheelSensitivitySlider.minValue=.5f;wheelSensitivitySlider.maxValue=6;wheelSensitivitySlider.wholeNumbers=false;
            var labelObject=new GameObject("Wheel sensitivity label",typeof(RectTransform));labelObject.transform.SetParent(settingsPanel.transform,false);
            var labelRect=labelObject.GetComponent<RectTransform>();labelRect.anchorMin=labelRect.anchorMax=new Vector2(.5f,.5f);labelRect.anchoredPosition=new Vector2(0,-117);labelRect.sizeDelta=new Vector2(300,24);
            wheelSensitivityLabel=labelObject.AddComponent<Text>();var example=settingsPanel.GetComponentInChildren<Text>(true);wheelSensitivityLabel.font=Font.CreateDynamicFontFromOSFont(new[]{"Microsoft YaHei","SimHei","Arial"},14);if(wheelSensitivityLabel.font==null)wheelSensitivityLabel.font=example==null?Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"):example.font;wheelSensitivityLabel.fontSize=14;wheelSensitivityLabel.color=new Color(.95f,.87f,.68f);wheelSensitivityLabel.alignment=TextAnchor.MiddleCenter;wheelSensitivityLabel.raycastTarget=false;
        }
        void UpdateWheelLabel(){if(wheelSensitivityLabel!=null)wheelSensitivityLabel.text="滚轮缩放速度  "+(zoom==null?2:zoom.wheelSensitivity).ToString("0.0")+"×";}
        public void SetWheelSensitivity(float value){value=Mathf.Clamp(value,.5f,6);if(zoom!=null)zoom.wheelSensitivity=value;PlayerPrefs.SetFloat("Campaign.WheelSensitivity",value);UpdateWheelLabel();}
        void Bind(Slider s,float value,UnityEngine.Events.UnityAction<float> callback) { if(s==null)return;s.SetValueWithoutNotify(value);s.onValueChanged.AddListener(callback); }
        void Update() { if(road==null)road=GetComponent<RoadMarchController>();if(free==null)free=GetComponent<FreeMarchController>();if(road!=null)road.planningSensitivity=planningSensitivity;if(free!=null)free.planningSensitivity=planningSensitivity; }
        public void SetPan(float v) { if(pan!=null)pan.SetMouseSensitivity(v); PlayerPrefs.SetFloat("Campaign.PanSensitivity",v); }
        public void SetPlanning(float v) { planningSensitivity=Mathf.Clamp(v,.25f,2);PlayerPrefs.SetFloat("Campaign.PlanningSensitivity",planningSensitivity); }
        public void SetBgm(float v) { bgmVolume=Mathf.Clamp01(v);PlayerPrefs.SetFloat("Campaign.BgmVolume",bgmVolume);ApplyAudio(); }
        public void SetEffects(float v) { effectsVolume=Mathf.Clamp01(v);PlayerPrefs.SetFloat("Campaign.EffectsVolume",effectsVolume);ApplyAudio(); }
        void ApplyAudio() { if(bgmSource!=null)bgmSource.volume=bgmVolume;foreach(var s in effectSources)if(s!=null)s.volume=effectsVolume; }
        public void PlayEffect(AudioClip clip,Vector3 position) { if(clip==null||effectSources==null)return;foreach(var s in effectSources)if(s!=null){s.transform.position=position;s.PlayOneShot(clip);return;} }
        void OnDestroy() { if(wheelSensitivitySlider!=null)wheelSensitivitySlider.onValueChanged.RemoveListener(SetWheelSensitivity); if(panSlider!=null)panSlider.onValueChanged.RemoveListener(SetPan);if(planningSlider!=null)planningSlider.onValueChanged.RemoveListener(SetPlanning);if(bgmSlider!=null)bgmSlider.onValueChanged.RemoveListener(SetBgm);if(effectsSlider!=null)effectsSlider.onValueChanged.RemoveListener(SetEffects);PlayerPrefs.Save(); }
    }
}
