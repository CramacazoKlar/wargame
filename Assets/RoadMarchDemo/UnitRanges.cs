using UnityEngine;
namespace CampaignDemo
{
    // 白色选中圈、攻击圈和主动交互共用单位的实际攻击半径。
    public static class UnitRanges
    {
        public static float Attack(BattleUnit unit)
        {
            if(unit==null)return 0;
            var battery=unit.GetComponent<ArtilleryBattery>();
            float radius=battery!=null&&battery.definition!=null?battery.definition.range:unit.combatDefinition!=null?unit.combatDefinition.attackRadius:0;
            return float.IsNaN(radius)||float.IsInfinity(radius)?0:Mathf.Max(0,radius);
        }
        public static float Interaction(BattleUnit unit){return Attack(unit);}
    }
}
