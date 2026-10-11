namespace CampaignDemo
{
    // Identity is persistent; perspective only describes the current viewer.
    public enum AbsoluteFaction { Neutral, Blue, Red, Other }
    public enum RelativeFaction { Neutral, Friendly, Enemy }

    public static class BattleFactions
    {
        public const string BluePlayerId = "A";
        public const string RedPlayerId = "B";

        public static AbsoluteFaction Absolute(string ownerId)
        {
            if (string.IsNullOrEmpty(ownerId)) return AbsoluteFaction.Neutral;
            if (ownerId == BluePlayerId) return AbsoluteFaction.Blue;
            if (ownerId == RedPlayerId) return AbsoluteFaction.Red;
            return AbsoluteFaction.Other;
        }

        public static RelativeFaction Relative(string ownerId, string viewerId)
        {
            if (string.IsNullOrEmpty(ownerId)) return RelativeFaction.Neutral;
            return ownerId == viewerId ? RelativeFaction.Friendly : RelativeFaction.Enemy;
        }

        public static string Name(string playerId)
        {
            switch (Absolute(playerId))
            {
                case AbsoluteFaction.Blue: return "蓝方";
                case AbsoluteFaction.Red: return "红方";
                case AbsoluteFaction.Neutral: return "中立";
                default: return playerId;
            }
        }
    }
}
