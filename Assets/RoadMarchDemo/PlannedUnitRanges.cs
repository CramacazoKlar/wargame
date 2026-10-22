using UnityEngine;
namespace CampaignDemo
{
    public sealed class PlannedUnitRanges : MonoBehaviour
    {
        public static void ApplyTo(BattleUnit unit)
        {
            if (unit == null || unit.GetComponent<RadarInstallation>() != null || !LocalPrototypeOrders.EnabledFor(unit)) return;
            var plan = Resources.Load<UnitRangePlanning>("UnitRangePlanning"); if (plan == null) return;
            var ranges = unit.GetComponent<PlannedUnitRanges>(); if (ranges == null) ranges = unit.gameObject.AddComponent<PlannedUnitRanges>(); ranges.Apply(unit, plan);
        }
        BattleUnit unit;
        UnitRangePlanning plan;
        ArmyCombatDefinition ownedCombat;
        ArtilleryDefinition ownedArtillery;
        int lastRank = -1;
        float originalVision, originalAttack;
        public void Apply(BattleUnit target, UnitRangePlanning profile)
        {
            unit = target; plan = profile; if (unit.combatDefinition == null || plan.For(unit.combatDefinition.role) == null) return;
            if (ownedCombat == null) { originalVision = unit.combatDefinition.visionRadius; originalAttack = unit.combatDefinition.attackRadius; ownedCombat = Instantiate(unit.combatDefinition); unit.combatDefinition = ownedCombat; var battery = unit.GetComponent<ArtilleryBattery>(); if (battery != null && battery.definition != null) { ownedArtillery = Instantiate(battery.definition); battery.definition = ownedArtillery; } }
            var role = unit.combatDefinition.role; var rank = unit.combatDefinition.echelon;
            unit.combatDefinition.visionRadius = role==UnitRole.Scout ? plan.Vision(role,rank) : (int)rank<(int)plan.baselineEchelon?originalVision:plan.Vision(role,rank);
            unit.combatDefinition.attackRadius = UnitRangePlanning.DirectFireRole(role)?plan.ordinaryAttackRadius:ownedArtillery!=null?originalAttack*Mathf.Pow(plan.attackPerEchelon,Mathf.Max(0,(int)rank-(int)plan.baselineEchelon)):(int)rank<(int)plan.baselineEchelon?originalAttack:plan.Attack(role,rank);
            if (ownedArtillery != null) ownedArtillery.range = plan.Attack(role, rank);
            var info = unit.GetComponent<ArmyTacticalInfo>(); if (info != null) { info.visionRadius = unit.combatDefinition.visionRadius; info.attackRadius = UnitRanges.Attack(unit); }
            lastRank = (int)rank;
        }
        void Update() { if (unit != null && plan != null && LocalPrototypeOrders.EnabledFor(unit) && lastRank != (int)unit.combatDefinition.echelon) Apply(unit, plan); }
        void OnDestroy() { if (ownedCombat != null) Destroy(ownedCombat); if (ownedArtillery != null) Destroy(ownedArtillery); }
    }
}
