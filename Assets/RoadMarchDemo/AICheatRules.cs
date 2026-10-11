using UnityEngine;
namespace CampaignDemo
{
    public static class AICheatRules
    {
        static AIBasicProfile ActiveProfile(BattleWorld world,string player)
        {
            if(world==null||player!=BattleFactions.RedPlayerId)return null;
            var commander=world.GetComponent<AIBasicCommander>();
            return commander!=null&&commander.Active&&commander.profile.enableCheats?commander.profile:null;
        }
        public static float Income(BattleWorld world,string player)
        {var p=ActiveProfile(world,player);return p==null?1:Mathf.Clamp(p.resourceIncomeMultiplier,1,10);}
        public static float Production(BattleWorld world,string player)
        {var p=ActiveProfile(world,player);return p==null?1:Mathf.Clamp(p.productionSpeedMultiplier,1,10);}
        public static bool FullIntel(BattleWorld world,string player)
        {var p=ActiveProfile(world,player);return p!=null&&p.omniscientEnemyIntel;}
    }
}
