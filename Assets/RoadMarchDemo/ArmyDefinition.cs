using UnityEngine;

namespace CampaignDemo
{
    [CreateAssetMenu(menuName="Campaign Demo/Army Type",fileName="ArmyType")]
    public sealed class ArmyDefinition : ScriptableObject
    {
        public string displayName="Infantry";
        [Min(0.01f)] public float speed=2;
        [Header("按比例尺标定道路移动时间")]
        public bool useReferenceTravelTime;
        [Min(1)] public float referenceDistanceMeters=5000;
        [Min(.01f)] public float referenceTravelMinutes=.5f;
        [Min(.01f)] public float roadSpeedRatio=1;
        public static float ReferenceRoadSpeed(float distanceMeters,float minutes,float metersPerUnit,float ratio=1)
        {return distanceMeters>0&&minutes>0&&metersPerUnit>0?distanceMeters*Mathf.Max(0,ratio)/(minutes*60*metersPerUnit):0;}
        public GameObject prefab;
        public ArmyFreeMarchDefinition freeMarchDefinition;
        public Sprite icon;
        public Vector2 iconSize=new Vector2(0.8f,0.5f);
        public Color iconColor=new Color(0.15f,0.75f,0.95f);
    }
}
