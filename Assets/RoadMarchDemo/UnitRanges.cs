using UnityEngine;
namespace CampaignDemo
{
    // 炮击射程与近距离交互分开，避免炮兵隔数公里进驻设施。
    public static class UnitRanges
    {
        public static float Attack(BattleUnit unit)
        {
            if(unit==null)return 0;
            var battery=unit.GetComponent<ArtilleryBattery>();
            float radius=battery!=null&&battery.definition!=null?battery.definition.range:unit.combatDefinition!=null?unit.combatDefinition.attackRadius:0;
            return float.IsNaN(radius)||float.IsInfinity(radius)?0:Mathf.Max(0,radius);
        }
        public static float Interaction(BattleUnit unit){if(unit==null||unit.combatDefinition==null)return 0;float radius=unit.combatDefinition.attackRadius;return float.IsNaN(radius)||float.IsInfinity(radius)?0:Mathf.Max(0,radius);}
    }
}
