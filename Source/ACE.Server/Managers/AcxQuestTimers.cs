using System;

namespace ACE.Server.Managers
{
    /// <summary>
    /// Project ACX: quest timer scaling (JJ 2026-11-11, ported from ClassicACE). quest_mindelta_rate scales every quest
    /// timer (1 = retail); timers shorter than quest_mindelta_rate_shortest aren't scaled and scaled ones never drop below
    /// it; quest_mindelta_rate_longest caps the longest timers (0 = no cap).
    /// </summary>
    public static class AcxQuestTimers
    {
        public static uint Scale(uint minDelta)
        {
            var rate = PropertyManager.GetDouble("quest_mindelta_rate", 1).Item;
            var shortest = (uint)Math.Max(0, PropertyManager.GetLong("quest_mindelta_rate_shortest").Item);
            var longest = (uint)Math.Max(0, PropertyManager.GetLong("quest_mindelta_rate_longest").Item);

            var scaled = minDelta;
            if (minDelta >= shortest)
                scaled = Math.Max(shortest, (uint)(minDelta * rate));
            if (longest > 0)
                scaled = Math.Min(scaled, longest);
            return scaled;
        }
    }
}
