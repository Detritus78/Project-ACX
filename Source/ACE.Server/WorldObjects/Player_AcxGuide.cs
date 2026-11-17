using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;
using ACE.Server.Entity.Actions;
using ACE.Server.Factories;
using ACE.Server.Managers;
using ACE.Server.Network.GameEvent.Events;
using ACE.Server.Network.GameMessages.Messages;

namespace ACE.Server.WorldObjects
{
    /// <summary>
    /// Project ACX: the ACX Guide (acx/Content/newplayer/10-acx-guide.sql), a book every character gets once, at its first
    /// login after this change: a new character's first login (JJ 2026-11-16: "We can hand off a book as part of the new
    /// player set up when they first login", then "Write the guide book like a book for dummies"). It arrives a few seconds
    /// in, once the pack is loaded; a character with no room for it gets it at the next login.
    /// Handed to a Town Crier, it pays 5 Low-Stakes Gambling Tokens, once per character (JJ: "making it a later turn in when
    /// people think they got it", then "No we can't do a acx mark. It will get abused lets make it 5 low stakes gambling
    /// coins"): the book is tradable and every character gets one, so the quest stamp keeps a stack of books from paying
    /// one character more than once.
    /// </summary>
    partial class Player
    {
        public const uint AcxGuideWcid = 900900;

        // Monty's Low-Stakes Gambling Token, which every gamesmaster takes (JJ: "they all accept any. I tested it")
        private const uint AcxGuideTokenWcid = 44715;
        private const int AcxGuideTokens = 5;
        private const string AcxGuideTurnInQuest = "AcxGuideTurnedIn";

        private void AcxGiveGuide()
        {
            if ((GetProperty(PropertyBool.AcxGuideGiven) ?? false) || IsOlthoiPlayer)
                return;

            var actionChain = new ActionChain();
            actionChain.AddDelaySeconds(5.0f);
            actionChain.AddAction(this, () =>
            {
                if (PlayerManager.GetOnlinePlayer(Guid) == null || (GetProperty(PropertyBool.AcxGuideGiven) ?? false))
                    return;

                var guide = WorldObjectFactory.CreateNewWorldObject(AcxGuideWcid);
                if (guide == null)
                    return;

                if (TryCreateInInventoryWithNetworking(guide))
                    SetProperty(PropertyBool.AcxGuideGiven, true);
                else
                    guide.Destroy();
            });
            actionChain.EnqueueChain();
        }

        /// <summary>
        /// The ACX Guide given to a Town Crier (GiveObjectToNPC): the crier looks it over like an IOU, then trades it for the
        /// tokens the first time
        /// </summary>
        private void AcxGuideTurnIn(WorldObject crier, WorldObject guide)
        {
            Session.Network.EnqueueSend(new GameMessageSystemChat($"You allow {crier.Name} to examine your {guide.NameWithMaterial}.", ChatMessageType.Broadcast));
            Session.Network.EnqueueSend(new GameEventInventoryServerSaveFailed(Session, guide.Guid.Full, WeenieError.TradeAiRefuseEmote));

            if (QuestManager.HasQuest(AcxGuideTurnInQuest))
            {
                Session.Network.EnqueueSend(new GameEventTell(crier, "You've already shown me you know your way around. Keep it, or pass it on to someone who doesn't.", this, ChatMessageType.Tell));
                return;
            }

            var tokens = WorldObjectFactory.CreateNewWorldObject(AcxGuideTokenWcid);
            if (tokens == null)
                return;
            tokens.SetStackSize(AcxGuideTokens);

            if (!CanAddToInventory(tokens))
            {
                tokens.Destroy();
                Session.Network.EnqueueSend(new GameEventTell(crier, "Make some room in your pack first.", this, ChatMessageType.Tell));
                return;
            }

            Session.Network.EnqueueSend(new GameEventTell(crier, "Think you've got it all figured out, do you? Then go try your luck.", this, ChatMessageType.Tell));
            Session.Network.EnqueueSend(new GameMessageSystemChat($"You give {crier.Name} {guide.Name}.", ChatMessageType.Broadcast));
            crier.EnqueueBroadcast(new GameMessageSound(crier.Guid, Sound.ReceiveItem));

            RemoveItemForGive(guide, null, false, null, 1, out _, true);

            if (TryCreateInInventoryWithNetworking(tokens))
            {
                QuestManager.Stamp(AcxGuideTurnInQuest);
                Session.Network.EnqueueSend(new GameMessageSystemChat($"{crier.Name} gives you {AcxGuideTokens} {tokens.GetNameWithMaterial(AcxGuideTokens)}.", ChatMessageType.Broadcast));
            }
            else
                tokens.Destroy();
        }
    }
}
