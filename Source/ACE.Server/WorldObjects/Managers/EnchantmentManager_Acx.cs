using ACE.Entity.Enum;
using ACE.Entity.Models;
using ACE.Server.Entity;
using ACE.Server.Network.GameMessages.Messages;

namespace ACE.Server.WorldObjects.Managers
{
    /// <summary>
    /// Project ACX: clearer spell-expiry messages (JJ 2026-11-11, ported from ClassicACE). When one of a player's own
    /// spells runs out, chat says "<spell> has expired", or "<spell> has expired, but it is surpassed by <other>" when a
    /// weaker one of the same kind is still up. If an equal or stronger one is still up, it stays quiet (no sound).
    /// </summary>
    partial class EnchantmentManager
    {
        private bool acxExpiring;

        /// <summary>
        /// Called from Remove for a player: sends the message and returns whether to play the expiry sound. A chat line, so
        /// nothing in it may stop the server: on any error it says only that the spell expired (2026-11-17, Live crashed here)
        /// </summary>
        private bool AcxExpiryMessage(PropertiesEnchantmentRegistry entry)
        {
            try
            {
                return AcxExpiryMessageInner(entry);
            }
            catch (System.Exception ex)
            {
                System.Console.WriteLine($"[ACX] spell expiry message for spell {entry.SpellId} failed: {ex.Message}");
                return true;
            }
        }

        private bool AcxExpiryMessageInner(PropertiesEnchantmentRegistry entry)
        {
            if (entry.SpellId == (uint)SpellId.Vitae || entry.SpellCategory == (SpellCategory)SpellCategory_Cooldown)
                return true;

            var spell = new Spell(entry.SpellId);
            if (spell.NotFound)
                return true;

            // the caster can't be null: BuildStack reads it when an equal spell is still up (a "refresh"), and Live crashed on
            // that on 2026-11-17 (a buff expiring beside the same buff from another source)
            var result = new AddEnchantmentResult();
            result.BuildStack(GetEnchantments(entry.SpellCategory), spell, WorldObject);

            var remaining = result.StackType switch
            {
                StackType.Surpass => result.SurpassSpell,
                StackType.Refresh => result.RefreshSpell,
                StackType.Surpassed => result.SurpassedSpell,
                _ => null,
            };

            if (remaining == null)
            {
                Player.Session.Network.EnqueueSend(new GameMessageSystemChat($"{spell.Name} has expired.", ChatMessageType.Magic));
                return true;
            }
            if (remaining.Power < spell.Power)
            {
                Player.Session.Network.EnqueueSend(new GameMessageSystemChat($"{spell.Name} has expired, but it is surpassed by {remaining.Name}.", ChatMessageType.Magic));
                return true;
            }
            return false;
        }
    }
}
