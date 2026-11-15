using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;
using ACE.Server.Entity;
using ACE.Server.Managers;
using ACE.Server.Network.GameEvent.Events;
using ACE.Server.Network.GameMessages.Messages;

namespace ACE.Server.WorldObjects
{
    /// <summary>
    /// Project ACX: allegiance tweaks (JJ 2026-11-11, ported from ClassicACE). /OfflineSwear swears to one of your own
    /// characters that's offline. While pass-up is on, a patron must be your level or higher (acx_patron_level_rule).
    /// Pass-up can be switched off or multiplied (allow_allegiance_passup, allegiance_passup_multiplier: 3 = 3x).
    /// </summary>
    partial class Player
    {
        /// <summary>
        /// Called from IsPledgable (and the offline swear): true if the patron is too low a level
        /// </summary>
        public bool AcxPatronTooLow(IPlayer target)
        {
            if (!PropertyManager.GetBool("acx_patron_level_rule").Item || !PropertyManager.GetBool("allow_allegiance_passup").Item || (target.Level ?? 1) >= (Level ?? 1))
                return false;

            Session.Network.EnqueueSend(new GameMessageSystemChat("You cannot swear to a lower level character.", ChatMessageType.Broadcast));
            Session.Network.EnqueueSend(new GameEventWeenieError(Session, WeenieError.AllegianceIllegalLevel));
            return true;
        }

        /// <summary>
        /// /OfflineSwear: swear allegiance to an offline character on the same account
        /// </summary>
        public string AcxOfflineSwear(string patronName)
        {
            if (PlayerManager.GetOnlinePlayer(patronName) != null)
                return "That character is online: swear to them in person.";

            var patron = PlayerManager.GetOfflinePlayer(patronName);
            if (patron == null || patron.IsDeleted || patron.IsPendingDeletion)
                return $"There's no character called {patronName}.";
            if (patron.Account?.AccountId != Account?.AccountId)
                return "You can only swear to an offline character on your own account.";

            if (PatronId != null)
                return "You've already sworn allegiance.";
            if (patron.Guid == Guid)
                return "You cannot swear allegiance to yourself.";
            if (AcxPatronTooLow(patron))
                return null;

            var targetNode = AllegianceManager.GetAllegianceNode(patron);
            if (targetNode != null)
            {
                if (targetNode.TotalVassals >= 11)
                    return $"{patron.Name} already has the maximum number of vassals.";
                if (AllegianceNode != null && AllegianceNode.IsMonarch && AllegianceNode.PlayerGuid == targetNode.Monarch.PlayerGuid)
                    return $"You cannot swear allegiance to {patron.Name}.";
                if (targetNode.Allegiance.IsBanned(Guid.Full))
                    return $"You are banned from joining {patron.Name}'s allegiance.";
            }
            if (House != null && House.SlumLord.HouseRequiresMonarch && House.HouseOwner == Guid.Full)
                return "You cannot swear allegiance while owning a mansion.";

            PatronId = patron.Guid.Full;
            UpdateProperty(PropertyInstanceId.Monarch, AllegianceManager.GetMonarch(patron).Guid.Full, true);
            ExistedBeforeAllegianceXpChanges = (patron.Level ?? 1) >= (Level ?? 1);

            if (Allegiance != null && Allegiance.MonarchId == Guid.Full)
                HandleMonarchSwear();

            SaveBiotaToDatabase();
            EnqueueBroadcastMotion(new Motion(MotionStance.NonCombat, MotionCommand.Kneel));

            AllegianceManager.OnSwearAllegiance(this);
            AllegianceXPGenerated = 0;
            AllegianceOfficerRank = null;

            Session.Network.EnqueueSend(new GameEventAllegianceUpdate(Session, Allegiance, AllegianceNode), new GameEventAllegianceAllegianceUpdateDone(Session));
            if (GetCharacterOption(CharacterOption.ListenToAllegianceChat) && Allegiance != null)
                JoinTurbineChatChannel("Allegiance");

            return $"{patron.Name} has accepted your oath of Allegiance!";
        }

        /// <summary>
        /// Project ACX: unlimited respec (JJ 2026-11-11, ClassicACE). Up to unlimited_respec_max_level (20), picking up
        /// the skill and attribute reset gems (Temple of Forgetfulness and Enlightenment) stamps no quest timer.
        /// </summary>
        public bool AcxUnlimitedRespec(string quest)
        {
            if (!PropertyManager.GetBool("unlimited_respec").Item || (Level ?? 1) > PropertyManager.GetLong("unlimited_respec_max_level").Item)
                return false;

            // retail's names, plus ClassicACE's own
            return quest is "SkillAlterationGemPickedUp" or "AttributeLoweringGemPickedUp" or "AttributeRaisingGemPickedUp"
                or "SkillForgetfulnessGemPickedUp" or "SkillEnlightenmentGemPickedUp" or "SkillPrimaryGemPickedUp" or "SkillSecondaryGemPickedUp";
        }
    }
}
