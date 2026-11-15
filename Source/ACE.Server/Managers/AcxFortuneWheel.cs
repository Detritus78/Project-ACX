using System;

using ACE.Common;
using ACE.Common.Extensions;
using ACE.Entity.Enum;
using ACE.Server.Entity.Actions;
using ACE.Server.Factories;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Server.WorldObjects;

namespace ACE.Server.Managers
{
    /// <summary>
    /// Project ACX: the Wheel of Fortune in Concordia's gambling hub (JJ 2026-11-14: "I like your 20hr idea"). Using it
    /// spins it once per character every 20 hours (quest AcxWheelOfFortune, 72000 s) and pays gambling tokens, which the
    /// gamesmasters take. Only ACX's own wheel (900788) does this; retail's wheels stay decoration. Prize table kept as
    /// first written (JJ 2026-11-14: "lets leave the fortune prize like it is").
    /// </summary>
    public static class AcxFortuneWheel
    {
        public const uint WheelWcid = 900788;
        private const string Quest = "AcxWheelOfFortune";

        private const uint LowStakesToken = 44715, MidStakesToken = 44716, HighStakesToken = 44717;

        // chance, token, how many (a Low-Stakes token is about 10,000 pyreals, Mid 20,000, High 50,000)
        private static readonly (double Chance, uint Wcid, int Count)[] Prizes =
        {
            (0.40, LowStakesToken, 2),
            (0.25, MidStakesToken, 1),
            (0.15, LowStakesToken, 5),
            (0.10, MidStakesToken, 2),
            (0.07, HighStakesToken, 1),
            (0.03, HighStakesToken, 3),
        };

        /// <summary>
        /// Called from Switch.OnActivate for the wheel: spins it, or says when this character may spin again
        /// </summary>
        public static void Spin(Switch wheel, Player player)
        {
            if (!player.QuestManager.CanSolve(Quest))
            {
                player.Session.Network.EnqueueSend(new GameMessageSystemChat($"You can spin the wheel again in {player.QuestManager.GetNextSolveTime(Quest).GetFriendlyString()}.", ChatMessageType.Broadcast));
                return;
            }

            var (_, wcid, count) = Roll();
            var prize = WorldObjectFactory.CreateNewWorldObject(wcid);
            if (prize == null)
                return;
            prize.SetStackSize(count);

            if (!player.HasEnoughBurdenToAddToInventory(prize) || !player.CanAddToInventory(prize))
            {
                prize.Destroy();
                player.Session.Network.EnqueueSend(new GameMessageSystemChat("Make room in your pack before you spin the wheel.", ChatMessageType.Broadcast));
                return;
            }

            player.QuestManager.Stamp(Quest);

            // the wheel turns, then pays
            wheel.EnqueueBroadcast(new GameMessageSound(wheel.Guid, Sound.TriggerActivated));
            var chain = new ActionChain();
            if (wheel.MotionTableId != 0)
                wheel.EnqueueMotion(chain, wheel.UseTargetAnimation != null ? (MotionCommand)wheel.UseTargetAnimation : MotionCommand.Twitch1, 1, false);
            chain.AddDelaySeconds(1.0);
            chain.AddAction(wheel, () =>
            {
                var name = count == 1 ? prize.Name : $"{count} {prize.GetPluralName()}";
                if (player.TryCreateInInventoryWithNetworking(prize))
                    player.Session.Network.EnqueueSend(new GameMessageSystemChat($"The wheel stops on {name}!", ChatMessageType.Broadcast));
                else
                    prize.Destroy();
            });
            chain.EnqueueChain();
        }

        private static (double, uint, int) Roll()
        {
            var roll = ThreadSafeRandom.Next(0.0f, 1.0f);
            foreach (var prize in Prizes)
            {
                if (roll < prize.Chance)
                    return prize;
                roll -= prize.Chance;
            }
            return Prizes[0];
        }
    }
}
