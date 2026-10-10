using UnityEngine;

namespace CampaignDemo
{
    [CreateAssetMenu(menuName="Campaign Demo/Army Type",fileName="ArmyType")]
    public sealed class ArmyDefinition : ScriptableObject
    {
        public string displayName="Infantry";
        [Min(0.01f)] public float speed=2;
        public GameObject prefab;
        public ArmyFreeMarchDefinition freeMarchDefinition;
        public Sprite icon;
        public Vector2 iconSize=new Vector2(0.8f,0.5f);
        public Color iconColor=new Color(0.15f,0.75f,0.95f);
    }
}
