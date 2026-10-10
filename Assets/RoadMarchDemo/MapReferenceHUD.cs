using UnityEngine;
using UnityEngine.UI;
namespace CampaignDemo
{
    public sealed class MapReferenceHUD : MonoBehaviour
    {
        public Camera mapCamera;
        public Text scaleLabel;
        public RectTransform scaleBar,compass;
        public float metersPerWorldUnit=1000;
        public float mapNorthDegrees;
        public float preferredPixels=140;
        void LateUpdate()
        {
            if(mapCamera==null||!mapCamera.orthographic||scaleBar==null)return;
            var canvas=scaleBar.GetComponentInParent<Canvas>(); float factor=canvas==null?1:canvas.scaleFactor;
            float metersPerPixel=2*mapCamera.orthographicSize/Mathf.Max(1,mapCamera.pixelHeight)*metersPerWorldUnit;
            float desired=metersPerPixel*preferredPixels*factor;
            float magnitude=Mathf.Pow(10,Mathf.Floor(Mathf.Log10(Mathf.Max(.001f,desired))));
            float step=desired/magnitude;float distance=(step>=5?5:step>=2?2:1)*magnitude;
            scaleBar.sizeDelta=new Vector2(distance/metersPerPixel/factor,scaleBar.sizeDelta.y);
            if(scaleLabel!=null)scaleLabel.text=distance>=1000?(distance/1000).ToString("0.#")+" km":distance.ToString("0")+" m";
            if(compass!=null)compass.localRotation=Quaternion.Euler(0,0,mapNorthDegrees-mapCamera.transform.eulerAngles.z);
        }
    }
}
