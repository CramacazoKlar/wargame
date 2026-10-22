using System;
using UnityEngine;

namespace CampaignDemo
{
    // All designer-facing range values live here; no combat damage or prices are configured here.
    [CreateAssetMenu(menuName = "Campaign Demo/策划/视野与攻击范围", fileName = "UnitRangePlanning")]
    public sealed class UnitRangePlanning : ScriptableObject
    {
        [Serializable] public sealed class Entry
        {
            public UnitRole role;
            [Min(0)] public float visionRadius;
            [Min(0)] public float attackRadius;
        }
        [Header("当前地图基准建制；基准大小保持原值")]
        public ArmyEchelon baselineEchelon = ArmyEchelon.Company;
        [Header("每升一级，相对于上一级的倍率")]
        [Min(1)] public float visionPerEchelon = 1.5f;
        [Min(1)] public float attackPerEchelon = 1.25f;
        [Header("普通直接交火统一射程；不随兵种或建制增长")]
        [Min(.01f)] public float ordinaryAttackRadius=.525f;
        public static bool DirectFireRole(UnitRole role){return role==UnitRole.Infantry||role==UnitRole.MountainInfantry||role==UnitRole.Engineer||role==UnitRole.Tank;}
        public Entry[] roles = new Entry[0];
        public Entry For(UnitRole role) { foreach (var row in roles) if (row.role == role) return row; return null; }
        public float Vision(UnitRole role, ArmyEchelon echelon) { var row = For(role==UnitRole.Scout?UnitRole.Infantry:role); return row == null ? 0 : row.visionRadius * (role==UnitRole.Scout?2:1) * Mathf.Pow(visionPerEchelon, Mathf.Max(0, (int)echelon - (int)baselineEchelon)); }
        public float Attack(UnitRole role, ArmyEchelon echelon) { if(DirectFireRole(role))return ordinaryAttackRadius; var row = For(role); return row == null ? 0 : row.attackRadius * Mathf.Pow(attackPerEchelon, Mathf.Max(0, (int)echelon - (int)baselineEchelon)); }
    }
}
