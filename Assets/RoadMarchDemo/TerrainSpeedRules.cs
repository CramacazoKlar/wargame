using System;

namespace CampaignDemo
{
    [Serializable]
    public struct TerrainSpeedPenalty
    {
        public TerrainKind terrain;
        public float reductionPercent;
    }

    public static class TerrainSpeedRules
    {
        public static float Calculate(float roadSpeed,float mode2Reduction,float terrainReduction)
        {
            if(float.IsNaN(roadSpeed)||float.IsInfinity(roadSpeed)) return 0;
            return Math.Max(0,roadSpeed)*Multiplier(mode2Reduction)*Multiplier(terrainReduction);
        }
        public static float Multiplier(float percent)
        { if(float.IsNaN(percent)||float.IsInfinity(percent)) percent=0; return 1-Math.Max(0,Math.Min(100,percent))/100f; }
        public static float TerrainReduction(TerrainKind kind,TerrainSpeedPenalty[] rules)
        {
            float result=0;
            if(rules!=null) foreach(var rule in rules) if(rule.terrain==kind) result=rule.reductionPercent;
            return result;
        }
    }
}
