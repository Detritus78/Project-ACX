using ACE.Entity.Enum;
using ACE.Server.Entity.Actions;
using ACE.Server.Network.GameMessages.Messages;

namespace ACE.Server.WorldObjects
{
    /// <summary>
    /// Project ACX: /tip (JJ 2026-11-19: "a server command /tip that tips the cow you have selected until you get the message
    /// that says you have done enough tipping for now"). Retail's cow tipping (Dwennon in Holtburg) wants 300 uses of a cow
    /// and then waits 20 hours, so this saves clicks and can't farm anything. It uses the selected cow, one tip each time
    /// the cow's own script is free (its tipping motion), until CowTipCounter is full; it stops if you walk off, lose the
    /// cow, die or type /tip again. The messages along the way are the cow's own. The count (JJ: "can you add a tip counter?
    /// And it checks the server to see how many tips you have done?") is the quest's own CowTipCounter: shown on starting,
    /// every 25 tips, on stopping, and by /tip count.
    /// </summary>
    partial class Player
    {
        private const string AcxCowTipCounter = "CowTipCounter";
        private const int AcxCowTipsNeeded = 300;

        private const int AcxCowTipReportEvery = 25;

        private Cow acxTipCow;
        private int acxTipLastReported;

        private int AcxCowTips => QuestManager.GetCurrentSolves(AcxCowTipCounter);

        public string AcxTip(string argument)
        {
            if (argument.Trim().Equals("count", System.StringComparison.OrdinalIgnoreCase))
                return QuestManager.HasQuest("OnCowTipQuest") || AcxCowTips > 0
                    ? $"Cows tipped: {AcxCowTips} of {AcxCowTipsNeeded}."
                    : "You're not tipping cows for Dwennon (Holtburg) right now.";

            if (acxTipCow != null)
            {
                acxTipCow = null;
                return $"You stop tipping at {AcxCowTips} of {AcxCowTipsNeeded}.";
            }

            if (selectedTarget?.TryGetWorldObject() is not Cow cow)
                return "Select a cow first.";
            if (!QuestManager.HasQuest("OnCowTipQuest"))
                return "Talk to Dwennon in Holtburg first.";
            if (AcxCowTips >= AcxCowTipsNeeded)
                return "You've done enough tipping for now: see Dwennon.";
            if (!IsWithinUseRadiusOf(cow))
                return "Get closer to the cow.";

            acxTipCow = cow;
            acxTipLastReported = AcxCowTips;
            AcxTipNext();
            return $"You start tipping: {acxTipLastReported} of {AcxCowTipsNeeded} so far. /tip again to stop.";
        }

        private void AcxTipNext()
        {
            var cow = acxTipCow;
            if (cow == null)
                return;

            if (IsDead || Teleporting || cow.IsDestroyed || cow.CurrentLandblock == null || !IsWithinUseRadiusOf(cow))
            {
                acxTipCow = null;
                Session.Network.EnqueueSend(new GameMessageSystemChat($"You stop tipping at {AcxCowTips} of {AcxCowTipsNeeded}.", ChatMessageType.Broadcast));
                return;
            }
            var tips = AcxCowTips;
            if (tips >= AcxCowTipsNeeded)
            {
                acxTipCow = null;   // the cow's last tip already said "done enough tipping for now"
                Session.Network.EnqueueSend(new GameMessageSystemChat($"Cows tipped: {tips} of {AcxCowTipsNeeded}. Go see Dwennon.", ChatMessageType.Broadcast));
                return;
            }
            if (tips / AcxCowTipReportEvery > acxTipLastReported / AcxCowTipReportEvery)
                Session.Network.EnqueueSend(new GameMessageSystemChat($"Cows tipped: {tips} of {AcxCowTipsNeeded}.", ChatMessageType.Broadcast));
            acxTipLastReported = tips;

            if (!cow.EmoteManager.IsBusy)
                cow.EmoteManager.OnUse(this);

            var chain = new ActionChain();
            chain.AddDelaySeconds(0.5);
            chain.AddAction(this, AcxTipNext);
            chain.EnqueueChain();
        }
    }
}
