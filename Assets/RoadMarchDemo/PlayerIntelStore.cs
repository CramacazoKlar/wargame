using System.Collections.Generic;
namespace CampaignDemo
{
    public sealed class PlayerIntelStore
    {
        readonly Dictionary<string,HashSet<string>> identified=new Dictionary<string,HashSet<string>>();
        public bool Knows(string player,BattleUnit unit)
        { return unit!=null&&(unit.ownerId==player||(identified.TryGetValue(player,out var set)&&set.Contains(unit.unitId))); }
        public void Identify(string player,BattleUnit unit)
        { if(unit==null||string.IsNullOrEmpty(player))return;if(!identified.TryGetValue(player,out var set)){set=new HashSet<string>();identified.Add(player,set);}set.Add(unit.unitId); }
        public void Reset(BattleUnit unit) { if(unit!=null)foreach(var set in identified.Values)set.Remove(unit.unitId); }
    }
}
