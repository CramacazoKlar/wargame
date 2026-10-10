using UnityEngine;
namespace CampaignDemo
{
    public sealed class ArmyTacticalInfo : MonoBehaviour
    {
        public string ownerId="A";
        public string armyLabel;
        [Min(0)] public float visionRadius=3;
        [Min(0)] public float attackRadius=1.5f;
        public string extraStatus;
        public string DisplayName { get { return string.IsNullOrEmpty(armyLabel)?name:armyLabel; } }
    }
}
