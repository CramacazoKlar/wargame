using UnityEngine;
namespace CampaignDemo
{
    public sealed class ArmyTacticalInfo : MonoBehaviour
    {
        public string ownerId="A";
        public string armyLabel;
        [Min(0)] public float visionRadius=1.5f;
        [Min(0)] public float attackRadius=0.75f;
        public string extraStatus;
        public string DisplayName { get { return string.IsNullOrEmpty(armyLabel)?name:armyLabel; } }
    }
}
